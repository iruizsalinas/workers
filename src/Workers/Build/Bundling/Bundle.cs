namespace Workers.Build;

/// <summary>Describes generated Worker bundle artifacts.</summary>
/// <param name="JavaScriptModulePath">Path to the generated JavaScript module.</param>
/// <param name="RuntimeAdapterPath">Path to the generated .NET runtime adapter module.</param>
/// <param name="ManifestPath">Path to the generated manifest JSON file.</param>
internal sealed record Bundle(
    string JavaScriptModulePath,
    string RuntimeAdapterPath,
    string ManifestPath)
{
    /// <summary>Path to the generated Wrangler configuration file, when one was written.</summary>
    public string? WranglerTomlPath { get; init; }
}
