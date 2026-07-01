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

        return type.GetMethod(entrypoint.MethodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new WorkersException($"Entrypoint method '{entrypoint.ContainingType}.{entrypoint.MethodName}' could not be found.");
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
