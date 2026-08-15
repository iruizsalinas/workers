namespace Workers;

public sealed class ForwardableEmailMessage
{
    public string From => WorkerApi.NotExecutable<string>();
    public string To => WorkerApi.NotExecutable<string>();
    public Headers Headers => WorkerApi.NotExecutable<Headers>();

    public void Reject(string reason) => WorkerApi.NotExecutable();
    public Task ForwardAsync(string recipient, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
}
