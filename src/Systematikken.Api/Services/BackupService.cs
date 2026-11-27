using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Systematikken.Api.Data;

namespace Systematikken.Api.Services;

/// <summary>
/// Every night, a consistent copy of the database into {DataDirectory}/backups, keeping the last 14.
/// The server copies that folder off the machine (see deploy/backup-offsite.sh). Uses SQLite's own
/// backup API, so it is safe while the app is running.
/// </summary>
public sealed class BackupService(IServiceScopeFactory scopes, IConfiguration config, IWebHostEnvironment env, TimeProvider clock, ILogger<BackupService> log) : BackgroundService
{
    private const int Keep = 14;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (env.IsEnvironment("Testing")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24), clock);
        do
        {
            try { RunOnce(); }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Backup failed"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public string RunOnce()
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var dataDir = config["DataDirectory"] ?? Path.Combine(env.ContentRootPath, "App_Data");
        var dir = Directory.CreateDirectory(Path.Combine(dataDir, "backups"));
        var file = Path.Combine(dir.FullName, $"systematikken-{clock.GetUtcNow():yyyy-MM-dd-HHmm}.db");

        using (var source = new SqliteConnection(db.Database.GetConnectionString()))
        using (var target = new SqliteConnection($"Data Source={file};Pooling=False"))
        {
            source.Open();
            target.Open();
            source.BackupDatabase(target);
        }

        foreach (var old in dir.GetFiles("systematikken-*.db").OrderByDescending(f => f.Name).Skip(Keep))
            old.Delete();

        log.LogInformation("Backup written to {File}", file);
        return file;
    }
}
