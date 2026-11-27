using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Systematikken.Api.Data;
using Systematikken.Billing;

namespace Systematikken.Api.Services;

/// <summary>
/// The demo on demo.systematikken.dk: one made-up institution with Danish children, parents, a waiting list and
/// two billed months, so people can try everything. Only runs when Demo:Enabled is on. All names, phone numbers
/// and addresses are made up; e-mails use example.com, phone numbers start with 11, which no Danish number does.
/// </summary>
public sealed class DemoData(IServiceScopeFactory scopes, TimeProvider clock, ILogger<DemoData> log)
{
    public const string DemoEmail = "demo@example.com";

    private static readonly string[] Girls = ["Ida", "Emma", "Alma", "Freja", "Clara", "Ella", "Karla", "Agnes", "Olivia", "Ellie", "Lærke", "Asta", "Saga", "Josefine", "Vilma", "Luna"];
    private static readonly string[] Boys = ["William", "Noah", "Oscar", "Carl", "Valdemar", "Aksel", "Malthe", "August", "Emil", "Alfred", "Arthur", "Viggo", "Theo", "Lucas", "Storm", "Bertram"];
    private static readonly string[] LastNames = ["Jensen", "Nielsen", "Hansen", "Pedersen", "Andersen", "Christensen", "Larsen", "Sørensen", "Rasmussen", "Jørgensen", "Petersen", "Madsen", "Kristensen", "Olsen", "Thomsen", "Poulsen", "Johansen", "Mortensen", "Lund", "Holm"];
    private static readonly string[] Mothers = ["Mette", "Louise", "Camilla", "Sarah", "Julie", "Anne", "Maria", "Katrine", "Line", "Signe", "Cecilie", "Maja"];
    private static readonly string[] Fathers = ["Mads", "Rasmus", "Kasper", "Martin", "Thomas", "Jonas", "Anders", "Christian", "Mikkel", "Simon", "Frederik", "Jakob"];
    private static readonly string[] Streets = ["Kastanievej", "Bøgeallé", "Mælkevej", "Æblehaven", "Rosenvænget", "Skovbrynet", "Engvej", "Birkelunden"];

    /// <summary>Deletes everything and builds the demo again. Called at start-up and every night.</summary>
    public async Task ResetAsync()
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDb>();

