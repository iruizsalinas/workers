using Workers;

namespace ScheduledTask;

public static class Worker
{
    [ScheduledEvent]
    public static Task ScheduledAsync(
        ScheduledEvent scheduled,
        Env environment,
        Context context)
    {
        return environment.Log().LogAsync($"Ran {scheduled.Cron} at {scheduled.ScheduledTime:O}");
    }
}
