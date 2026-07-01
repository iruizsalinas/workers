using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Workers;

/// <summary>Context passed to a matched route handler.</summary>
public sealed class RouteContext
{
    private readonly object? _data;

    /// <summary>Creates a route context.</summary>
    public RouteContext(
        Env environment,
        Context executionContext,
        RouteParameters parameters,
        IReadOnlyList<string>? allowedMethods = null,
        object? data = null)
    {
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        ExecutionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        AllowedMethods = allowedMethods is null
            ? Array.Empty<string>()
            : Array.AsReadOnly(allowedMethods.ToArray());
        _data = data;
    }

    /// <summary>The Worker environment.</summary>
    public Env Environment { get; }

    /// <summary>The Worker execution context.</summary>
    public Context ExecutionContext { get; }

    /// <summary>The route parameters captured from the path.</summary>
    public RouteParameters Parameters { get; }

    /// <summary>The methods allowed for a matched path when handling a method-not-allowed response.</summary>
    public IReadOnlyList<string> AllowedMethods { get; }

    /// <summary>Gets a route parameter by name.</summary>
    public string? Param(string name) => Parameters.Get(name);

    /// <summary>Tries to get a route parameter by name.</summary>
    public bool TryParam(string name, [NotNullWhen(true)] out string? value) =>
        Parameters.TryGet(name, out value);

    /// <summary>Gets a route parameter by name, throwing when the parameter is missing.</summary>
    public string RequiredParam(string name) => Parameters.GetRequired(name);

    /// <summary>Deserializes route parameters into a typed object.</summary>
    public T Params<T>(JsonSerializerOptions? options = null) => Parameters.As<T>(options);

    /// <summary>Gets an environment binding by name and type.</summary>
    public T Binding<T>(string name) => Environment.Get<T>(name);

    /// <summary>Gets a plaintext environment variable binding.</summary>
    public string Var(string name) => Environment.Var(name);

    /// <summary>Gets an object environment variable binding.</summary>
    public T ObjectVar<T>(string name, JsonSerializerOptions? options = null) =>
        Environment.ObjectVar<T>(name, options);

    /// <summary>Gets a secret binding.</summary>
    public string Secret(string name) => Environment.Secret(name);

    /// <summary>Gets a raw live binding proxy for JSON-compatible access to bindings without a dedicated wrapper.</summary>
    public RawBinding RawBinding(string name) => Environment.RawBinding(name);

    /// <summary>Gets a Workers KV namespace binding by name.</summary>
    public IKvNamespace Kv(string name) => Environment.Kv(name);

    /// <summary>Gets a Workers R2 bucket binding by name.</summary>
    public IR2Bucket Bucket(string name) => Environment.Bucket(name);

    /// <summary>Gets a service binding by name.</summary>
    public IServiceBinding Service(string name) => Environment.Service(name);

    /// <summary>Gets an assets binding by name.</summary>
    public IFetcherBinding Assets(string name) => Environment.Assets(name);

    /// <summary>Gets an mTLS certificate binding by name.</summary>
    public IFetcherBinding MtlsCertificate(string name) => Environment.MtlsCertificate(name);

    /// <summary>Gets a Dynamic Dispatch binding by name.</summary>
    public IDynamicDispatcherBinding DynamicDispatcher(string name) => Environment.DynamicDispatcher(name);

    /// <summary>Gets a Workers Queue producer binding by name.</summary>
    public IQueueProducer Queue(string name) => Environment.Queue(name);

    /// <summary>Gets a D1 database binding by name.</summary>
    public ID1Database D1(string name) => Environment.D1(name);

    /// <summary>Gets the default Workers Cache.</summary>
    public ICache Cache() => Environment.Cache();

    /// <summary>Gets a named Workers Cache.</summary>
    public ICache Cache(string name) => Environment.Cache(name);

    /// <summary>Gets a Durable Object namespace binding by name.</summary>
    public IDurableObjectNamespace DurableObject(string name) => Environment.DurableObject(name);

    /// <summary>Gets a Rate Limiting binding by name.</summary>
    public IRateLimiter RateLimiter(string name) => Environment.RateLimiter(name);

    /// <summary>Gets an Analytics Engine dataset binding by name.</summary>
    public IAnalyticsEngineDataset AnalyticsEngine(string name) => Environment.AnalyticsEngine(name);

    /// <summary>Gets a Send Email binding by name.</summary>
    public ISendEmailBinding SendEmail(string name) => Environment.SendEmail(name);

    /// <summary>Gets a Version Metadata binding by name.</summary>
    public IVersionMetadataBinding VersionMetadata(string name) => Environment.VersionMetadata(name);

    /// <summary>Gets a Workers AI binding by name.</summary>
    public IAiBinding Ai(string name) => Environment.Ai(name);

    /// <summary>Gets a Workflows binding by name.</summary>
    public IWorkflowBinding Workflow(string name) => Environment.Workflow(name);

    /// <summary>Gets a Cloudflare Images binding by name.</summary>
    public IImagesBinding Images(string name) => Environment.Images(name);

    /// <summary>Gets a Media Transformations binding by name.</summary>
    public IMediaBinding Media(string name) => Environment.Media(name);

    /// <summary>Gets a Vectorize index binding by name.</summary>
    public IVectorizeIndex Vectorize(string name) => Environment.Vectorize(name);

    /// <summary>Gets a Secret Store binding by name.</summary>
    public ISecretStoreBinding SecretStore(string name) => Environment.SecretStore(name);

    /// <summary>Gets a Hyperdrive binding by name.</summary>
    public IHyperdriveBinding Hyperdrive(string name) => Environment.Hyperdrive(name);

