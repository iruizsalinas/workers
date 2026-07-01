using System.Reflection;
using System.Text.Json;

namespace Workers;

/// <summary>Helpers for creating typed clients over Workers RPC stubs and bindings.</summary>
public static class RpcClient
{
    /// <summary>Creates a typed RPC client for a service binding.</summary>
    public static T AsRpc<T>(this IServiceBinding binding, JsonSerializerOptions? options = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(binding);
        return RpcProxy<T>.Create(new ServiceRpcInvoker(binding, options));
    }

    /// <summary>Creates a typed RPC client for a Durable Object stub.</summary>
    public static T AsRpc<T>(this IDurableObjectStub stub, JsonSerializerOptions? options = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(stub);
        return RpcProxy<T>.Create(new DurableObjectRpcInvoker(stub, options));
    }

    /// <summary>Creates a typed RPC client for a returned object-capability stub.</summary>
    public static T AsRpc<T>(this RpcStub stub, JsonSerializerOptions? options = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(stub);
        return RpcProxy<T>.Create(new StubRpcInvoker(stub, options));
    }

    /// <summary>Gets a typed RPC client for a Worker inside a Dynamic Dispatch namespace.</summary>
    public static T GetRpc<T>(
        this IDynamicDispatcherBinding binding,
        string name,
        JsonSerializerOptions? options = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(binding);
        return binding.Get(name).AsRpc<T>(options);
    }

    private class RpcProxy<T> : DispatchProxy
        where T : class
    {
        private IRpcInvoker? _invoker;

        public static T Create(IRpcInvoker invoker)
        {
            var proxy = DispatchProxy.Create<T, RpcProxy<T>>();
            ((RpcProxy<T>)(object)proxy)._invoker = invoker;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
                throw new WorkersException("Typed RPC invocation is missing a target method.");

            if (targetMethod.DeclaringType == typeof(object))
                return InvokeObjectMethod(targetMethod, args ?? []);

            return _invoker is null
                ? throw new WorkersException("Typed RPC proxy is not initialized.")
                : InvokeRpc(_invoker, targetMethod, args ?? []);
        }

        private static object? InvokeObjectMethod(MethodInfo method, object?[] args) =>
            method.Name switch
            {
                nameof(ToString) => $"RPC proxy for {typeof(T).FullName}",
                nameof(GetHashCode) => typeof(T).GetHashCode(),
                nameof(Equals) => false,
                _ => throw new WorkersException($"Unsupported object method '{method.Name}' on typed RPC proxy.")
            };
    }

    private interface IRpcInvoker
    {
        Task<JsonElement> InvokeAsync(
            string methodName,
            IReadOnlyList<object?> arguments,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken);

        Task<TResult?> InvokeAsync<TResult>(
            string methodName,
            IReadOnlyList<object?> arguments,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken);

        Task<RpcStub> InvokeStubAsync(
            string methodName,
            IReadOnlyList<object?> arguments,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken);

        Task InvokeVoidAsync(
            string methodName,
            IReadOnlyList<object?> arguments,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken);
    }

    private sealed record ServiceRpcInvoker(
        IServiceBinding Binding,
        JsonSerializerOptions? Options) : IRpcInvoker
    {
        public Task<JsonElement> InvokeAsync(
            string methodName,
            IReadOnlyList<object?> arguments,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken) =>
            Binding.InvokeAsync(methodName, arguments, options ?? Options, cancellationToken);

        public Task<TResult?> InvokeAsync<TResult>(
            string methodName,
            IReadOnlyList<object?> arguments,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken) =>
            Binding.InvokeAsync<TResult>(methodName, arguments, options ?? Options, cancellationToken);

        public Task<RpcStub> InvokeStubAsync(
            string methodName,
            IReadOnlyList<object?> arguments,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken) =>
            Binding.InvokeStubAsync(methodName, arguments, options ?? Options, cancellationToken);

        public Task InvokeVoidAsync(
            string methodName,
            IReadOnlyList<object?> arguments,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken) =>
            Binding.InvokeVoidAsync(methodName, arguments, options ?? Options, cancellationToken);
    }

