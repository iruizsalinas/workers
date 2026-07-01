using System.Globalization;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Workers.Build;

/// <summary>Assembles a Cloudflare Worker bundle around a published .NET WebAssembly app.</summary>
internal static class PublishBundleWriter
{
    /// <summary>Copies the WebAssembly runtime files and writes Worker glue files.</summary>
    public static async Task<Bundle> WriteAsync(
        PublishBundleOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.PublishDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.AssemblyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.JavaScriptModule);
        ValidateGeneratedWranglerOptions(options);
        BundleWriter.ValidateJavaScriptModule(options.JavaScriptModule);

        var publishDirectory = Path.GetFullPath(options.PublishDirectory);
        var assemblyPath = Path.GetFullPath(options.AssemblyPath);
        var outputDirectory = Path.GetFullPath(options.OutputDirectory);

        if (!Directory.Exists(publishDirectory))
            throw new DirectoryNotFoundException($"Publish directory '{publishDirectory}' does not exist.");

        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Worker assembly '{assemblyPath}' does not exist.", assemblyPath);

        var frameworkDirectory = FindFrameworkDirectory(publishDirectory);
        PrepareOutputDirectory(outputDirectory, options.JavaScriptModule);
        var outputFrameworkDirectory = Path.Combine(outputDirectory, "_framework");
        CopyDirectory(frameworkDirectory, outputFrameworkDirectory);
        PatchDotnetLoader(outputFrameworkDirectory);

        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        var scannedManifest = EntrypointScanner.Scan(assembly);
        var manifest = scannedManifest with { JavaScriptModule = options.JavaScriptModule };

        var bundle = await BundleWriter.WriteAsync(
            manifest,
            new BundleOptions(outputDirectory, RuntimeAdapterOptions: options.RuntimeAdapterOptions),
            cancellationToken);

        var wranglerTomlPath = Path.Combine(outputDirectory, BundleOptions.WranglerTomlFileName);
        await File.WriteAllTextAsync(
            wranglerTomlPath,
            WriteWranglerToml(outputDirectory, options, manifest),
            cancellationToken);

