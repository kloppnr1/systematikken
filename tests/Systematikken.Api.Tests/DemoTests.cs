using System.Net;
using System.Text.Json;

namespace Systematikken.Api.Tests;

/// <summary>The demo builds a full, believable institution and anyone can log in to it with one button.</summary>
public class DemoTests : IDisposable
{
    private readonly ApiFactory _f = new(demo: true);
    public void Dispose() => _f.Dispose();

    [Fact]
    public async Task DemoLoginShowsAFullInstitution()
    {
        var c = _f.Browser();
        Assert.True((await c.Get("/api/me")).GetProperty("demo").GetBoolean());
        await c.Post("/auth/demo");

        var me = await c.Get("/api/me");
        Assert.Equal("Børnehuset Mælkebøtten", me.GetProperty("institution").GetProperty("name").GetString());

        var children = (await c.Get("/api/children?filter=alle")).EnumerateArray().ToList();
        Assert.InRange(children.Count, 25, 40);
        Assert.Contains(children, x => x.GetProperty("balance").GetDecimal() > 0); // two families have not paid

        var waiting = (await c.Get("/api/applicants")).GetProperty("applicants").EnumerateArray().ToList();
        Assert.Contains(waiting, a => a.GetProperty("status").GetString() == "Har sagt ja");

        var billing = await c.Get("/api/billing");
        Assert.Equal(2, billing.GetProperty("runs").GetArrayLength());
        Assert.Contains(billing.GetProperty("problems").EnumerateArray(), p => p.GetProperty("kind").GetString() == "MissingSubsidy");

        var todo = (await c.Get("/api/todo")).GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString()).ToList();
        Assert.Contains(todo, t => t!.Contains("har sagt ja"));
        Assert.Contains(todo, t => t!.Contains("ikke svaret"));
    }

    [Fact]
    public async Task DemoLoginIsOffOutsideTheDemo()
    {
        using var normal = new ApiFactory();
        var r = await normal.Browser().PostAsync("/auth/demo", null);
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }
}
