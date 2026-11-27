namespace Systematikken.Billing;

public enum CareGroup
{
    Nursery,
    Kindergarten,
}

/// <summary>The age at which a child moves from nursery to kindergarten price. Set per municipality.</summary>
public readonly record struct AgeCutover(int Years, int Months)
{
    public DateOnly CutoverDate(DateOnly dateOfBirth) => dateOfBirth.AddYears(Years).AddMonths(Months);

    /// <summary>Rule R3: kindergarten from the first month that starts after the cut-over date.</summary>
    public CareGroup GroupFor(DateOnly dateOfBirth, BillingMonth month) =>
        CutoverDate(dateOfBirth) < month.FirstDay ? CareGroup.Kindergarten : CareGroup.Nursery;
}

/// <summary>The institution's price settings. See docs/fee-rules.md.</summary>
public sealed record FeeSettings
{
    public required decimal NurseryPrice { get; init; }
    public required decimal KindergartenPrice { get; init; }
    public decimal? NurseryPartTimePrice { get; init; }
    public decimal? KindergartenPartTimePrice { get; init; }
    public required AgeCutover Cutover { get; init; }

    /// <summary>A calendar month (1–12) with no charge, for example 7 for July.</summary>
    public int? MonthWithoutCharge { get; init; }

    /// <summary>Rule R5. Starting on this day or later, or ending before it, is a half month.</summary>
    public int HalfMonthDay { get; init; } = 16;

    public decimal? Deposit { get; init; }
    public int DepositMonthsAhead { get; init; }
    public decimal? SignupFee { get; init; }
    public decimal? MealScheme { get; init; }

    public bool IsWithoutCharge(BillingMonth month) => MonthWithoutCharge == month.Month;

    public decimal PriceFor(CareGroup group, bool partTime) => group switch
    {
        CareGroup.Nursery => partTime && NurseryPartTimePrice is { } np ? np : NurseryPrice,
        CareGroup.Kindergarten => partTime && KindergartenPartTimePrice is { } kp ? kp : KindergartenPrice,
        _ => throw new ArgumentOutOfRangeException(nameof(group)),
    };

    /// <summary>Rule R1: the month after <paramref name="lastBilled"/>, skipping the month without charge.</summary>
    public BillingMonth NextBillingMonth(BillingMonth lastBilled)
    {
        var next = lastBilled.AddMonths(1);
        return IsWithoutCharge(next) ? next.AddMonths(1) : next;
    }
}
