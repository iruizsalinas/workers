using Workers.Build;
using Xunit;

namespace Workers.Tests;

public sealed class PublishBundleWriterTests
{
    [Fact]
    public async Task AssemblesBundleFromPublishedFrameworkDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "workers-tests", Guid.NewGuid().ToString("N"));
        var publishDirectory = Path.Combine(root, "publish");
        var outputDirectory = Path.Combine(root, "dist");
        var frameworkDirectory = Path.Combine(publishDirectory, "_framework");

        try
        {
            Directory.CreateDirectory(frameworkDirectory);
            Directory.CreateDirectory(Path.Combine(outputDirectory, "_framework"));
            await File.WriteAllTextAsync(Path.Combine(frameworkDirectory, "dotnet.boot.js"), """
                export const config = /*json-start*/{
                  "resources": {
                    "wasmSymbols": [
                      {
                        "name": "dotnet.native.js.symbols"
                      }
                    ],
                    "assembly": []
                  }
                }/*json-end*/;
                """);
            await File.WriteAllTextAsync(Path.Combine(frameworkDirectory, "dotnet.js"), """
                const Se="object"==typeof process&&"object"==typeof process.versions&&"string"==typeof process.versions.node;
                const url = /*! webpackIgnore: true */import.meta.url;
                Pe.locateFile=e=>"URL"in globalThis&&globalThis.URL!==C?new URL(e,Pe.scriptDirectory).toString():M(e)?e:Pe.scriptDirectory+e;
                const t=await e.pendingDownloadInternal.response,o=t.headers&&t.headers.get?t.headers.get("Content-Type"):void 0;let n;if("function"==typeof WebAssembly.compileStreaming&&"application/wasm"===o)n=await WebAssembly.compileStreaming(t);else{ke&&"application/wasm"!==o&&E('WebAssembly resource does not have the expected content type "application/wasm", so falling back to slower ArrayBuffer instantiation.');const e=await t.arrayBuffer();Pe.diagnosticTracing&&b("instantiate_wasm_module buffered"),n=Ie?await Promise.resolve(new WebAssembly.Module(e)):await WebAssembly.compile(e)}
                export const dotnet = {};
                """);
            await File.WriteAllTextAsync(Path.Combine(frameworkDirectory, "dotnet.js.map"), "{}");
            await File.WriteAllTextAsync(Path.Combine(frameworkDirectory, "dotnet.runtime.js"), "const tt=\"object\"==typeof process&&\"object\"==typeof process.versions&&\"string\"==typeof process.versions.node; export const runtime = {};\n//# sourceMappingURL=dotnet.runtime.js.map");
            await File.WriteAllTextAsync(Path.Combine(frameworkDirectory, "dotnet.runtime.js.map"), "{}");
            await File.WriteAllTextAsync(Path.Combine(frameworkDirectory, "dotnet.native.js.symbols"), "symbols");
            await File.WriteAllTextAsync(Path.Combine(frameworkDirectory, "dotnet.es6.lib.js"), "invalid worker module");
            await File.WriteAllTextAsync(Path.Combine(frameworkDirectory, "dotnet.native.wasm"), "wasm");
            await File.WriteAllTextAsync(Path.Combine(frameworkDirectory, "System.Text.Json.wasm"), "wasm");
            await File.WriteAllTextAsync(Path.Combine(frameworkDirectory, "HelloWorld.wasm"), "wasm");
            await File.WriteAllTextAsync(Path.Combine(frameworkDirectory, "icudt.dat"), "data");
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "_framework", "stale.wasm"), "stale");
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "old-worker.js"), "stale");
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "stale-root.wasm"), "stale");
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "stale-root.dat"), "stale");

            var bundle = await PublishBundleWriter.WriteAsync(new PublishBundleOptions(
                publishDirectory,
                typeof(HelloWorld.Worker).Assembly.Location,
                outputDirectory)
            {
                Name = "hello-world",
                CompatibilityDate = "2026-06-30"
            });

            Assert.True(File.Exists(bundle.JavaScriptModulePath));
            Assert.True(File.Exists(bundle.RuntimeAdapterPath));
            Assert.True(File.Exists(bundle.ManifestPath));
            Assert.True(File.Exists(bundle.WranglerTomlPath));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "_framework", "dotnet.js")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "_framework", "dotnet.native.wasm")));
            Assert.False(File.Exists(Path.Combine(outputDirectory, "_framework", "dotnet.js.map")));
            Assert.False(File.Exists(Path.Combine(outputDirectory, "_framework", "dotnet.runtime.js.map")));
            Assert.False(File.Exists(Path.Combine(outputDirectory, "_framework", "dotnet.native.js.symbols")));
            Assert.False(File.Exists(Path.Combine(outputDirectory, "_framework", "stale.wasm")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "old-worker.js")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "stale-root.wasm")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "stale-root.dat")));

            var manifestJson = await File.ReadAllTextAsync(bundle.ManifestPath);
            Assert.Contains("\"entryAssembly\": \"HelloWorld.dll\"", manifestJson, StringComparison.Ordinal);
            Assert.Contains("\"containingType\": \"HelloWorld.Worker\"", manifestJson, StringComparison.Ordinal);

            var wranglerToml = await File.ReadAllTextAsync(bundle.WranglerTomlPath);
            Assert.Contains("name = \"hello-world\"", wranglerToml, StringComparison.Ordinal);
            Assert.Contains("main = \"worker.js\"", wranglerToml, StringComparison.Ordinal);
            Assert.Contains("compatibility_date = \"2026-06-30\"", wranglerToml, StringComparison.Ordinal);
            Assert.Contains("find_additional_modules = true", wranglerToml, StringComparison.Ordinal);
            Assert.Contains("no_bundle = true", wranglerToml, StringComparison.Ordinal);
            Assert.Contains("type = \"CompiledWasm\"", wranglerToml, StringComparison.Ordinal);
            Assert.Contains("globs = [\"**/dotnet.native.wasm\"]", wranglerToml, StringComparison.Ordinal);
            Assert.Contains("\"_framework/dotnet.js\"", wranglerToml, StringComparison.Ordinal);
            Assert.Contains("\"_framework/dotnet.runtime.js\"", wranglerToml, StringComparison.Ordinal);
            Assert.DoesNotContain("compatibility_flags", wranglerToml, StringComparison.Ordinal);
            Assert.DoesNotContain("observability", wranglerToml, StringComparison.Ordinal);
            Assert.DoesNotContain("migrations", wranglerToml, StringComparison.Ordinal);
            Assert.DoesNotContain("dotnet.es6.lib.js", wranglerToml, StringComparison.Ordinal);
            Assert.Contains("\"_framework/HelloWorld.wasm\"", wranglerToml, StringComparison.Ordinal);
            Assert.Contains("\"_framework/System.Text.Json.wasm\"", wranglerToml, StringComparison.Ordinal);
            Assert.Contains("\"_framework/icudt.dat\"", wranglerToml, StringComparison.Ordinal);
            Assert.DoesNotContain("\"_framework/dotnet.native.wasm\"", wranglerToml, StringComparison.Ordinal);
            Assert.DoesNotContain("stale-root.wasm", wranglerToml, StringComparison.Ordinal);
            Assert.DoesNotContain("stale-root.dat", wranglerToml, StringComparison.Ordinal);

            var adapter = await File.ReadAllTextAsync(bundle.RuntimeAdapterPath);
            Assert.Contains("import(`./_framework/${name}`)", adapter, StringComparison.Ordinal);

            var bootConfig = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "_framework", "dotnet.boot.js"));
            Assert.DoesNotContain("wasmSymbols", bootConfig, StringComparison.Ordinal);

            var runtimeLoader = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "_framework", "dotnet.runtime.js"));
            Assert.DoesNotContain("sourceMappingURL", runtimeLoader, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RejectsFlatPublishDirectoryWithoutFrameworkAssets()
    {
        var root = Path.Combine(Path.GetTempPath(), "workers-tests", Guid.NewGuid().ToString("N"));
        var publishDirectory = Path.Combine(root, "publish");
        var outputDirectory = Path.Combine(root, "dist");

        try
        {
            Directory.CreateDirectory(publishDirectory);
            await File.WriteAllTextAsync(Path.Combine(publishDirectory, "dotnet.native.wasm"), "wasm");

            await Assert.ThrowsAsync<DirectoryNotFoundException>(() => PublishBundleWriter.WriteAsync(new PublishBundleOptions(
                publishDirectory,
                typeof(HelloWorld.Worker).Assembly.Location,
                outputDirectory)));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RejectsInvalidCompatibilityDate()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => PublishBundleWriter.WriteAsync(new PublishBundleOptions(
            "publish",
            "worker.dll",
            "dist")
        {
            CompatibilityDate = "June 30 2026"
        }));
    }

    [Theory]
    [InlineData("../escaped.js")]
    [InlineData("..\\escaped.js")]
    [InlineData("nested/worker.js")]
    [InlineData("C:\\temp\\worker.js")]
    public async Task RejectsJavaScriptModulePathsOutsideTheBundleRoot(string javaScriptModule)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => PublishBundleWriter.WriteAsync(new PublishBundleOptions(
            "publish",
            "worker.dll",
            "dist")
        {
            JavaScriptModule = javaScriptModule
        }));
    }
}
