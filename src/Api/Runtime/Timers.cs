namespace Workers;

/// <summary><c>setTimeout</c>, <c>setInterval</c> and their cancellation. Timers only advance while the Worker is doing I/O.</summary>
public static class Timers
{
    public static TimerHandle SetTimeout(Action callback, double delayMs = 0) => WorkerApi.NotExecutable<TimerHandle>();
    public static void ClearTimeout(TimerHandle? handle) => WorkerApi.NotExecutable();
    public static TimerHandle SetInterval(Action callback, double delayMs = 0) => WorkerApi.NotExecutable<TimerHandle>();
    public static void ClearInterval(TimerHandle? handle) => WorkerApi.NotExecutable();
}

/// <summary>An opaque timer, used to cancel it.</summary>
public sealed class TimerHandle
{
    private TimerHandle() { }
}

/// <summary>The <c>scheduler</c> global.</summary>
public static class Scheduler
{
    /// <summary>Waits for a duration, or rejects when the signal aborts, as <c>scheduler.wait</c>.</summary>
    public static Task WaitAsync(double delayMs, AbortSignal? signal = null) => WorkerApi.NotExecutable<Task>();
}
