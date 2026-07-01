using System.Text.Json;
using Workers.Interop;

namespace Workers;

/// <summary>Options used when creating a unique Durable Object ID.</summary>
public sealed record DurableObjectIdOptions
{
    /// <summary>Restricts the object to a supported jurisdiction.</summary>
    public string? Jurisdiction { get; init; }
}

/// <summary>Options used when resolving a Durable Object stub.</summary>
public sealed record DurableObjectGetOptions
{
    /// <summary>Provides a location hint to the Workers runtime.</summary>
    public string? LocationHint { get; init; }
}

/// <summary>A stringified Durable Object ID plus optional name metadata.</summary>
public sealed class DurableObjectId : IEquatable<DurableObjectId>
{
    /// <summary>Creates a Durable Object ID.</summary>
    public DurableObjectId(string value, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
        Name = name;
    }

    /// <summary>The stringified Durable Object ID.</summary>
    public string Value { get; }

    /// <summary>The name used to create this ID, when available.</summary>
    public string? Name { get; }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    public bool Equals(DurableObjectId? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DurableObjectId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
}

internal sealed class DurableObjectNamespaceBinding : IDurableObjectNamespace
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public DurableObjectNamespaceBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public Task<DurableObjectId> IdFromNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return DispatchIdAsync("durable.idFromName", new { name }, cancellationToken);
    }

    public Task<DurableObjectId> IdFromStringAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return DispatchIdAsync("durable.idFromString", new { id }, cancellationToken);
    }

    public Task<DurableObjectId> NewUniqueIdAsync(
        DurableObjectIdOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchIdAsync("durable.newUniqueId", new { options }, cancellationToken);
    }

    public IDurableObjectStub Get(DurableObjectId id, DurableObjectGetOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        return DurableObjectStubBinding.ForId(_invocationId, _bindingName, id, options, _dispatcher);
    }

    public IDurableObjectStub GetByName(string name, DurableObjectGetOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return DurableObjectStubBinding.ForName(_invocationId, _bindingName, name, options, _dispatcher);
    }

    private async Task<DurableObjectId> DispatchIdAsync(
        string operation,
        object payload,
        CancellationToken cancellationToken)
    {
        var result = await DispatchAsync(operation, payload, cancellationToken);
        var id = JsonSerializer.Deserialize<DurableObjectIdPayload>(result, JsonOptions)
            ?? throw new WorkersException("Durable Object namespace returned an empty ID result.");

        return new DurableObjectId(id.Id, id.Name);
    }

    private Task<string> DispatchAsync(string operation, object payload, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            operation,
            JsonSerializer.Serialize(payload, JsonOptions));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private sealed record DurableObjectIdPayload(string Id, string? Name);
}

internal sealed class DurableObjectStubBinding : IDurableObjectStub
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly DurableObjectStubTarget _target;
    private readonly DurableObjectGetOptions? _options;
    private readonly IBindingDispatcher _dispatcher;

    private DurableObjectStubBinding(
        string invocationId,
        string bindingName,
        DurableObjectStubTarget target,
        DurableObjectGetOptions? options,
        IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _target = target;
        _options = options;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public static DurableObjectStubBinding ForId(
        string invocationId,
        string bindingName,
        DurableObjectId id,
        DurableObjectGetOptions? options,
        IBindingDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new DurableObjectStubBinding(
            invocationId,
            bindingName,
            new DurableObjectStubTarget(id.Value, Name: null),
            options,
            dispatcher);
    }

    public static DurableObjectStubBinding ForName(
        string invocationId,
        string bindingName,
        string name,
        DurableObjectGetOptions? options,
        IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new DurableObjectStubBinding(
            invocationId,
            bindingName,
            new DurableObjectStubTarget(Id: null, Name: name),
            options,
            dispatcher);
    }

    public Task<Response> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        return FetchAsync(Request.Get(url), cancellationToken);
    }

    public async Task<Response> FetchAsync(Request request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            "durable.fetch",
            JsonSerializer.Serialize(
                new DurableObjectFetchPayload(
                    _target,
                    _options,
                    RequestEnvelope.FromRequest(request)),
                JsonOptions));

        var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
        return JsonSerializer.Deserialize<ResponseEnvelope>(result, JsonOptions)?.ToResponse(_invocationId, _dispatcher)
            ?? throw new WorkersException("Durable Object stub returned an empty response envelope.");
    }

    public async Task<JsonElement> InvokeAsync(
        string methodName,
        IReadOnlyList<object?>? arguments = null,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        options ??= JsonOptions;

        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            "durable.rpc",
            JsonSerializer.Serialize(
                new DurableObjectRpcPayload(
                    _target,
                    _options,
                    methodName,
                    RpcArguments.Serialize(arguments, options)),
                JsonOptions));

        var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
        var envelope = JsonSerializer.Deserialize<DurableObjectRpcResult>(result, JsonOptions)
            ?? throw new WorkersException("Durable Object RPC returned an empty result.");

        return envelope.Value;
    }

    public async Task<TResult?> InvokeAsync<TResult>(
        string methodName,
        IReadOnlyList<object?>? arguments = null,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var value = await InvokeAsync(methodName, arguments, options, cancellationToken);
        return value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? default
            : value.Deserialize<TResult>(options ?? JsonOptions);
    }

    public async Task<RpcStub> InvokeStubAsync(
        string methodName,
        IReadOnlyList<object?>? arguments = null,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        options ??= JsonOptions;

        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            "durable.rpcStub",
            JsonSerializer.Serialize(
                new DurableObjectRpcPayload(
                    _target,
                    _options,
                    methodName,
                    RpcArguments.Serialize(arguments, options)),
                JsonOptions));

        var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
        var envelope = JsonSerializer.Deserialize<DurableObjectRpcStubResult>(result, JsonOptions)
            ?? throw new WorkersException("Durable Object RPC returned an empty stub result.");

        return new RpcStub(_invocationId, envelope.Handle, _dispatcher);
    }

    public async Task InvokeVoidAsync(
        string methodName,
        IReadOnlyList<object?>? arguments = null,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _ = await InvokeAsync(methodName, arguments, options, cancellationToken);
    }

    private sealed record DurableObjectStubTarget(string? Id, string? Name);

    private sealed record DurableObjectFetchPayload(
        DurableObjectStubTarget Target,
        DurableObjectGetOptions? Options,
        RequestEnvelope Request);

    private sealed record DurableObjectRpcPayload(
        DurableObjectStubTarget Target,
        DurableObjectGetOptions? Options,
        string MethodName,
        IReadOnlyList<JsonElement> Arguments);

    private sealed record DurableObjectRpcResult(JsonElement Value);

    private sealed record DurableObjectRpcStubResult(string Handle);
}
