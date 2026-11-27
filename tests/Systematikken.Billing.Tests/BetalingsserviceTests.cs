using Systematikken.Billing.Betalingsservice;

namespace Systematikken.Billing.Tests;

public class BetalingsserviceTests
{
    private static readonly Creditor Creditor = new()
    {
        Cvr = "12345678",
        PbsNumber = "87654321",
        DataSupplierIdentification = 42,
        MainText = "Børnehuset Mælkebøtten",
    };

    private static readonly BillingMonth Jan = new(2017, 1);

    private static Collection Example(int paymentNumber = 7, decimal? amount = null) => new()
    {
        PaymentNumber = paymentNumber,
        PayerName = "Ida Holm",
        ChildFirstName = "Liva",
        Address = "Mælkevej 3",
        PostCode = "8000",
        Amount = amount ?? 1582.75m,
        Lines =
        [
            new(LineKind.Price, Jan, 4233.50m, "Forældrebetaling vuggestue"),
            new(LineKind.FreePlace, Jan, -1100.25m, "Friplads"),
            new(LineKind.SiblingDiscount, Jan, -1550.50m, "Søskenderabat"),
        ],
    };

    private static string[] Records(string file) => file.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void EveryRecordIs128CharactersWithCrlf()
    {
        var file = CollectionFile.Write(Creditor, 1, new(2017, 1, 1), new(2016, 12, 20), [Example(1), Example(2)]);
        Assert.EndsWith("\r\n", file);
        Assert.All(Records(file), r => Assert.Equal(128, r.Length));
    }

    [Fact]
    public void RecordOrderPerCollection()
    {
        var types = Records(CollectionFile.Write(Creditor, 1, new(2017, 1, 1), new(2016, 12, 20), [Example()]))
            .Select(r => r[..5]);
        Assert.Equal(
            ["BS002", "BS012", "BS022", "BS022", "BS022", "BS022", "BS042", "BS052", "BS052", "BS052", "BS052", "BS092", "BS992"],
            types);
    }

    [Fact]
    public void DeliveryStartCarriesCvrTypeAndSerialNumber()
    {
        var start = Records(CollectionFile.Write(Creditor, 16, new(2017, 1, 1), new(2016, 12, 20), [Example()]))[0];
        Assert.Equal("BS0021234567" + "8BS106010000000016", start[..30]);
    }

    [Fact]
    public void CollectionRecordHasDateSignAndAmountInOre()
    {
        var r = Records(CollectionFile.Write(Creditor, 1, new(2017, 1, 1), new(2016, 12, 20), [Example()]))
            .Single(x => x.StartsWith("BS042"));
        Assert.Equal("87654321", r.Substring(5, 8));
        Assert.Equal("0280", r.Substring(13, 4));
        Assert.Equal("000000000000007", r.Substring(27, 15));
        Assert.Equal("01012017", r.Substring(51, 8));
        Assert.Equal("1", r.Substring(59, 1));
        Assert.Equal("0000000158275", r.Substring(60, 13));
    }

    [Fact]
    public void TextLinesHaveAmountRightAligned()
    {
        var lines = CollectionFile.TextLines(Example()).ToList();
        Assert.Equal("", lines[0]);
        Assert.Equal("Forældrebetaling vuggestue".PadRight(52) + "4.233,50", lines[1]);
        Assert.Equal("Friplads".PadRight(51) + "-1.100,25", lines[2]);
        Assert.All(lines.Skip(1), l => Assert.Equal(60, l.Length));
    }

