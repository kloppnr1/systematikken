using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Systematikken.Api.Data;
using Systematikken.Api.Services;

namespace Systematikken.Api.Endpoints;

/// <summary>Creating an institution, inviting colleagues, and the institution's settings.</summary>
public static class InstitutionEndpoints
{
    public sealed record CreateInstitution(string? Name, int? MunicipalityId, string? ContactEmail, int CutoverYears, int CutoverMonths, int SchoolStartMonth);

    public sealed record SettingsDto(
        string Name, string ContactEmail, string ContactPhone,
        decimal NurseryPrice, decimal KindergartenPrice, decimal? NurseryPartTimePrice, decimal? KindergartenPartTimePrice,
        int? MonthWithoutCharge, int HalfMonthDay, decimal? Deposit, int DepositMonthsAhead, decimal? SignupFee, decimal? MealScheme,
        int NurseryCapacity, int KindergartenCapacity,
        string Cvr, string PbsNumber, string DebtorGroup, string BetalingsserviceText,
        string FirstBillingMonth, int WaitingListRetentionMonths);

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/municipalities", async (AppDb db) =>
            await db.Municipalities.OrderBy(m => m.Name)
                .Select(m => new { m.Id, m.Name, m.Confirmed, m.CutoverYears, m.CutoverMonths, m.SchoolStartMonth }).ToListAsync());

        app.MapPost("/api/institutions", async (CreateInstitution body, HttpContext ctx, AppDb db, Tenant tenant, TimeProvider clock) =>
        {
            if (tenant.InstitutionId is not null) return Results.Conflict();
            var errors = new Errors()
                .Require("name", body.Name, "Skriv institutionens navn.")
                .Require("contactEmail", body.ContactEmail, "Skriv en e-mail, forældre kan skrive til.");
            if (body.ContactEmail is { } e && e.Length > 0 && !e.Contains('@')) errors.Add("contactEmail", "Det ligner ikke en e-mailadresse.");
            var municipality = await db.Municipalities.SingleOrDefaultAsync(m => m.Id == body.MunicipalityId);
            if (municipality is null) errors.Add("municipalityId", "Vælg kommune.");
            else if (!municipality.Confirmed && (body.CutoverYears is < 1 or > 4 || body.CutoverMonths is < 0 or > 11 || body.SchoolStartMonth is < 1 or > 12))
                errors.Add("cutoverYears", "Tjek alder og skolestart.");
            if (errors.Any) return errors.Result();

            if (!municipality!.Confirmed)
            {
                municipality.CutoverYears = body.CutoverYears;
                municipality.CutoverMonths = body.CutoverMonths;
                municipality.SchoolStartMonth = body.SchoolStartMonth;
                municipality.Confirmed = true;
            }

            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var name = body.Name!.Trim();
            var institution = new Institution
            {
                Name = name,
                Slug = await UniqueSlugAsync(db, name),
                MunicipalityId = municipality.Id,
                ContactEmail = body.ContactEmail!.Trim(),
                BetalingsserviceText = name.Length > 60 ? name[..60] : name,
                FirstBillingMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(1),
            };
            db.Institutions.Add(institution);
            db.Memberships.Add(new Membership { InstitutionId = institution.Id, UserId = ctx.User.UserId() });
            await db.SaveChangesAsync();
            return Results.Ok(new { institution.Id, institution.Name, institution.Slug });
        });

        app.MapGet("/api/invitations/{token}", async (string token, HttpContext ctx, AppDb db, TimeProvider clock) =>
        {
            var inv = await FindInvitation(db, token, clock);
            if (inv is null) return Results.NotFound();
            var name = await db.Institutions.Where(i => i.Id == inv.InstitutionId).Select(i => i.Name).SingleAsync();
            return Results.Ok(new { institutionName = name, invitedEmail = inv.Email, matches = inv.Email == ctx.User.Email() });
        });

        app.MapPost("/api/invitations/{token}/accept", async (string token, HttpContext ctx, AppDb db, TimeProvider clock) =>
        {
            var inv = await FindInvitation(db, token, clock);
            if (inv is null) return Results.NotFound();
            if (inv.Email != ctx.User.Email()) return Results.Problem(detail: $"Invitationen er sendt til {inv.Email}.", statusCode: 403);
            var userId = ctx.User.UserId();
            if (!await db.Memberships.AnyAsync(m => m.UserId == userId && m.InstitutionId == inv.InstitutionId))
                db.Memberships.Add(new Membership { UserId = userId, InstitutionId = inv.InstitutionId });
            inv.AcceptedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        var g = app.MapGroup("/api").AddEndpointFilter<RequireInstitution>();

        g.MapGet("/settings", async (AppDb db) =>
        {
            var i = await db.Institutions.Include(x => x.Municipality).SingleAsync(x => x.Id == db.InstitutionId);
            return Results.Ok(new
            {
                settings = ToDto(i),
                municipality = new { i.Municipality!.Name, i.Municipality.CutoverYears, i.Municipality.CutoverMonths, i.Municipality.SchoolStartMonth },
                slug = i.Slug,
                billingStarted = await db.BillingRuns.AnyAsync(),
            });
        });

        g.MapPut("/settings", async (SettingsDto s, AppDb db) =>
        {
            var i = await db.Institutions.SingleAsync(x => x.Id == db.InstitutionId);
            var errors = new Errors().Require("name", s.Name, "Skriv institutionens navn.");
            if (s.NurseryPrice < 0) errors.Add("nurseryPrice", "Prisen kan ikke være negativ.");
            if (s.KindergartenPrice < 0) errors.Add("kindergartenPrice", "Prisen kan ikke være negativ.");
            if (s.HalfMonthDay is < 2 or > 28) errors.Add("halfMonthDay", "Vælg en dag mellem 2 og 28.");
            if (s.MonthWithoutCharge is < 1 or > 12) errors.Add("monthWithoutCharge", "Vælg en måned.");
            if (s.DepositMonthsAhead is < 0 or > 12) errors.Add("depositMonthsAhead", "Mellem 0 og 12 måneder.");
            if (s.Cvr.Length > 0 && !(s.Cvr.Length == 8 && s.Cvr.All(char.IsAsciiDigit))) errors.Add("cvr", "CVR-nummer er 8 cifre.");
            if (s.PbsNumber.Length > 0 && !(s.PbsNumber.Length == 8 && s.PbsNumber.All(char.IsAsciiDigit))) errors.Add("pbsNumber", "PBS-nummer er 8 cifre.");
            if (!(s.DebtorGroup.Length is > 0 and <= 5 && s.DebtorGroup.All(char.IsAsciiDigit))) errors.Add("debtorGroup", "Debitorgruppe er op til 5 cifre.");
            if (s.BetalingsserviceText.Length > 60) errors.Add("betalingsserviceText", "Højst 60 tegn.");
            if (s.NurseryCapacity < 0 || s.KindergartenCapacity < 0) errors.Add("nurseryCapacity", "Antal pladser kan ikke være negativt.");
            if (s.WaitingListRetentionMonths is < 1 or > 60) errors.Add("waitingListRetentionMonths", "Mellem 1 og 60 måneder.");
            if (!Http.TryMonth(s.FirstBillingMonth, out var first)) errors.Add("firstBillingMonth", "Vælg en måned.");
            if (errors.Any) return errors.Result();

            i.Name = s.Name.Trim();
            i.ContactEmail = s.ContactEmail.Trim();
            i.ContactPhone = s.ContactPhone.Trim();
            i.NurseryPrice = s.NurseryPrice;
            i.KindergartenPrice = s.KindergartenPrice;
            i.NurseryPartTimePrice = s.NurseryPartTimePrice;
            i.KindergartenPartTimePrice = s.KindergartenPartTimePrice;
            i.MonthWithoutCharge = s.MonthWithoutCharge;
            i.HalfMonthDay = s.HalfMonthDay;
            i.Deposit = s.Deposit is > 0 ? s.Deposit : null;
            i.DepositMonthsAhead = s.DepositMonthsAhead;
            i.SignupFee = s.SignupFee is > 0 ? s.SignupFee : null;
            i.MealScheme = s.MealScheme is > 0 ? s.MealScheme : null;
            i.NurseryCapacity = s.NurseryCapacity;
            i.KindergartenCapacity = s.KindergartenCapacity;
            i.Cvr = s.Cvr;
            i.PbsNumber = s.PbsNumber;
            i.DebtorGroup = s.DebtorGroup.PadLeft(5, '0');
            i.BetalingsserviceText = s.BetalingsserviceText.Trim();
            i.WaitingListRetentionMonths = s.WaitingListRetentionMonths;
            if (!await db.BillingRuns.AnyAsync()) i.FirstBillingMonth = first; // fixed once billing has started
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(i));
        });

        // All the institution's data in one ZIP. The data belongs to the institution.
        g.MapGet("/export", async (ExportService export, TimeProvider clock) =>
            Results.File(await export.CreateAsync(), "application/zip", $"systematikken-data-{clock.GetLocalNow():yyyy-MM-dd}.zip"));

        g.MapGet("/staff", async (AppDb db, HttpContext ctx, TimeProvider clock) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var members = await db.Memberships.Where(m => m.InstitutionId == db.InstitutionId)
                .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { m.Id, u.Email, u.DisplayName, me = u.Id == ctx.User.UserId() })
                .ToListAsync();
            var invitations = await db.Invitations.Where(i => i.AcceptedAt == null && i.ExpiresAt > now)
                .Select(i => new { i.Id, i.Email, i.ExpiresAt, url = $"{ctx.Request.BaseUrl()}/invitation/{i.Token}" }).ToListAsync();
            return Results.Ok(new { members, invitations });
        });

        g.MapPost("/staff/invitations", async (InviteBody body, AppDb db, HttpContext ctx, TimeProvider clock) =>
        {
            var email = body.Email?.Trim().ToLowerInvariant() ?? "";
            if (!email.Contains('@')) return Http.Invalid("email", "Skriv kollegaens e-mailadresse.");
            var inv = new Invitation { Email = email, Token = Tokens.New(), ExpiresAt = clock.GetUtcNow().UtcDateTime.AddDays(14) };
            db.Invitations.Add(inv);
            await db.SaveChangesAsync();
            return Results.Ok(new { inv.Id, inv.Email, inv.ExpiresAt, url = $"{ctx.Request.BaseUrl()}/invitation/{inv.Token}" });
        });

        g.MapDelete("/staff/invitations/{id:int}", async (int id, AppDb db) =>
        {
            await db.Invitations.Where(i => i.Id == id).ExecuteDeleteAsync();
            return Results.NoContent();
        });

        g.MapDelete("/staff/members/{id:int}", async (int id, AppDb db, HttpContext ctx) =>
        {
            var m = await db.Memberships.SingleOrDefaultAsync(x => x.Id == id && x.InstitutionId == db.InstitutionId);
            if (m is null) return Results.NotFound();
            if (m.UserId == ctx.User.UserId()) return Http.Invalid("member", "Du kan ikke fjerne dig selv.");
            db.Memberships.Remove(m);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    public sealed record InviteBody(string? Email);

    private static SettingsDto ToDto(Institution i) => new(
        i.Name, i.ContactEmail, i.ContactPhone,
        i.NurseryPrice, i.KindergartenPrice, i.NurseryPartTimePrice, i.KindergartenPartTimePrice,
        i.MonthWithoutCharge, i.HalfMonthDay, i.Deposit, i.DepositMonthsAhead, i.SignupFee, i.MealScheme,
        i.NurseryCapacity, i.KindergartenCapacity,
        i.Cvr, i.PbsNumber, i.DebtorGroup, i.BetalingsserviceText,
        i.FirstBillingMonth.ToString("yyyy-MM"), i.WaitingListRetentionMonths);

    private static async Task<Invitation?> FindInvitation(AppDb db, string token, TimeProvider clock)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return await db.Invitations.IgnoreQueryFilters().SingleOrDefaultAsync(i => i.Token == token && i.AcceptedAt == null && i.ExpiresAt > now);
    }

    public static async Task<string> UniqueSlugAsync(AppDb db, string name)
    {
        var normalized = name.ToLowerInvariant().Replace("æ", "ae").Replace("ø", "oe").Replace("å", "aa").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsAsciiLetterOrDigit(ch) ? ch : '-');
        }
        var slug = string.Join('-', sb.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (slug.Length == 0) slug = "institution";
        if (slug.Length > 40) slug = slug[..40].Trim('-');

        var candidate = slug;
        for (var n = 2; await db.Institutions.AnyAsync(i => i.Slug == candidate); n++) candidate = $"{slug}-{n}";
        return candidate;
    }
}
