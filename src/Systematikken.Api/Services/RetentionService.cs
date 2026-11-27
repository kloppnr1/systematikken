using Microsoft.EntityFrameworkCore;
using Systematikken.Api.Data;

namespace Systematikken.Api.Services;

/// <summary>
/// Once a day, deletes waiting-list entries that were declined or removed longer ago than the institution keeps them,
/// and invitations that expired. Enrolled applicants are deleted too: their data now lives on the child.
/// </summary>
public sealed class RetentionService(IServiceScopeFactory scopes, TimeProvider clock, ILogger<RetentionService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24), clock);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Retention run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var now = clock.GetUtcNow().UtcDateTime;

        var institutions = await db.Institutions.Select(i => new { i.Id, i.WaitingListRetentionMonths }).ToListAsync(ct);
        foreach (var i in institutions)
        {
            var cutoff = now.AddMonths(-i.WaitingListRetentionMonths);
            var deleted = await db.Applicants.IgnoreQueryFilters()
                .Where(a => a.InstitutionId == i.Id && a.ClosedAt != null && a.ClosedAt < cutoff)
                .ExecuteDeleteAsync(ct);
            if (deleted > 0) log.LogInformation("Deleted {Count} closed waiting-list entries", deleted);
        }

        await db.Invitations.IgnoreQueryFilters().Where(x => x.AcceptedAt == null && x.ExpiresAt < now).ExecuteDeleteAsync(ct);
    }
}
