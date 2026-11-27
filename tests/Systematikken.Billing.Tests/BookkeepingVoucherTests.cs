namespace Systematikken.Billing.Tests;

/// <summary>The test case in docs/accounting-voucher.md.</summary>
public class BookkeepingVoucherTests
{
    [Fact]
    public void ExcludesCarryOverAndPayments()
    {
        var month = new BillingMonth(2017, 1);
        var settings = new FeeSettings
        {
            NurseryPrice = 4233.50m,
            KindergartenPrice = 1233.25m,
            Cutover = new AgeCutover(2, 11),
            Deposit = 1099m,
            DepositMonthsAhead = 2,
            MealScheme = 300m,
        };

        var payer = Guid.NewGuid();
        var nursery = new ChildForBilling
        {
            Id = Guid.NewGuid(), PaymentNumber = 1, DateOfBirth = new(2015, 11, 1),
            StartDate = new(2016, 6, 1), HasBeenInvoiced = true, DepositCharged = true, PayerId = payer,
        };
        var kindergarten = new ChildForBilling
        {
            Id = Guid.NewGuid(), PaymentNumber = 2, DateOfBirth = new(2013, 11, 1),
            StartDate = new(2017, 1, 16), DepositCharged = true, PayerId = payer,
        };
        var future = new ChildForBilling
        {
            Id = Guid.NewGuid(), PaymentNumber = 3, DateOfBirth = new(2016, 1, 1),
            StartDate = new(2017, 3, 1), PayerId = payer,
        };

        // Meal scheme only for the kindergarten child in the doc's example, so give the nursery child none.
        var result = FeeCalculator.Calculate(new BillingInput
        {
            Month = month,
            Settings = settings,
            Children = [nursery, kindergarten, future],
            Subsidies = [new(nursery.Id, month, 1100.25m, 1550.50m), new(kindergarten.Id, month, 0m, 0m)],
            Ledger = [new(nursery.Id, new(2016, 12), 1.00m)],
            ManualLines = [new(nursery.Id, "Kostordning (fritaget)", -300m)],
        });

        var voucher = BookkeepingVoucher.From(result);

        Assert.Equal(5150.12m, voucher.Opkraevninger);
        Assert.Equal(2650.75m, voucher.Tilskudssum);
        Assert.Equal(3598.37m, voucher.Opkraevningssum);
    }
}
