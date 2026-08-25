namespace Workers;

public interface IDurableObjectNamespace : IBinding
{
    Task<DurableObjectId> IdFromNameAsync(string name, CancellationToken cancellationToken = default);
    Task<DurableObjectId> IdFromStringAsync(string id, CancellationToken cancellationToken = default);
    Task<DurableObjectId> NewUniqueIdAsync(DurableObjectIdOptions? options = null, CancellationToken cancellationToken = default);
    IDurableObjectStub Get(DurableObjectId id, DurableObjectGetOptions? options = null);
    IDurableObjectStub GetByName(string name, DurableObjectGetOptions? options = null);
}

public interface IDurableObjectStub : IFetcherBinding
{
    Task<TResult?> InvokeAsync<TResult>(string methodName, IEnumerable<object?>? arguments = null, CancellationToken cancellationToken = default);
    Task InvokeVoidAsync(string methodName, IEnumerable<object?>? arguments = null, CancellationToken cancellationToken = default);
}

public sealed record DurableObjectIdOptions
{
    public string? Jurisdiction { get; init; }
}

public sealed record DurableObjectGetOptions
{
    public string? LocationHint { get; init; }
}

public sealed class DurableObjectId
{
    public string Value => WorkerApi.NotExecutable<string>();
    public string? Name => WorkerApi.NotExecutable<string?>();

    public override string ToString() => WorkerApi.NotExecutable<string>();
}