    [Fact]
    public void EndRecordsCountAndTotal()
    {
        var records = Records(CollectionFile.Write(Creditor, 1, new(2017, 1, 1), new(2016, 12, 20), [Example(1), Example(2, 100m)]));
        var sectionEnd = records[^2];
        var deliveryEnd = records[^1];

        Assert.Equal("00000000002", sectionEnd.Substring(31, 11)); // collections
        Assert.Equal("000000000168275", sectionEnd.Substring(42, 15)); // 1,582.75 + 100.00 in øre
        Assert.Equal("00000000008", sectionEnd.Substring(57, 11)); // text lines
        Assert.Equal("00000000008", sectionEnd.Substring(83, 11)); // name and address records

        Assert.Equal("00000000001", deliveryEnd.Substring(20, 11)); // sections
        Assert.Equal("00000000002", deliveryEnd.Substring(31, 11));
        Assert.Equal("000000000168275", deliveryEnd.Substring(42, 15));
    }

    [Fact]
    public void RefusesNothingToCollect() =>
        Assert.Throws<ArgumentException>(() => CollectionFile.Write(Creditor, 1, new(2017, 1, 1), new(2016, 12, 20), [Example(amount: 0m)]));

    [Fact]
    public void LongNamesAreCutNotOverflowing()
    {
        var c = Example() with { PayerName = new string('A', 60) };
        Assert.All(Records(CollectionFile.Write(Creditor, 1, new(2017, 1, 1), new(2016, 12, 20), [c])), r => Assert.Equal(128, r.Length));
    }

    // Payment file (0602). Built from the record layout; values are made up.

    private static string PaymentRecord(string code, int customer, string date, long ore, string payDate, string bookDate, long paidOre) =>
        ("BS042" + "87654321" + code + "000" + "00001" + customer.ToString().PadLeft(15, '0') + "000000000" + date + "1"
            + ore.ToString().PadLeft(13, '0')).PadRight(103) + payDate + bookDate + paidOre.ToString().PadLeft(13, '0');

    private static string PaymentFileWith(params string[] records) => string.Join("\r\n",
    [
        "BS00212345678BS10602" + "0000000001" + new string(' ', 19) + "020117",
        "BS01287654321021100000000001" + new string(' ', 21) + "020117",
        .. records,
        "BS09287654321021100000".PadRight(128),
        "BS99212345678BS10602".PadRight(128),
    ]) + "\r\n";

    [Fact]
    public void ReadsCompletedPayments()
    {
        var file = PaymentFile.Read(PaymentFileWith(PaymentRecord("0236", 7, "010117", 158275, "020117", "020117", 158275)));

        Assert.Equal("87654321", file.PbsNumber);
        Assert.Equal(new DateOnly(2017, 1, 2), file.CreatedOn);
        var p = Assert.Single(file.Payments);
        Assert.Equal(new Payment(7, new(2017, 1, 1), 1582.75m, new(2017, 1, 2), new(2017, 1, 2), 1582.75m), p);
        Assert.Empty(file.Unhandled);
    }

    [Fact]
    public void ToleratesTrimmedLines()
    {
        var file = PaymentFile.Read(PaymentFileWith(PaymentRecord("0236", 7, "010117", 100, "020117", "020117", 100)).Replace("        \r\n", "\r\n"));
        Assert.Single(file.Payments);
    }

    [Fact]
    public void OtherTransactionCodesAreShownNotBooked()
    {
        var file = PaymentFile.Read(PaymentFileWith(
            PaymentRecord("0236", 7, "010117", 100, "020117", "020117", 100),
            PaymentRecord("0237", 8, "010117", 100, "020117", "020117", 0)));

        Assert.Single(file.Payments);
        Assert.Equal(new UnhandledRecord(4, "0237", "000000000000008"), Assert.Single(file.Unhandled));
    }

    [Fact]
    public void RejectsOtherDeliveries() =>
        Assert.Throws<FormatException>(() => PaymentFile.Read("BS00212345678BS10601".PadRight(128) + "\r\n" + "BS992".PadRight(128)));

    [Fact]
    public void RejectsIncompleteFile() =>
        Assert.Throws<FormatException>(() => PaymentFile.Read(PaymentFileWith().Replace("BS992", "BS042")));
}
