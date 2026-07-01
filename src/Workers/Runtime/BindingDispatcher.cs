using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Workers;

/// <summary>Dispatches platform binding operations to the JavaScript Worker runtime.</summary>
public interface IBindingDispatcher
{
    /// <summary>Dispatches a binding operation and returns a JSON payload.</summary>
    Task<string> DispatchAsync(BindingInvocation invocation, CancellationToken cancellationToken = default);
}

/// <summary>A platform binding operation request.</summary>
/// <param name="InvocationId">The live Worker invocation id.</param>
/// <param name="BindingName">The binding name in <c>env</c>.</param>
/// <param name="Operation">The operation identifier.</param>
/// <param name="PayloadJson">The operation payload as JSON.</param>
public sealed record BindingInvocation(
    string InvocationId,
    string BindingName,
    string Operation,
    string PayloadJson);

/// <summary>Provides the current platform binding dispatcher.</summary>
public static class BindingDispatcher
{
    private static readonly AsyncLocal<IBindingDispatcher?> DispatcherOverride = new();

    /// <summary>The dispatcher used when no scoped override is active.</summary>
    public static IBindingDispatcher Default { get; set; } = JavaScriptBindingDispatcher.Instance;

    /// <summary>The dispatcher visible to new environments on the current async flow.</summary>
    public static IBindingDispatcher Current => DispatcherOverride.Value ?? Default;

    /// <summary>Temporarily overrides the dispatcher for the current async flow.</summary>
    public static IDisposable Use(IBindingDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        var previous = DispatcherOverride.Value;
        DispatcherOverride.Value = dispatcher;
        return new Scope(previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly IBindingDispatcher? _previous;
        private bool _disposed;

        public Scope(IBindingDispatcher? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            DispatcherOverride.Value = _previous;
            _disposed = true;
        }
    }
}

internal sealed class JavaScriptBindingDispatcher : IBindingDispatcher
{
    public static JavaScriptBindingDispatcher Instance { get; } = new();

    public Task<string> DispatchAsync(BindingInvocation invocation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsBrowser())
            throw new PlatformNotSupportedException("Worker platform bindings are only available in the browser WebAssembly runtime.");

        return JavaScriptWorkerBindingInterop.DispatchAsync(
            invocation.InvocationId,
            invocation.BindingName,
            invocation.Operation,
            invocation.PayloadJson);
    }
}

[SupportedOSPlatform("browser")]
internal static partial class JavaScriptWorkerBindingInterop
{
    [JSImport("cloudflareWorkers.bindings.dispatch", "dotnet.js")]
    internal static partial Task<string> DispatchAsync(
        string invocationId,
        string bindingName,
        string operation,
        string payloadJson);
}
