using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Workers;

/// <summary>A Workers RPC object-capability stub returned from a remote method.</summary>
public sealed partial class RpcStub : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly RpcStubJsonContext JsonContext = new(new JsonSerializerOptions(JsonOptions));

    private readonly string _invocationId;
    private readonly IBindingDispatcher _dispatcher;
    private bool _disposed;

    internal RpcStub(string invocationId, string handle, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        _invocationId = invocationId;
        Handle = handle;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    internal string Handle { get; }

    /// <summary>Invokes a method on the remote RPC stub and returns a JSON-compatible value.</summary>
    public async Task<JsonElement> InvokeAsync(
        string methodName,
        IReadOnlyList<object?>? arguments = null,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        var result = await DispatchAsync(
            "rpc.stub.invoke",
            new RpcMethodRequest(
                Handle,
                methodName,
                RpcArguments.Serialize(arguments, options ?? JsonOptions)),
            JsonContext.RpcMethodRequest,
            cancellationToken);

        return JsonSerializer.Deserialize(result, JsonContext.RpcResult)?.Value
            ?? throw new WorkersException("RPC stub returned an empty result.");
    }

    /// <summary>Invokes a method on the remote RPC stub and deserializes the JSON-compatible result.</summary>
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

    /// <summary>Invokes a method that returns another object-capability stub.</summary>
    public async Task<RpcStub> InvokeStubAsync(
        string methodName,
        IReadOnlyList<object?>? arguments = null,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        var result = await DispatchAsync(
            "rpc.stub.invokeStub",
            new RpcMethodRequest(
                Handle,
                methodName,
                RpcArguments.Serialize(arguments, options ?? JsonOptions)),
            JsonContext.RpcMethodRequest,
            cancellationToken);

        return CreateStub(result);
    }

    /// <summary>Calls the remote RPC stub when the capability itself is callable.</summary>
    public async Task<JsonElement> CallAsync(
        IReadOnlyList<object?>? arguments = null,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "rpc.stub.call",
            new RpcCallRequest(Handle, RpcArguments.Serialize(arguments, options ?? JsonOptions)),
            JsonContext.RpcCallRequest,
            cancellationToken);

        return JsonSerializer.Deserialize(result, JsonContext.RpcResult)?.Value
            ?? throw new WorkersException("RPC stub returned an empty call result.");
    }

    /// <summary>Calls the remote RPC stub when the capability itself is callable and deserializes the result.</summary>
    public async Task<TResult?> CallAsync<TResult>(
        IReadOnlyList<object?>? arguments = null,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var value = await CallAsync(arguments, options, cancellationToken);
        return value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? default
            : value.Deserialize<TResult>(options ?? JsonOptions);
    }

    /// <summary>Calls the remote RPC stub when the capability itself is callable and returns another stub.</summary>
    public async Task<RpcStub> CallStubAsync(
        IReadOnlyList<object?>? arguments = null,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "rpc.stub.callStub",
            new RpcCallRequest(Handle, RpcArguments.Serialize(arguments, options ?? JsonOptions)),
            JsonContext.RpcCallRequest,
            cancellationToken);

        return CreateStub(result);
    }

    /// <summary>Creates an independent duplicate of this stub handle.</summary>
    public async Task<RpcStub> DuplicateAsync(CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "rpc.stub.dup",
            new RpcHandleRequest(Handle),
            JsonContext.RpcHandleRequest,
            cancellationToken);
        return CreateStub(result);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await DispatchAsync(
            "rpc.stub.dispose",
            new RpcHandleRequest(Handle),
            JsonContext.RpcHandleRequest,
            CancellationToken.None);
    }

    private RpcStub CreateStub(string result)
    {
        var envelope = JsonSerializer.Deserialize(result, JsonContext.RpcStubEnvelope)
            ?? throw new WorkersException("RPC call returned an empty stub result.");

        return new RpcStub(_invocationId, envelope.Handle, _dispatcher);
    }

    private Task<string> DispatchAsync<TPayload>(
        string operation,
        TPayload payload,
        JsonTypeInfo<TPayload> typeInfo,
        CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            "$rpc",
            operation,
            JsonSerializer.Serialize(payload, typeInfo));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private sealed record RpcHandleRequest(string Handle);

    private sealed record RpcMethodRequest(
        string Handle,
        string MethodName,
        IReadOnlyList<JsonElement> Arguments);

    private sealed record RpcCallRequest(string Handle, IReadOnlyList<JsonElement> Arguments);

    private sealed record RpcResult(JsonElement Value);

    private sealed record RpcStubEnvelope(string Handle);

    [JsonSerializable(typeof(RpcHandleRequest))]
    [JsonSerializable(typeof(RpcMethodRequest))]
    [JsonSerializable(typeof(RpcCallRequest))]
    [JsonSerializable(typeof(RpcResult))]
    [JsonSerializable(typeof(RpcStubEnvelope))]
    private sealed partial class RpcStubJsonContext : JsonSerializerContext
    {
    }
}

internal static class RpcArguments
{
    internal static IReadOnlyList<JsonElement> Serialize(
        IReadOnlyList<object?>? arguments,
        JsonSerializerOptions options)
    {
        if (arguments is null || arguments.Count == 0)
            return [];

        var serialized = new JsonElement[arguments.Count];
        for (var i = 0; i < arguments.Count; i++)
        {
            serialized[i] = arguments[i] is RpcStub stub
                ? JsonSerializer.SerializeToElement(new RpcStubArgument(stub.Handle), options)
                : JsonSerializer.SerializeToElement(arguments[i], options);
        }

        return serialized;
    }

    private sealed record RpcStubArgument([property: JsonPropertyName("rpcStubHandle")] string Handle);
}
