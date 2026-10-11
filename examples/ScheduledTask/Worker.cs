using Workers;

namespace ScheduledTask;

public static class Worker
{
    [Scheduled]
    public static Task ScheduledAsync(ScheduledController scheduled)
    {
        Log.Info($"Ran {scheduled.Cron} at {scheduled.ScheduledTime.ToIsoString()}");
        return Task.CompletedTask;
    }
}
