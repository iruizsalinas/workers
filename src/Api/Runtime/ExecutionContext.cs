namespace Workers;

public sealed class ExecutionContext
{
    private ExecutionContext() { }

    public void WaitUntil(Task promise) => WorkerApi.NotExecutable();
    public void PassThroughOnException() => WorkerApi.NotExecutable();
    /// <summary>Properties supplied by the caller of this entrypoint, such as a service binding's <c>props</c>.</summary>
    public T Props<T>() => WorkerApi.NotExecutable<T>();
}
