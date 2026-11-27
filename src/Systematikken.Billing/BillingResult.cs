namespace Systematikken.Billing;

public enum LineKind
{
    Price,
    FreePlace,
    SiblingDiscount,
    MealScheme,
    Deposit,
    SignupFee,
    DebtCarriedOver,
    OverpaymentCarriedOver,
    Manual,
}

/// <summary>
/// One line on a charge. Positive amounts are owed by the payer.
/// <see cref="Month"/> is the month the line belongs to, which differs from the billing month for catch-up lines.
/// </summary>
public sealed record ChargeLine(LineKind Kind, BillingMonth Month, decimal Amount, string Text)
{
    /// <summary>
    /// Carried-over lines only show an existing balance again. They must not be posted to the ledger,
    /// or the same debt would be counted twice.
    /// </summary>
    public bool IsCarryOver => Kind is LineKind.DebtCarriedOver or LineKind.OverpaymentCarriedOver;
}

public enum ChargeStatus
{
    /// <summary>The child is there this month and pays for it.</summary>
    Charged,

    /// <summary>The child starts later; only the deposit is charged now.</summary>
    DepositOnly,

    NotStarted,
    HasLeft,
}

public sealed record ChildCharge(
    Guid ChildId,
    int PaymentNumber,
    Guid? PayerId,
    ChargeStatus Status,
    CareGroup? Group,
    IReadOnlyList<ChargeLine> Lines)
{
    /// <summary>Rule R13: the sum of the lines. Each line is already rounded.</summary>
    public decimal AmountToPay => Lines.Sum(l => l.Amount);
}

public enum ProblemKind
{
    /// <summary>Rule R11: the charge has lines but no payer is chosen.</summary>
    MissingPayer,

    /// <summary>Rule R9: the child had a subsidy last month and has none this month.</summary>
    MissingSubsidy,

    /// <summary>The charge comes to 0 or less, so nothing is collected. It must be paid back or carried over.</summary>
    NothingToCollect,
}

public sealed record Problem(Guid ChildId, ProblemKind Kind);

public sealed record BillingResult(BillingMonth Month, IReadOnlyList<ChildCharge> Charges, IReadOnlyList<Problem> Problems)
{
    /// <summary>The charges that go in the Betalingsservice collection file.</summary>
    public IEnumerable<ChildCharge> ToCollect => Charges.Where(c => c.AmountToPay > 0);
}
