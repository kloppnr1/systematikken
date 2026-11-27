using System.Globalization;

namespace Systematikken.Billing.Betalingsservice;

/// <summary>A completed automatic payment (BS042, transaction code 0236).</summary>
public sealed record Payment(
    int PaymentNumber,
    DateOnly CollectionDate,
    decimal AmountCollected,
    DateOnly PaymentDate,
    DateOnly BookkeepingDate,
    decimal AmountPaid);

/// <summary>A record in the file the system does not book automatically. The manager sees it.</summary>
public sealed record UnhandledRecord(int LineNumber, string TransactionCode, string CustomerNumber);

public sealed record PaymentFileContent(
    string PbsNumber,
    DateOnly CreatedOn,
    IReadOnlyList<Payment> Payments,
    IReadOnlyList<UnhandledRecord> Unhandled);

/// <summary>
/// Reads delivery 0602 (payment information). See docs/betalingsservice.md.
/// Lines may have their trailing spaces cut, so each line is padded back to 128 characters.
/// </summary>
public static class PaymentFile
{
    public static PaymentFileContent Read(string content)
    {
        var lines = content.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0)
            .Select(l => l.PadRight(128))
            .ToList();

        if (lines.Count < 2 || !lines[0].StartsWith("BS002") || lines[0].Substring(16, 4) != "0602")
            throw new FormatException("This is not a Betalingsservice payment file (delivery 0602).");
        if (!lines[^1].StartsWith("BS992"))
            throw new FormatException("The file is incomplete: the end record is missing.");

        var created = Date6(lines[0], 49);
        string? pbsNumber = null;
        var payments = new List<Payment>();
        var unhandled = new List<UnhandledRecord>();

        for (var i = 1; i < lines.Count - 1; i++)
        {
            var line = lines[i];
            switch (line[..5])
            {
                case "BS012":
                    pbsNumber ??= line.Substring(5, 8);
                    break;
                case "BS042":
                    var code = line.Substring(13, 4);
                    if (code == "0236")
                        payments.Add(new Payment(
                            PaymentNumber: int.Parse(line.Substring(25, 15), CultureInfo.InvariantCulture),
                            CollectionDate: Date6(line, 49),
                            AmountCollected: Ore(line, 56),
                            PaymentDate: Date6(line, 103),
                            BookkeepingDate: Date6(line, 109),
                            AmountPaid: Ore(line, 115)));
                    else
                        unhandled.Add(new UnhandledRecord(i + 1, code, line.Substring(25, 15).Trim()));
                    break;
            }
        }

        return new PaymentFileContent(pbsNumber ?? throw new FormatException("The section start record is missing."), created, payments, unhandled);
    }

    private static DateOnly Date6(string line, int index) =>
        DateOnly.ParseExact(line.Substring(index, 6), "ddMMyy", CultureInfo.InvariantCulture);

    private static decimal Ore(string line, int index) =>
        long.Parse(line.Substring(index, 13), CultureInfo.InvariantCulture) / 100m;
}
