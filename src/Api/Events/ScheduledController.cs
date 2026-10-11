namespace Workers;

public sealed class ScheduledController
{
    private ScheduledController() { }

    public string Cron => WorkerApi.NotExecutable<string>();
    public Date ScheduledTime => WorkerApi.NotExecutable<Date>();
    public void NoRetry() => WorkerApi.NotExecutable();
}
