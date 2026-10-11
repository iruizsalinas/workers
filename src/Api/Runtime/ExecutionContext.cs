namespace Workers;

public sealed class ExecutionContext
{
    private ExecutionContext() { }

    /// <summary>Loopback bindings to this Worker's own exports. Requires a compatibility date of 2025-11-17 or later,
    /// or the <c>enable_ctx_exports</c> flag.</summary>
    public Exports Exports => WorkerApi.NotExecutable<Exports>();
    /// <summary>Purges this zone's cache; null when the Worker does not run on a cache-enabled route.</summary>
    public CacheContext? Cache => WorkerApi.NotExecutable<CacheContext?>();
    /// <summary>The Cloudflare Access context; null unless an Access application protects the request.</summary>
    public CloudflareAccessContext? Access => WorkerApi.NotExecutable<CloudflareAccessContext?>();
    /// <summary>Custom spans, recorded when tracing is enabled for the Worker.</summary>
    public Tracing Tracing => WorkerApi.NotExecutable<Tracing>();

    public void WaitUntil(Task promise) => WorkerApi.NotExecutable();
    public void PassThroughOnException() => WorkerApi.NotExecutable();
    /// <summary>Properties supplied by the caller of this entrypoint, such as a service binding's <c>props</c>.</summary>
    public T Props<T>() => WorkerApi.NotExecutable<T>();
    /// <summary>Ends the invocation immediately, failing it with the reason; code after the call does not run.</summary>
    public void Abort(object? reason = null) => WorkerApi.NotExecutable();
}

/// <summary>
/// Loopback bindings to the module's exports, read by export name, such as <c>nameof(MyEntrypoint)</c>.
/// Calls through these bindings stay inside this Worker.
/// </summary>
public sealed class Exports
{
    private Exports() { }

    /// <summary>A service binding to an exported <see cref="WorkerEntrypoint"/>, or <c>default</c> for the fetch handler.</summary>
    public Fetcher Service(string name) => WorkerApi.NotExecutable<Fetcher>();
    /// <summary>A service binding whose entrypoint receives these props as <see cref="ExecutionContext.Props{T}"/>.
    /// Props must be structured-cloneable.</summary>
    public Fetcher Service(string name, object props) => WorkerApi.NotExecutable<Fetcher>();
    /// <summary>The namespace of an exported <see cref="DurableObject"/> class that has storage.</summary>
    public DurableObjectNamespace DurableObject(string name) => WorkerApi.NotExecutable<DurableObjectNamespace>();
    /// <summary>The binding of a Workflow class listed in the Wrangler <c>exports</c> field.</summary>
    public Workflow Workflow(string name) => WorkerApi.NotExecutable<Workflow>();
}

/// <summary>Cloudflare Access details for a request that an Access application authenticated.</summary>
public sealed class CloudflareAccessContext
{
    private CloudflareAccessContext() { }

    /// <summary>The audience tag of the Access application, from the validated Access JWT.</summary>
    public string Aud => WorkerApi.NotExecutable<string>();
    /// <summary>The authenticated user's identity, or null for service-token authentication.</summary>
    public Task<CloudflareAccessIdentity?> GetIdentityAsync() => WorkerApi.NotExecutable<Task<CloudflareAccessIdentity?>>();
}

/// <summary>The identity Cloudflare Access reports for a user. Fields the identity provider does not supply are null.</summary>
public sealed class CloudflareAccessIdentity
{
    private CloudflareAccessIdentity() { }

    public string? Email => WorkerApi.NotExecutable<string?>();
    public string? Name => WorkerApi.NotExecutable<string?>();
    public string? UserUuid => WorkerApi.NotExecutable<string?>();
    public string? AccountId => WorkerApi.NotExecutable<string?>();
    /// <summary>The login time, in seconds since the Unix epoch.</summary>
    public double? Iat => WorkerApi.NotExecutable<double?>();
    /// <summary>The user's IP address when they authenticated.</summary>
    public string? Ip => WorkerApi.NotExecutable<string?>();
    /// <summary>The authentication methods used, such as <c>pwd</c>.</summary>
    public IReadOnlyList<string>? Amr => WorkerApi.NotExecutable<IReadOnlyList<string>?>();
    public CloudflareAccessIdentityProvider? Idp => WorkerApi.NotExecutable<CloudflareAccessIdentityProvider?>();
    public CloudflareAccessGeo? Geo => WorkerApi.NotExecutable<CloudflareAccessGeo?>();
    public IReadOnlyList<CloudflareAccessGroup>? Groups => WorkerApi.NotExecutable<IReadOnlyList<CloudflareAccessGroup>?>();
    /// <summary>Device posture check results, keyed by check ID.</summary>
    public JsonValue? DevicePosture => WorkerApi.NotExecutable<JsonValue?>();
    public bool? IsWarp => WorkerApi.NotExecutable<bool?>();
    public bool? IsGateway => WorkerApi.NotExecutable<bool?>();
    /// <summary>Reads an identity field by its JSON name, including provider-specific claims.</summary>
    public JsonValue this[string name] => WorkerApi.NotExecutable<JsonValue>();
}

public sealed class CloudflareAccessIdentityProvider
{
    private CloudflareAccessIdentityProvider() { }

    public string Id => WorkerApi.NotExecutable<string>();
    public string Type => WorkerApi.NotExecutable<string>();
}

public sealed class CloudflareAccessGeo
{
    private CloudflareAccessGeo() { }

    public string Country => WorkerApi.NotExecutable<string>();
}

public sealed class CloudflareAccessGroup
{
    private CloudflareAccessGroup() { }

    public string Id => WorkerApi.NotExecutable<string>();
    public string Name => WorkerApi.NotExecutable<string>();
    public string? Email => WorkerApi.NotExecutable<string?>();
}
