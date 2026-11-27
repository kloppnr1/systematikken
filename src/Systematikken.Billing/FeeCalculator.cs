namespace Systematikken.Billing;

/// <summary>
/// Works out one month's charges. Pure: no database, no clock, no I/O.
/// Each rule refers to docs/fee-rules.md.
/// </summary>
public static class FeeCalculator
{
    public static BillingResult Calculate(BillingInput input)
    {
        var month = input.Month;
        var settings = input.Settings;

        if (settings.IsWithoutCharge(month))
            throw new InvalidOperationException($"{month} is the month without charge and is never billed.");

        var subsidies = input.Subsidies.ToDictionary(s => (s.ChildId, s.Month));
        var ledger = input.Ledger.ToLookup(e => e.ChildId);
        var manual = input.ManualLines.ToLookup(m => m.ChildId);

        var charges = new List<ChildCharge>();
        var problems = new List<Problem>();

        foreach (var child in input.Children)
        {
            var status = StatusFor(child, month);
            var lines = new List<ChargeLine>();
            CareGroup? group = null;

            if (status == ChargeStatus.Charged)
            {
                group = settings.Cutover.GroupFor(child.DateOfBirth, month);
                var firstInvoice = !child.HasBeenInvoiced;

                // R6: the first invoice catches up on missed months.
                if (firstInvoice)
                {
                    for (var m = BillingMonth.Of(child.StartDate!.Value); m < month; m = m.AddMonths(1))
                    {
                        if (!settings.IsWithoutCharge(m))
                            AddMonthLines(lines, child, m, settings, subsidies, catchUp: true);
                    }
                }

                AddMonthLines(lines, child, month, settings, subsidies, catchUp: false);

                if (firstInvoice)
                {
                    // R7: one-off fees on the first invoice.
                    if (settings.Deposit is { } deposit && !child.DepositCharged)
                        lines.Add(new(LineKind.Deposit, month, Money.Round(deposit), "Depositum"));
                    if (settings.SignupFee is { } fee)
                        lines.Add(new(LineKind.SignupFee, month, Money.Round(fee), "Indmeldelsesgebyr"));
                }

                AddCarryOver(lines, ledger[child.Id], month);

                // R9: a subsidy that stopped is usually one the municipality renewed but nobody entered yet.
                if (subsidies.ContainsKey((child.Id, month.AddMonths(-1))) && !subsidies.ContainsKey((child.Id, month)))
                    problems.Add(new(child.Id, ProblemKind.MissingSubsidy));
            }
            else if (IsDepositDue(child, month, settings))
            {
                // R7: deposit ahead of the start, or on the next list if the right month has passed.
                status = ChargeStatus.DepositOnly;
                lines.Add(new(LineKind.Deposit, month, Money.Round(settings.Deposit!.Value), "Depositum"));
            }

            // R12: manual lines.
            foreach (var m in manual[child.Id])
                lines.Add(new(LineKind.Manual, month, Money.Round(m.Amount), m.Text));

            var charge = new ChildCharge(child.Id, child.PaymentNumber, child.PayerId, status, group, lines);
            charges.Add(charge);

            if (lines.Count > 0)
            {
                if (child.PayerId is null)
                    problems.Add(new(child.Id, ProblemKind.MissingPayer));
                if (charge.AmountToPay <= 0)
                    problems.Add(new(child.Id, ProblemKind.NothingToCollect));
            }
        }

        return new BillingResult(month, charges, problems);
    }

    /// <summary>R2.</summary>
    public static ChargeStatus StatusFor(ChildForBilling child, BillingMonth month)
    {
        if (child.StartDate is not { } start || start > month.LastDay) return ChargeStatus.NotStarted;
        if (child.EndDate is { } end && end < month.FirstDay) return ChargeStatus.HasLeft;
        return ChargeStatus.Charged;
    }

    /// <summary>R5.</summary>
    public static bool IsHalfMonth(ChildForBilling child, BillingMonth month, int halfMonthDay) =>
        (child.StartDate is { } start && month.Contains(start) && start.Day >= halfMonthDay) ||
        (child.EndDate is { } end && month.Contains(end) && end.Day < halfMonthDay);

    private static bool IsDepositDue(ChildForBilling child, BillingMonth month, FeeSettings settings)
    {
        if (settings.Deposit is null || child.DepositCharged || child.HasBeenInvoiced) return false;
        if (child.StartDate is not { } start) return false;
        var startMonth = BillingMonth.Of(start);
        return startMonth > month && startMonth <= month.AddMonths(settings.DepositMonthsAhead);
    }

    /// <summary>R3, R4, R5, R8 and R9 for one month.</summary>
    private static void AddMonthLines(
        List<ChargeLine> lines,
        ChildForBilling child,
        BillingMonth m,
        FeeSettings settings,
        Dictionary<(Guid, BillingMonth), SubsidyEntry> subsidies,
        bool catchUp)
    {
        var group = settings.Cutover.GroupFor(child.DateOfBirth, m);
        var price = settings.PriceFor(group, child.IsPartTime);
        var half = IsHalfMonth(child, m, settings.HalfMonthDay);
        var suffix = catchUp ? $" {m}" : "";

        var groupName = group == CareGroup.Nursery ? "vuggestue" : "børnehave";
        var priceText = $"Forældrebetaling {groupName}{suffix}" + (half ? " (halv måned)" : "");
        lines.Add(new(LineKind.Price, m, Money.Round(half ? price / 2 : price), priceText));

        if (subsidies.TryGetValue((child.Id, m), out var subsidy))
        {
            if (subsidy.FreePlace != 0)
                lines.Add(new(LineKind.FreePlace, m, -Money.Round(subsidy.FreePlace), $"Friplads{suffix}"));
            if (subsidy.SiblingDiscount != 0)
                lines.Add(new(LineKind.SiblingDiscount, m, -Money.Round(subsidy.SiblingDiscount), $"Søskenderabat{suffix}"));
        }

        // R8: always the full amount, also in a half month.
        if (settings.MealScheme is { } meal)
            lines.Add(new(LineKind.MealScheme, m, Money.Round(meal), $"Kostordning{suffix}"));
    }

    /// <summary>R10: show the balance from earlier months, one line per month it comes from, newest first.</summary>
    private static void AddCarryOver(List<ChargeLine> lines, IEnumerable<LedgerEntry> entries, BillingMonth month)
    {
        var byMonth = entries
            .Where(e => e.Month < month)
            .GroupBy(e => e.Month)
            .Select(g => (Month: g.Key, Net: g.Sum(e => e.Amount)))
            .OrderByDescending(x => x.Month)
            .ToList();

        var balance = byMonth.Sum(x => x.Net);
        if (balance == 0) return;

        var owed = balance > 0;
        var remaining = Math.Abs(balance);

        foreach (var (m, net) in byMonth)
        {
            if (remaining == 0) break;
            if (owed ? net <= 0 : net >= 0) continue;

            var take = Math.Min(Math.Abs(net), remaining);
            remaining -= take;

            lines.Add(owed
                ? new(LineKind.DebtCarriedOver, month, take, $"Opkrævning af restgæld fra {m}")
                : new(LineKind.OverpaymentCarriedOver, month, -take, $"For stor betaling overført fra {m}"));
        }
    }
}

public static class Money
{
    /// <summary>R13: whole øre, half an øre to the even øre.</summary>
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.ToEven);
}
