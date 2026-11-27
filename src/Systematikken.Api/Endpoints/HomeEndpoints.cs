using Microsoft.EntityFrameworkCore;
using Systematikken.Api.Data;
using Systematikken.Api.Services;
using Systematikken.Billing;
using Systematikken.Billing.Occupancy;

namespace Systematikken.Api.Endpoints;

/// <summary>The to-do list, search and the occupancy report.</summary>
public static class HomeEndpoints
{
    public sealed record TodoItem(string Id, string Title, string Detail, string Link, string Action, bool Important = false);

    public static void Map(WebApplication app)
    {
        var g = app.MapGroup("/api").AddEndpointFilter<RequireInstitution>();

        // Worked out from the data each time. Nothing to tick off by hand: an item goes away when the thing is done.
        g.MapGet("/todo", async (AppDb db, BillingService billing, TimeProvider clock) =>
        {
            var institution = await billing.InstitutionAsync();
            var now = clock.GetUtcNow().UtcDateTime;
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var items = new List<TodoItem>();
            var setup = new List<TodoItem>();

            if (institution.NurseryPrice == 0 && institution.KindergartenPrice == 0)
                setup.Add(new("prices", "Tilføj jeres priser", "Pris for vuggestue og børnehave pr. måned.", "/indstillinger#priser", "Tilføj priser"));
            if (institution.PbsNumber.Length == 0 || institution.Cvr.Length == 0)
                setup.Add(new("bs", "Udfyld jeres Betalingsservice-aftale", "PBS-nummer og CVR-nummer fra jeres bank.", "/indstillinger#betalingsservice", "Udfyld"));
            if (institution.NurseryCapacity == 0 && institution.KindergartenCapacity == 0)
                setup.Add(new("capacity", "Sæt antal pladser", "Så kan I se, hvor mange pladser der er ledige måned for måned.", "/indstillinger#pladser", "Sæt pladser"));
            if (await db.Memberships.CountAsync(m => m.InstitutionId == institution.Id) < 2)
                setup.Add(new("colleague", "Inviter en kollega", "I bør være to, så den ene kan hjælpe den anden ind, hvis login driller.", "/indstillinger#kolleger", "Inviter"));
            if (!await db.Applicants.AnyAsync() && !await db.Children.AnyAsync())
                setup.Add(new("link", "Del jeres tilmeldingslink", "Læg linket på jeres hjemmeside, så forældre kan skrive sig op.", "/venteliste#link", "Se linket"));

            var weekAgo = now.AddDays(-7);
            var newCount = await db.Applicants.CountAsync(a => a.Status == ApplicantStatus.Waiting && a.CreatedAt > weekAgo);
            if (newCount > 0)
                items.Add(new("new", $"{newCount} ny{(newCount == 1 ? "" : "e")} på ventelisten", "Skrevet op inden for den sidste uge.", "/venteliste", "Se ventelisten"));

            var reminderDue = now.AddDays(-5);
            foreach (var a in await db.Applicants.Where(a => a.Status == ApplicantStatus.Offered && a.OfferedAt < reminderDue).ToListAsync())
                items.Add(new($"remind-{a.Id}", $"{a.ChildName} har ikke svaret på tilbuddet", $"Tilbudt {Fmt.Date(a.OfferedAt!.Value)}.", $"/venteliste/{a.Id}", "Send en påmindelse"));

            foreach (var a in await db.Applicants.Where(a => a.Status == ApplicantStatus.Accepted).ToListAsync())
                items.Add(new($"enrol-{a.Id}", $"{a.ChildName} har sagt ja", $"Start {Fmt.Date(a.OfferedStart)}. Indmeld {a.ChildName.Split(' ')[0]}, så kommer barnet med i opkrævningen.", $"/venteliste/{a.Id}", "Indmeld", true));

            foreach (var c in await db.Children.Where(c => c.PayerId == null && (c.EndDate == null || c.EndDate >= today)).ToListAsync())
                items.Add(new($"payer-{c.Id}", $"{c.FullName} har ingen betaler", "Vælg hvem der betaler.", $"/born/{c.Id}", "Vælg betaler", true));

            if (institution.NurseryPrice > 0 || institution.KindergartenPrice > 0)
            {
                var next = await billing.NextMonthAsync(institution);
                if (next.FirstDay.AddDays(-20) <= today)
                {
                    var draft = await billing.DraftAsync(institution, next);
                    var blocking = draft.Blocking.Count();
                    items.Add(blocking > 0
                        ? new("billing", $"Opkrævning for {Fmt.Month(next)}: {blocking} skal rettes", "Ret dem, så kan måneden godkendes.", "/opkraevning", "Ret og godkend", true)
                        : new("billing", $"Opkrævning for {Fmt.Month(next)} er klar", $"{draft.Result.ToCollect.Count()} opkrævninger på {Fmt.Kr(draft.Result.ToCollect.Sum(c => c.AmountToPay))}.", "/opkraevning", "Se og godkend", true));
                }
            }

            var runs = await db.BillingRuns.OrderByDescending(r => r.Month).Take(3).ToListAsync();
            var paidMonths = (await db.Ledger.Where(e => e.Kind == LedgerKind.Payment).Select(e => e.Month).Distinct().ToListAsync()).ToHashSet();
            foreach (var run in runs.Where(r => !paidMonths.Contains(r.Month) && today >= r.Month.AddDays(3)))
                items.Add(new($"import-{run.Id}", $"Indlæs betalinger for {Fmt.Month(run.Month)}", "Hent betalingsfilen hos jeres bank og læg den ind her.", "/opkraevning/betalinger", "Indlæs betalinger"));

            if (runs.FirstOrDefault() is { } last && paidMonths.Contains(last.Month))
            {
                var owing = (await db.Ledger.Select(e => new { e.ChildId, e.Amount }).ToListAsync())
                    .GroupBy(e => e.ChildId).Count(x => x.Sum(e => e.Amount) > 0);
                if (owing > 0)
                    items.Add(new("owing", $"{owing} betaler{(owing == 1 ? "" : "e")} mangler at betale", "Beløbet kommer med på næste opkrævning.", "/born?filter=gaeld", "Se hvem"));
            }

            return Results.Ok(new { items = items.OrderByDescending(i => i.Important), setup });
        });

        // One box for children, parents and the waiting list.
        g.MapGet("/search", async (string? q, AppDb db) =>
        {
            q = q?.Trim() ?? "";
            if (q.Length < 2) return Results.Ok(new { children = Array.Empty<object>(), parents = Array.Empty<object>(), applicants = Array.Empty<object>() });
            bool Hit(params string?[] f) => f.Any(x => x is not null && x.Contains(q, StringComparison.CurrentCultureIgnoreCase));

            // SQLite compares case-sensitively outside ASCII, so filter in memory. Institutions are small.
            var children = await db.Children.Include(c => c.Parents).ToListAsync();
            var applicants = await db.Applicants.Where(a => a.Status != ApplicantStatus.Enrolled && a.Status != ApplicantStatus.Removed).ToListAsync();
            return Results.Ok(new
            {
                children = children.Where(c => Hit(c.FullName, c.PaymentNumber.ToString())).Take(20).Select(c => new { c.Id, name = c.FullName, c.DateOfBirth }),
                parents = children.SelectMany(c => c.Parents.Select(p => new { p, c })).Where(x => Hit(x.p.Name, x.p.Email, x.p.Phone))
                    .DistinctBy(x => x.p.Id).Take(20).Select(x => new { x.p.Id, x.p.Name, x.p.Phone, x.p.Email, childId = x.c.Id, childName = x.c.FullName }),
                applicants = applicants.Where(a => Hit(a.ChildName, a.Parent1Name, a.Parent2Name, a.Parent1Email, a.Parent2Email, a.Parent1Phone, a.Parent2Phone))
                    .Take(20).Select(a => new { a.Id, a.ChildName, a.DesiredStart }),
            });
        });

        g.MapGet("/occupancy", async (string? from, int? months, AppDb db, BillingService billing, TimeProvider clock) =>
        {
            var institution = await billing.InstitutionAsync();
            var start = Http.TryMonth(from, out var f) ? BillingMonth.Of(f) : BillingMonth.Of(DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
            var count = Math.Clamp(months ?? 12, 1, 24);
            var settings = new OccupancySettings(BillingService.SettingsFor(institution).Cutover, institution.Municipality!.SchoolStartMonth,
                institution.NurseryCapacity, institution.KindergartenCapacity);

            var children = (await db.Children.Where(c => c.StartDate != null).ToListAsync())
                .Select(c => new OccupancyPerson(c.Id, c.FullName, c.DateOfBirth, c.StartDate!.Value, c.EndDate)).ToList();
            // O4: only those still in the running count on the waiting-list layer.
            var applicants = (await db.Applicants.Where(a => a.Status == ApplicantStatus.Waiting || a.Status == ApplicantStatus.Offered).ToListAsync())
                .Select(a => new OccupancyPerson(a.Id, a.ChildName, a.DateOfBirth, a.OfferedStart ?? a.DesiredStart, null)).ToList();

            var report = OccupancyReport.Build(start, count, settings, children, applicants);
            object Group(OccupancyGroup x) => new
            {
                x.Count, x.Capacity, x.FreePlaces, x.WaitingList,
                present = x.Present.Select(p => new { p.Id, p.Name }),
                joining = x.Joining.Select(p => new { p.Id, p.Name }),
                leaving = x.Leaving.Select(p => new { p.Id, p.Name }),
            };
            return Results.Ok(new
            {
                schoolStartMonth = settings.SchoolStartMonth,
                months = report.Select(m => new { month = m.Month.FirstDay, nursery = Group(m.Nursery), kindergarten = Group(m.Kindergarten) }),
            });
        });
    }
}
