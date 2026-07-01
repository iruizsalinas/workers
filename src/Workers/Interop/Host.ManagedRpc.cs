using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;

namespace Workers.Interop;

[SupportedOSPlatform("browser")]
internal static partial class Host
{
    private static string RetainManagedRpcTarget(RpcTarget target, ManagedRpcTargetEntry? existingEntry = null)
    {
        var handle = $"managed-rpc:{Interlocked.Increment(ref nextRpcTargetId)}";
        var entry = existingEntry ?? new ManagedRpcTargetEntry(target);
        entry.Retain();
        RpcTargets[handle] = entry;
        return handle;
    }

    private static ManagedRpcTargetEntry ResolveManagedRpcTarget(string handle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        return RpcTargets.TryGetValue(handle, out var entry)
            ? entry
            : throw new WorkersException($"RPC target handle '{handle}' is not defined.");
    }

    private static async Task ReleaseManagedRpcTargetAsync(string handle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        if (!RpcTargets.TryRemove(handle, out var entry))
            return;

        if (!entry.Release())
            return;

        switch (entry.Target)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Managed RPC arguments are intentionally deserialized dynamically from Workers RPC payloads; the app assembly is rooted by Workers.props and reflection JSON is enabled.")]
    private static object[] ConvertArguments(
        MethodInfo method,
        IReadOnlyList<JsonElement> arguments,
        string invocationId)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != arguments.Count)
        {
            throw new WorkersException(
                $"Durable Object RPC method '{Format(method)}' expects {parameters.Length} arguments but received {arguments.Count}.");
        }

        var converted = new object[parameters.Length];
        for (var index = 0; index < parameters.Length; index++)
        {
            converted[index] = parameters[index].ParameterType == typeof(RpcStub)
                ? ToRpcStub(arguments[index], invocationId)
                : arguments[index].Deserialize(parameters[index].ParameterType, JsonOptions)!;
        }

        return converted;
    }

    private static RpcStub ToRpcStub(JsonElement argument, string invocationId)
    {
        if (!argument.TryGetProperty("rpcStubHandle", out var handleProperty))
            throw new WorkersException("RPC stub argument is missing a rpcStubHandle.");

        var handle = handleProperty.GetString();
        if (string.IsNullOrWhiteSpace(handle))
            throw new WorkersException("RPC stub argument has an empty rpcStubHandle.");

        return new RpcStub(invocationId, handle, BindingDispatcher.Current);
    }

    private static bool IsGenericTask(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>);

    private static bool IsGenericValueTask(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>);
}
