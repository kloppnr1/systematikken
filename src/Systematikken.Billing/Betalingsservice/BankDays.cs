namespace Systematikken.Billing.Betalingsservice;

/// <summary>
/// Danish banking days and the Betalingsservice delivery deadline.
/// Source: Betalingsservice "Vejledning for kreditorer", November 2026 edition.
/// </summary>
public static class BankDays
{
    /// <summary>
    /// Banking days are all days except Saturdays, Sundays and public holidays, the Friday after
    /// Ascension Day, Constitution Day (5 June), Christmas Eve and New Year's Eve.
    /// Great Prayer Day was abolished from 2024 and is a banking day.
    /// </summary>
    public static bool IsBankDay(DateOnly d)
    {
        if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        if ((d.Month, d.Day) is (1, 1) or (6, 5) or (12, 24) or (12, 25) or (12, 26) or (12, 31)) return false;
        var easter = EasterSunday(d.Year);
        int[] fromEaster = [-3, -2, 0, 1, 39, 40, 49, 50]; // Maundy Thursday, Good Friday, Easter, Easter Monday, Ascension + Friday, Whitsun + Monday
        return !fromEaster.Any(n => easter.AddDays(n) == d);
    }

    /// <summary>
    /// The collection file (BS 0601) for a payment month must reach Betalingsservice by the
    /// 6th-last banking day of the month before.
    /// </summary>
    public static DateOnly DeliveryDeadline(BillingMonth paymentMonth)
    {
        var d = paymentMonth.FirstDay.AddDays(-1);
        for (var found = 0; ; d = d.AddDays(-1))
            if (IsBankDay(d) && ++found == 6) return d;
    }

    /// <summary>
    /// The day the money is actually taken. The file asks for the 1st of the month; when that is not a
    /// banking day, Betalingsservice moves it to the next banking day (its default for the debtor group).
    /// </summary>
    public static DateOnly PaymentDay(BillingMonth month)
    {
        var d = month.FirstDay;
        while (!IsBankDay(d)) d = d.AddDays(1);
        return d;
    }

    /// <summary>Gregorian Easter Sunday (anonymous Gregorian algorithm).</summary>
    public static DateOnly EasterSunday(int y)
    {
        int a = y % 19, b = y / 100, c = y % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451, month = (h + l - 7 * m + 114) / 31, day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(y, month, day);
    }
}
