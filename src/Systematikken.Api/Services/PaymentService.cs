using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Systematikken.Billing.Betalingsservice;
using Systematikken.Api.Data;

namespace Systematikken.Api.Services;

public sealed record ImportOutcome(bool AlreadyImported, int Booked, decimal Total, IReadOnlyList<string> NotBooked);

public sealed class PaymentService(AppDb db)
{
    /// <summary>
    /// Books completed Betalingsservice payments against the children. A payment that matches no child,
    /// and every record the reader does not book, is listed for the manager. The rest of the file is still booked.
    /// </summary>
    public async Task<ImportOutcome> ImportAsync(Institution institution, string fileName, byte[] bytes)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (await db.PaymentImports.AnyAsync(p => p.FileHash == hash))
            return new ImportOutcome(true, 0, 0, []);

        var content = PaymentFile.Read(CollectionFile.FileEncoding.GetString(bytes));
        if (content.PbsNumber != institution.PbsNumber)
            throw new FormatException($"The file is for PBS number {content.PbsNumber}, not this institution's {institution.PbsNumber}.");

        var children = await db.Children.ToDictionaryAsync(c => c.PaymentNumber);
        var import = new PaymentImport { InstitutionId = institution.Id, FileName = fileName, FileHash = hash };
        db.PaymentImports.Add(import);
        await db.SaveChangesAsync();

        var notBooked = new List<string>();
        foreach (var p in content.Payments)
        {
            if (!children.TryGetValue(p.PaymentNumber, out var child))
            {
                notBooked.Add($"Betaling på {p.AmountPaid:N2} kr. til betalingsnummer {p.PaymentNumber}: intet barn har det nummer.");
                continue;
            }

            db.Ledger.Add(new Data.LedgerEntry
            {
                ChildId = child.Id,
                Month = new DateOnly(p.CollectionDate.Year, p.CollectionDate.Month, 1),
                Kind = LedgerKind.Payment,
                LineKind = "Payment",
                Text = $"Betalingsservice {p.PaymentDate:dd-MM-yyyy}",
                Amount = -p.AmountPaid,
                PaymentImportId = import.Id,
            });
            import.PaymentCount++;
            import.Total += p.AmountPaid;
        }

        notBooked.AddRange(content.Unhandled.Select(u =>
            $"Linje {u.LineNumber}: transaktionskode {u.TransactionCode}, kundenummer {u.CustomerNumber.TrimStart('0')}. Registrér den selv, hvis det er en betaling."));

        import.NotBooked = string.Join("\n", notBooked);
        await db.SaveChangesAsync();
        return new ImportOutcome(false, import.PaymentCount, import.Total, notBooked);
    }
}
