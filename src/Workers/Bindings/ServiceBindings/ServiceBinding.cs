using System.Text.Json;

namespace Workers;

internal sealed class ServiceBinding : IServiceBinding
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public ServiceBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public Task<Response> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        return FetchAsync(url, options: null, cancellationToken);
    }

    public Task<Response> FetchAsync(
        string url,
        FetchOptions? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        return FetchAsync(Request.Get(url), options, cancellationToken);
    }

    public async Task<Response> FetchAsync(Request request, CancellationToken cancellationToken = default)
    {
        return await FetchAsync(request, options: null, cancellationToken);
    }

    public async Task<Response> FetchAsync(
        Request request,
        FetchOptions? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            "service.fetch",
            JsonSerializer.Serialize(FetchBindingRequest.From(request, options), FetchBindingJsonContext.Default.FetchBindingRequest));

        var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
        return JsonSerializer.Deserialize(result, FetchBindingJsonContext.Default.ResponseEnvelope)?.ToResponse(_invocationId, _dispatcher)
            ?? throw new WorkersException("Service binding returned an empty response envelope.");
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
            "service.rpc",
            JsonSerializer.Serialize(
                new ServiceRpcRequest(
                    methodName,
                    RpcArguments.Serialize(arguments, options)),
                JsonOptions));

        var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
        return JsonSerializer.Deserialize<ServiceRpcResult>(result, JsonOptions)?.Value
            ?? throw new WorkersException("Service binding RPC returned an empty result.");
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
            "service.rpcStub",
            JsonSerializer.Serialize(
                new ServiceRpcRequest(methodName, RpcArguments.Serialize(arguments, options)),
                JsonOptions));

        var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
        var envelope = JsonSerializer.Deserialize<ServiceRpcStubResult>(result, JsonOptions)
            ?? throw new WorkersException("Service binding RPC returned an empty stub result.");

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

    private sealed record ServiceRpcRequest(string MethodName, IReadOnlyList<JsonElement> Arguments);

    private sealed record ServiceRpcResult(JsonElement Value);

    private sealed record ServiceRpcStubResult(string Handle);
}
