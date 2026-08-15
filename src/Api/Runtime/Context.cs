namespace Workers;

public sealed class Context
{
    public void WaitUntil(Task task) => WorkerApi.NotExecutable();
    public void PassThroughOnException() => WorkerApi.NotExecutable();
    public T Props<T>() => WorkerApi.NotExecutable<T>();
}
