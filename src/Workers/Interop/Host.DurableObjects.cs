using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Versioning;

namespace Workers.Interop;

[SupportedOSPlatform("browser")]
internal static partial class Host
{
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Durable Object constructors are invoked dynamically from manifest-resolved user types; the app assembly is rooted by Workers.props.")]
    private static object CreateDurableObjectInstance(
        RuntimeDurableObject durableObject,
        string durableObjectId,
        EnvEnvelope? environmentEnvelope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(durableObjectId);

        var environment = ToEnvironment(environmentEnvelope);
        var invocationId = environmentEnvelope?.InvocationId
            ?? throw new WorkersException("Durable Object invocation is missing an invocation id.");
        var state = new DurableObjectState(
            invocationId,
            new DurableObjectId(durableObjectId),
            BindingDispatcher.Current);
        var type = ResolveDurableObjectType(durableObject);

        return Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [state, environment],
            culture: null)
            ?? throw new WorkersException($"Durable Object '{durableObject.ContainingType}' could not be constructed.");
    }

    private static WebSocket CreateWebSocket(
        EnvEnvelope? environmentEnvelope,
        string handle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        var invocationId = environmentEnvelope?.InvocationId
            ?? throw new WorkersException("Durable Object WebSocket invocation is missing an invocation id.");

        return new WebSocket(invocationId, handle, BindingDispatcher.Current);
    }
}
