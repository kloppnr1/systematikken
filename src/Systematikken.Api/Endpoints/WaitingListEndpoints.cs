using Microsoft.EntityFrameworkCore;
using Systematikken.Api.Data;
using Systematikken.Api.Services;
using Systematikken.Billing;

namespace Systematikken.Api.Endpoints;

/// <summary>
/// The waiting list in sign-up order. It shows siblings; staff decide who gets a place.
/// No e-mail server: the manager sends offers from their own mail app or phone, with the text and the answer link filled in.
/// </summary>
public static class WaitingListEndpoints
{
    public sealed record EditBody(string? ChildName, DateOnly DateOfBirth, DateOnly DesiredStart, bool HasSibling, string? Note);
    public sealed record OfferBody(DateOnly? Start);
    public sealed record AnswerBody(bool Yes);

    public static (string Text, string Tone) StatusOf(Applicant a) => a.Status switch
    {
        ApplicantStatus.Waiting => ("Venter", ""),
        ApplicantStatus.Offered => ($"Tilbudt {Fmt.Date(a.OfferedAt!.Value)}", "accent"),
        ApplicantStatus.Accepted => ("Har sagt ja", "good"),
        ApplicantStatus.Declined => ("Sagde nej", "warn"),
        ApplicantStatus.Removed => ("Fjernet", ""),
        _ => ("Indmeldt", "good"),
    };

