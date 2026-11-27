using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Systematikken.Api.Tests;

/// <summary>The real app on a fresh SQLite file, with the development login switched on.</summary>
public sealed class ApiFactory(bool demo = false) : WebApplicationFactory<Program>
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "systematikken-tests", Guid.NewGuid().ToString("N"));

    public ApiFactory() : this(false) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("DataDirectory", _dir);
        builder.UseSetting("Auth:DevLogin", "true");
        builder.UseSetting("Operator:Emails", "drift@systematikken.dk, anden@systematikken.dk");
        if (demo) builder.UseSetting("Demo:Enabled", "true");
    }

    /// <summary>A browser-like client: keeps cookies and sends the header every change needs.</summary>
    public HttpClient Browser()
    {
        var c = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Requested-With", "systematikken");
        return c;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(_dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

public static class Json
{
    public static async Task<JsonElement> Get(this HttpClient c, string url)
    {
        var r = await c.GetAsync(url);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<JsonElement> Send(this HttpClient c, HttpMethod method, string url, object? body = null)
    {
        var r = await c.SendAsync(new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) });
        if (!r.IsSuccessStatusCode) throw new HttpRequestException($"{method} {url}: {(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
        return r.StatusCode == System.Net.HttpStatusCode.NoContent ? default : await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static Task<JsonElement> Post(this HttpClient c, string url, object? body = null) => c.Send(HttpMethod.Post, url, body ?? new { });
    public static Task<JsonElement> Put(this HttpClient c, string url, object body) => c.Send(HttpMethod.Put, url, body);

    public static async Task<HttpClient> SignedIn(this ApiFactory f, string email, string? institution = null)
    {
        var c = f.Browser();
        await c.Post("/auth/dev", new { email });
        if (institution is not null)
        {
            var municipalities = await c.Get("/api/municipalities");
            var aarhus = municipalities.EnumerateArray().First(m => m.GetProperty("name").GetString() == "Aarhus").GetProperty("id").GetInt32();
            await c.Post("/api/institutions", new
            {
                name = institution, municipalityId = aarhus, contactEmail = email,
                cutoverYears = 2, cutoverMonths = 11, schoolStartMonth = 8,
            });
        }
        return c;
    }
}
