namespace Systematikken.Billing.Tests;

/// <summary>The test cases in docs/fee-rules.md, numbered the same way, plus the edge cases the rules imply.</summary>
public class FeeRulesTests
{
    private const decimal Nursery = 4233.50m;
    private const decimal Kindergarten = 1233.25m;

    private static readonly FeeSettings Settings = new()
    {
        NurseryPrice = Nursery,
        KindergartenPrice = Kindergarten,
        NurseryPartTimePrice = 2550.00m,
        KindergartenPartTimePrice = 800.50m,
        Cutover = new AgeCutover(2, 11),
    };

    private static readonly Guid Payer = Guid.NewGuid();

    private static ChildForBilling Child(string born, string? start, string? end = null, bool invoiced = true, bool partTime = false) => new()
    {
        Id = Guid.NewGuid(),
        PaymentNumber = 1,
        DateOfBirth = DateOnly.Parse(born),
        StartDate = start is null ? null : DateOnly.Parse(start),
        EndDate = end is null ? null : DateOnly.Parse(end),
        HasBeenInvoiced = invoiced,
        IsPartTime = partTime,
        PayerId = Payer,
    };

    private static ChildForBilling NurseryChild(string start, bool invoiced = true) => Child("2015-11-01", start, invoiced: invoiced);
    private static ChildForBilling KindergartenChild(string start, bool invoiced = true) => Child("2013-11-01", start, invoiced: invoiced);

    private static BillingMonth M(int year, int month) => new(year, month);

    private static ChildCharge Bill(ChildForBilling child, BillingMonth month, FeeSettings? settings = null,
        IReadOnlyCollection<SubsidyEntry>? subsidies = null, IReadOnlyCollection<LedgerEntry>? ledger = null) =>
        Run(child, month, settings, subsidies, ledger).Charges.Single();

    private static BillingResult Run(ChildForBilling child, BillingMonth month, FeeSettings? settings = null,
        IReadOnlyCollection<SubsidyEntry>? subsidies = null, IReadOnlyCollection<LedgerEntry>? ledger = null) =>
        FeeCalculator.Calculate(new BillingInput
        {
            Month = month,
            Settings = settings ?? Settings,
            Children = [child],
            Subsidies = subsidies ?? [],
            Ledger = ledger ?? [],
        });

    [Fact]
    public void Case01_NurseryFullMonth()
    {
        var charge = Bill(NurseryChild("2017-01-01", invoiced: false), M(2017, 1));
        Assert.Equal(ChargeStatus.Charged, charge.Status);
        Assert.Equal(CareGroup.Nursery, charge.Group);
        Assert.Equal(4233.50m, charge.AmountToPay);
    }

    [Fact]
    public void Case02_SiblingDiscountAndFreePlace()
    {
        var child = NurseryChild("2017-01-01", invoiced: false);
        var charge = Bill(child, M(2017, 1), subsidies: [new(child.Id, M(2017, 1), 1100.25m, 1550.50m)]);
        Assert.Equal(1582.75m, charge.AmountToPay);
        Assert.Equal([LineKind.Price, LineKind.FreePlace, LineKind.SiblingDiscount], charge.Lines.Select(l => l.Kind));
    }

    [Fact]
    public void Case03_HasLeft()
    {
        var charge = Bill(Child("2015-11-01", "2016-01-01", end: "2016-12-31"), M(2017, 1));
        Assert.Equal(ChargeStatus.HasLeft, charge.Status);
        Assert.Empty(charge.Lines);
    }

    [Fact]
    public void Case04_NotStarted()
    {
        var charge = Bill(KindergartenChild("2017-02-01", invoiced: false), M(2017, 1));
        Assert.Equal(ChargeStatus.NotStarted, charge.Status);
        Assert.Empty(charge.Lines);
    }

    [Fact]
    public void Case05_HalfMonthRoundsToEven()
    {
        var charge = Bill(KindergartenChild("2017-01-16", invoiced: false), M(2017, 1));
        Assert.Equal(616.62m, charge.AmountToPay);
        Assert.Equal("Forældrebetaling børnehave (halv måned)", charge.Lines.Single().Text);
    }

