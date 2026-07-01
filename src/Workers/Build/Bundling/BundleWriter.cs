using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workers.Build;

/// <summary>Writes generated Worker build artifacts to disk.</summary>
internal static class BundleWriter
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Writes the JavaScript module and manifest JSON files.</summary>
    public static async Task<Bundle> WriteAsync(
        BuildManifest manifest,
        BundleOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputDirectory);
        ValidateJavaScriptModule(manifest.JavaScriptModule);

        Directory.CreateDirectory(options.OutputDirectory);

        var modulePath = Path.Combine(options.OutputDirectory, manifest.JavaScriptModule);
        var adapterPath = Path.Combine(options.OutputDirectory, BundleOptions.RuntimeAdapterFileName);
        var manifestPath = Path.Combine(options.OutputDirectory, BundleOptions.ManifestFileName);

        var module = ModuleWriter.WriteModule(manifest, options.ModuleOptions);
        var runtimeAdapterOptions = RuntimeAdapterOptions.FromManifest(manifest);
        if (options.RuntimeAdapterOptions is not null)
        {
            runtimeAdapterOptions = runtimeAdapterOptions with
            {
                IncludePlatformApis = options.RuntimeAdapterOptions.IncludePlatformApis
            };
        }

        var adapter = RuntimeAdapterWriter.WriteAdapter(runtimeAdapterOptions);
        var manifestJson = JsonSerializer.Serialize(manifest, ManifestJsonOptions);

        await File.WriteAllTextAsync(modulePath, module, cancellationToken);
        await File.WriteAllTextAsync(adapterPath, adapter, cancellationToken);
        await File.WriteAllTextAsync(manifestPath, manifestJson, cancellationToken);

        return new Bundle(modulePath, adapterPath, manifestPath);
    }

    internal static void ValidateJavaScriptModule(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (Path.IsPathRooted(value)
            || value.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || value.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || value is "." or ".."
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("JavaScriptModule must be a simple file name, such as worker.js.", nameof(value));
        }
    }
}
