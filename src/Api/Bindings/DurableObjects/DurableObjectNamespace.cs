namespace Workers;

public sealed class DurableObjectNamespace
{
    private DurableObjectNamespace() { }

    public DurableObjectId NewUniqueId(DurableObjectNamespaceNewUniqueIdOptions? options = null) => WorkerApi.NotExecutable<DurableObjectId>();
    public DurableObjectId IdFromName(string name) => WorkerApi.NotExecutable<DurableObjectId>();
    public DurableObjectId IdFromString(string id) => WorkerApi.NotExecutable<DurableObjectId>();
    public DurableObjectStub Get(DurableObjectId id, DurableObjectNamespaceGetDurableObjectOptions? options = null) =>
        WorkerApi.NotExecutable<DurableObjectStub>();
    public DurableObjectStub GetByName(string name, DurableObjectNamespaceGetDurableObjectOptions? options = null) =>
        WorkerApi.NotExecutable<DurableObjectStub>();
    /// <summary>A namespace whose new objects are created in a jurisdiction: <c>eu</c>, <c>us</c>, <c>fedramp</c> or <c>fedramp-high</c>.</summary>
    public DurableObjectNamespace Jurisdiction(string jurisdiction) => WorkerApi.NotExecutable<DurableObjectNamespace>();
}

public sealed class DurableObjectStub : Fetcher
{
    private DurableObjectStub() { }

    public DurableObjectId Id => WorkerApi.NotExecutable<DurableObjectId>();
    public string? Name => WorkerApi.NotExecutable<string?>();
}

public sealed class DurableObjectId
{
    private DurableObjectId() { }

    public string? Name => WorkerApi.NotExecutable<string?>();
    public string? Jurisdiction => WorkerApi.NotExecutable<string?>();

    public bool Equals(DurableObjectId other) => WorkerApi.NotExecutable<bool>();
    public override string ToString() => WorkerApi.NotExecutable<string>();
}

public sealed class DurableObjectNamespaceNewUniqueIdOptions
{
    public string? Jurisdiction { get; init; }
}

public sealed class DurableObjectNamespaceGetDurableObjectOptions
{
    /// <summary>Where to create the object if it does not exist yet, such as <c>weur</c>, <c>enam</c> or <c>apac-ne</c>.</summary>
    public string? LocationHint { get; init; }
    public DurableObjectRoutingMode? RoutingMode { get; init; }
}

public enum DurableObjectRoutingMode
{
    /// <summary>Routes requests to the object's primary instance, never to a read replica.</summary>
    PrimaryOnly
}
