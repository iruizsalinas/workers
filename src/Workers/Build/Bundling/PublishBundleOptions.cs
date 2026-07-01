namespace Workers.Build;

/// <summary>Options used when assembling a Worker bundle from a published .NET WebAssembly app.</summary>
/// <param name="PublishDirectory">The directory produced by <c>dotnet publish</c>.</param>
/// <param name="AssemblyPath">The managed assembly containing Worker event handlers.</param>
/// <param name="OutputDirectory">The directory that receives the Cloudflare Worker bundle.</param>
internal sealed record PublishBundleOptions(
    string PublishDirectory,
    string AssemblyPath,
    string OutputDirectory)
{
    /// <summary>The generated Worker JavaScript module file name.</summary>
    public string JavaScriptModule { get; init; } = "worker.js";

    /// <summary>The Worker name written to the generated Wrangler configuration.</summary>
    public string? Name { get; init; }

    /// <summary>The compatibility date written to the generated Wrangler configuration.</summary>
    public string CompatibilityDate { get; init; } = "2025-01-01";

    /// <summary>Options for runtime adapter generation.</summary>
    public RuntimeAdapterOptions? RuntimeAdapterOptions { get; init; }
}
