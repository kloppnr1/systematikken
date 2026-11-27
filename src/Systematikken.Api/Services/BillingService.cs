using Microsoft.EntityFrameworkCore;
using Systematikken.Billing;
using Systematikken.Billing.Betalingsservice;
using Systematikken.Api.Data;
using BillingLedgerEntry = Systematikken.Billing.LedgerEntry;
using LedgerEntry = Systematikken.Api.Data.LedgerEntry;

namespace Systematikken.Api.Services;

public sealed record DraftMonth(
    BillingMonth Month,
    BillingResult Result,
    IReadOnlyDictionary<Guid, Child> Children,
    BookkeepingVoucher Voucher)
{
    /// <summary>Problems that stop approval. A charge of 0 or less is only a warning.</summary>
    public IEnumerable<Problem> Blocking => Result.Problems.Where(p => p.Kind != ProblemKind.NothingToCollect);
    public bool CanApprove => !Blocking.Any();
}

public sealed class BillingService(AppDb db, TimeProvider clock)
{
    public static FeeSettings SettingsFor(Institution i) => new()
    {
        NurseryPrice = i.NurseryPrice,
        KindergartenPrice = i.KindergartenPrice,
        NurseryPartTimePrice = i.NurseryPartTimePrice,
        KindergartenPartTimePrice = i.KindergartenPartTimePrice,
        Cutover = new AgeCutover(i.Municipality!.CutoverYears, i.Municipality.CutoverMonths),
        MonthWithoutCharge = i.MonthWithoutCharge,
        HalfMonthDay = i.HalfMonthDay,
        Deposit = i.Deposit,
        DepositMonthsAhead = i.DepositMonthsAhead,
        SignupFee = i.SignupFee,
        MealScheme = i.MealScheme,
    };

    public async Task<Institution> InstitutionAsync() =>
        await db.Institutions.Include(i => i.Municipality).SingleAsync(i => i.Id == db.InstitutionId);

    /// <summary>R1: the month after the last approved one, or the first billing month.</summary>
    public async Task<BillingMonth> NextMonthAsync(Institution institution)
    {
        var last = await db.BillingRuns.OrderByDescending(r => r.Month).Select(r => (DateOnly?)r.Month).FirstOrDefaultAsync();
        var settings = SettingsFor(institution);
        if (last is { } l) return settings.NextBillingMonth(BillingMonth.Of(l));

        var first = BillingMonth.Of(institution.FirstBillingMonth);
        return settings.IsWithoutCharge(first) ? first.AddMonths(1) : first;
    }

    public async Task<DraftMonth> DraftAsync(Institution institution, BillingMonth month)
    {
        var children = await db.Children.Include(c => c.Payer).ToListAsync();
        var first = month.FirstDay;
        // Expand each subsidy to the months it covers. Catch-up months need earlier ones too; three years back is plenty.
        // When periods overlap, the one that starts latest wins.
        var subsidies = (await db.Subsidies.ToListAsync())
            .OrderBy(s => s.FromMonth)
            .SelectMany(s => MonthsCovered(s, month.AddMonths(-36), month)
                .Select(m => new SubsidyEntry(s.ChildId, m, s.FreePlace, s.SiblingDiscount)))
            .GroupBy(s => (s.ChildId, s.Month)).Select(g => g.Last())
            .ToList();
        var ledger = (await db.Ledger.ToListAsync())
            .Select(e => new BillingLedgerEntry(e.ChildId, BillingMonth.Of(e.Month), e.Amount)).ToList();
        var manual = (await db.ManualCharges.Where(m => m.Month == first).ToListAsync())
            .Select(m => new ManualLine(m.ChildId, m.Text, m.Amount)).ToList();

        var input = new BillingInput
        {
            Month = month,
            Settings = SettingsFor(institution),
            Children = children.Select(c => new ChildForBilling
            {
                Id = c.Id,
                PaymentNumber = c.PaymentNumber,
                DateOfBirth = c.DateOfBirth,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                IsPartTime = c.IsPartTime,
                HasBeenInvoiced = c.HasBeenInvoiced,
                DepositCharged = c.DepositCharged,
                PayerId = c.PayerId,
            }).ToList(),
            Subsidies = subsidies,
            Ledger = ledger,
            ManualLines = manual,
        };

        var result = FeeCalculator.Calculate(input);
        return new DraftMonth(month, result, children.ToDictionary(c => c.Id), BookkeepingVoucher.From(result));
    }