    [Fact]
    public void Case06_MovesToKindergartenTheMonthAfterCutover()
    {
        var child = Child("2015-01-01", "2017-01-16");
        Assert.Equal(Nursery, Bill(child, M(2017, 12)).AmountToPay);
        Assert.Equal(Kindergarten, Bill(child, M(2018, 1)).AmountToPay);
    }

    [Fact]
    public void Case07_CutoverMidMonth()
    {
        var settings = Settings with { Cutover = new AgeCutover(2, 9) };
        var child = Child("2015-01-15", "2017-01-16");
        Assert.Equal(Nursery, Bill(child, M(2017, 9), settings).AmountToPay);
        Assert.Equal(Nursery, Bill(child, M(2017, 10), settings).AmountToPay);
        Assert.Equal(Kindergarten, Bill(child, M(2017, 11), settings).AmountToPay);
    }

    [Fact]
    public void Case08_FirstInvoiceCatchesUp()
    {
        var charge = Bill(KindergartenChild("2017-02-01", invoiced: false), M(2017, 3));
        Assert.Equal(2466.50m, charge.AmountToPay);
        Assert.Equal([M(2017, 2), M(2017, 3)], charge.Lines.Select(l => l.Month));
        Assert.Equal("Forældrebetaling børnehave 02-2017", charge.Lines[0].Text);
    }

    [Fact]
    public void Case09_FirstInvoiceCatchesUpHalfMonth()
    {
        var charge = Bill(KindergartenChild("2017-02-16", invoiced: false), M(2017, 3));
        Assert.Equal(1849.87m, charge.AmountToPay);
    }

    [Fact]
    public void Case10_UnpaidAmountIsCarriedOver()
    {
        var child = NurseryChild("2017-01-01", invoiced: false);
        var charge = Bill(child, M(2017, 1), ledger: [new(child.Id, M(2016, 11), 1200m), new(child.Id, M(2016, 11), -1199m)]);
        Assert.Equal(4234.50m, charge.AmountToPay);
        var carry = charge.Lines.Single(l => l.IsCarryOver);
        Assert.Equal("Opkrævning af restgæld fra 11-2016", carry.Text);
        Assert.Equal(1.00m, carry.Amount);
    }

    [Fact]
    public void Case11_OverpaymentIsCarriedOver()
    {
        var child = NurseryChild("2017-01-01", invoiced: false);
        var charge = Bill(child, M(2017, 1), ledger: [new(child.Id, M(2016, 11), 1200m), new(child.Id, M(2016, 11), -1201m)]);
        Assert.Equal(4232.50m, charge.AmountToPay);
        Assert.Equal("For stor betaling overført fra 11-2016", charge.Lines.Single(l => l.IsCarryOver).Text);
    }

    [Fact]
    public void Case12_CarryOverComesFromTheMonthThatOwes()
    {
        var child = NurseryChild("2017-01-01", invoiced: false);
        var charge = Bill(child, M(2017, 1), ledger:
        [
            new(child.Id, M(2016, 10), 1200m), new(child.Id, M(2016, 10), -1000m),
            new(child.Id, M(2016, 11), 1200m), new(child.Id, M(2016, 11), -1399.26m),
        ]);
        Assert.Equal(4234.24m, charge.AmountToPay);
        var carry = charge.Lines.Single(l => l.IsCarryOver);
        Assert.Equal("Opkrævning af restgæld fra 10-2016", carry.Text);
        Assert.Equal(0.74m, carry.Amount);
    }

    [Fact]
    public void Case13_PartTimePrices()
    {
        var child = Child("2015-01-01", "2017-01-16", partTime: true);
        Assert.Equal(2550.00m, Bill(child, M(2017, 12)).AmountToPay);
        Assert.Equal(800.50m, Bill(child, M(2018, 1)).AmountToPay);
    }

    [Fact]
    public void Case14_PartTimeHalfMonth()
    {
        var child = Child("2013-11-01", "2017-01-16", invoiced: false, partTime: true);
        Assert.Equal(400.25m, Bill(child, M(2017, 1)).AmountToPay);
    }

    private static readonly FeeSettings WithDeposit = Settings with { Deposit = 1099m, DepositMonthsAhead = 2 };

