namespace Workers;

public sealed class AbortController
{
    public AbortController() => WorkerApi.NotExecutable();
    public AbortSignal Signal => WorkerApi.NotExecutable<AbortSignal>();
    public void Abort(object? reason = null) => WorkerApi.NotExecutable();
}

public sealed class AbortSignal
{
    private AbortSignal() { }
    public bool Aborted => WorkerApi.NotExecutable<bool>();
    public object? Reason => WorkerApi.NotExecutable<object?>();
    public void ThrowIfAborted() => WorkerApi.NotExecutable();
    public static AbortSignal Abort(object? reason = null) => WorkerApi.NotExecutable<AbortSignal>();
    public static AbortSignal Timeout(double milliseconds) => WorkerApi.NotExecutable<AbortSignal>();
    public static AbortSignal Any(AbortSignal[] signals) => WorkerApi.NotExecutable<AbortSignal>();
}
