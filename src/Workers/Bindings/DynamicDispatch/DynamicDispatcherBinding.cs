using System.Text.Json;
using Workers.Interop;

namespace Workers;

internal sealed class DynamicDispatcherBinding : IDynamicDispatcherBinding
{
    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public DynamicDispatcherBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public IServiceBinding Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new DynamicDispatcherServiceBinding(_invocationId, _bindingName, name, _dispatcher);
    }

    private sealed class DynamicDispatcherServiceBinding : IServiceBinding
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly string _invocationId;
        private readonly string _bindingName;
        private readonly string _serviceName;
        private readonly IBindingDispatcher _dispatcher;

        public DynamicDispatcherServiceBinding(
            string invocationId,
            string bindingName,
            string serviceName,
            IBindingDispatcher dispatcher)
        {
            _invocationId = invocationId;
            _bindingName = bindingName;
            _serviceName = serviceName;
            _dispatcher = dispatcher;
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
                "dynamicDispatcher.fetch",
                JsonSerializer.Serialize(
                    new DynamicDispatcherFetchRequest(_serviceName, FetchBindingRequest.From(request, options)),
                    JsonOptions));

            var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
            return JsonSerializer.Deserialize<ResponseEnvelope>(result, JsonOptions)?.ToResponse(_invocationId, _dispatcher)
                ?? throw new WorkersException("Dynamic Dispatch binding returned an empty response envelope.");
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
                "dynamicDispatcher.rpc",
                JsonSerializer.Serialize(
                    new DynamicDispatcherRpcRequest(
                        _serviceName,
                        methodName,
                        RpcArguments.Serialize(arguments, options)),
                    JsonOptions));

            var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
            return JsonSerializer.Deserialize<DynamicDispatcherRpcResult>(result, JsonOptions)?.Value
                ?? throw new WorkersException("Dynamic Dispatch RPC returned an empty result.");
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
                "dynamicDispatcher.rpcStub",
                JsonSerializer.Serialize(
                    new DynamicDispatcherRpcRequest(
                        _serviceName,
                        methodName,
                        RpcArguments.Serialize(arguments, options)),
                    JsonOptions));

            var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
            var envelope = JsonSerializer.Deserialize<DynamicDispatcherRpcStubResult>(result, JsonOptions)
                ?? throw new WorkersException("Dynamic Dispatch RPC returned an empty stub result.");

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
    }

    private sealed record DynamicDispatcherFetchRequest(string Name, FetchBindingRequest Fetch);

    private sealed record DynamicDispatcherRpcRequest(
        string Name,
        string MethodName,
        IReadOnlyList<JsonElement> Arguments);

    private sealed record DynamicDispatcherRpcResult(JsonElement Value);

    private sealed record DynamicDispatcherRpcStubResult(string Handle);
}