    [Fact]
    public void Case15_DepositChargedMonthsAhead()
    {
        var charge = Bill(Child("2015-01-01", "2018-12-01", invoiced: false), M(2018, 10), WithDeposit);
        Assert.Equal(ChargeStatus.DepositOnly, charge.Status);
        Assert.Equal(1099m, charge.AmountToPay);
    }

    [Fact]
    public void Case15b_DepositNotChargedTooEarly()
    {
        var child = Child("2015-01-01", "2019-01-01", invoiced: false);
        Assert.Equal(ChargeStatus.NotStarted, Bill(child, M(2018, 10), WithDeposit).Status);
        Assert.Equal(1099m, Bill(child, M(2018, 11), WithDeposit).AmountToPay);
    }

    [Fact]
    public void Case16_MonthWithoutChargeIsSkipped()
    {
        var settings = Settings with { MonthWithoutCharge = 7 };
        Assert.Equal(M(2017, 8), settings.NextBillingMonth(M(2017, 6)));
        Assert.Equal(M(2017, 9), settings.NextBillingMonth(M(2017, 8)));
    }

    [Fact]
    public void Case17_EndsOnThe15thIsHalf() =>
        Assert.Equal(616.62m, Bill(Child("2013-11-01", "2016-01-01", end: "2017-03-15"), M(2017, 3)).AmountToPay);

    [Fact]
    public void Case18_EndsOnThe16thIsFull() =>
        Assert.Equal(Kindergarten, Bill(Child("2013-11-01", "2016-01-01", end: "2017-03-16"), M(2017, 3)).AmountToPay);

    [Fact]
    public void Case19_InstitutionHalfMonthDay()
    {
        var settings = Settings with { HalfMonthDay = 10 };
        Assert.Equal(616.62m, Bill(KindergartenChild("2017-01-10", invoiced: false), M(2017, 1), settings).AmountToPay);
    }

    [Fact]
    public void Case20_InstitutionHalfMonthDayDayBefore()
    {
        var settings = Settings with { HalfMonthDay = 10 };
        Assert.Equal(Kindergarten, Bill(KindergartenChild("2017-01-09", invoiced: false), M(2017, 1), settings).AmountToPay);
    }

    [Fact]
    public void Case21_FullMealInHalfMonth()
    {
        var settings = Settings with { MealScheme = 300m };
        Assert.Equal(916.62m, Bill(KindergartenChild("2017-01-16", invoiced: false), M(2017, 1), settings).AmountToPay);
    }

    // Edge cases the rules imply.

    [Fact]
    public void RoundsHalfOreToEven()
    {
        Assert.Equal(616.62m, Money.Round(616.625m));
        Assert.Equal(616.64m, Money.Round(616.635m));
    }

    [Fact]
    public void CatchUpSkipsMonthWithoutCharge()
    {
        var settings = Settings with { MonthWithoutCharge = 7 };
        var charge = Bill(KindergartenChild("2017-06-01", invoiced: false), M(2017, 8), settings);
        Assert.Equal([M(2017, 6), M(2017, 8)], charge.Lines.Select(l => l.Month));
    }

    [Fact]
    public void CatchUpMonthsGetTheirOwnSubsidy()
    {
        var child = KindergartenChild("2017-02-01", invoiced: false);
        var charge = Bill(child, M(2017, 3), subsidies: [new(child.Id, M(2017, 2), 200m, 0m)]);
        Assert.Equal(2266.50m, charge.AmountToPay);
        Assert.Equal("Friplads 02-2017", charge.Lines.Single(l => l.Kind == LineKind.FreePlace).Text);
    }

    [Fact]
    public void CatchUpMealSchemeIsDatedToItsMonth()
    {
        var settings = Settings with { MealScheme = 300m };
        var charge = Bill(KindergartenChild("2017-02-01", invoiced: false), M(2017, 3), settings);
        Assert.Equal([M(2017, 2), M(2017, 3)], charge.Lines.Where(l => l.Kind == LineKind.MealScheme).Select(l => l.Month));
    }

