using System.Globalization;
using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Systematikken.Api.Data;
using Systematikken.Billing.Betalingsservice;

namespace Systematikken.Api.Services;

/// <summary>
/// All of one institution's data as a ZIP of CSV files. The data is the institution's, so it can always take it with it.
/// The files use semicolons, a BOM and Danish decimals, so they open correctly in a Danish Excel.
/// </summary>
public sealed class ExportService(AppDb db)
{
    private static readonly CultureInfo Da = CultureInfo.GetCultureInfo("da-DK");

    public async Task<byte[]> CreateAsync()
    {
        var institution = await db.Institutions.Include(i => i.Municipality).SingleAsync(i => i.Id == db.InstitutionId);
        var children = await db.Children.Include(c => c.Parents).Include(c => c.Payer).OrderBy(c => c.PaymentNumber).ToListAsync();
        var parents = await db.Parents.Include(p => p.Children).OrderBy(p => p.Name).ToListAsync();
        var applicants = await db.Applicants.OrderBy(a => a.CreatedAt).ToListAsync();
        var subsidies = await db.Subsidies.OrderBy(s => s.ChildId).ThenBy(s => s.FromMonth).ToListAsync();
        var manual = await db.ManualCharges.OrderBy(m => m.Month).ToListAsync();
        var ledger = await db.Ledger.OrderBy(l => l.Month).ThenBy(l => l.Id).ToListAsync();
        var runs = await db.BillingRuns.OrderBy(r => r.Month).ToListAsync();
        var imports = await db.PaymentImports.OrderBy(p => p.ImportedAt).ToListAsync();
        var staff = await db.Memberships.Where(m => m.InstitutionId == institution.Id).Include(m => m.User).ToListAsync();

        var number = children.ToDictionary(c => c.Id, c => c.PaymentNumber);
        var name = children.ToDictionary(c => c.Id, c => c.FullName);
        string No(Guid id) => number.TryGetValue(id, out var n) ? n.ToString(Da) : "";
        string Name(Guid id) => name.GetValueOrDefault(id, "");

        using var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            var i = institution;
            Csv(archive, "institution.csv", ["Felt", "Værdi"],
            [
                ["Navn", i.Name], ["Tilmeldingsadresse", "/tilmeld/" + i.Slug], ["Kontakt-e-mail", i.ContactEmail], ["Kontakttelefon", i.ContactPhone],
                ["Kommune", i.Municipality?.Name], ["Vuggestuepris", D(i.NurseryPrice)], ["Børnehavepris", D(i.KindergartenPrice)],
                ["Vuggestuepris deltid", D(i.NurseryPartTimePrice)], ["Børnehavepris deltid", D(i.KindergartenPartTimePrice)],
                ["Betalingsfri måned", i.MonthWithoutCharge?.ToString(Da)], ["Halv måned fra dag", i.HalfMonthDay.ToString(Da)],
                ["Depositum", D(i.Deposit)], ["Depositum måneder før start", i.DepositMonthsAhead.ToString(Da)],
                ["Indmeldelsesgebyr", D(i.SignupFee)], ["Kostordning", D(i.MealScheme)],
                ["Vuggestuepladser", i.NurseryCapacity.ToString(Da)], ["Børnehavepladser", i.KindergartenCapacity.ToString(Da)],
                ["CVR", i.Cvr], ["PBS-nummer", i.PbsNumber], ["Debitorgruppe", i.DebtorGroup], ["Tekst på Betalingsservice", i.BetalingsserviceText],
                ["Første opkrævningsmåned", Month(i.FirstBillingMonth)], ["Venteliste gemmes i måneder", i.WaitingListRetentionMonths.ToString(Da)],
                ["Næste betalingsnummer", i.NextPaymentNumber.ToString(Da)], ["Oprettet", Time(i.CreatedAt)],
            ]);

            Csv(archive, "boern.csv",
                ["Betalingsnummer", "Fornavn", "Efternavn", "Fødselsdato", "Start", "Slut", "Deltid", "Betaler", "Forældre", "Note", "Oprettet"],
                children.Select(c => new[]
                {
                    c.PaymentNumber.ToString(Da), c.FirstName, c.LastName, Date(c.DateOfBirth), Date(c.StartDate), Date(c.EndDate),
                    YesNo(c.IsPartTime), c.Payer?.Name, string.Join(", ", c.Parents.Select(p => p.Name)), c.Note, Time(c.CreatedAt),
                }));

            Csv(archive, "foraeldre.csv", ["Navn", "E-mail", "Telefon", "Adresse", "Postnr.", "By", "Børn (betalingsnumre)"],
                parents.Select(p => new[]
                {
                    p.Name, p.Email, p.Phone, p.Address, p.PostCode, p.City,
                    string.Join(", ", p.Children.OrderBy(c => c.PaymentNumber).Select(c => c.PaymentNumber.ToString(Da))),
                }));

            Csv(archive, "venteliste.csv",
                ["Barn", "Fødselsdato", "Ønsket start", "Søskende", "Note", "Forælder 1", "E-mail 1", "Telefon 1", "Forælder 2", "E-mail 2", "Telefon 2",
                 "Adresse", "Postnr.", "By", "Status", "Skrevet op", "Tilbudt", "Tilbudt start", "Svaret", "Lukket"],
                applicants.Select(a => new[]
                {
                    a.ChildName, Date(a.DateOfBirth), Date(a.DesiredStart), YesNo(a.HasSibling), a.Note,
                    a.Parent1Name, a.Parent1Email, a.Parent1Phone, a.Parent2Name, a.Parent2Email, a.Parent2Phone,
                    a.Address, a.PostCode, a.City, Status(a.Status), Time(a.CreatedAt), Time(a.OfferedAt), Date(a.OfferedStart), Time(a.AnsweredAt), Time(a.ClosedAt),
                }));

            Csv(archive, "tilskud.csv", ["Betalingsnummer", "Barn", "Fra måned", "Til måned", "Friplads", "Søskenderabat"],
                subsidies.Select(s => new[] { No(s.ChildId), Name(s.ChildId), Month(s.FromMonth), Month(s.ToMonth), D(s.FreePlace), D(s.SiblingDiscount) }));

            Csv(archive, "manuelle-poster.csv", ["Betalingsnummer", "Barn", "Måned", "Tekst", "Beløb"],
                manual.Select(m => new[] { No(m.ChildId), Name(m.ChildId), Month(m.Month), m.Text, D(m.Amount) }));

            Csv(archive, "posteringer.csv", ["Betalingsnummer", "Barn", "Måned", "Type", "Tekst", "Beløb", "Bogført"],
                ledger.Select(l => new[]
                {
                    No(l.ChildId), Name(l.ChildId), Month(l.Month), l.Kind == LedgerKind.Charge ? "Opkrævning" : "Betaling",
                    l.Text, D(l.Amount), Time(l.CreatedAt),
                }));

            Csv(archive, "opkraevninger.csv",
                ["Måned", "Godkendt", "Godkendt af", "Løbenummer", "Antal opkrævninger", "Total", "Opkrævningssum", "Opkrævninger", "Tilskudssum"],
                runs.Select(r => new[]
                {
                    Month(r.Month), Time(r.ApprovedAt), r.ApprovedBy, r.SerialNumber.ToString(Da), r.ChargeCount.ToString(Da),
                    D(r.Total), D(r.Opkraevningssum), D(r.Opkraevninger), D(r.Tilskudssum),
                }));

            foreach (var r in runs.Where(r => r.CollectionFile.Length > 0))
            {
                var entry = archive.CreateEntry($"betalingsservice-filer/betalingsservice-{r.Month:yyyy-MM}.txt");
                await using var s = entry.Open();
                await s.WriteAsync(CollectionFile.FileEncoding.GetBytes(r.CollectionFile));
            }

            Csv(archive, "indlaeste-betalingsfiler.csv", ["Filnavn", "Indlæst", "Antal betalinger", "Total", "Ikke bogført"],
                imports.Select(p => new[] { p.FileName, Time(p.ImportedAt), p.PaymentCount.ToString(Da), D(p.Total), p.NotBooked.Replace('\n', ' ').Trim() }));

            Csv(archive, "kolleger.csv", ["Navn", "E-mail"],
                staff.Select(m => new[] { m.User?.DisplayName, m.User?.Email }));
        }
        return zip.ToArray();
    }

    private static void Csv(ZipArchive archive, string file, string?[] header, IEnumerable<string?[]> rows)
    {
        var sb = new StringBuilder();
        Line(sb, header);
        foreach (var row in rows) Line(sb, row);
        using var s = archive.CreateEntry(file).Open();
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        s.Write(bytes);
    }

    private static void Line(StringBuilder sb, string?[] cells)
    {
        sb.AppendJoin(';', cells.Select(Cell));
        sb.Append("\r\n");
    }

    /// <summary>Quotes a cell when it holds a separator, a quote or a line break. A leading = + - @ is escaped so Excel does not run it as a formula.</summary>
    private static string Cell(string? value)
    {
        var v = value ?? "";
        if (v.Length > 0 && "=+-@".Contains(v[0]) && !decimal.TryParse(v, NumberStyles.Number, Da, out _) && !IsPhone(v)) v = "'" + v;
        return v.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }

    private static bool IsPhone(string v) => v[0] == '+' && v.Skip(1).All(c => char.IsAsciiDigit(c) || c == ' ');

    private static string D(decimal? d) => d?.ToString("0.00", Da) ?? "";
    private static string Date(DateOnly? d) => d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
    private static string Month(DateOnly? d) => d?.ToString("yyyy-MM", CultureInfo.InvariantCulture) ?? "";
    private static string YesNo(bool b) => b ? "Ja" : "Nej";

    private static string Time(DateTime? utc) => utc is { } t
        ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(t, DateTimeKind.Utc), Copenhagen).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
        : "";

    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    private static string Status(ApplicantStatus s) => s switch
    {
        ApplicantStatus.Waiting => "Venter",
        ApplicantStatus.Offered => "Tilbudt",
        ApplicantStatus.Accepted => "Sagt ja",
        ApplicantStatus.Declined => "Sagt nej",
        ApplicantStatus.Enrolled => "Indmeldt",
        ApplicantStatus.Removed => "Fjernet",
        _ => s.ToString(),
    };
}
