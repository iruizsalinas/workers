namespace Workers.Build;

/// <summary>Options used when writing Worker build artifacts.</summary>
/// <param name="OutputDirectory">The directory that receives generated artifacts.</param>
/// <param name="ModuleOptions">Options for JavaScript module generation.</param>
/// <param name="RuntimeAdapterOptions">Options for runtime adapter generation.</param>
internal sealed record BundleOptions(
    string OutputDirectory,
    ModuleOptions? ModuleOptions = null,
    RuntimeAdapterOptions? RuntimeAdapterOptions = null)
{
    /// <summary>The manifest file emitted next to the generated Worker module.</summary>
    public const string ManifestFileName = "worker.manifest.json";

    /// <summary>The default runtime adapter file emitted next to the generated Worker module.</summary>
    public const string RuntimeAdapterFileName = "dotnet.js";

    /// <summary>The Wrangler configuration file emitted for publish bundles.</summary>
    public const string WranglerTomlFileName = "wrangler.toml";
}