    /// <summary>Sends a request through the Workers global fetch API.</summary>
    public Task<Response> FetchAsync(Request request, CancellationToken cancellationToken = default) =>
        Environment.FetchAsync(request, cancellationToken);

    /// <summary>Sends a request through the Workers global fetch API.</summary>
    public Task<Response> FetchAsync(
        Request request,
        FetchOptions? options,
        CancellationToken cancellationToken = default) =>
        Environment.FetchAsync(request, options, cancellationToken);

    /// <summary>Sends a GET request through the Workers global fetch API.</summary>
    public Task<Response> FetchAsync(string url, CancellationToken cancellationToken = default) =>
        Environment.FetchAsync(url, cancellationToken);

    /// <summary>Sends a GET request through the Workers global fetch API.</summary>
    public Task<Response> FetchAsync(
        string url,
        FetchOptions? options,
        CancellationToken cancellationToken = default) =>
        Environment.FetchAsync(url, options, cancellationToken);

    /// <summary>Waits using the Workers event loop timer APIs.</summary>
    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default) =>
        Environment.DelayAsync(duration, cancellationToken);

    /// <summary>Gets helpers for Workers platform cryptography APIs.</summary>
    public Crypto Crypto() => Environment.Crypto();

    /// <summary>Gets helpers for writing to the Workers console.</summary>
    public Log Log() => Environment.Log();

    /// <summary>Creates a native Workers HTMLRewriter for transforming HTML responses.</summary>
    public HtmlRewriter HtmlRewriter() => Environment.HtmlRewriter();

    /// <summary>Connects to an outbound TCP socket using the Workers runtime.</summary>
    public Task<Socket> ConnectSocketAsync(
        string address,
        SocketOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Environment.ConnectSocketAsync(address, options, cancellationToken);

    /// <summary>Connects to an outbound TCP socket using the Workers runtime.</summary>
    public Task<Socket> ConnectSocketAsync(
        SocketAddress address,
        SocketOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Environment.ConnectSocketAsync(address, options, cancellationToken);

    /// <summary>Creates an abort controller for cancellable platform operations.</summary>
    public Task<AbortController> CreateAbortControllerAsync(CancellationToken cancellationToken = default) =>
        Environment.CreateAbortControllerAsync(cancellationToken);

    /// <summary>Creates a WebSocket pair using the Workers runtime.</summary>
    public Task<WebSocketPair> WebSocketPairAsync(CancellationToken cancellationToken = default) =>
        Environment.WebSocketPairAsync(cancellationToken);

    /// <summary>Connects to an upstream WebSocket using the Workers runtime.</summary>
    public Task<WebSocket> ConnectWebSocketAsync(string url, CancellationToken cancellationToken = default) =>
        Environment.ConnectWebSocketAsync(url, cancellationToken);

    /// <summary>Connects to an upstream WebSocket using the Workers runtime and requested subprotocols.</summary>
    public Task<WebSocket> ConnectWebSocketAsync(
        string url,
        IEnumerable<string> protocols,
        CancellationToken cancellationToken = default) =>
        Environment.ConnectWebSocketAsync(url, protocols, cancellationToken);

    /// <summary>Schedules work using the Workers waitUntil model.</summary>
    public void WaitUntil(Task task) => ExecutionContext.WaitUntil(task);

    /// <summary>Requests Workers fail-open pass-through behavior if the route handler throws an unhandled exception.</summary>
    public void PassThroughOnException() => ExecutionContext.PassThroughOnException();

    /// <summary>Deserializes props supplied to the Worker execution context.</summary>
    public T Props<T>(JsonSerializerOptions? options = null) => ExecutionContext.Props<T>(options);

    /// <summary>Gets the application data attached to the router.</summary>
    public T Data<T>()
        where T : notnull
    {
        if (_data is null)
            throw new WorkersException("Route context does not contain application data.");

        if (_data is T data)
            return data;

        throw new WorkersException(
            $"Route context application data is '{_data.GetType().FullName}', not '{typeof(T).FullName}'.");
    }

    /// <summary>Tries to get the application data attached to the router.</summary>
    public bool TryGetData<T>(out T? data)
        where T : notnull
    {
        if (_data is T value)
        {
            data = value;
            return true;
        }

        data = default;
        return false;
    }
}

/// <summary>Route parameters captured by the router.</summary>
public sealed class RouteParameters
{
    private readonly IReadOnlyDictionary<string, string> _values;

    /// <summary>Creates route parameters from a dictionary.</summary>
    public RouteParameters(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = Copy(values);
    }

    /// <summary>Gets a parameter value by name.</summary>
    public string? Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _values.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>Tries to get a parameter value by name.</summary>
    public bool TryGet(string name, [NotNullWhen(true)] out string? value)
    {
        value = Get(name);
        return value is not null;
    }

    /// <summary>Gets a parameter value by name, throwing when the parameter is missing.</summary>
    public string GetRequired(string name) =>
        Get(name) ?? throw new WorkersException($"Route parameter '{name}' is not defined.");

    /// <summary>Deserializes route parameters into a typed object.</summary>
    public T As<T>(JsonSerializerOptions? options = null)
    {
        var entries = _values
            .Select(static pair => new QueryParameter(pair.Key, pair.Value))
            .ToArray();
        return QueryObject.Deserialize<T>(entries, options, "Route parameters");
    }

    /// <summary>Returns the parameters as a dictionary.</summary>
    public IReadOnlyDictionary<string, string> AsDictionary() => _values;

    private static ReadOnlyDictionary<string, string> Copy(IReadOnlyDictionary<string, string> values)
    {
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
            copy.Add(key, value);

        return new ReadOnlyDictionary<string, string>(copy);
    }
}
