using Microsoft.EntityFrameworkCore;
using Systematikken.Api.Data;
using Systematikken.Api.Services;
using Systematikken.Billing;
using Systematikken.Billing.Betalingsservice;

namespace Systematikken.Api.Endpoints;

/// <summary>
/// The month you step through: check, approve, send to the bank. The draft is worked out fresh from the data on
/// every request, so a fix shows up at once. Approving is final; mistakes are fixed with a credit on a later month.
/// </summary>
public static class BillingEndpoints
{
    public sealed record SubsidyFix(Guid ChildId, decimal FreePlace, decimal SiblingDiscount);
    public sealed record ApproveBody(string Month);

    public static void Map(WebApplication app)
    {
        var g = app.MapGroup("/api/billing").AddEndpointFilter<RequireInstitution>();

        g.MapGet("", async (AppDb db, BillingService billing) =>
        {
            var institution = await billing.InstitutionAsync();
            var runs = await db.BillingRuns.OrderByDescending(r => r.Month)
                .Select(r => new { r.Id, r.Month, r.ApprovedAt, r.ChargeCount, r.Total }).ToListAsync();

            string? setup = null;
            if (institution.NurseryPrice <= 0 && institution.KindergartenPrice <= 0)
                setup = "Tilføj jeres priser i indstillingerne, før I kan lave en opkrævning.";
            else if (institution.PbsNumber.Length != 8 || institution.Cvr.Length != 8)
                setup = "Udfyld PBS-nummer og CVR-nummer i indstillingerne. Betalingsservice-filen skal bruge dem.";
            if (setup is not null) return Results.Ok(new { setupProblem = setup, runs });

            var month = await billing.NextMonthAsync(institution);
            var draft = await billing.DraftAsync(institution, month);
            var previousRun = await db.BillingRuns.OrderByDescending(r => r.Month).FirstOrDefaultAsync();

            return Results.Ok(new
            {
                month = month.ToString(),
                monthStart = month.FirstDay,
                canApprove = draft.CanApprove,
                problems = draft.Result.Problems.Select(p => new
                {
                    p.ChildId,
                    name = draft.Children[p.ChildId].FullName,
                    kind = p.Kind.ToString(),
                    blocking = p.Kind != ProblemKind.NothingToCollect,
                }),
                changes = await ChangesAsync(db, institution, draft, previousRun),
                charges = draft.Result.Charges.Where(c => c.Lines.Count > 0).Select(c =>
                {
                    var child = draft.Children[c.ChildId];
                    return new
                    {
                        c.ChildId, name = child.FullName, payer = child.Payer?.Name, status = c.Status.ToString(),
                        group = c.Group?.ToString(),
                        lines = c.Lines.Select(l => new { kind = l.Kind.ToString(), month = l.Month.ToString(), l.Amount, l.Text }),
                        amount = c.AmountToPay,
                    };
                }).OrderBy(c => c.name),
                totals = new
                {
                    toCollect = draft.Result.ToCollect.Sum(c => c.AmountToPay),
                    count = draft.Result.ToCollect.Count(),
                    freePlace = -Sum(draft, LineKind.FreePlace),
                    siblingDiscount = -Sum(draft, LineKind.SiblingDiscount),
                    voucher = draft.Voucher,
                },
                runs,
            });
        });

        // Fix a missing subsidy inline: an amount (can be 0) from this month onwards.
        g.MapPost("/subsidy", async (SubsidyFix body, AppDb db, BillingService billing) =>
        {
            if (!await db.Children.AnyAsync(c => c.Id == body.ChildId)) return Results.NotFound();
            if (body.FreePlace < 0 || body.SiblingDiscount < 0) return Http.Invalid("freePlace", "Tilskud kan ikke være negativt.");
            var month = await billing.NextMonthAsync(await billing.InstitutionAsync());
            var s = new Subsidy { ChildId = body.ChildId, FromMonth = month.FirstDay, FreePlace = Money.Round(body.FreePlace), SiblingDiscount = Money.Round(body.SiblingDiscount) };
            db.Subsidies.Add(s);
            await db.SaveChangesAsync();
            return Results.Ok(new { s.Id });
        });

        g.MapPost("/approve", async (ApproveBody body, HttpContext ctx, BillingService billing) =>
        {
            var institution = await billing.InstitutionAsync();
            var next = await billing.NextMonthAsync(institution);
            if (body.Month != next.ToString()) return Results.Conflict(new { detail = "Måneden er allerede godkendt." });
            try
            {
                var run = await billing.ApproveAsync(institution, next, ctx.User.Email());
                return Results.Ok(new { run.Id, run.Month });
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict(new { detail = "Der er noget, der skal rettes først." });
            }
        });

        g.MapGet("/runs/{id:int}", async (int id, AppDb db) =>
        {
            var r = await db.BillingRuns.SingleOrDefaultAsync(x => x.Id == id);
            if (r is null) return Results.NotFound();
            var hasPayments = await db.Ledger.AnyAsync(e => e.Month == r.Month && e.Kind == LedgerKind.Payment);
            return Results.Ok(new
            {
                r.Id, r.Month, r.ApprovedAt, r.ApprovedBy, r.ChargeCount, r.Total, r.SerialNumber,
                voucher = new { r.Opkraevningssum, r.Opkraevninger, r.Tilskudssum },
                fileName = FileName(r), hasPayments,
            });
        });

        g.MapGet("/runs/{id:int}/file", async (int id, AppDb db) =>
            await db.BillingRuns.SingleOrDefaultAsync(x => x.Id == id) is { } r
                ? Results.File(CollectionFile.FileEncoding.GetBytes(r.CollectionFile), "text/plain", FileName(r))
                : Results.NotFound());

        g.MapGet("/runs/{id:int}/voucher", async (int id, AppDb db) =>
        {
            var r = await db.BillingRuns.SingleOrDefaultAsync(x => x.Id == id);
            if (r is null) return Results.NotFound();
            var name = await db.Institutions.Where(i => i.Id == db.InstitutionId).Select(i => i.Name).SingleAsync();
            var voucher = new BookkeepingVoucher(BillingMonth.Of(r.Month), r.Opkraevningssum, r.Opkraevninger, r.Tilskudssum);
            return Results.File(VoucherPdf.Create(name, voucher), "application/pdf", $"bogfoeringsbilag-{r.Month:yyyy-MM}.pdf");
        });

        g.MapPost("/payments", async (IFormFile file, BillingService billing, PaymentService payments) =>
        {
            if (file.Length is 0 or > 5_000_000) return Http.Invalid("file", "Vælg betalingsfilen fra jeres bank.");
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            try
            {
                var outcome = await payments.ImportAsync(await billing.InstitutionAsync(), file.FileName, ms.ToArray());
                return Results.Ok(outcome);
            }
            catch (FormatException ex)
            {
                return Http.Invalid("file", ex.Message.StartsWith("The file is for PBS number")
                    ? "Filen er til en anden PBS-aftale end jeres."
                    : "Det er ikke en betalingsfil fra Betalingsservice. Hent filen med betalingsoplysninger hos banken.");
            }
        }).DisableAntiforgery();

        g.MapGet("/payments", async (AppDb db) =>
            (await db.PaymentImports.OrderByDescending(p => p.ImportedAt).Take(24).ToListAsync())
                .Select(p => new { p.Id, p.FileName, p.ImportedAt, p.PaymentCount, p.Total, notBooked = p.NotBooked.Split('\n', StringSplitOptions.RemoveEmptyEntries) }));
    }

