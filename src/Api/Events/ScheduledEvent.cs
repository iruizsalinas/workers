namespace Workers;

public sealed class ScheduledEvent
{
    public string Cron => WorkerApi.NotExecutable<string>();
    public Date ScheduledTime => WorkerApi.NotExecutable<Date>();
    public void NoRetry() => WorkerApi.NotExecutable();
}
