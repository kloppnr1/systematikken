using System.Globalization;
using Systematikken.Billing;

namespace Systematikken.Api;

/// <summary>Danish formatting for everything the user reads.</summary>
public static class Fmt
{
    public static readonly CultureInfo Da = CultureInfo.GetCultureInfo("da-DK");

    public static string Kr(decimal amount) => amount.ToString("N2", Da) + " kr.";
    public static string Date(DateOnly? d) => d?.ToString("d. MMM yyyy", Da) ?? "–";
    public static string Date(DateTime d) => d.ToLocalTime().ToString("d. MMM yyyy", Da);
    public static string Month(BillingMonth m) => m.FirstDay.ToString("MMMM yyyy", Da);
    public static string Month(DateOnly firstDay) => firstDay.ToString("MMMM yyyy", Da);
    public static string MonthName(int month) => Da.DateTimeFormat.GetMonthName(month);
    public static string GroupName(CareGroup g) => g == CareGroup.Nursery ? "Vuggestue" : "Børnehave";

    /// <summary>Parses "1.234,50" or "1234.50" as typed by a person.</summary>
    public static bool TryParseAmount(string? text, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Replace("kr.", "").Replace("kr", "").Trim();
        return decimal.TryParse(t, NumberStyles.Number, Da, out amount);
    }

    public static int Age(DateOnly dob, DateOnly today)
    {
        var age = today.Year - dob.Year;
        if (today < dob.AddYears(age)) age--;
        return age;
    }
}