    private sealed record DurableObjectRpcInvoker(
        IDurableObjectStub Stub,
        JsonSerializerOptions? Options) : IRpcInvoker
    {
        public Task<JsonElement> InvokeAsync(
            string methodName,
            IReadOnlyList<object?> arguments,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken) =>
            Stub.InvokeAsync(methodName, arguments, options ?? Options, cancellationToken);

        public Task<TResult?> InvokeAsync<TResult>(
            string methodName,
            IReadOnlyList<object?> arguments,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken) =>
            Stub.InvokeAsync<TResult>(methodName, arguments, options ?? Options, cancellationToken);

        public Task<RpcStub> InvokeStubAsync(
            string methodName,
            IReadOnlyList<object?> arguments,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken) =>
            Stub.InvokeStubAsync(methodName, arguments, options ?? Options, cancellationToken);

        public Task InvokeVoidAsync(
            string methodName,
            IReadOnlyList<object?> arguments,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken) =>
            Stub.InvokeVoidAsync(methodName, arguments, options ?? Options, cancellationToken);
    }

    private sealed record StubRpcInvoker(
        RpcStub Stub,
        JsonSerializerOptions? Options) : IRpcInvoker
    {
        public Task<JsonElement> InvokeAsync(
            string methodName,
            IReadOnlyList<object?> arguments,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken) =>
            Stub.InvokeAsync(methodName, arguments, options ?? Options, cancellationToken);

        public Task<TResult?> InvokeAsync<TResult>(
            string methodName,
            IReadOnlyList<object?> arguments,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken) =>
            Stub.InvokeAsync<TResult>(methodName, arguments, options ?? Options, cancellationToken);

        public Task<RpcStub> InvokeStubAsync(
            string methodName,
            IReadOnlyList<object?> arguments,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken) =>
            Stub.InvokeStubAsync(methodName, arguments, options ?? Options, cancellationToken);

        public Task InvokeVoidAsync(
            string methodName,
            IReadOnlyList<object?> arguments,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken) =>
            Stub.InvokeAsync(methodName, arguments, options ?? Options, cancellationToken);
    }

    private static object InvokeRpc(IRpcInvoker invoker, MethodInfo method, object?[] args)
    {
        var (arguments, cancellationToken) = Arguments(args, method.GetParameters());
        var returnType = method.ReturnType;

        if (returnType == typeof(Task))
            return invoker.InvokeVoidAsync(method.Name, arguments, options: null, cancellationToken);

        if (returnType == typeof(ValueTask))
            return new ValueTask(invoker.InvokeVoidAsync(method.Name, arguments, options: null, cancellationToken));

        if (returnType == typeof(Task<JsonElement>))
            return invoker.InvokeAsync(method.Name, arguments, options: null, cancellationToken);

        if (returnType == typeof(Task<RpcStub>))
            return invoker.InvokeStubAsync(method.Name, arguments, options: null, cancellationToken);

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            var helper = typeof(RpcClient)
                .GetMethod(nameof(InvokeTaskAsync), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(resultType);
            return helper.Invoke(null, [invoker, method.Name, arguments, cancellationToken])!;
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            if (resultType == typeof(RpcStub))
                return new ValueTask<RpcStub>(
                    invoker.InvokeStubAsync(method.Name, arguments, options: null, cancellationToken));

            var helper = typeof(RpcClient)
                .GetMethod(nameof(InvokeValueTaskAsync), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(resultType);
            return helper.Invoke(null, [invoker, method.Name, arguments, cancellationToken])!;
        }

        throw new WorkersException(
            $"Typed RPC method '{method.DeclaringType?.FullName}.{method.Name}' must return Task, Task<T>, ValueTask, or ValueTask<T>.");
    }

    private static async Task<TResult?> InvokeTaskAsync<TResult>(
        IRpcInvoker invoker,
        string methodName,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken) =>
        await invoker.InvokeAsync<TResult>(methodName, arguments, options: null, cancellationToken)
            ;

    private static async ValueTask<TResult?> InvokeValueTaskAsync<TResult>(
        IRpcInvoker invoker,
        string methodName,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken) =>
        await invoker.InvokeAsync<TResult>(methodName, arguments, options: null, cancellationToken)
            ;

    private static (IReadOnlyList<object?> Arguments, CancellationToken CancellationToken) Arguments(
        object?[] args,
        ParameterInfo[] parameters)
    {
        if (args.Length == 0)
            return ([], CancellationToken.None);

        if (parameters[^1].ParameterType == typeof(CancellationToken))
            return (args[..^1], args[^1] as CancellationToken? ?? CancellationToken.None);

        return (args, CancellationToken.None);
    }
}