    /// <summary>The months from <paramref name="earliest"/> to <paramref name="until"/> that a subsidy covers.</summary>
    private static IEnumerable<BillingMonth> MonthsCovered(Subsidy s, BillingMonth earliest, BillingMonth until)
    {
        var from = BillingMonth.Of(s.FromMonth);
        var to = s.ToMonth is { } t ? BillingMonth.Of(t) : until;
        for (var m = from < earliest ? earliest : from; m <= to && m <= until; m = m.AddMonths(1))
            yield return m;
    }

    /// <summary>Locks the month: posts the lines to the books, marks first invoices and deposits, and writes the collection file.</summary>
    public async Task<BillingRun> ApproveAsync(Institution institution, BillingMonth month, string approvedBy)
    {
        if (month != await NextMonthAsync(institution))
            throw new InvalidOperationException("Only the next month can be approved.");

        var draft = await DraftAsync(institution, month);
        if (!draft.CanApprove)
            throw new InvalidOperationException("The month has problems that must be fixed first.");

        institution.LastSerialNumber++;
        var run = new BillingRun
        {
            InstitutionId = institution.Id,
            Month = month.FirstDay,
            ApprovedAt = clock.GetUtcNow().UtcDateTime,
            ApprovedBy = approvedBy,
            SerialNumber = institution.LastSerialNumber,
            ChargeCount = draft.Result.ToCollect.Count(),
            Total = draft.Result.ToCollect.Sum(c => c.AmountToPay),
            Opkraevningssum = draft.Voucher.Opkraevningssum,
            Opkraevninger = draft.Voucher.Opkraevninger,
            Tilskudssum = draft.Voucher.Tilskudssum,
        };

        var collections = draft.Result.ToCollect.Select(c =>
        {
            var child = draft.Children[c.ChildId];
            var payer = child.Payer!;
            return new Collection
            {
                PaymentNumber = c.PaymentNumber,
                PayerName = payer.Name,
                ChildFirstName = child.FirstName,
                Address = payer.Address,
                PostCode = payer.PostCode,
                Amount = c.AmountToPay,
                Lines = c.Lines,
            };
        });

        run.CollectionFile = CollectionFile.Write(
            new Creditor
            {
                Cvr = institution.Cvr,
                PbsNumber = institution.PbsNumber,
                DebtorGroup = institution.DebtorGroup,
                DataSupplierIdentification = 1,
                MainText = institution.BetalingsserviceText,
            },
            run.SerialNumber, month.FirstDay, DateOnly.FromDateTime(run.ApprovedAt), collections);

        db.BillingRuns.Add(run);
        await db.SaveChangesAsync();

        foreach (var charge in draft.Result.Charges)
        {
            var child = draft.Children[charge.ChildId];
            foreach (var line in charge.Lines.Where(l => !l.IsCarryOver))
            {
                db.Ledger.Add(new LedgerEntry
                {
                    ChildId = child.Id,
                    Month = line.Month.FirstDay,
                    Kind = LedgerKind.Charge,
                    LineKind = line.Kind.ToString(),
                    Text = line.Text,
                    Amount = line.Amount,
                    BillingRunId = run.Id,
                });
            }
            if (charge.Lines.Any(l => l.Kind == LineKind.Price)) child.HasBeenInvoiced = true;
            if (charge.Lines.Any(l => l.Kind == LineKind.Deposit)) child.DepositCharged = true;
        }

        db.ManualCharges.RemoveRange(db.ManualCharges.Where(m => m.Month == month.FirstDay));
        await db.SaveChangesAsync();
        return run;
    }

    /// <summary>A child's balance: what is still owed (positive) or overpaid (negative).</summary>
    public async Task<decimal> BalanceAsync(Guid childId) =>
        (await db.Ledger.Where(e => e.ChildId == childId).Select(e => e.Amount).ToListAsync()).Sum();
}
