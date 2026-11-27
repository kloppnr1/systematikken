using Microsoft.EntityFrameworkCore;
using Systematikken.Api.Data;

namespace Systematikken.Api.Endpoints;

/// <summary>What parents use without logging in: the sign-up form and the answer to an offered place.</summary>
public static class PublicEndpoints
{
    public sealed record SignupBody(
        string? ChildName, DateOnly? DateOfBirth, DateOnly? DesiredStart, bool HasSibling, string? Note,
        string? Parent1Name, string? Parent1Email, string? Parent1Phone,
        string? Parent2Name, string? Parent2Email, string? Parent2Phone,
        string? Address, string? PostCode, string? City,
        bool Consent,
        string? Website);

    public sealed record AnswerBody(bool Yes);

    public static void Map(WebApplication app)
    {
        var g = app.MapGroup("/api/public").AllowAnonymous().RequireRateLimiting("public");

        g.MapGet("/institutions/{slug}", async (string slug, AppDb db) =>
            await db.Institutions.Where(i => i.Slug == slug)
                .Select(i => new { i.Name, i.ContactEmail, i.WaitingListRetentionMonths })
                .SingleOrDefaultAsync() is { } i ? Results.Ok(i) : Results.NotFound());

        g.MapPost("/institutions/{slug}/signup", async (string slug, SignupBody b, AppDb db, Tenant tenant, TimeProvider clock) =>
        {
            var institution = await db.Institutions.SingleOrDefaultAsync(i => i.Slug == slug);
            if (institution is null) return Results.NotFound();

            // A hidden field people leave empty and spam robots fill in. Pretend it worked.
            if (!string.IsNullOrEmpty(b.Website)) return Results.Ok(new { receipt = Tokens.New()[..8].ToUpperInvariant() });

            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var errors = new Errors()
                .Require("childName", b.ChildName, "Skriv barnets navn.")
                .Require("parent1Name", b.Parent1Name, "Skriv dit navn.")
                .Require("parent1Phone", b.Parent1Phone, "Skriv dit telefonnummer.")
                .Require("address", b.Address, "Skriv jeres adresse.")
                .Require("city", b.City, "Skriv by.");
            if (b.Parent1Email is not { } e1 || !e1.Contains('@')) errors.Add("parent1Email", "Skriv din e-mailadresse.");
            if (b.Parent2Email.Clean() is { } e2 && !e2.Contains('@')) errors.Add("parent2Email", "Det ligner ikke en e-mailadresse.");
            if (b.PostCode is not { Length: 4 } pc || !pc.All(char.IsAsciiDigit)) errors.Add("postCode", "Postnummer er fire cifre.");
            if (b.DateOfBirth is not { } dob) errors.Add("dateOfBirth", "Skriv barnets fødselsdato eller termin.");
            else if (dob > today.AddMonths(10) || dob < today.AddYears(-7)) errors.Add("dateOfBirth", "Tjek fødselsdatoen.");
            if (b.DesiredStart is not { } ds) errors.Add("desiredStart", "Skriv hvornår I ønsker start.");
            else if (ds < today.AddMonths(-1)) errors.Add("desiredStart", "Ønsket start kan ikke ligge i fortiden.");
            if ((b.Note?.Length ?? 0) > 1000) errors.Add("note", "Højst 1000 tegn.");
            if (!b.Consent) errors.Add("consent", "Sæt flueben for at sende.");
            if (errors.Any) return errors.Result();

            tenant.InstitutionId = institution.Id;
            var a = new Applicant
            {
                ChildName = b.ChildName!.Trim(),
                DateOfBirth = b.DateOfBirth!.Value,
                DesiredStart = b.DesiredStart!.Value,
                HasSibling = b.HasSibling,
                Note = b.Note.Clean() ?? "",
                Parent1Name = b.Parent1Name!.Trim(),
                Parent1Email = b.Parent1Email!.Trim(),
                Parent1Phone = b.Parent1Phone!.Trim(),
                Parent2Name = b.Parent2Name.Clean() ?? "",
                Parent2Email = b.Parent2Email.Clean() ?? "",
                Parent2Phone = b.Parent2Phone.Clean() ?? "",
                Address = b.Address!.Trim(),
                PostCode = b.PostCode!.Trim(),
                City = b.City!.Trim(),
            };
            db.Applicants.Add(a);
            await db.SaveChangesAsync();
            return Results.Ok(new { receipt = a.Id.ToString("N")[..8].ToUpperInvariant() });
        });

        g.MapGet("/answer/{token}", async (string token, AppDb db) =>
        {
            var a = await db.Applicants.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.AnswerToken == token);
            if (a is null) return Results.NotFound();
            var name = await db.Institutions.Where(i => i.Id == a.InstitutionId).Select(i => i.Name).SingleAsync();
            return Results.Ok(new { a.ChildName, institutionName = name, a.OfferedStart, state = a.Status.ToString() });
        });

        g.MapPost("/answer/{token}", async (string token, AnswerBody body, AppDb db, Tenant tenant, TimeProvider clock) =>
        {
            var a = await db.Applicants.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.AnswerToken == token);
            if (a is null) return Results.NotFound();
            if (a.Status != ApplicantStatus.Offered) return Results.Conflict(new { state = a.Status.ToString() });
            tenant.InstitutionId = a.InstitutionId;
            WaitingListEndpoints.Answer(a, body.Yes, clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
            return Results.Ok(new { state = a.Status.ToString() });
        });
    }
}
