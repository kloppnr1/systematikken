using Systematikken.Billing.Betalingsservice;

namespace Systematikken.Billing.Tests;

public class BankDaysTests
{
    [Theory]
    [InlineData(2026, 4, 5)]
    [InlineData(2025, 4, 20)]
    [InlineData(2027, 3, 28)]
    public void Easter(int y, int m, int d) => Assert.Equal(new DateOnly(y, m, d), BankDays.EasterSunday(y));

    [Theory]
    [InlineData("2026-04-02", false)] // Maundy Thursday
    [InlineData("2026-05-15", false)] // Friday after Ascension
    [InlineData("2026-05-01", true)]  // Great Prayer Day is gone; 1 May is not a bank holiday
    [InlineData("2026-06-05", false)] // Constitution Day
    [InlineData("2026-12-24", false)]
    [InlineData("2026-12-31", false)]
    [InlineData("2026-11-27", true)]
    public void BankDay(string date, bool expected) => Assert.Equal(expected, BankDays.IsBankDay(DateOnly.Parse(date)));

    [Theory]
    [InlineData(2026, 12, "2026-12-01")] // Tuesday
    [InlineData(2026, 8, "2026-08-03")]  // 1 August is a Saturday
    [InlineData(2027, 1, "2027-01-04")]  // New Year's Day is a Friday
    public void Payment(int y, int m, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), BankDays.PaymentDay(new BillingMonth(y, m)));

    [Theory]
    [InlineData(2026, 12, "2026-11-23")] // Nov 30 is a Monday: 30, 27, 26, 25, 24, 23
    [InlineData(2026, 1, "2025-12-18")]  // skips 31, 26, 25, 24 December
    [InlineData(2027, 1, "2026-12-21")]  // 30, 29, 28, 23, 22, 21
    public void Deadline(int y, int m, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), BankDays.DeliveryDeadline(new BillingMonth(y, m)));
}