    [Fact]
    public void DepositOnFirstInvoiceWhenNotAhead()
    {
        var settings = Settings with { Deposit = 1099m };
        var charge = Bill(KindergartenChild("2017-01-01", invoiced: false), M(2017, 1), settings);
        Assert.Equal(Kindergarten + 1099m, charge.AmountToPay);
    }

    [Fact]
    public void DepositNeverTwice()
    {
        var child = KindergartenChild("2017-01-01", invoiced: false) with { DepositCharged = true };
        Assert.Equal(Kindergarten, Bill(child, M(2017, 1), WithDeposit).AmountToPay);
    }

    [Fact]
    public void LateEnrolmentGetsDepositOnNextList()
    {
        // Starts in December, 2 months ahead means October, but the child was enrolled in November.
        var charge = Bill(Child("2015-01-01", "2018-12-01", invoiced: false), M(2018, 11), WithDeposit);
        Assert.Equal(ChargeStatus.DepositOnly, charge.Status);
        Assert.Equal(1099m, charge.AmountToPay);
    }

    [Fact]
    public void SignupFeeOnlyOnFirstInvoice()
    {
        var settings = Settings with { SignupFee = 500m };
        Assert.Equal(Kindergarten + 500m, Bill(KindergartenChild("2017-01-01", invoiced: false), M(2017, 1), settings).AmountToPay);
        Assert.Equal(Kindergarten, Bill(KindergartenChild("2017-01-01", invoiced: true), M(2017, 2), settings).AmountToPay);
    }

    [Fact]
    public void MissingPayerIsAProblem()
    {
        var child = KindergartenChild("2017-01-01") with { PayerId = null };
        Assert.Contains(new Problem(child.Id, ProblemKind.MissingPayer), Run(child, M(2017, 2)).Problems);
    }

    [Fact]
    public void CreditIsAProblemAndNotCollected()
    {
        var child = KindergartenChild("2017-01-01");
        var result = Run(child, M(2017, 2), ledger: [new(child.Id, M(2017, 1), -2000m)]);
        Assert.Contains(new Problem(child.Id, ProblemKind.NothingToCollect), result.Problems);
        Assert.Empty(result.ToCollect);
    }

    [Fact]
    public void SubsidyThatStoppedIsFlagged()
    {
        var a = KindergartenChild("2017-01-01");
        var b = KindergartenChild("2017-01-01");
        var c = KindergartenChild("2017-01-01");
        var result = FeeCalculator.Calculate(new BillingInput
        {
            Month = M(2017, 2),
            Settings = Settings,
            Children = [a, b, c],
            // a: subsidy continues. b: had one in January, none in February. c: never had one.
            Subsidies = [new(a.Id, M(2017, 1), 100m, 0m), new(a.Id, M(2017, 2), 100m, 0m), new(b.Id, M(2017, 1), 100m, 0m)],
        });
        Assert.Equal([new Problem(b.Id, ProblemKind.MissingSubsidy)], result.Problems);
    }

    [Fact]
    public void MonthWithoutChargeCannotBeBilled()
    {
        var settings = Settings with { MonthWithoutCharge = 7 };
        Assert.Throws<InvalidOperationException>(() => Run(KindergartenChild("2017-01-01"), M(2017, 7), settings));
    }

    [Fact]
    public void CarryOverLinesAreNotPostedAgain()
    {
        // Paying the whole January amount leaves a zero balance once only the non-carry-over lines are posted.
        var child = NurseryChild("2017-01-01", invoiced: false);
        var ledger = new List<LedgerEntry> { new(child.Id, M(2016, 11), 1200m), new(child.Id, M(2016, 11), -1199m) };
        var january = Bill(child, M(2017, 1), ledger: ledger);

        ledger.AddRange(january.Lines.Where(l => !l.IsCarryOver).Select(l => new LedgerEntry(child.Id, M(2017, 1), l.Amount)));
        ledger.Add(new(child.Id, M(2017, 1), -january.AmountToPay));

        var february = Bill(child with { HasBeenInvoiced = true }, M(2017, 2), ledger: ledger);
        Assert.DoesNotContain(february.Lines, l => l.IsCarryOver);
        Assert.Equal(Nursery, february.AmountToPay);
    }
}