    public static void Map(WebApplication app)
    {
        var g = app.MapGroup("/api/applicants").AddEndpointFilter<RequireInstitution>();

        g.MapGet("", async (string? filter, AppDb db, BillingService billing, TimeProvider clock) =>
        {
            var institution = await billing.InstitutionAsync();
            var cutover = BillingService.SettingsFor(institution).Cutover;
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var closed = filter == "afsluttede";
            var list = (await db.Applicants.Where(a => a.Status != ApplicantStatus.Enrolled).ToListAsync())
                .Where(a => closed
                    ? a.Status is ApplicantStatus.Declined or ApplicantStatus.Removed
                    : a.Status is ApplicantStatus.Waiting or ApplicantStatus.Offered or ApplicantStatus.Accepted)
                .OrderBy(a => a.CreatedAt)
                .Select((a, i) =>
                {
                    var (status, tone) = StatusOf(a);
                    var at = BillingMonth.Of(a.DesiredStart < today ? today : a.DesiredStart);
                    return new
                    {
                        a.Id, position = i + 1, a.ChildName, a.DateOfBirth, a.DesiredStart, a.HasSibling, a.CreatedAt,
                        group = cutover.GroupFor(a.DateOfBirth, at).ToString(), status, tone,
                        emails = new[] { a.Parent1Email, a.Parent2Email }.Where(e => e.Length > 0),
                    };
                }).ToList();
            return Results.Ok(new { applicants = list, signupUrl = SignupUrl(institution) });
        });

        g.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, AppDb db, BillingService billing, TimeProvider clock) =>
        {
            var a = await db.Applicants.SingleOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            var institution = await billing.InstitutionAsync();
            var (status, tone) = StatusOf(a);
            var answerUrl = $"{ctx.Request.BaseUrl()}/svar/{a.AnswerToken}";
            var first = a.ChildName.Split(' ')[0];
            var greeting = $"Hej {a.Parent1Name.Split(' ')[0]}\n\n";
            var signoff = $"\n\nSvar ja eller nej her: {answerUrl}\n\nVenlig hilsen\n{institution.Name}";
            var reminderDue = a.Status == ApplicantStatus.Offered && a.OfferedAt < clock.GetUtcNow().UtcDateTime.AddDays(-5);

            return Results.Ok(new
            {
                a.Id, a.ChildName, a.DateOfBirth, a.DesiredStart, a.HasSibling, a.Note, a.CreatedAt, a.OfferedAt, a.OfferedStart, a.ChildId,
                state = a.Status.ToString(), status, tone,
                parents = new[]
                {
                    new { name = a.Parent1Name, email = a.Parent1Email, phone = a.Parent1Phone },
                    new { name = a.Parent2Name, email = a.Parent2Email, phone = a.Parent2Phone },
                }.Where(p => p.name.Length > 0),
                address = $"{a.Address}, {a.PostCode} {a.City}",
                message = a.Status != ApplicantStatus.Offered ? null : new
                {
                    reminder = reminderDue,
                    subject = reminderDue ? $"Påmindelse: plads til {a.ChildName}" : $"Plads til {a.ChildName}",
                    text = greeting + (reminderDue
                        ? $"Vi mangler dit svar på pladsen til {first} fra {Fmt.Date(a.OfferedStart)}."
                        : $"Vi kan tilbyde {first} en plads i {institution.Name} fra {Fmt.Date(a.OfferedStart)}.") + signoff,
                    emails = new[] { a.Parent1Email, a.Parent2Email }.Where(e => e.Length > 0),
                    phone = a.Parent1Phone,
                },
            });
        });

        g.MapPut("/{id:guid}", async (Guid id, EditBody body, AppDb db) =>
        {
            var a = await db.Applicants.SingleOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(body.ChildName)) return Http.Invalid("childName", "Barnet skal have et navn.");
            a.ChildName = body.ChildName.Trim();
            a.DateOfBirth = body.DateOfBirth;
            a.DesiredStart = body.DesiredStart;
            a.HasSibling = body.HasSibling;
            a.Note = body.Note.Clean() ?? "";
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapPost("/{id:guid}/offer", async (Guid id, OfferBody body, AppDb db, TimeProvider clock) =>
        {
            var a = await db.Applicants.SingleOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            if (body.Start is null) return Http.Invalid("start", "Vælg startdato.");
            a.Status = ApplicantStatus.Offered;
            a.OfferedStart = body.Start;
            a.OfferedAt = clock.GetUtcNow().UtcDateTime;
            a.AnsweredAt = null;
            a.ClosedAt = null;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // The manager records an answer given by phone or in person.
        g.MapPost("/{id:guid}/answer", async (Guid id, AnswerBody body, AppDb db, TimeProvider clock) =>
        {
            var a = await db.Applicants.SingleOrDefaultAsync(x => x.Id == id);
            if (a is null || a.Status != ApplicantStatus.Offered) return Results.NotFound();
            Answer(a, body.Yes, clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapPost("/{id:guid}/enrol", async (Guid id, AppDb db, ChildService children, TimeProvider clock) =>
        {
            var a = await db.Applicants.SingleOrDefaultAsync(x => x.Id == id);
            if (a is null || a.Status != ApplicantStatus.Accepted) return Results.NotFound();
            var child = await children.EnrolAsync(a, clock.GetUtcNow().UtcDateTime);
            return Results.Ok(new { child.Id, name = child.FullName, child.StartDate });
        });

        g.MapPost("/{id:guid}/remove", async (Guid id, AppDb db, TimeProvider clock) =>
        {
            var a = await db.Applicants.SingleOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            a.Status = ApplicantStatus.Removed;
            a.ClosedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Back on the list as waiting. Also the undo of "remove". A new answer link, so an old one stops working.
        g.MapPost("/{id:guid}/restore", async (Guid id, AppDb db) =>
        {
            var a = await db.Applicants.SingleOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            a.Status = ApplicantStatus.Waiting;
            a.ClosedAt = null;
            a.OfferedAt = null;
            a.OfferedStart = null;
            a.AnsweredAt = null;
            a.AnswerToken = Tokens.New();
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    public static void Answer(Applicant a, bool yes, DateTime now)
    {
        a.Status = yes ? ApplicantStatus.Accepted : ApplicantStatus.Declined;
        a.AnsweredAt = now;
        a.ClosedAt = yes ? null : now;
    }

    private static string SignupUrl(Institution i) => $"/tilmeld/{i.Slug}";
}
