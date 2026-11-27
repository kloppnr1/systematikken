using System.Globalization;
using System.Text;
using Systematikken.Billing;

namespace Systematikken.Api.Services;

/// <summary>
/// The monthly "Bogføringsbilag" as a one-page PDF (docs/accounting-voucher.md).
/// Written by hand with the built-in Helvetica font, so it needs no PDF library and no font files on the server.
/// </summary>
public static class VoucherPdf
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");

    public static byte[] Create(string institutionName, BookkeepingVoucher voucher)
    {
        var month = voucher.Month.FirstDay.ToString("MMMM yyyy", Danish);
        string Amount(decimal a) => a.ToString("0.00", Danish);

        var content = new StringBuilder();
        void Text(double x, double y, int size, string text) =>
            content.Append(CultureInfo.InvariantCulture, $"BT /F1 {size} Tf {x} {y} Td ({Escape(text)}) Tj ET\n");
        void Line(double x1, double y1, double x2, double y2) =>
            content.Append(CultureInfo.InvariantCulture, $"{x1} {y1} m {x2} {y2} l S\n");

        Text(56, 780, 16, "Bogføringsbilag");
        Text(56, 758, 11, $"{institutionName}, {month}");

        double[] cols = [56, 236, 416];
        string[] heads = ["Opkrævningssum", "Opkrævninger", "Tilskudssum"];
        string[] values = [Amount(voucher.Opkraevningssum), Amount(voucher.Opkraevninger), Amount(voucher.Tilskudssum)];

        content.Append("0.5 w\n");
        Line(56, 730, 596 - 56, 730);
        Line(56, 708, 596 - 56, 708);
        Line(56, 686, 596 - 56, 686);
        for (var i = 0; i < 3; i++)
        {
            Text(cols[i] + 4, 715, 10, heads[i]);
            Text(cols[i] + 4, 693, 10, values[i]);
        }

        Text(56, 650, 8, "Opkrævningssum = Opkrævninger + depositum + indmeldelsesgebyr - Tilskudssum. Overførte beløb og indbetalinger er ikke med.");

        return Build(content.ToString());
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    /// <summary>Assembles a single-page A4 PDF. Text is WinAnsi, which matches ISO-8859-1 for Danish letters.</summary>
    private static byte[] Build(string pageContent)
    {
        var enc = Encoding.Latin1;
        var stream = enc.GetBytes(pageContent);
        var objects = new List<byte[]>
        {
            enc.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            enc.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            enc.GetBytes("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 596 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>"),
            enc.GetBytes($"<< /Length {stream.Length} >>\nstream\n").Concat(stream).Concat(enc.GetBytes("\nendstream")).ToArray(),
            enc.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),
        };

        using var ms = new MemoryStream();
        void Write(string s) => ms.Write(enc.GetBytes(s));

        Write("%PDF-1.4\n");
        var offsets = new List<long>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(ms.Position);
            Write($"{i + 1} 0 obj\n");
            ms.Write(objects[i]);
            Write("\nendobj\n");
        }

        var xref = ms.Position;
        Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) Write($"{o:D10} 00000 n \n");
        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }
}
