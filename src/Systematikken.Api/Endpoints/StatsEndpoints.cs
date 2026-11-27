using Microsoft.EntityFrameworkCore;
using Systematikken.Api.Data;
using Systematikken.Api.Services;

namespace Systematikken.Api.Endpoints;

/// <summary>The front page reports visits and clicks here; the operator reads the numbers on /tal.</summary>
public static class StatsEndpoints
{
    public sealed record Hit(string? Name, string? Source);

    public static void Map(WebApplication app)
    {
        app.MapPost("/api/stats/hit", async (Hit body, StatsService stats, IConfiguration config) =>
        {
            if (config.GetValue<bool>("Demo:Enabled")) return Results.NoContent();
            if (body.Name is null || !StatsService.PageEvents.Contains(body.Name)) return Results.BadRequest();
            await stats.CountAsync(body.Name, body.Source);
            return Results.NoContent();
        }).AllowAnonymous().RequireRateLimiting("public");

        app.MapGet("/api/stats", async (HttpContext ctx, StatsService stats, AppDb db) =>
        {
            if (!stats.IsOperator(ctx.User.Email())) return Results.NotFound();
            var from = stats.Today.AddDays(-59);
            var rows = await db.DailyCounts.Where(c => c.Day >= from)
                .Select(c => new { c.Day, c.Name, c.Source, c.Count }).ToListAsync();
            var first = await db.DailyCounts.OrderBy(c => c.Day).Select(c => (DateOnly?)c.Day).FirstOrDefaultAsync();
            return Results.Ok(new { today = stats.Today, countingSince = first, rows });
        });
    }
}
