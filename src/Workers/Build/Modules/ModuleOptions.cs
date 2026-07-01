namespace Workers.Build;

/// <summary>Options used when generating the JavaScript Worker module.</summary>
/// <param name="RuntimeAdapterModule">The module that exports the default Worker factory.</param>
internal sealed record ModuleOptions(string RuntimeAdapterModule)
{
    /// <summary>The default JavaScript module generation options.</summary>
    public static ModuleOptions Default { get; } = new("./dotnet.js");
}
