namespace Workers;

public static class Timers
{
    public static Task WaitAsync(double milliseconds, AbortSignal? signal = null) => WorkerApi.NotExecutable<Task>();
    public static TimerHandle SetTimeout(Action callback, double milliseconds) => WorkerApi.NotExecutable<TimerHandle>();
    public static void ClearTimeout(TimerHandle handle) => WorkerApi.NotExecutable();
}

public sealed class TimerHandle;
