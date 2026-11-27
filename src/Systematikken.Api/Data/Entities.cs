using Microsoft.AspNetCore.Identity;

namespace Systematikken.Api.Data;

public sealed class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
}

/// <summary>Cut-over age and school-start month are set per municipality, not per institution.</summary>
public sealed class Municipality
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int CutoverYears { get; set; } = 2;
    public int CutoverMonths { get; set; } = 11;
    public int SchoolStartMonth { get; set; } = 8;

    /// <summary>The first institution in a municipality confirms the rules. After that only the operator changes them.</summary>
    public bool Confirmed { get; set; }
}

public sealed class Institution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    /// <summary>The public sign-up address: /tilmeld/{Slug}.</summary>
    public string Slug { get; set; } = "";

    public string ContactEmail { get; set; } = "";
    public string ContactPhone { get; set; } = "";

    public int MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }

    // Prices (docs/fee-rules.md)
    public decimal NurseryPrice { get; set; }
    public decimal KindergartenPrice { get; set; }
    public decimal? NurseryPartTimePrice { get; set; }
    public decimal? KindergartenPartTimePrice { get; set; }
    public int? MonthWithoutCharge { get; set; } = 7;
    public int HalfMonthDay { get; set; } = 16;
    public decimal? Deposit { get; set; }
    public int DepositMonthsAhead { get; set; }
    public decimal? SignupFee { get; set; }
    public decimal? MealScheme { get; set; }

    // Occupancy report
    public int NurseryCapacity { get; set; }
    public int KindergartenCapacity { get; set; }

    // Betalingsservice
    public string Cvr { get; set; } = "";
    public string PbsNumber { get; set; } = "";
    public string DebtorGroup { get; set; } = "00001";
    public string BetalingsserviceText { get; set; } = "";
    public long LastSerialNumber { get; set; }

    /// <summary>The first month the system bills. Later months follow on from the last approved one.</summary>
    public DateOnly FirstBillingMonth { get; set; }

    /// <summary>Waiting-list entries that are declined or removed are deleted after this many months.</summary>
    public int WaitingListRetentionMonths { get; set; } = 12;

    public int NextPaymentNumber { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Membership
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public AppUser? User { get; set; }
    public Guid InstitutionId { get; set; }
    public Institution? Institution { get; set; }
}

public sealed class Invitation
{
    public int Id { get; set; }
    public Guid InstitutionId { get; set; }
    public string Email { get; set; } = "";
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
}

public sealed class Child
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public int PaymentNumber { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsPartTime { get; set; }
    public bool HasBeenInvoiced { get; set; }
    public bool DepositCharged { get; set; }
    public Guid? PayerId { get; set; }
    public Parent? Payer { get; set; }
    public string Note { get; set; } = "";
    public List<Parent> Parents { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string FullName => $"{FirstName} {LastName}".Trim();
}

public sealed class Parent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string PostCode { get; set; } = "";
    public string City { get; set; } = "";
    public List<Child> Children { get; set; } = [];
}

public enum ApplicantStatus
{
    Waiting,
    Offered,
    Accepted,
    Declined,
    Enrolled,
    Removed,
}

/// <summary>A child on the waiting list, usually from the public sign-up form.</summary>
public sealed class Applicant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public string ChildName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public DateOnly DesiredStart { get; set; }
    public bool HasSibling { get; set; }
    public string Note { get; set; } = "";

    public string Parent1Name { get; set; } = "";
    public string Parent1Email { get; set; } = "";
    public string Parent1Phone { get; set; } = "";
    public string Parent2Name { get; set; } = "";
    public string Parent2Email { get; set; } = "";
    public string Parent2Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string PostCode { get; set; } = "";
    public string City { get; set; } = "";

    public ApplicantStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? OfferedAt { get; set; }
    public DateTime? AnsweredAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateOnly? OfferedStart { get; set; }

    /// <summary>The secret in the parent's accept/decline link.</summary>
    public string AnswerToken { get; set; } = Tokens.New();

    public Guid? ChildId { get; set; }
}

/// <summary>Free-place subsidy and sibling discount, valid from a month until changed or ended.</summary>
public sealed class Subsidy
{
    public int Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid ChildId { get; set; }
    public DateOnly FromMonth { get; set; }
    public DateOnly? ToMonth { get; set; }
    public decimal FreePlace { get; set; }
    public decimal SiblingDiscount { get; set; }
}

/// <summary>A manual charge or credit waiting to go on a month that is not approved yet.</summary>
public sealed class ManualCharge
{
    public int Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid ChildId { get; set; }
    public DateOnly Month { get; set; }
    public string Text { get; set; } = "";
    public decimal Amount { get; set; }
}

public enum LedgerKind
{
    Charge,
    Payment,
}

/// <summary>The books for one child. Positive is owed by the payer, negative is paid or credited.</summary>
public sealed class LedgerEntry
{
    public int Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid ChildId { get; set; }
    public DateOnly Month { get; set; }
    public LedgerKind Kind { get; set; }
    public string LineKind { get; set; } = "";
    public string Text { get; set; } = "";
    public decimal Amount { get; set; }
    public int? BillingRunId { get; set; }
    public int? PaymentImportId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An approved month. Approving is final; mistakes are fixed with a credit on a later month.</summary>
public sealed class BillingRun
{
    public int Id { get; set; }
    public Guid InstitutionId { get; set; }
    public DateOnly Month { get; set; }
    public DateTime ApprovedAt { get; set; }
    public string ApprovedBy { get; set; } = "";
    public long SerialNumber { get; set; }
    public string CollectionFile { get; set; } = "";
    public int ChargeCount { get; set; }
    public decimal Total { get; set; }
    public decimal Opkraevningssum { get; set; }
    public decimal Opkraevninger { get; set; }
    public decimal Tilskudssum { get; set; }
}

public sealed class PaymentImport
{
    public int Id { get; set; }
    public Guid InstitutionId { get; set; }
    public string FileName { get; set; } = "";
    public string FileHash { get; set; } = "";
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public int PaymentCount { get; set; }
    public decimal Total { get; set; }

    /// <summary>Records the system did not book, one per line, for the manager.</summary>
    public string NotBooked { get; set; } = "";
}

public static class Tokens
{
    public static string New() => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