        return bundle with { WranglerTomlPath = wranglerTomlPath };
    }

    private static string FindFrameworkDirectory(string publishDirectory)
    {
        var candidates = new[]
        {
            Path.Combine(publishDirectory, "_framework"),
            Path.Combine(publishDirectory, "wwwroot", "_framework"),
            Path.Combine(publishDirectory, "AppBundle", "_framework")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        throw new DirectoryNotFoundException(
            $"Publish directory '{publishDirectory}' does not contain a .NET WebAssembly _framework directory.");
    }

    private static void PrepareOutputDirectory(string directory, string javaScriptModule)
    {
        Directory.CreateDirectory(directory);
        DeleteDirectory(Path.Combine(directory, "_framework"));
        DeleteFile(Path.Combine(directory, javaScriptModule));
        DeleteFile(Path.Combine(directory, BundleOptions.RuntimeAdapterFileName));
        DeleteFile(Path.Combine(directory, BundleOptions.ManifestFileName));
        DeleteFile(Path.Combine(directory, BundleOptions.WranglerTomlFileName));
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            if (ShouldSkipFrameworkFile(file))
                continue;

            var destination = Path.Combine(destinationDirectory, Path.GetFileName(file));
            File.Copy(file, destination, overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            if (IsReparsePoint(directory))
                continue;

            var destination = Path.Combine(destinationDirectory, Path.GetFileName(directory));
            CopyDirectory(directory, destination);
        }
    }

    private static bool IsReparsePoint(string path) =>
        File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);

    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private static bool ShouldSkipFrameworkFile(string path)
    {
        var fileName = Path.GetFileName(path);

        return string.Equals(Path.GetExtension(fileName), ".map", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".symbols", StringComparison.OrdinalIgnoreCase);
    }

    private static void PatchDotnetLoader(string frameworkDirectory)
    {
        PatchFile(
            Path.Combine(frameworkDirectory, "dotnet.boot.js"),
            static source => RemoveWasmSymbols(source));

        PatchFile(
            Path.Combine(frameworkDirectory, "dotnet.js"),
            static source =>
            {
                source = ReplaceRequired(
                    source,
                    "const Se=\"object\"==typeof process&&\"object\"==typeof process.versions&&\"string\"==typeof process.versions.node",
                    "const Se=false",
                    "dotnet.js Node environment detection");
                source = ReplaceRequired(
                    source,
                    "/*! webpackIgnore: true */import.meta.url",
                    "(/* Workers fallback */ (import.meta.url || \"./_framework/dotnet.js\"))",
                    "dotnet.js import.meta.url fallback");
                source = ReplaceRequired(
                    source,
                    "Pe.locateFile=e=>\"URL\"in globalThis&&globalThis.URL!==C?new URL(e,Pe.scriptDirectory).toString():M(e)?e:Pe.scriptDirectory+e",
                    "Pe.locateFile=e=>M(e)?e:Pe.scriptDirectory+e",
                    "dotnet.js locateFile patch");
                return ReplaceRequired(
                    source,
                    "const t=await e.pendingDownloadInternal.response,o=t.headers&&t.headers.get?t.headers.get(\"Content-Type\"):void 0;let n;if(\"function\"==typeof WebAssembly.compileStreaming&&\"application/wasm\"===o)n=await WebAssembly.compileStreaming(t);else{ke&&\"application/wasm\"!==o&&E('WebAssembly resource does not have the expected content type \"application/wasm\", so falling back to slower ArrayBuffer instantiation.');const e=await t.arrayBuffer();Pe.diagnosticTracing&&b(\"instantiate_wasm_module buffered\"),n=Ie?await Promise.resolve(new WebAssembly.Module(e)):await WebAssembly.compile(e)}",
                    "const t=await e.pendingDownloadInternal.response;let n;if(t instanceof WebAssembly.Module)n=t;else if(t.compiledModule instanceof WebAssembly.Module)n=t.compiledModule;else{const o=t.headers&&t.headers.get?t.headers.get(\"Content-Type\"):void 0;if(\"function\"==typeof WebAssembly.compileStreaming&&\"application/wasm\"===o)n=await WebAssembly.compileStreaming(t);else{ke&&\"application/wasm\"!==o&&E('WebAssembly resource does not have the expected content type \"application/wasm\", so falling back to slower ArrayBuffer instantiation.');const e=await t.arrayBuffer();Pe.diagnosticTracing&&b(\"instantiate_wasm_module buffered\"),n=Ie?await Promise.resolve(new WebAssembly.Module(e)):await WebAssembly.compile(e)}}",
                    "dotnet.js precompiled WebAssembly module support");
            });

        PatchFile(
            Path.Combine(frameworkDirectory, "dotnet.runtime.js"),
            static source => ReplaceRequired(
                RemoveSourceMappingUrl(source),
                "const tt=\"object\"==typeof process&&\"object\"==typeof process.versions&&\"string\"==typeof process.versions.node",
                "const tt=false",
                "dotnet.runtime.js Node environment detection"));

        PatchFile(
            Path.Combine(frameworkDirectory, "dotnet.native.js"),
            static source => ReplaceRequired(
                source,
                "var ENVIRONMENT_IS_NODE = typeof process == 'object' && typeof process.versions == 'object' && typeof process.versions.node == 'string';",
                "var ENVIRONMENT_IS_NODE = false;",
                "dotnet.native.js Node environment detection"));
    }

    private static string RemoveSourceMappingUrl(string source) =>
        Regex.Replace(source, @"\r?\n//# sourceMappingURL=.*?(?=\r?\n|$)", string.Empty);

    private static string RemoveWasmSymbols(string source)
    {
        const string JsonStartMarker = "/*json-start*/";
        const string JsonEndMarker = "/*json-end*/";

        var jsonStart = source.IndexOf(JsonStartMarker, StringComparison.Ordinal);
        var jsonEnd = source.IndexOf(JsonEndMarker, StringComparison.Ordinal);
        if (jsonStart < 0 || jsonEnd < jsonStart)
            throw new InvalidOperationException("dotnet.boot.js does not contain the expected JSON markers.");

        jsonStart += JsonStartMarker.Length;
        var json = source[jsonStart..jsonEnd];
        var config = JsonNode.Parse(json)?.AsObject();
        if (config is null)
            throw new InvalidOperationException("dotnet.boot.js does not contain a valid boot configuration object.");

        if (config["resources"] is JsonObject resources)
            resources.Remove("wasmSymbols");

        var patchedJson = config.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        return string.Concat(source.AsSpan(0, jsonStart), patchedJson, source.AsSpan(jsonEnd));
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string description)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new InvalidOperationException($"Could not apply required .NET loader patch: {description}.");

        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }

    private static void PatchFile(string path, Func<string, string> patch)
    {
        if (!File.Exists(path))
            return;

        var source = File.ReadAllText(path);
        File.WriteAllText(path, patch(source));
    }

    private static string WriteWranglerToml(
        string outputDirectory,
        PublishBundleOptions options,
        BuildManifest manifest)
    {
        var javaScriptModules = GetJavaScriptModules(outputDirectory);
        var dataModules = GetDataModules(Path.Combine(outputDirectory, "_framework"), outputDirectory);

        var builder = new StringBuilder();
        builder.Append("name = \"");
        builder.Append(EscapeTomlString(options.Name ?? SanitizeWorkerName(manifest.EntryAssembly)));
        builder.AppendLine("\"");
        builder.Append("main = \"");
        builder.Append(EscapeTomlString(manifest.JavaScriptModule));
        builder.AppendLine("\"");
        builder.Append("compatibility_date = \"");
        builder.Append(EscapeTomlString(options.CompatibilityDate));
        builder.AppendLine("\"");
        builder.AppendLine("find_additional_modules = true");
        builder.AppendLine("no_bundle = true");

        builder.AppendLine();
        builder.AppendLine("[[rules]]");
        builder.AppendLine("type = \"ESModule\"");
        builder.Append("globs = [");
        builder.Append(string.Join(", ", javaScriptModules.Select(static path => $"\"{EscapeTomlString(path)}\"")));
        builder.AppendLine("]");
        builder.AppendLine("fallthrough = true");
        builder.AppendLine();
        builder.AppendLine("[[rules]]");
        builder.AppendLine("type = \"CompiledWasm\"");
        builder.AppendLine("globs = [\"**/dotnet.native.wasm\"]");
        builder.AppendLine("fallthrough = false");

        if (dataModules.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("[[rules]]");
            builder.AppendLine("type = \"Data\"");
            builder.Append("globs = [");
            builder.Append(string.Join(", ", dataModules.Select(static path => $"\"{EscapeTomlString(path)}\"")));
            builder.AppendLine("]");
            builder.AppendLine("fallthrough = true");
        }

        return builder.ToString();
    }

    private static void ValidateGeneratedWranglerOptions(PublishBundleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.CompatibilityDate);

        if (!DateOnly.TryParseExact(options.CompatibilityDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new ArgumentException("CompatibilityDate must use the yyyy-MM-dd format.", nameof(options));
    }

    private static string[] GetJavaScriptModules(string outputDirectory)
    {
        var moduleNames = new[]
        {
            BundleOptions.RuntimeAdapterFileName,
            "_framework/dotnet.boot.js",
            "_framework/dotnet.js",
            "_framework/dotnet.native.js",
            "_framework/dotnet.runtime.js"
        };

        return moduleNames
            .Where(module => File.Exists(Path.Combine(outputDirectory, module.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();
    }

    private static string[] GetDataModules(string frameworkDirectory, string outputDirectory)
    {
        return Directory.EnumerateFiles(frameworkDirectory, "*", SearchOption.AllDirectories)
            .Where(static path =>
                string.Equals(Path.GetExtension(path), ".dat", StringComparison.OrdinalIgnoreCase)
                || (string.Equals(Path.GetExtension(path), ".wasm", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(Path.GetFileName(path), "dotnet.native.wasm", StringComparison.OrdinalIgnoreCase)))
            .Select(path => Path.GetRelativePath(outputDirectory, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string SanitizeWorkerName(string entryAssembly)
    {
        var name = Path.GetFileNameWithoutExtension(entryAssembly);
        var builder = new StringBuilder(name.Length);
        var previousHyphen = false;

        foreach (var character in name)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousHyphen = false;
            }
            else if (!previousHyphen && builder.Length > 0)
            {
                builder.Append('-');
                previousHyphen = true;
            }
        }

        return builder.ToString().Trim('-') is { Length: > 0 } sanitized
            ? sanitized
            : "worker";
    }

    private static string EscapeTomlString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
