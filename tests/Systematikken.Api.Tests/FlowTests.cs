using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Systematikken.Api.Tests;

/// <summary>Whole flows through the real API, the way the React app uses it.</summary>
public class FlowTests : IDisposable
{
    private readonly ApiFactory _f = new();
    public void Dispose() => _f.Dispose();

    private static string NextMonth(int add = 1) => DateTime.Today.AddMonths(add).ToString("yyyy-MM");
    private static string FirstOf(int add = 1) => NextMonth(add) + "-01";

    private static async Task ConfigureBilling(HttpClient c)
    {
        var s = (await c.Get("/api/settings")).GetProperty("settings");
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(s.GetRawText())!;
        dict["nurseryPrice"] = 4233.50m;
        dict["kindergartenPrice"] = 1233.25m;
        dict["cvr"] = "12345678";
        dict["pbsNumber"] = "87654321";
        dict["monthWithoutCharge"] = null;
        await c.Put("/api/settings", dict);
    }

    [Fact]
    public async Task AnonymousUserIsNotSignedIn()
    {
        var me = await _f.Browser().Get("/api/me");
        Assert.False(me.GetProperty("signedIn").GetBoolean());
        Assert.True(me.GetProperty("devLogin").GetBoolean());
    }

    [Fact]
    public async Task ApiNeedsLogin()
    {
        var r = await _f.Browser().GetAsync("/api/children");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task ChangesNeedTheRequestHeader()
    {
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/auth/dev", new { email = "a@b.dk" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task NewUserCreatesInstitution()
    {
        var c = await _f.SignedIn("leder@moelle.dk");
        var before = await c.Get("/api/me");
        Assert.Equal(JsonValueKind.Null, before.GetProperty("institution").ValueKind);

        var r = await c.GetAsync("/api/children");
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);

        c = await _f.SignedIn("leder2@moelle.dk", "Børnehuset Mælkebøtten");
        var me = await c.Get("/api/me");
        Assert.Equal("Børnehuset Mælkebøtten", me.GetProperty("institution").GetProperty("name").GetString());
        Assert.Equal("boernehuset-maelkeboetten", me.GetProperty("institution").GetProperty("slug").GetString());
    }

    [Fact]
    public async Task InstitutionsCannotSeeEachOther()
    {
        var a = await _f.SignedIn("a@a.dk", "Huset A");
        var b = await _f.SignedIn("b@b.dk", "Huset B");
        var child = await a.Post("/api/children", new
        {
            firstName = "Alma", lastName = "Krogh", dateOfBirth = "2024-05-01", startDate = FirstOf(), isPartTime = false,
            payer = new { name = "Sofie Krogh", address = "Vej 1", postCode = "8000", city = "Aarhus" },
        });
        var id = child.GetProperty("id").GetString();

        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/children/{id}")).StatusCode);
        Assert.Empty((await b.Get("/api/children")).EnumerateArray());
        Assert.Single((await a.Get("/api/children")).EnumerateArray());
    }

    [Fact]
    public async Task ExportHoldsOnlyTheInstitutionsOwnData()
    {
        var a = await _f.SignedIn("a@a.dk", "Huset A");
        var b = await _f.SignedIn("b@b.dk", "Huset B");
        await a.Post("/api/children", new
        {
            firstName = "Alma", lastName = "Krogh", dateOfBirth = "2024-05-01", startDate = FirstOf(), isPartTime = false,
            payer = new { name = "Sofie Krogh; mor", phone = "+45 12 34 56 78", address = "Vej 1", postCode = "8000", city = "Aarhus" },
        });
        var slug = (await a.Get("/api/me")).GetProperty("institution").GetProperty("slug").GetString();
        await _f.Browser().Post($"/api/public/institutions/{slug}/signup", new
        {
            childName = "=HYPERLINK(\"x\")", dateOfBirth = DateTime.Today.AddYears(-1).ToString("yyyy-MM-dd"), desiredStart = FirstOf(2),
            hasSibling = false, parent1Name = "Ida Holm", parent1Email = "ida@holm.dk", parent1Phone = "12345678",
            address = "Mælkevej 3", postCode = "8000", city = "Aarhus", consent = true,
        });

        var r = await a.GetAsync("/api/export");
        r.EnsureSuccessStatusCode();
        Assert.Equal("application/zip", r.Content.Headers.ContentType!.MediaType);
        using var zip = new System.IO.Compression.ZipArchive(await r.Content.ReadAsStreamAsync());
        string Read(string name) => new StreamReader(zip.GetEntry(name)!.Open(), Encoding.UTF8).ReadToEnd();

        Assert.Contains("Huset A", Read("institution.csv"));
        Assert.Contains("1;Alma;Krogh;2024-05-01", Read("boern.csv"));
        var parents = Read("foraeldre.csv");
        Assert.Contains("\"Sofie Krogh; mor\"", parents);
        Assert.Contains(";+45 12 34 56 78;", parents);
        var waiting = Read("venteliste.csv");
        Assert.Contains("\"'=HYPERLINK(\"\"x\"\")\"", waiting);
        Assert.Contains("Venter", waiting);
        Assert.Contains("a@a.dk", Read("kolleger.csv"));

        var other = await b.GetAsync("/api/export");
        using var otherZip = new System.IO.Compression.ZipArchive(await other.Content.ReadAsStreamAsync());
        var otherChildren = new StreamReader(otherZip.GetEntry("boern.csv")!.Open()).ReadToEnd();
        Assert.DoesNotContain("Alma", otherChildren);
        Assert.DoesNotContain("Huset A", new StreamReader(otherZip.GetEntry("institution.csv")!.Open()).ReadToEnd());
        Assert.DoesNotContain("a@a.dk", new StreamReader(otherZip.GetEntry("kolleger.csv")!.Open()).ReadToEnd());
    }

    [Fact]
    public async Task InvitedColleagueJoins()
    {
        var a = await _f.SignedIn("leder@a.dk", "Huset A");
        var inv = await a.Post("/api/staff/invitations", new { email = "Kollega@A.dk" });
        var token = inv.GetProperty("url").GetString()!.Split('/').Last();

        var wrong = await _f.SignedIn("fremmed@x.dk");
        Assert.Equal(HttpStatusCode.Forbidden, (await wrong.PostAsJsonAsync($"/api/invitations/{token}/accept", new { })).StatusCode);

        var b = await _f.SignedIn("kollega@a.dk");
        Assert.Single((await b.Get("/api/me")).GetProperty("invitations").EnumerateArray());
        await b.Post($"/api/invitations/{token}/accept");
        Assert.Equal("Huset A", (await b.Get("/api/me")).GetProperty("institution").GetProperty("name").GetString());
    }

    [Fact]
    public async Task FromSignupToChild()
    {
        var manager = await _f.SignedIn("leder@a.dk", "Huset A");
        var slug = (await manager.Get("/api/me")).GetProperty("institution").GetProperty("slug").GetString();
        var parent = _f.Browser();

        // A parent signs up on the public form.
        var receipt = await parent.Post($"/api/public/institutions/{slug}/signup", new
        {
            childName = "Liva Holm", dateOfBirth = DateTime.Today.AddYears(-1).ToString("yyyy-MM-dd"), desiredStart = FirstOf(2),
            hasSibling = true, parent1Name = "Ida Holm", parent1Email = "ida@holm.dk", parent1Phone = "12345678",
            address = "Mælkevej 3", postCode = "8000", city = "Aarhus", consent = true,
        });
        Assert.Equal(8, receipt.GetProperty("receipt").GetString()!.Length);

        // The manager sees the applicant and offers a place.
        var list = await manager.Get("/api/applicants");
        var applicant = list.GetProperty("applicants").EnumerateArray().Single();
        var id = applicant.GetProperty("id").GetString();
        Assert.True(applicant.GetProperty("hasSibling").GetBoolean());

        await manager.Post($"/api/applicants/{id}/offer", new { start = FirstOf(2) });
        var detail = await manager.Get($"/api/applicants/{id}");
        var text = detail.GetProperty("message").GetProperty("text").GetString()!;
        var token = text.Split("/svar/")[1].Split('\n')[0];
        Assert.Contains("Liva", text);

        // The parent answers yes on the link.
        Assert.Equal("Offered", (await parent.Get($"/api/public/answer/{token}")).GetProperty("state").GetString());
        await parent.Post($"/api/public/answer/{token}", new { yes = true });

        // The to-do list asks the manager to create the child; doing it makes the first parent the payer.
        var todo = await manager.Get("/api/todo");
        Assert.Contains(todo.GetProperty("items").EnumerateArray(), i => i.GetProperty("title").GetString()!.Contains("har sagt ja"));
        var child = await manager.Post($"/api/applicants/{id}/enrol");
        var card = await manager.Get($"/api/children/{child.GetProperty("id").GetString()}");
        Assert.Equal("Liva", card.GetProperty("firstName").GetString());
        Assert.Equal("Ida Holm", card.GetProperty("parents")[0].GetProperty("name").GetString());
        Assert.Equal(card.GetProperty("parents")[0].GetProperty("id").GetString(), card.GetProperty("payerId").GetString());
    }

    [Fact]
    public async Task SignupRejectsBadInput()
    {
        var manager = await _f.SignedIn("leder@a.dk", "Huset A");
        var slug = (await manager.Get("/api/me")).GetProperty("institution").GetProperty("slug").GetString();
        var r = await _f.Browser().PostAsJsonAsync($"/api/public/institutions/{slug}/signup", new { childName = "X", consent = false });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var errors = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("consent", out _));
        Assert.True(errors.TryGetProperty("postCode", out _));
    }

