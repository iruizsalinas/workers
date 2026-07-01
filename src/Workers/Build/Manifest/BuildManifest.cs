namespace Workers.Build;

/// <summary>Describes the files and entrypoints emitted by a Worker build.</summary>
/// <param name="EntryAssembly">The managed assembly containing Worker entrypoints.</param>
/// <param name="JavaScriptModule">The generated JavaScript module loaded by Workers.</param>
/// <param name="WasmModule">The WebAssembly module produced by the .NET toolchain.</param>
/// <param name="Entrypoints">The Worker event handlers discovered in the assembly.</param>
internal sealed record BuildManifest(
    string EntryAssembly,
    string JavaScriptModule,
    string WasmModule,
    IReadOnlyList<Entrypoint> Entrypoints)
{
    /// <summary>Durable Object classes exported by the Worker module.</summary>
    public IReadOnlyList<DurableObjectEntrypoint> DurableObjects { get; init; } = [];
}

/// <summary>Identifies a single Worker event handler.</summary>
/// <param name="Kind">The Worker event kind.</param>
/// <param name="ContainingType">The full name of the containing CLR type.</param>
/// <param name="MethodName">The CLR method name.</param>
internal sealed record Entrypoint(
    EntrypointKind Kind,
    string ContainingType,
    string MethodName);

/// <summary>Identifies one Durable Object class exported by the Worker module.</summary>
/// <param name="ExportName">The JavaScript class name exported from the Worker module.</param>
/// <param name="ContainingType">The full name of the CLR Durable Object type.</param>
/// <param name="FetchMethodName">The CLR fetch handler method name, when present.</param>
/// <param name="AlarmMethodName">The CLR alarm handler method name, when present.</param>
/// <param name="WebSocketMessageMethodName">The CLR hibernatable WebSocket message handler method name, when present.</param>
/// <param name="WebSocketCloseMethodName">The CLR hibernatable WebSocket close handler method name, when present.</param>
/// <param name="WebSocketErrorMethodName">The CLR hibernatable WebSocket error handler method name, when present.</param>
internal sealed record DurableObjectEntrypoint(
    string ExportName,
    string ContainingType,
    string? FetchMethodName,
    string? AlarmMethodName,
    string? WebSocketMessageMethodName = null,
    string? WebSocketCloseMethodName = null,
    string? WebSocketErrorMethodName = null)
{
    /// <summary>Public JSON-compatible RPC methods exported by this Durable Object class.</summary>
    public IReadOnlyList<DurableObjectRpcMethod> RpcMethods { get; init; } = [];
}

/// <summary>Identifies one public RPC method on a Durable Object class.</summary>
/// <param name="Name">The method name exposed on the JavaScript Durable Object class.</param>
/// <param name="MethodName">The CLR method name.</param>
internal sealed record DurableObjectRpcMethod(string Name, string MethodName);

/// <summary>Supported Worker event kinds.</summary>
internal enum EntrypointKind
{
    /// <summary>A fetch event handler.</summary>
    Fetch,

    /// <summary>A scheduled event handler.</summary>
    Scheduled,

    /// <summary>A queue consumer event handler.</summary>
    Queue,

    /// <summary>An inbound email event handler.</summary>
    Email,

    /// <summary>A tail event handler.</summary>
    Tail
}
