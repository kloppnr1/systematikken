using Microsoft.EntityFrameworkCore;
using Systematikken.Api.Data;
using Systematikken.Api.Services;
using Systematikken.Billing;

namespace Systematikken.Api.Endpoints;

public static class ChildEndpoints
{
    public sealed record ParentBody(Guid? Id, string? Name, string? Email, string? Phone, string? Address, string? PostCode, string? City);
    public sealed record NewChild(string? FirstName, string? LastName, DateOnly? DateOfBirth, DateOnly? StartDate, bool IsPartTime, ParentBody Payer);
    public sealed record ChildDetails(string? FirstName, string? LastName, DateOnly DateOfBirth, DateOnly? StartDate, DateOnly? EndDate, bool IsPartTime, string? Note);
    public sealed record PayerBody(Guid ParentId);
    public sealed record SubsidyBody(string? FromMonth, string? ToMonth, decimal FreePlace, decimal SiblingDiscount);
    public sealed record ManualBody(string? Text, decimal Amount);
    public sealed record PaymentBody(decimal Amount, string? Month, DateOnly? PaidOn);

    public static object ParentDto(Parent p) => new { p.Id, p.Name, p.Email, p.Phone, p.Address, p.PostCode, p.City };

    public static (string Text, string Tone) StatusOf(Child c, DateOnly today) =>
        c.StartDate is null || c.StartDate > today ? ("Starter " + Fmt.Date(c.StartDate), "accent")
        : c.EndDate is { } end && end < today ? ("Udmeldt", "")
        : c.EndDate is { } e ? ($"Stopper {Fmt.Date(e)}", "warn")
        : ("Går her", "good");

