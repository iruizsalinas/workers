namespace Workers;

public static class Timers
{
    public static TimerHandle SetTimeout(Action callback, TimeSpan delay) => WorkerApi.NotExecutable<TimerHandle>();
    public static void ClearTimeout(TimerHandle handle) => WorkerApi.NotExecutable();
}

public sealed class TimerHandle;
