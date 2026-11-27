using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Systematikken.Api.Tests;

/// <summary>Counting visits and clicks without anything about the visitor, and who may read the numbers.</summary>
public class StatsTests : IDisposable
{
    private readonly ApiFactory _f = new();
    public void Dispose() => _f.Dispose();

    private static int Sum(JsonElement stats, string name, string? source = null) =>
        stats.GetProperty("rows").EnumerateArray()
            .Where(r => r.GetProperty("name").GetString() == name && (source is null || r.GetProperty("source").GetString() == source))
            .Sum(r => r.GetProperty("count").GetInt32());

    [Fact]
    public async Task FrontPageEventsAreCountedPerSource()
    {
        var visitor = _f.Browser();
        await visitor.Post("/api/stats/hit", new { name = "besog", source = "linkedin" });
        await visitor.Post("/api/stats/hit", new { name = "besog", source = "linkedin" });
        await visitor.Post("/api/stats/hit", new { name = "besog", source = "direkte" });
        await visitor.Post("/api/stats/hit", new { name = "demo", source = "LinkedIn" });
        await visitor.Post("/api/stats/hit", new { name = "besog", source = "<script>" });

        var unknown = await visitor.PostAsJsonAsync("/api/stats/hit", new { name = "noget-andet", source = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

        var op = await _f.SignedIn("Drift@Systematikken.dk");
        Assert.True((await op.Get("/api/me")).GetProperty("operator").GetBoolean());
        var stats = await op.Get("/api/stats");
        Assert.Equal(2, Sum(stats, "besog", "linkedin"));
        Assert.Equal(1, Sum(stats, "besog", "direkte"));
        Assert.Equal(1, Sum(stats, "besog", "andet"));
        Assert.Equal(1, Sum(stats, "demo", "linkedin"));
    }

    [Fact]
    public async Task NewInstitutionsAreCounted()
    {
        await _f.SignedIn("leder@a.dk", "Huset A");
        await _f.SignedIn("leder@b.dk", "Huset B");
        var stats = await (await _f.SignedIn("anden@systematikken.dk")).Get("/api/stats");
        Assert.Equal(2, Sum(stats, "institution"));
    }

    [Fact]
    public async Task OnlyTheOperatorSeesTheNumbers()
    {
        var manager = await _f.SignedIn("leder@a.dk", "Huset A");
        Assert.False((await manager.Get("/api/me")).GetProperty("operator").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync("/api/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.Browser().GetAsync("/api/stats")).StatusCode);
    }
}