    public static void Map(WebApplication app)
    {
        var g = app.MapGroup("/api/children").AddEndpointFilter<RequireInstitution>();

        g.MapGet("", async (string? filter, AppDb db, BillingService billing, TimeProvider clock) =>
        {
            var institution = await billing.InstitutionAsync();
            var cutover = BillingService.SettingsFor(institution).Cutover;
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var children = await db.Children.Include(c => c.Payer).OrderBy(c => c.FirstName).ThenBy(c => c.LastName).ToListAsync();
            var balances = (await db.Ledger.Select(e => new { e.ChildId, e.Amount }).ToListAsync())
                .GroupBy(e => e.ChildId).ToDictionary(x => x.Key, x => x.Sum(e => e.Amount));

            return children.Select(c =>
            {
                var (status, tone) = StatusOf(c, today);
                return new
                {
                    c.Id, name = c.FullName, group = cutover.GroupFor(c.DateOfBirth, BillingMonth.Of(today)).ToString(),
                    payer = c.Payer?.Name, status, tone, left = status == "Udmeldt", balance = balances.GetValueOrDefault(c.Id),
                };
            }).Where(c => (filter ?? "aktive") switch
            {
                "udmeldte" => c.left,
                "gaeld" => c.balance > 0,
                "alle" => true,
                _ => !c.left,
            }).ToList();
        });

        g.MapPost("", async (NewChild body, ChildService children) =>
        {
            var errors = new Errors()
                .Require("firstName", body.FirstName, "Skriv barnets fornavn.")
                .Require("payer.name", body.Payer?.Name, "Skriv betalerens navn.")
                .Require("payer.address", body.Payer?.Address, "Skriv adresse. Betalingsservice skal bruge den.");
            if (body.DateOfBirth is null) errors.Add("dateOfBirth", "Skriv fødselsdato.");
            if (body.StartDate is null) errors.Add("startDate", "Skriv første dag.");
            if (!IsPostCode(body.Payer?.PostCode)) errors.Add("payer.postCode", "Postnummer er fire cifre.");
            if (errors.Any) return errors.Result();

            var child = await children.CreateAsync(
                new Child { FirstName = body.FirstName!.Trim(), LastName = body.LastName.Clean() ?? "", DateOfBirth = body.DateOfBirth!.Value, StartDate = body.StartDate, IsPartTime = body.IsPartTime },
                [ToParent(body.Payer!, new Parent())]);
            return Results.Ok(new { child.Id, name = child.FullName });
        });

        g.MapGet("/{id:guid}", async (Guid id, AppDb db, BillingService billing, TimeProvider clock) =>
        {
            var c = await db.Children.Include(x => x.Parents).SingleOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();
            var institution = await billing.InstitutionAsync();
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var (status, tone) = StatusOf(c, today);
            var ledger = (await db.Ledger.Where(e => e.ChildId == id).ToListAsync()).OrderByDescending(e => e.Month).ThenByDescending(e => e.Id).ToList();
            var nextMonth = await billing.NextMonthAsync(institution);

            return Results.Ok(new
            {
                c.Id, c.FirstName, c.LastName, name = c.FullName, c.DateOfBirth, c.StartDate, c.EndDate, c.IsPartTime, c.Note, c.PaymentNumber, c.PayerId,
                group = BillingService.SettingsFor(institution).Cutover.GroupFor(c.DateOfBirth, BillingMonth.Of(today)).ToString(),
                status, tone,
                parents = c.Parents.Select(ParentDto),
                subsidies = await db.Subsidies.Where(s => s.ChildId == id).OrderByDescending(s => s.FromMonth)
                    .Select(s => new { s.Id, fromMonth = s.FromMonth, toMonth = s.ToMonth, s.FreePlace, s.SiblingDiscount }).ToListAsync(),
                manual = await db.ManualCharges.Where(m => m.ChildId == id).Select(m => new { m.Id, m.Text, m.Amount, m.Month }).ToListAsync(),
                ledger = ledger.Take(36).Select(e => new { e.Id, e.Month, e.Text, e.Amount, kind = e.Kind.ToString() }),
                balance = ledger.Sum(e => e.Amount),
                nextMonth = nextMonth.FirstDay,
            });
        });

        g.MapPut("/{id:guid}", async (Guid id, ChildDetails body, AppDb db) =>
        {
            var c = await db.Children.SingleOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();
            var errors = new Errors().Require("firstName", body.FirstName, "Barnet skal have et fornavn.");
            if (body.EndDate is not null && body.StartDate is not null && body.EndDate < body.StartDate)
                errors.Add("endDate", "Sidste dag kan ikke ligge før første dag.");
            if (errors.Any) return errors.Result();

            c.FirstName = body.FirstName!.Trim();
            c.LastName = body.LastName.Clean() ?? "";
            c.DateOfBirth = body.DateOfBirth;
            c.StartDate = body.StartDate;
            c.EndDate = body.EndDate;
            c.IsPartTime = body.IsPartTime;
            c.Note = body.Note.Clean() ?? "";
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapPost("/{id:guid}/parents", async (Guid id, ParentBody body, AppDb db) =>
        {
            var c = await db.Children.Include(x => x.Parents).SingleOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();
            var errors = new Errors().Require("name", body.Name, "Skriv forælderens navn.");
            if (!string.IsNullOrWhiteSpace(body.PostCode) && !IsPostCode(body.PostCode)) errors.Add("postCode", "Postnummer er fire cifre.");
            if (errors.Any) return errors.Result();

            var p = body.Id is { } pid ? c.Parents.SingleOrDefault(x => x.Id == pid) : null;
            if (body.Id is not null && p is null) return Results.NotFound();
            if (p is null)
            {
                p = new Parent();
                c.Parents.Add(p);
            }
            ToParent(body, p);
            c.PayerId ??= p.Id;
            await db.SaveChangesAsync();
            return Results.Ok(ParentDto(p));
        });

        g.MapDelete("/{id:guid}/parents/{parentId:guid}", async (Guid id, Guid parentId, AppDb db) =>
        {
            var c = await db.Children.Include(x => x.Parents).SingleOrDefaultAsync(x => x.Id == id);
            var p = c?.Parents.SingleOrDefault(x => x.Id == parentId);
            if (c is null || p is null) return Results.NotFound();
            c.Parents.Remove(p);
            if (c.PayerId == parentId) c.PayerId = c.Parents.FirstOrDefault()?.Id;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapPut("/{id:guid}/payer", async (Guid id, PayerBody body, AppDb db) =>
        {
            var c = await db.Children.Include(x => x.Parents).SingleOrDefaultAsync(x => x.Id == id);
            if (c is null || c.Parents.All(p => p.Id != body.ParentId)) return Results.NotFound();
            c.PayerId = body.ParentId;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapPost("/{id:guid}/subsidies", async (Guid id, SubsidyBody body, AppDb db) =>
        {
            if (!await db.Children.AnyAsync(c => c.Id == id)) return Results.NotFound();
            var errors = new Errors();
            if (!Http.TryMonth(body.FromMonth, out var from)) errors.Add("fromMonth", "Vælg den måned tilskuddet gælder fra.");
            DateOnly? to = null;
            if (body.ToMonth.Clean() is { } tm)
            {
                if (!Http.TryMonth(tm, out var t) || t < from) errors.Add("toMonth", "Slutmåneden skal ligge efter startmåneden.");
                else to = t;
            }
            if (body.FreePlace < 0 || body.SiblingDiscount < 0) errors.Add("freePlace", "Tilskud kan ikke være negativt.");
            if (errors.Any) return errors.Result();

            var s = new Subsidy { ChildId = id, FromMonth = from, ToMonth = to, FreePlace = Money.Round(body.FreePlace), SiblingDiscount = Money.Round(body.SiblingDiscount) };
            db.Subsidies.Add(s);
            await db.SaveChangesAsync();
            return Results.Ok(new { s.Id });
        });

        g.MapDelete("/{id:guid}/subsidies/{subsidyId:int}", async (Guid id, int subsidyId, AppDb db) =>
        {
            var n = await db.Subsidies.Where(s => s.Id == subsidyId && s.ChildId == id).ExecuteDeleteAsync();
            return n == 0 ? Results.NotFound() : Results.NoContent();
        });

        g.MapPost("/{id:guid}/manual", async (Guid id, ManualBody body, AppDb db, BillingService billing) =>
        {
            if (!await db.Children.AnyAsync(c => c.Id == id)) return Results.NotFound();
            var errors = new Errors().Require("text", body.Text, "Skriv en tekst.");
            if (body.Amount == 0) errors.Add("amount", "Skriv et beløb.");
            if ((body.Text?.Length ?? 0) > 45) errors.Add("text", "Højst 45 tegn, så det passer på betalingsoversigten.");
            if (errors.Any) return errors.Result();

            var month = await billing.NextMonthAsync(await billing.InstitutionAsync());
            var m = new ManualCharge { ChildId = id, Month = month.FirstDay, Text = body.Text!.Trim(), Amount = Money.Round(body.Amount) };
            db.ManualCharges.Add(m);
            await db.SaveChangesAsync();
            return Results.Ok(new { m.Id, month = month.FirstDay });
        });

        g.MapDelete("/{id:guid}/manual/{manualId:int}", async (Guid id, int manualId, AppDb db) =>
        {
            var n = await db.ManualCharges.Where(m => m.Id == manualId && m.ChildId == id).ExecuteDeleteAsync();
            return n == 0 ? Results.NotFound() : Results.NoContent();
        });

        // For payments Betalingsservice did not book: payment slips, bank transfers, cash.
        g.MapPost("/{id:guid}/payments", async (Guid id, PaymentBody body, AppDb db, TimeProvider clock) =>
        {
            if (!await db.Children.AnyAsync(c => c.Id == id)) return Results.NotFound();
            var errors = new Errors();
            if (body.Amount <= 0) errors.Add("amount", "Skriv beløbet, der er betalt.");
            if (!Http.TryMonth(body.Month, out var month)) errors.Add("month", "Vælg hvilken måned betalingen er for.");
            if (errors.Any) return errors.Result();

            var paidOn = body.PaidOn ?? DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var e = new Data.LedgerEntry
            {
                ChildId = id, Month = month, Kind = LedgerKind.Payment, LineKind = "ManualPayment",
                Text = $"Betaling registreret {Fmt.Date(paidOn)}", Amount = -Money.Round(body.Amount),
            };
            db.Ledger.Add(e);
            await db.SaveChangesAsync();
            return Results.Ok(new { e.Id });
        });

        // Undo of a manually registered payment. Payments from Betalingsservice files and charges cannot be deleted.
        g.MapDelete("/{id:guid}/payments/{entryId:int}", async (Guid id, int entryId, AppDb db) =>
        {
            var n = await db.Ledger.Where(e => e.Id == entryId && e.ChildId == id && e.LineKind == "ManualPayment").ExecuteDeleteAsync();
            return n == 0 ? Results.NotFound() : Results.NoContent();
        });
    }

    private static bool IsPostCode(string? s) => s is { Length: 4 } && s.All(char.IsAsciiDigit);

    private static Parent ToParent(ParentBody b, Parent p)
    {
        p.Name = b.Name!.Trim();
        p.Email = b.Email.Clean() ?? "";
        p.Phone = b.Phone.Clean() ?? "";
        p.Address = b.Address.Clean() ?? "";
        p.PostCode = b.PostCode.Clean() ?? "";
        p.City = b.City.Clean() ?? "";
        return p;
    }
}
