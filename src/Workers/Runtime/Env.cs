using System.Text.Json;

namespace Workers;

/// <summary>Represents bindings supplied to a Worker deployment.</summary>
public sealed class Env
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, object?> _bindings;
    private readonly IBindingDispatcher _bindingDispatcher;
    private readonly string? _invocationId;

    /// <summary>Creates an environment from the provided bindings.</summary>
    public Env(IEnumerable<KeyValuePair<string, object?>>? bindings = null)
        : this(bindings, invocationId: null, bindingDispatcher: null)
    {
    }

    internal Env(
        IEnumerable<KeyValuePair<string, object?>>? bindings,
        string? invocationId,
        IBindingDispatcher? bindingDispatcher)
    {
        _bindings = bindings?.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        _invocationId = invocationId;
        _bindingDispatcher = bindingDispatcher ?? BindingDispatcher.Current;
    }

    /// <summary>Sets or replaces a binding value.</summary>
    public void Set(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _bindings[name] = value;
    }

    /// <summary>Gets a binding by name and type.</summary>
    public T Get<T>(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!_bindings.TryGetValue(name, out var value))
            throw new WorkersException($"Binding '{name}' is not defined.");

        if (value is T typed)
            return typed;

        var actual = value?.GetType().FullName ?? "null";
        throw new WorkersException($"Binding '{name}' is '{actual}', not '{typeof(T).FullName}'.");
    }

    /// <summary>Attempts to get a binding by name and type.</summary>
    public bool TryGet<T>(string name, out T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_bindings.TryGetValue(name, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>Gets a plaintext environment variable binding.</summary>
    public string Var(string name) => Get<string>(name);

    /// <summary>Gets an object environment variable binding.</summary>
    public T ObjectVar<T>(string name, JsonSerializerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        options ??= JsonOptions;

        if (!_bindings.TryGetValue(name, out var value))
            throw new WorkersException($"Binding '{name}' is not defined.");

        if (value is T typed)
            return typed;

        if (value is JsonElement element)
            return element.Deserialize<T>(options)
                ?? throw new WorkersException($"Object binding '{name}' could not be deserialized as '{typeof(T).FullName}'.");

        var serialized = JsonSerializer.Serialize(value, options);
        return JsonSerializer.Deserialize<T>(serialized, options)
            ?? throw new WorkersException($"Object binding '{name}' could not be deserialized as '{typeof(T).FullName}'.");
    }

    /// <summary>Gets a secret binding.</summary>
    public string Secret(string name) => Get<string>(name);

    /// <summary>Gets a raw live binding proxy for JSON-compatible access to bindings without a dedicated wrapper.</summary>
    public RawBinding RawBinding(string name) => new(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets a Workers KV namespace binding by name.</summary>
    public IKvNamespace Kv(string name) => new KvNamespaceBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets a Workers R2 bucket binding by name.</summary>
    public IR2Bucket Bucket(string name) => new R2BucketBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets a service binding by name.</summary>
    public IServiceBinding Service(string name) => new ServiceBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets an assets binding by name.</summary>
    public IFetcherBinding Assets(string name) => new ServiceBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets an mTLS certificate binding by name.</summary>
    public IFetcherBinding MtlsCertificate(string name) => new ServiceBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets a Dynamic Dispatch binding by name.</summary>
    public IDynamicDispatcherBinding DynamicDispatcher(string name) =>
        new DynamicDispatcherBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets a Workers Queue producer binding by name.</summary>
    public IQueueProducer Queue(string name) => new QueueProducerBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets a D1 database binding by name.</summary>
    public ID1Database D1(string name) => new D1DatabaseBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets the default Workers Cache.</summary>
    public ICache Cache() => new CacheBinding(RequireInvocationId(), bindingName: "$default", _bindingDispatcher);

    /// <summary>Gets a named Workers Cache.</summary>
    public ICache Cache(string name) => new CacheBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets a Durable Object namespace binding by name.</summary>
    public IDurableObjectNamespace DurableObject(string name) =>
        new DurableObjectNamespaceBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets a Rate Limiting binding by name.</summary>
    public IRateLimiter RateLimiter(string name) => new RateLimiterBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets an Analytics Engine dataset binding by name.</summary>
    public IAnalyticsEngineDataset AnalyticsEngine(string name) =>
        new AnalyticsEngineDatasetBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets a Send Email binding by name.</summary>
    public ISendEmailBinding SendEmail(string name) => new SendEmailBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets a Version Metadata binding by name.</summary>
    public IVersionMetadataBinding VersionMetadata(string name) =>
        new VersionMetadataBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets a Workers AI binding by name.</summary>
    public IAiBinding Ai(string name) => new AiBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets a Workflows binding by name.</summary>
    public IWorkflowBinding Workflow(string name) => new WorkflowBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets a Cloudflare Images binding by name.</summary>
    public IImagesBinding Images(string name) => new ImagesBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets a Media Transformations binding by name.</summary>
    public IMediaBinding Media(string name) => new MediaBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets a Vectorize index binding by name.</summary>
    public IVectorizeIndex Vectorize(string name) => new VectorizeIndexBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets a Secret Store binding by name.</summary>
    public ISecretStoreBinding SecretStore(string name) => new SecretStoreBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets a Hyperdrive binding by name.</summary>
    public IHyperdriveBinding Hyperdrive(string name) => new HyperdriveBinding(RequireInvocationId(), name, _bindingDispatcher);

    /// <summary>Gets helpers for Workers platform cryptography APIs.</summary>
    public Crypto Crypto() => new(RequireInvocationId(), _bindingDispatcher);

    /// <summary>Gets helpers for writing to the Workers console.</summary>
    public Log Log() => new(RequireInvocationId(), _bindingDispatcher);

    /// <summary>Creates a native Workers HTMLRewriter for transforming HTML responses.</summary>
    public HtmlRewriter HtmlRewriter() => new(RequireInvocationId(), _bindingDispatcher);

    /// <summary>Connects to an outbound TCP socket using the Workers runtime.</summary>
    public Task<Socket> ConnectSocketAsync(
        string address,
        SocketOptions? options = null,
        CancellationToken cancellationToken = default) =>
        SocketFactory.ConnectAsync(RequireInvocationId(), address, options, _bindingDispatcher, cancellationToken);

    /// <summary>Connects to an outbound TCP socket using the Workers runtime.</summary>
    public Task<Socket> ConnectSocketAsync(
        SocketAddress address,
        SocketOptions? options = null,
        CancellationToken cancellationToken = default) =>
        SocketFactory.ConnectAsync(RequireInvocationId(), address, options, _bindingDispatcher, cancellationToken);

    /// <summary>Waits using the Workers event loop timer APIs.</summary>
    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        var milliseconds = ToDelayMilliseconds(duration);
        var invocation = new BindingInvocation(
            RequireInvocationId(),
            "$runtime",
            "runtime.delay",
            JsonSerializer.Serialize(new { milliseconds }, JsonOptions));

        return _bindingDispatcher.DispatchAsync(invocation, cancellationToken);
    }

    /// <summary>Creates an abort controller for cancellable platform operations.</summary>
    public async Task<AbortController> CreateAbortControllerAsync(CancellationToken cancellationToken = default)
    {
        var invocationId = RequireInvocationId();
        var invocation = new BindingInvocation(invocationId, "$abort", "abort.create", "{}");
        var result = await _bindingDispatcher.DispatchAsync(invocation, cancellationToken);
        var envelope = JsonSerializer.Deserialize<AbortControllerEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("Abort controller creation returned an empty result.");

        return new AbortController(invocationId, envelope.Handle, _bindingDispatcher);
    }

    /// <summary>Sends a request through the Workers global fetch API.</summary>
    public Task<Response> FetchAsync(Request request, CancellationToken cancellationToken = default) =>
        new FetchBinding(RequireInvocationId(), _bindingDispatcher).FetchAsync(request, cancellationToken);

    /// <summary>Sends a request through the Workers global fetch API.</summary>
    public Task<Response> FetchAsync(
        Request request,
        FetchOptions? options,
        CancellationToken cancellationToken = default) =>
        new FetchBinding(RequireInvocationId(), _bindingDispatcher).FetchAsync(request, options, cancellationToken);

    /// <summary>Sends a GET request through the Workers global fetch API.</summary>
    public Task<Response> FetchAsync(string url, CancellationToken cancellationToken = default) =>
        new FetchBinding(RequireInvocationId(), _bindingDispatcher).FetchAsync(url, cancellationToken);

    /// <summary>Sends a GET request through the Workers global fetch API.</summary>
    public Task<Response> FetchAsync(
        string url,
        FetchOptions? options,
        CancellationToken cancellationToken = default) =>
        new FetchBinding(RequireInvocationId(), _bindingDispatcher).FetchAsync(url, options, cancellationToken);

    /// <summary>Creates a WebSocket pair using the Workers runtime.</summary>
    public Task<WebSocketPair> WebSocketPairAsync(CancellationToken cancellationToken = default) =>
        WebSocketFactory.CreatePairAsync(RequireInvocationId(), _bindingDispatcher, cancellationToken);

    /// <summary>Connects to an upstream WebSocket using the Workers runtime.</summary>
    public Task<WebSocket> ConnectWebSocketAsync(string url, CancellationToken cancellationToken = default) =>
        WebSocketFactory.ConnectAsync(RequireInvocationId(), url, protocols: null, _bindingDispatcher, cancellationToken);

    /// <summary>Connects to an upstream WebSocket using the Workers runtime and requested subprotocols.</summary>
    public Task<WebSocket> ConnectWebSocketAsync(
        string url,
        IEnumerable<string> protocols,
        CancellationToken cancellationToken = default) =>
        WebSocketFactory.ConnectAsync(RequireInvocationId(), url, protocols, _bindingDispatcher, cancellationToken);

    private string RequireInvocationId() =>
        _invocationId ?? throw new WorkersException("Platform binding proxies require a live Worker invocation.");

    private static int ToDelayMilliseconds(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Delay duration cannot be negative.");

        if (duration.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Delay duration is too large for Workers timers.");

        return (int)duration.TotalMilliseconds;
    }

    private sealed record AbortControllerEnvelope(string Handle);
}
