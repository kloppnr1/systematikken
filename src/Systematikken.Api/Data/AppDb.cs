using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Systematikken.Api.Data;

/// <summary>The institution the current request works for. Every institution's data is filtered by it.</summary>
public sealed class Tenant
{
    public Guid? InstitutionId { get; set; }
}

public sealed class AppDb(DbContextOptions<AppDb> options, Tenant tenant) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Municipality> Municipalities => Set<Municipality>();
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Child> Children => Set<Child>();
    public DbSet<Parent> Parents => Set<Parent>();
    public DbSet<Applicant> Applicants => Set<Applicant>();
    public DbSet<Subsidy> Subsidies => Set<Subsidy>();
    public DbSet<ManualCharge> ManualCharges => Set<ManualCharge>();
    public DbSet<LedgerEntry> Ledger => Set<LedgerEntry>();
    public DbSet<BillingRun> BillingRuns => Set<BillingRun>();
    public DbSet<PaymentImport> PaymentImports => Set<PaymentImport>();

    public Guid? InstitutionId => tenant.InstitutionId;

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Institution>().HasIndex(i => i.Slug).IsUnique();
        b.Entity<Membership>().HasIndex(m => new { m.UserId, m.InstitutionId }).IsUnique();
        b.Entity<Invitation>().HasIndex(i => i.Token).IsUnique();
        b.Entity<Child>().HasIndex(c => new { c.InstitutionId, c.PaymentNumber }).IsUnique();
        b.Entity<Child>().HasMany(c => c.Parents).WithMany(p => p.Children).UsingEntity("ChildParents");
        b.Entity<Child>().HasOne(c => c.Payer).WithMany().HasForeignKey(c => c.PayerId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Applicant>().HasIndex(a => a.AnswerToken).IsUnique();
        b.Entity<BillingRun>().HasIndex(r => new { r.InstitutionId, r.Month }).IsUnique();
        b.Entity<PaymentImport>().HasIndex(p => new { p.InstitutionId, p.FileHash }).IsUnique();

        // Each institution only ever sees its own rows.
        b.Entity<Invitation>().HasQueryFilter(x => x.InstitutionId == InstitutionId);
        b.Entity<Child>().HasQueryFilter(x => x.InstitutionId == InstitutionId);
        b.Entity<Parent>().HasQueryFilter(x => x.InstitutionId == InstitutionId);
        b.Entity<Applicant>().HasQueryFilter(x => x.InstitutionId == InstitutionId);
        b.Entity<Subsidy>().HasQueryFilter(x => x.InstitutionId == InstitutionId);
        b.Entity<ManualCharge>().HasQueryFilter(x => x.InstitutionId == InstitutionId);
        b.Entity<LedgerEntry>().HasQueryFilter(x => x.InstitutionId == InstitutionId);
        b.Entity<BillingRun>().HasQueryFilter(x => x.InstitutionId == InstitutionId);
        b.Entity<PaymentImport>().HasQueryFilter(x => x.InstitutionId == InstitutionId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampInstitution();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampInstitution();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>New rows get the current institution, and rows from another institution can never be saved.</summary>
    private void StampInstitution()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            var prop = entry.Metadata.FindProperty("InstitutionId");
            if (prop is null || entry.Entity is Membership or Institution) continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;

            var value = (Guid)entry.Property("InstitutionId").CurrentValue!;
            if (entry.State == EntityState.Added && value == Guid.Empty && InstitutionId is { } id)
                entry.Property("InstitutionId").CurrentValue = id;
            else if (InstitutionId is { } current && value != current)
                throw new InvalidOperationException("Tried to save data belonging to another institution.");
        }
    }
}
