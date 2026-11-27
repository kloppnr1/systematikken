namespace Systematikken.Billing;

/// <summary>The three amounts on the monthly "Bogføringsbilag". See docs/accounting-voucher.md.</summary>
public sealed record BookkeepingVoucher(BillingMonth Month, decimal Opkraevningssum, decimal Opkraevninger, decimal Tilskudssum)
{
    public static BookkeepingVoucher From(BillingResult result)
    {
        var lines = result.Charges.SelectMany(c => c.Lines).ToList();

        decimal Sum(params LineKind[] kinds) => lines.Where(l => kinds.Contains(l.Kind)).Sum(l => l.Amount);

        var opkraevninger = Sum(LineKind.Price, LineKind.MealScheme, LineKind.Manual);
        var tilskud = -Sum(LineKind.FreePlace, LineKind.SiblingDiscount);
        var oneOff = Sum(LineKind.Deposit, LineKind.SignupFee);

        return new(result.Month, opkraevninger + oneOff - tilskud, opkraevninger, tilskud);
    }
}