        SqliteConnection.ClearAllPools();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await Seed.MunicipalitiesAsync(db);
        await BuildAsync(sp, db);
        log.LogInformation("Demo data rebuilt");
    }

    private async Task BuildAsync(IServiceProvider sp, AppDb db)
    {
        var rnd = new Random(2026); // the same demo every night
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var now = clock.GetUtcNow().UtcDateTime;

        var users = sp.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = DemoEmail, Email = DemoEmail, EmailConfirmed = true, DisplayName = "Demo" };
        await users.CreateAsync(user);

        var aarhus = await db.Municipalities.SingleAsync(m => m.Name == "Aarhus");
        aarhus.CutoverYears = 2; aarhus.CutoverMonths = 11; aarhus.SchoolStartMonth = 8; aarhus.Confirmed = true;

        var institution = new Institution
        {
            Name = "Børnehuset Mælkebøtten",
            Slug = "maelkeboetten",
            ContactEmail = "kontakt@example.com",
            ContactPhone = "11 22 33 44",
            MunicipalityId = aarhus.Id,
            NurseryPrice = 4150m,
            KindergartenPrice = 2480m,
            MealScheme = 350m,
            Deposit = 1000m,
            DepositMonthsAhead = 1,
            NurseryCapacity = 12,
            KindergartenCapacity = 22,
            Cvr = "12345678",
            PbsNumber = "87654321",
            BetalingsserviceText = "Børnehuset Mælkebøtten",
            FirstBillingMonth = thisMonth.AddMonths(-1),
        };
        db.Institutions.Add(institution);
        db.Memberships.Add(new Membership { InstitutionId = institution.Id, UserId = user.Id });
        await db.SaveChangesAsync();
        sp.GetRequiredService<Tenant>().InstitutionId = institution.Id;

        string Phone() => $"11 {rnd.Next(10, 99)} {rnd.Next(10, 99)} {rnd.Next(10, 99)}";
        string Address() => $"{Streets[rnd.Next(Streets.Length)]} {rnd.Next(1, 60)}";

        // Families: most have one child, some have two (siblings share parents and get a sibling discount).
        var children = sp.GetRequiredService<ChildService>();
        var created = new List<Child>();
        var usedLast = new HashSet<string>();
        for (var family = 0; family < 26; family++)
        {
            var last = LastNames[family % LastNames.Length] + (usedLast.Contains(LastNames[family % LastNames.Length]) ? "-" + LastNames[(family + 7) % LastNames.Length] : "");
            usedLast.Add(LastNames[family % LastNames.Length]);
            var mother = new Parent { Name = $"{Mothers[rnd.Next(Mothers.Length)]} {last}", Email = $"familien.{Slug(last)}@example.com", Phone = Phone(), Address = Address(), PostCode = "8000", City = "Aarhus C" };
            var father = new Parent { Name = $"{Fathers[rnd.Next(Fathers.Length)]} {last}", Email = $"far.{Slug(last)}@example.com", Phone = Phone(), Address = mother.Address, PostCode = "8000", City = "Aarhus C" };
            var kids = family % 5 == 0 ? 2 : 1;

            for (var k = 0; k < kids; k++)
            {
                var ageDays = rnd.Next(320, 2050); // about 10 months to 5½ years
                var dob = today.AddDays(-ageDays);
                var start = dob.AddMonths(rnd.Next(9, 14));
                start = new DateOnly(start.Year, start.Month, rnd.Next(4) == 0 ? 16 : 1);
                if (start > today) start = new DateOnly(today.Year, today.Month, 1);
                var girl = rnd.Next(2) == 0;
                var child = new Child
                {
                    FirstName = girl ? Girls[rnd.Next(Girls.Length)] : Boys[rnd.Next(Boys.Length)],
                    LastName = last,
                    DateOfBirth = dob,
                    StartDate = start,
                    IsPartTime = rnd.Next(12) == 0,
                };
                // Parents are shared between siblings: the second child reuses the first child's parents.
                var parents = k == 0 ? new[] { mother, father } : created[^1].Parents.ToArray();
                created.Add(await children.CreateAsync(child, parents));
            }
        }

        // Two children who start soon (one next month, one the month after), and one leaving at the end of next month.
        foreach (var (first, last, months) in new[] { ("Asta", "Bach", 1), ("Bertram", "Krogh", 2) })
        {
            var p = new Parent { Name = $"Signe {last}", Email = $"{Slug(last)}@example.com", Phone = Phone(), Address = Address(), PostCode = "8000", City = "Aarhus C" };
            created.Add(await children.CreateAsync(new Child { FirstName = first, LastName = last, DateOfBirth = today.AddMonths(-11 - months), StartDate = thisMonth.AddMonths(months) }, [p]));
        }
        var leaving = created.OrderByDescending(c => c.DateOfBirth).Last();
        leaving.EndDate = thisMonth.AddMonths(2).AddDays(-1);

        // Subsidies from the municipality: free place for a few, sibling discount for the younger siblings.
        foreach (var c in created.Where((_, i) => i % 7 == 3))
            db.Subsidies.Add(new Subsidy { ChildId = c.Id, FromMonth = thisMonth.AddMonths(-6), FreePlace = 1240m, SiblingDiscount = 0 });
        foreach (var group in created.GroupBy(c => c.LastName).Where(g => g.Count() == 2))
            db.Subsidies.Add(new Subsidy { ChildId = group.OrderBy(c => c.DateOfBirth).Last().Id, FromMonth = thisMonth.AddMonths(-6), SiblingDiscount = 1240m });
        // One free place that ran out this month: the next billing month asks the manager about it.
        var expired = created.First(c => c.StartDate < thisMonth && !db.Subsidies.Local.Any(s => s.ChildId == c.Id));
        db.Subsidies.Add(new Subsidy { ChildId = expired.Id, FromMonth = thisMonth.AddMonths(-4), ToMonth = thisMonth, FreePlace = 2075m });
        await db.SaveChangesAsync();

        // Bill last month and this month, and book the payments, except two families who have not paid yet.
        var billing = sp.GetRequiredService<BillingService>();
        var inst = await billing.InstitutionAsync();
        while ((await billing.NextMonthAsync(inst)).FirstDay <= thisMonth)
        {
            var run = await billing.ApproveAsync(inst, await billing.NextMonthAsync(inst), DemoEmail);
            var charged = (await db.Ledger.Where(e => e.BillingRunId == run.Id).ToListAsync()).GroupBy(e => e.ChildId);
            var skip = run.Month == thisMonth ? 2 : 0;
            foreach (var g in charged.Skip(skip))
                db.Ledger.Add(new Data.LedgerEntry
                {
                    ChildId = g.Key, Month = run.Month, Kind = LedgerKind.Payment, LineKind = "Payment",
                    Text = $"Betalingsservice {run.Month:dd-MM-yyyy}", Amount = -g.Sum(e => e.Amount),
                });
            await db.SaveChangesAsync();
        }
        db.ManualCharges.Add(new ManualCharge { ChildId = created[4].Id, Month = (await billing.NextMonthAsync(inst)).FirstDay, Text = "Udflugt til Dyrehaven", Amount = 75m });

        // The waiting list: mostly waiting, some new this week, one offer without an answer, one yes, one no.
        var applicants = new List<Applicant>();
        for (var i = 0; i < 12; i++)
        {
            var girl = rnd.Next(2) == 0;
            var last = LastNames[(i * 3 + 5) % LastNames.Length];
            var dob = today.AddDays(-rnd.Next(-120, 500)); // some are not born yet
            applicants.Add(new Applicant
            {
                ChildName = $"{(girl ? Girls[rnd.Next(Girls.Length)] : Boys[rnd.Next(Boys.Length)])} {last}",
                DateOfBirth = dob,
                DesiredStart = new DateOnly(dob.Year, dob.Month, 1).AddMonths(rnd.Next(10, 14)) is var d && d < thisMonth ? thisMonth.AddMonths(rnd.Next(2, 9)) : d,
                HasSibling = i % 4 == 1,
                Parent1Name = $"{Mothers[rnd.Next(Mothers.Length)]} {last}",
                Parent1Email = $"{Slug(last)}{i}@example.com",
                Parent1Phone = Phone(),
                Parent2Name = i % 3 == 0 ? "" : $"{Fathers[rnd.Next(Fathers.Length)]} {last}",
                Address = Address(),
                PostCode = "8000",
                City = "Aarhus C",
                CreatedAt = now.AddDays(-(i < 2 ? i + 1 : 20 + i * 17)),
                Note = i == 5 ? "Vi bor tæt på og cykler forbi hver dag." : "",
            });
        }
        var offered = applicants[6];
        offered.Status = ApplicantStatus.Offered; offered.OfferedAt = now.AddDays(-7); offered.OfferedStart = thisMonth.AddMonths(2);
        var accepted = applicants[7];
        accepted.Status = ApplicantStatus.Accepted; accepted.OfferedAt = now.AddDays(-10); accepted.AnsweredAt = now.AddDays(-2); accepted.OfferedStart = thisMonth.AddMonths(2);
        var declined = applicants[8];
        declined.Status = ApplicantStatus.Declined; declined.OfferedAt = now.AddDays(-30); declined.AnsweredAt = now.AddDays(-25); declined.ClosedAt = now.AddDays(-25); declined.OfferedStart = thisMonth.AddMonths(1);
        db.Applicants.AddRange(applicants);
        await db.SaveChangesAsync();
    }

    private static string Slug(string s) =>
        s.ToLowerInvariant().Replace("æ", "ae").Replace("ø", "oe").Replace("å", "aa").Replace(" ", "").Replace("-", "");
}

/// <summary>Builds the demo at start-up and again every night at about 03:00.</summary>
public sealed class DemoResetService(DemoData demo, TimeProvider clock, ILogger<DemoResetService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = clock.GetLocalNow();
            var next = now.Date.AddDays(1).AddHours(3);
            try { await Task.Delay(next - now.DateTime, clock, stoppingToken); }
            catch (OperationCanceledException) { return; }
            try { await demo.ResetAsync(); }
            catch (Exception ex) { log.LogError(ex, "Demo reset failed"); }
        }
    }
}
