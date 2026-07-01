using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;

namespace Workers.Interop;

[SupportedOSPlatform("browser")]
internal static partial class Host
{
    private static async Task<Response> AwaitResponseAsync(MethodInfo method, object? result)
    {
        if (result is Response response)
            return response;

        if (result is Task<Response> task)
            return await task;

        if (result is ValueTask<Response> valueTask)
            return await valueTask;

        throw new WorkersException($"Fetch entrypoint '{Format(method)}' returned an unsupported value.");
    }

    private static Env ToEnvironment(EnvEnvelope? envelope) =>
        (envelope ?? EnvEnvelope.Empty).ToEnvironment();

    private static Context ToExecutionContext(ContextEnvelope? envelope) =>
        (envelope ?? ContextEnvelope.Empty).ToExecutionContext();

    private static object? Invoke(MethodInfo method, params object[] parameters)
    {
        try
        {
            return method.Invoke(null, parameters);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static object? Invoke(object instance, MethodInfo method, params object[] parameters)
    {
        try
        {
            return method.Invoke(instance, parameters);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static async Task AwaitVoidLikeAsync(MethodInfo method, object? result)
    {
        if (method.ReturnType == typeof(void))
            return;

        if (result is Task task)
        {
            await task;
            return;
        }

        if (result is ValueTask valueTask)
        {
            await valueTask;
            return;
        }

        throw new WorkersException($"Entrypoint '{Format(method)}' returned an unsupported value.");
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Durable Object RPC return values are intentionally serialized dynamically; the app assembly is rooted by Workers.props and reflection JSON is enabled for browser-wasm workers.")]
    private static async Task<DurableObjectRpcResult> AwaitRpcResultAsync(MethodInfo method, object? result)
    {
        var value = await AwaitResultAsync(method, result);
        if (value is RpcTarget target)
            return new DurableObjectRpcResult(Value: null, RpcTargetHandle: RetainManagedRpcTarget(target));

        return new DurableObjectRpcResult(JsonSerializer.SerializeToElement(value, JsonOptions), RpcTargetHandle: null);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Task<T> and ValueTask<T> results are read dynamically so Worker handlers can return normal .NET async result shapes.")]
    private static async Task<object?> AwaitResultAsync(MethodInfo method, object? result)
    {
        var returnType = method.ReturnType;
        if (returnType == typeof(void))
            return null;

        if (returnType == typeof(Task))
        {
            await ((Task)result!);
            return null;
        }

        if (returnType == typeof(ValueTask))
        {
            await ((ValueTask)result!);
            return null;
        }

        if (IsGenericTask(returnType) && result is Task task)
        {
            await task;
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }

        if (IsGenericValueTask(returnType))
        {
            var valueTaskAsTask = (Task)returnType.GetMethod(nameof(ValueTask<int>.AsTask))!.Invoke(result, null)!;
            await valueTaskAsTask;
            return valueTaskAsTask.GetType().GetProperty("Result")?.GetValue(valueTaskAsTask);
        }

        return result;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Worker entrypoint types come from the generated manifest; the app assembly is rooted by Workers.props.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Worker entrypoint methods come from the generated manifest; the app assembly is rooted by Workers.props.")]
    private static MethodInfo Resolve(RuntimeBuildManifest manifest, RuntimeEntrypointKind kind)
    {
        var entrypoint = manifest.Entrypoints.SingleOrDefault(entrypoint => entrypoint.Kind == kind)
            ?? throw new WorkersException($"Manifest does not contain a {kind} entrypoint.");

        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(entrypoint.ContainingType, throwOnError: false, ignoreCase: false))
            .FirstOrDefault(static type => type is not null)
            ?? throw new WorkersException($"Entrypoint type '{entrypoint.ContainingType}' could not be found.");

        return ResolveEntrypointMethod(type, entrypoint, kind);
    }

    private static MethodInfo ResolveEntrypointMethod(
        Type type,
        RuntimeEntrypoint entrypoint,
        RuntimeEntrypointKind kind)
    {
        var methods = type
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(method => method.Name == entrypoint.MethodName && EntrypointSignatureMatches(method, kind))
            .ToArray();

        return methods.Length switch
        {
            1 => methods[0],
            0 => throw new WorkersException($"Entrypoint method '{entrypoint.ContainingType}.{entrypoint.MethodName}' could not be found."),
            _ => throw new WorkersException($"Entrypoint method '{entrypoint.ContainingType}.{entrypoint.MethodName}' is ambiguous.")
        };
    }

    private static bool EntrypointSignatureMatches(MethodInfo method, RuntimeEntrypointKind kind) =>
        kind switch
        {
            RuntimeEntrypointKind.Fetch => HasReturn(method, typeof(Response))
                && HasParameters(method, typeof(Request), typeof(Env), typeof(Context)),
            RuntimeEntrypointKind.Scheduled => HasVoidLikeReturn(method)
                && HasParameters(method, typeof(ScheduledEvent), typeof(Env), typeof(Context)),
            RuntimeEntrypointKind.Queue => HasVoidLikeReturn(method) && HasQueueParameters(method),
            RuntimeEntrypointKind.Email => HasVoidLikeReturn(method)
                && HasParameters(method, typeof(ForwardableEmailMessage), typeof(Env), typeof(Context)),
            RuntimeEntrypointKind.Tail => HasVoidLikeReturn(method)
                && HasParameters(method, typeof(TailEvent), typeof(Env), typeof(Context)),
            _ => false
        };

    private static bool HasReturn(MethodInfo method, Type resultType) =>
        method.ReturnType == resultType
        || method.ReturnType == typeof(Task<>).MakeGenericType(resultType)
        || method.ReturnType == typeof(ValueTask<>).MakeGenericType(resultType);

    private static bool HasVoidLikeReturn(MethodInfo method) =>
        method.ReturnType == typeof(void)
        || method.ReturnType == typeof(Task)
        || method.ReturnType == typeof(ValueTask);

    private static bool HasParameters(MethodInfo method, params Type[] expectedTypes)
    {
        var parameters = method.GetParameters();
        return parameters.Length == expectedTypes.Length
            && parameters.Select(static parameter => parameter.ParameterType).SequenceEqual(expectedTypes);
    }

    private static bool HasQueueParameters(MethodInfo method)
    {
        var parameters = method.GetParameters();
        return parameters.Length == 3
            && parameters[0].ParameterType.IsGenericType
            && parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(QueueMessageBatch<>)
            && parameters[1].ParameterType == typeof(Env)
            && parameters[2].ParameterType == typeof(Context);
    }

    private static RuntimeDurableObject ResolveDurableObject(RuntimeBuildManifest manifest, string exportName) =>
        manifest.DurableObjects.SingleOrDefault(durableObject => durableObject.ExportName == exportName)
        ?? throw new WorkersException($"Manifest does not contain a Durable Object export named '{exportName}'.");

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Durable Object types come from the generated manifest; the app assembly is rooted by Workers.props.")]
    private static Type ResolveDurableObjectType(RuntimeDurableObject durableObject) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(durableObject.ContainingType, throwOnError: false, ignoreCase: false))
            .FirstOrDefault(static type => type is not null)
        ?? throw new WorkersException($"Durable Object type '{durableObject.ContainingType}' could not be found.");

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Durable Object methods come from the generated manifest; the app assembly is rooted by Workers.props.")]
    private static MethodInfo ResolveDurableObjectMethod(
        RuntimeDurableObject durableObject,
        string? methodName,
        string handlerName)
    {
        if (methodName is null)
            throw new WorkersException($"Durable Object '{durableObject.ExportName}' does not define a {handlerName} handler.");

        var type = ResolveDurableObjectType(durableObject);
        return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new WorkersException($"Durable Object method '{durableObject.ContainingType}.{methodName}' could not be found.");
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Durable Object RPC methods come from the generated manifest; the app assembly is rooted by Workers.props.")]
    private static MethodInfo ResolveDurableObjectRpcMethod(RuntimeDurableObject durableObject, string methodName)
    {
        var rpcMethod = durableObject.RpcMethods.SingleOrDefault(method => method.Name == methodName)
            ?? throw new WorkersException($"Durable Object '{durableObject.ExportName}' does not define RPC method '{methodName}'.");
        var type = ResolveDurableObjectType(durableObject);
        return type.GetMethod(rpcMethod.MethodName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new WorkersException($"Durable Object RPC method '{durableObject.ContainingType}.{rpcMethod.MethodName}' could not be found.");
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Managed RPC target methods are discovered dynamically from live RPC target objects.")]
    private static MethodInfo ResolveManagedRpcTargetMethod(Type type, string methodName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == methodName && !method.IsSpecialName)
            .ToArray();

        return methods.Length switch
        {
            1 => methods[0],
            0 => throw new WorkersException($"RPC target '{type.FullName}' does not define method '{methodName}'."),
            _ => throw new WorkersException($"RPC target '{type.FullName}' has more than one method named '{methodName}'.")
        };
    }
}