    private static decimal Sum(DraftMonth d, LineKind kind) => d.Result.Charges.SelectMany(c => c.Lines).Where(l => l.Kind == kind).Sum(l => l.Amount);

    private static string FileName(BillingRun r) => $"betalingsservice-{r.Month:yyyy-MM}.txt";

    private static async Task<List<object>> ChangesAsync(AppDb db, Institution institution, DraftMonth draft, BillingRun? previous)
    {
        var changes = new List<object>();
        if (previous is null) return changes; // the first month has nothing to compare with
        var cutover = BillingService.SettingsFor(institution).Cutover;
        var charged = draft.Result.Charges.Where(c => c.Lines.Any(l => l.Kind == LineKind.Price)).Select(c => c.ChildId).ToHashSet();
        var before = previous is null ? [] : (await db.Ledger.Where(e => e.BillingRunId == previous.Id && e.LineKind == nameof(LineKind.Price)).Select(e => e.ChildId).ToListAsync()).ToHashSet();
        var prevMonth = previous is null ? draft.Month.AddMonths(-1) : BillingMonth.Of(previous.Month);

        foreach (var id in charged.Where(id => !before.Contains(id)))
        {
            var c = draft.Children[id];
            changes.Add(new { tag = "Ny", tone = "good", text = $"{c.FullName} starter {Fmt.Date(c.StartDate)}." });
        }
        foreach (var id in before.Where(id => !charged.Contains(id)))
        {
            if (!draft.Children.TryGetValue(id, out var c)) continue;
            changes.Add(new { tag = "Stoppet", tone = "", text = $"{c.FullName} er ikke med. Sidste dag {Fmt.Date(c.EndDate)}." });
        }
        foreach (var id in charged.Where(before.Contains))
        {
            var c = draft.Children[id];
            if (cutover.GroupFor(c.DateOfBirth, prevMonth) == CareGroup.Nursery && cutover.GroupFor(c.DateOfBirth, draft.Month) == CareGroup.Kindergarten)
                changes.Add(new { tag = "Ny pris", tone = "accent", text = $"{c.FullName} betaler nu børnehavepris." });
        }
        return changes;
    }
}
