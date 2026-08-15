namespace Workers;

public sealed class ScheduledEvent
{
    public string Cron => WorkerApi.NotExecutable<string>();
    public DateTimeOffset ScheduledTime => WorkerApi.NotExecutable<DateTimeOffset>();
    public string Type => WorkerApi.NotExecutable<string>();
    public long Schedule => WorkerApi.NotExecutable<long>();
}
