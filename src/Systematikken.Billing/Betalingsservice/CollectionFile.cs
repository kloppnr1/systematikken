using System.Globalization;
using System.Text;

namespace Systematikken.Billing.Betalingsservice;

/// <summary>The institution's Betalingsservice agreement.</summary>
public sealed record Creditor
{
    /// <summary>CVR number of the data supplier, 8 digits.</summary>
    public required string Cvr { get; init; }

    /// <summary>The creditor's PBS number, 8 digits.</summary>
    public required string PbsNumber { get; init; }

    public string DebtorGroup { get; init; } = "00001";

    /// <summary>Creditor's identification with the data supplier, for own use.</summary>
    public required long DataSupplierIdentification { get; init; }

    /// <summary>The heading shown to every payer, up to 60 characters.</summary>
    public required string MainText { get; init; }
}

/// <summary>One collection: one child's charge for the month.</summary>
public sealed record Collection
{
    public required int PaymentNumber { get; init; }
    public required string PayerName { get; init; }
    public required string ChildFirstName { get; init; }
    public required string Address { get; init; }
    public required string PostCode { get; init; }
    public required decimal Amount { get; init; }
    public required IReadOnlyList<ChargeLine> Lines { get; init; }
}

/// <summary>
/// Writes delivery 0601 (collection data), section 0112. See docs/betalingsservice.md for the record layouts.
/// Records are 128 characters, CRLF line endings, ISO-8859-1.
/// </summary>
public static class CollectionFile
{
    public static readonly Encoding FileEncoding = Encoding.Latin1;
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private const int TextWidth = 60;

    /// <param name="serialNumber">Delivery serial number. Must go up by one for every file the institution sends.</param>
    public static string Write(Creditor creditor, long serialNumber, DateOnly collectionDate, DateOnly creationDate, IEnumerable<Collection> collections)
    {
        var list = collections.ToList();
        if (list.Any(c => c.Amount <= 0))
            throw new ArgumentException("Only amounts above 0 can be collected.", nameof(collections));

        var records = new List<string>
        {
            Rec("BS002", N(creditor.Cvr, 8), "BS1", "0601", N(serialNumber, 10), S(19), S(6), S(73)),
            Rec("BS012", N(creditor.PbsNumber, 8), "0112", S(5), N(creditor.DebtorGroup, 5), N(creditor.DataSupplierIdentification, 15),
                S(4), Date(creationDate), S(4), S(10), X(creditor.MainText, TextWidth)),
        };

        int count042 = 0, count052 = 0, count022 = 0;
        decimal total = 0;

        foreach (var c in list)
        {
            var head = (string code, int recordNo) => Rec(code, N(creditor.PbsNumber, 8), code == "BS042" ? "0280" : code == "BS052" ? "0241" : "0240",
                N(recordNo, 5), N(creditor.DebtorGroup, 5), N(c.PaymentNumber, 15));

            records.Add(head("BS022", 1) + Z(9) + X(c.PayerName, 35) + Pad(Z(14), 42));
            records.Add(head("BS022", 2) + Z(9) + X($"({c.ChildFirstName})", 35) + Pad(Z(14), 42));
            records.Add(head("BS022", 3) + Z(9) + X(c.Address, 35) + Pad(Z(14), 42));
            records.Add(head("BS022", 9) + Z(9) + S(15) + X(c.PostCode, 4) + S(3) + S(55));
            count022 += 4;

            records.Add(head("BS042", 0) + Z(9) + Date(collectionDate) + "1" + Ore(c.Amount) + S(30) + Z(2) + S(15) + S(8));
            count042++;
            total += c.Amount;

            foreach (var text in TextLines(c))
            {
                records.Add(head("BS052", 0) + Z(9) + S(1) + X(text, TextWidth) + S(16));
                count052++;
            }
        }

        records.Add(Rec("BS092", N(creditor.PbsNumber, 8), "0112", S(5), N(creditor.DebtorGroup, 5), S(4),
            N(count042, 11), Ore(total, 15), N(count052, 11), S(15), N(count022, 11), S(34)));
        records.Add(Rec("BS992", N(creditor.Cvr, 8), "BS1", "0601", N(1, 11), N(count042, 11), Ore(total, 15), N(count052, 11),
            Z(15), N(count022, 11), Pad(Z(31), 34)));

        foreach (var r in records)
            if (r.Length != 128) throw new InvalidOperationException($"Record is {r.Length} characters, not 128: {r[..5]}");

        return string.Join("\r\n", records) + "\r\n";
    }

    /// <summary>An empty first line, then one line per charge line: text left, amount right.</summary>
    public static IEnumerable<string> TextLines(Collection c)
    {
        yield return "";
        foreach (var line in c.Lines)
        {
            var amount = line.Amount.ToString("N2", Danish);
            var room = TextWidth - amount.Length - 1;
            var text = line.Text.Length > room ? line.Text[..room] : line.Text;
            yield return text.PadRight(TextWidth - amount.Length) + amount;
        }
    }

    private static string Rec(params string[] parts) => string.Concat(parts);
    private static string S(int n) => new(' ', n);
    private static string Z(int n) => new('0', n);
    private static string Pad(string s, int n) => s.PadRight(n);
    private static string Date(DateOnly d) => d.ToString("ddMMyyyy", CultureInfo.InvariantCulture);

    /// <summary>Alphanumeric field: left-aligned, space-filled, cut to length, outside ISO-8859-1 becomes '?'.</summary>
    private static string X(string value, int n)
    {
        var chars = value.Select(ch => ch <= 'ÿ' ? ch : '?').ToArray();
        var s = new string(chars);
        return s.Length >= n ? s[..n] : s.PadRight(n);
    }

    /// <summary>Numeric field: right-aligned, zero-filled. Fails rather than cut a number.</summary>
    private static string N(long value, int n) => N(value.ToString(CultureInfo.InvariantCulture), n);

    private static string N(string digits, int n)
    {
        if (digits.Length > n || !digits.All(char.IsAsciiDigit))
            throw new ArgumentException($"'{digits}' does not fit a {n}-digit field.");
        return digits.PadLeft(n, '0');
    }

    /// <summary>Amount in øre without sign.</summary>
    private static string Ore(decimal amount, int n = 13) => N((long)(Math.Abs(Money.Round(amount)) * 100), n);
}
