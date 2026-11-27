namespace Systematikken.Billing;

public sealed record ChildForBilling
{
    public required Guid Id { get; init; }

    /// <summary>The customer number in the Betalingsservice files.</summary>
    public required int PaymentNumber { get; init; }

    public required DateOnly DateOfBirth { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public bool IsPartTime { get; init; }

    /// <summary>True once the child has been on an approved month with a price line.</summary>
    public bool HasBeenInvoiced { get; init; }

    public bool DepositCharged { get; init; }
    public Guid? PayerId { get; init; }
}

/// <summary>Free-place subsidy and sibling discount for one child in one month, as given by the municipality.</summary>
public sealed record SubsidyEntry(Guid ChildId, BillingMonth Month, decimal FreePlace, decimal SiblingDiscount);

/// <summary>
/// One posting already in the books for a child. Positive means the payer owes it (a charge),
/// negative means it reduces what they owe (a payment or a credit).
/// </summary>
public sealed record LedgerEntry(Guid ChildId, BillingMonth Month, decimal Amount);

/// <summary>Rule R12: a manual charge (positive) or credit (negative) the manager adds to a month.</summary>
public sealed record ManualLine(Guid ChildId, string Text, decimal Amount);

public sealed record BillingInput
{
    public required BillingMonth Month { get; init; }
    public required FeeSettings Settings { get; init; }
    public required IReadOnlyCollection<ChildForBilling> Children { get; init; }
    public IReadOnlyCollection<SubsidyEntry> Subsidies { get; init; } = [];
    public IReadOnlyCollection<LedgerEntry> Ledger { get; init; } = [];
    public IReadOnlyCollection<ManualLine> ManualLines { get; init; } = [];
}
