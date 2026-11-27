using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Systematikken.Api.Data;

namespace Systematikken.Api.Services;

/// <summary>
/// Counts what people do on the front page and in the app, one number per day. Nothing about the visitor is kept:
/// no cookie, no IP address, no browser details. Only the kind of event and where the visitor came from.
/// </summary>
public sealed partial class StatsService(AppDb db, TimeProvider clock, IConfiguration config)
{
    /// <summary>Events the front page may report. Logins and new institutions are counted by the server itself.</summary>
    public static readonly string[] PageEvents = ["besog", "demo", "kom-i-gang", "log-ind"];

    public const string Login = "login";
    public const string NewInstitution = "institution";

    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Copenhagen).DateTime);

    public async Task CountAsync(string name, string? source = null)
    {
        var day = Today;
        var src = CleanSource(source);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO DailyCounts (Day, Name, Source, Count) VALUES ({day}, {name}, {src}, 1)
            ON CONFLICT (Day, Name, Source) DO UPDATE SET Count = Count + 1
            """);
    }

    /// <summary>A source is a short word like "linkedin" or a website's host name. Anything else becomes "andet".</summary>
    public static string CleanSource(string? source)
    {
        var s = (source ?? "").Trim().ToLowerInvariant();
        if (s.Length == 0) return "";
        return SourcePattern().IsMatch(s) ? s : "andet";
    }

    [GeneratedRegex("^[a-z0-9.-]{1,40}$")]
    private static partial Regex SourcePattern();

    /// <summary>The people who run Systematikken: Operator:Emails, separated by commas.</summary>
    public bool IsOperator(string? email) =>
        !string.IsNullOrEmpty(email) &&
        (config["Operator:Emails"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(email, StringComparer.OrdinalIgnoreCase);
}
