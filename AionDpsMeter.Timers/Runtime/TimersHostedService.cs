using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Schedule;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AionDpsMeter.Timers.Runtime;

public static class TimersPaths
{
    public static string Of(string fileName) => Path.Combine(AppContext.BaseDirectory, fileName);
}

/// <summary>Loads saved state and the schedule, then every second watches for kills, saves when something changed
/// (at most every 5 s) and refreshes schedule.json from GitHub every 6 hours.</summary>
public sealed class TimersHostedService(
    ScheduleSource schedule,
    FieldBossTracker tracker,
    KillWatcher kills,
    TimerStateStore store,
    TimeProvider time,
    ILogger<TimersHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan SaveEvery = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromHours(6);

    private volatile bool dirty;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            schedule.LoadLocal();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Built-in schedule could not be read");
        }
        try
        {
            tracker.Import(store.Load());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Saved boss timers could not be loaded; starting empty");
        }
        tracker.Changed += () => dirty = true;
        dirty = false;
        _ = RefreshAsync(ct);

        var lastSave = time.GetUtcNow();
        var lastRefresh = lastSave;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), time);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    kills.Tick();
                    var now = time.GetUtcNow();
                    if (dirty && now - lastSave >= SaveEvery)
                    {
                        dirty = false;
                        lastSave = now;
                        store.Save(tracker.Export());
                    }
                    if (now - lastRefresh >= RefreshEvery)
                    {
                        lastRefresh = now;
                        _ = RefreshAsync(ct);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Timers tick failed; continuing"); // one bad tick must not stop the timers
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            store.Save(tracker.Export());
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        var ok = false;
        try
        {
            ok = await schedule.RefreshAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Schedule refresh threw");
        }
        logger.LogInformation("Schedule refresh {Result}; using {Origin}", ok ? "succeeded" : "failed", schedule.Origin);
    }
}
