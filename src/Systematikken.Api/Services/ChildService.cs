using Microsoft.EntityFrameworkCore;
using Systematikken.Api.Data;

namespace Systematikken.Api.Services;

public sealed class ChildService(AppDb db)
{
    /// <summary>Creates a child with the next free payment number. The first parent becomes the payer.</summary>
    public async Task<Child> CreateAsync(Child child, IEnumerable<Parent> parents)
    {
        var institution = await db.Institutions.SingleAsync(i => i.Id == db.InstitutionId);
        child.PaymentNumber = institution.NextPaymentNumber++;

        // A child who started before the institution's first month in Systematikken was billed in the old system.
        // Without this, the first invoice would catch up on every month since the start (rule R6).
        if (child.StartDate is { } start && start < institution.FirstBillingMonth)
        {
            child.HasBeenInvoiced = true;
            child.DepositCharged = true;
        }
        foreach (var p in parents.Where(p => !string.IsNullOrWhiteSpace(p.Name)))
        {
            var existing = string.IsNullOrWhiteSpace(p.Email) ? null
                : await db.Parents.FirstOrDefaultAsync(x => x.Email.ToLower() == p.Email.ToLower());
            child.Parents.Add(existing ?? p);
        }
        child.Payer = child.Parents.FirstOrDefault();
        db.Children.Add(child);
        await db.SaveChangesAsync();
        return child;
    }

    /// <summary>Turns an applicant who said yes into a child, with their parents. The applicant is then closed.</summary>
    public async Task<Child> EnrolAsync(Applicant a, DateTime now)
    {
        var names = a.ChildName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var child = new Child
        {
            FirstName = names.ElementAtOrDefault(0) ?? a.ChildName,
            LastName = names.ElementAtOrDefault(1) ?? "",
            DateOfBirth = a.DateOfBirth,
            StartDate = a.OfferedStart ?? a.DesiredStart,
            Note = a.Note,
        };
        var parents = new[]
        {
            new Parent { Name = a.Parent1Name, Email = a.Parent1Email, Phone = a.Parent1Phone, Address = a.Address, PostCode = a.PostCode, City = a.City },
            new Parent { Name = a.Parent2Name, Email = a.Parent2Email, Phone = a.Parent2Phone, Address = a.Address, PostCode = a.PostCode, City = a.City },
        };
        await CreateAsync(child, parents);

        a.Status = ApplicantStatus.Enrolled;
        a.ChildId = child.Id;
        a.ClosedAt = now;
        await db.SaveChangesAsync();
        return child;
    }
}