    [Fact]
    public async Task MonthIsApprovedAndPaid()
    {
        var c = await _f.SignedIn("leder@a.dk", "Huset A");
        await ConfigureBilling(c);
        var child = await c.Post("/api/children", new
        {
            firstName = "Oscar", lastName = "Lund", dateOfBirth = DateTime.Today.AddYears(-1).ToString("yyyy-MM-dd"), startDate = FirstOf(), isPartTime = false,
            payer = new { name = "Mette Lund", address = "Vej 2", postCode = "8000", city = "Aarhus" },
        });
        var childId = child.GetProperty("id").GetString();

        // Draft: one nursery child, full month.
        var draft = await c.Get("/api/billing");
        Assert.Equal(NextMonth()[5..] + "-" + NextMonth()[..4], draft.GetProperty("month").GetString());
        Assert.True(draft.GetProperty("canApprove").GetBoolean());
        Assert.Equal(4233.50m, draft.GetProperty("totals").GetProperty("toCollect").GetDecimal());

        // Approve, then the collection file and voucher exist.
        var run = await c.Post("/api/billing/approve", new { month = draft.GetProperty("month").GetString() });
        var runId = run.GetProperty("id").GetInt32();
        var file = await c.GetStringAsync($"/api/billing/runs/{runId}/file");
        Assert.StartsWith("BS0021234567", file);
        Assert.Contains("0000000423350", file);
        var pdf = await c.GetByteArrayAsync($"/api/billing/runs/{runId}/voucher");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));

        // The same month cannot be approved twice.
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/billing/approve", new { month = draft.GetProperty("month").GetString() })).StatusCode);

        // The bank's payment file books the payment; the child owes nothing.
        var card = await c.Get($"/api/children/{childId}");
        Assert.Equal(4233.50m, card.GetProperty("balance").GetDecimal());
        var paymentNumber = card.GetProperty("paymentNumber").GetInt32();
        var collection = DateTime.Today.AddMonths(1);
        var date = new DateTime(collection.Year, collection.Month, 1).ToString("ddMMyy");
        var line = ("BS042" + "87654321" + "0236" + "000" + "00001" + paymentNumber.ToString().PadLeft(15, '0') + "000000000" + date + "1"
            + "423350".PadLeft(13, '0')).PadRight(103) + date + date + "423350".PadLeft(13, '0');
        var payFile = string.Join("\r\n",
            "BS00212345678BS10602" + "0000000001" + new string(' ', 19) + date,
            "BS01287654321021100000000001" + new string(' ', 21) + date,
            line,
            "BS092".PadRight(128),
            "BS992".PadRight(128)) + "\r\n";

        using var form = new MultipartFormDataContent { { new ByteArrayContent(Encoding.Latin1.GetBytes(payFile)), "file", "D0001.BS1" } };
        var import = await (await c.PostAsync("/api/billing/payments", form)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, import.GetProperty("booked").GetInt32());

        Assert.Equal(0m, (await c.Get($"/api/children/{childId}")).GetProperty("balance").GetDecimal());

        // Importing the same file again books nothing.
        using var again = new MultipartFormDataContent { { new ByteArrayContent(Encoding.Latin1.GetBytes(payFile)), "file", "D0001.BS1" } };
        var second = await (await c.PostAsync("/api/billing/payments", again)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(second.GetProperty("alreadyImported").GetBoolean());
    }

    [Fact]
    public async Task ChildFromBeforeSystematikkenIsNotCaughtUp()
    {
        // Started a year ago, billed in the old system. The first month in Systematikken is only that month.
        var c = await _f.SignedIn("leder@a.dk", "Huset A");
        await ConfigureBilling(c);
        await c.Post("/api/children", new
        {
            firstName = "Freja", dateOfBirth = DateTime.Today.AddYears(-1).AddMonths(-6).ToString("yyyy-MM-dd"),
            startDate = DateTime.Today.AddYears(-1).ToString("yyyy-MM-01"), isPartTime = false,
            payer = new { name = "Anders", address = "Vej 1", postCode = "8000", city = "Aarhus" },
        });
        var draft = await c.Get("/api/billing");
        Assert.Equal(4233.50m, draft.GetProperty("totals").GetProperty("toCollect").GetDecimal());
        Assert.Empty(draft.GetProperty("changes").EnumerateArray());
    }

    [Fact]
    public async Task MissingPayerBlocksApproval()
    {
        var c = await _f.SignedIn("leder@a.dk", "Huset A");
        await ConfigureBilling(c);
        var child = await c.Post("/api/children", new
        {
            firstName = "Alma", dateOfBirth = DateTime.Today.AddYears(-1).ToString("yyyy-MM-dd"), startDate = FirstOf(), isPartTime = false,
            payer = new { name = "Sofie", address = "Vej 1", postCode = "8000", city = "Aarhus" },
        });
        var id = child.GetProperty("id").GetString();
        var parentId = (await c.Get($"/api/children/{id}")).GetProperty("parents")[0].GetProperty("id").GetString();
        await c.Send(HttpMethod.Delete, $"/api/children/{id}/parents/{parentId}");

        var draft = await c.Get("/api/billing");
        Assert.False(draft.GetProperty("canApprove").GetBoolean());
        Assert.Equal("MissingPayer", draft.GetProperty("problems")[0].GetProperty("kind").GetString());
    }

    [Fact]
    public async Task BackupIsACompleteDatabase()
    {
        await _f.SignedIn("leder@a.dk", "Huset A");
        var backup = _f.Services.GetRequiredService<Services.BackupService>();
        var file = backup.RunOnce();

        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={file};Mode=ReadOnly;Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Institutions";
        Assert.Equal("Huset A", cmd.ExecuteScalar());
    }

    [Fact]
    public async Task OccupancyCountsChildren()
    {
        var c = await _f.SignedIn("leder@a.dk", "Huset A");
        await c.Post("/api/children", new
        {
            firstName = "Alma", dateOfBirth = DateTime.Today.AddYears(-1).ToString("yyyy-MM-dd"), startDate = FirstOf(), isPartTime = false,
            payer = new { name = "Sofie", address = "Vej 1", postCode = "8000", city = "Aarhus" },
        });
        var report = await c.Get($"/api/occupancy?from={NextMonth()}&months=2");
        var first = report.GetProperty("months")[0];
        Assert.Equal(1, first.GetProperty("nursery").GetProperty("count").GetInt32());
        Assert.Single(first.GetProperty("nursery").GetProperty("joining").EnumerateArray());
    }
}
