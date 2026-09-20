namespace Workers;

public interface IDurableObjectNamespace : IBinding
{
    DurableObjectId IdFromName(string name);
    DurableObjectId IdFromString(string id);
    DurableObjectId NewUniqueId(DurableObjectIdOptions? options = null);
    IDurableObjectNamespace Jurisdiction(string jurisdiction);
    IDurableObjectStub Get(DurableObjectId id, DurableObjectGetOptions? options = null);
    IDurableObjectStub GetByName(string name, DurableObjectGetOptions? options = null);
}

public interface IDurableObjectStub : IFetcherBinding
{
    DurableObjectId Id { get; }
    string? Name { get; }

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
    public string? Name => WorkerApi.NotExecutable<string?>();
    public string? Jurisdiction => WorkerApi.NotExecutable<string?>();

    public bool Equals(DurableObjectId other) => WorkerApi.NotExecutable<bool>();
    public override string ToString() => WorkerApi.NotExecutable<string>();
}
