namespace Workers;

public sealed class AbortController
{
    public AbortSignal Signal => WorkerApi.NotExecutable<AbortSignal>();

    public void Abort(string? reason = null) => WorkerApi.NotExecutable();
}

public sealed class AbortSignal;
