namespace Systematikken.Billing;

/// <summary>A calendar month. Billing always works on whole months.</summary>
public readonly record struct BillingMonth : IComparable<BillingMonth>
{
    public BillingMonth(int year, int month)
    {
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        Year = year;
        Month = month;
    }

    public int Year { get; }
    public int Month { get; }

    public static BillingMonth Of(DateOnly date) => new(date.Year, date.Month);

    public DateOnly FirstDay => new(Year, Month, 1);
    public DateOnly LastDay => FirstDay.AddMonths(1).AddDays(-1);

    public BillingMonth AddMonths(int months) => Of(FirstDay.AddMonths(months));

    public bool Contains(DateOnly date) => date.Year == Year && date.Month == Month;

    public int CompareTo(BillingMonth other) => (Year, Month).CompareTo((other.Year, other.Month));

    public static bool operator <(BillingMonth a, BillingMonth b) => a.CompareTo(b) < 0;
    public static bool operator >(BillingMonth a, BillingMonth b) => a.CompareTo(b) > 0;
    public static bool operator <=(BillingMonth a, BillingMonth b) => a.CompareTo(b) <= 0;
    public static bool operator >=(BillingMonth a, BillingMonth b) => a.CompareTo(b) >= 0;

    /// <summary>"11-2016", the form used in texts to payers.</summary>
    public override string ToString() => $"{Month:00}-{Year}";
}
