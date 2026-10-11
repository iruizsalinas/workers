using Workers;

namespace ScheduledTask;

public static class Worker
{
    [Scheduled]
    public static Task ScheduledAsync(
        ScheduledEvent scheduled,
        Env environment,
        Context context)
    {
        Log.Info($"Ran {scheduled.Cron} at {scheduled.ScheduledTime.ToIsoString()}");
        return Task.CompletedTask;
    }
}
