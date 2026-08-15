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
        Console.WriteLine($"Ran {scheduled.Cron} at {scheduled.ScheduledTime:O}");
        return Task.CompletedTask;
    }
}
