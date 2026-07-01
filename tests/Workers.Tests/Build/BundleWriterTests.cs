using Workers.Build;
using Xunit;

namespace Workers.Tests;

public sealed class BundleWriterTests
{
    [Fact]
    public async Task WritesWorkerModuleAndManifest()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "workers-tests", Guid.NewGuid().ToString("N"));
        var manifest = new BuildManifest(
            "Example.dll",
            "worker.js",
            "Example.wasm",
            [
                new Entrypoint(EntrypointKind.Fetch, "Example.Worker", "FetchAsync")
            ]);

        try
        {
            var bundle = await BundleWriter.WriteAsync(
                manifest,
                new BundleOptions(outputDirectory));

            Assert.True(File.Exists(bundle.JavaScriptModulePath));
            Assert.True(File.Exists(bundle.RuntimeAdapterPath));
            Assert.True(File.Exists(bundle.ManifestPath));

            var module = await File.ReadAllTextAsync(bundle.JavaScriptModulePath);
            var adapter = await File.ReadAllTextAsync(bundle.RuntimeAdapterPath);
            var manifestJson = await File.ReadAllTextAsync(bundle.ManifestPath);

            Assert.Contains("async fetch(request, env, ctx)", module, StringComparison.Ordinal);
            Assert.Contains("import { dotnet } from './_framework/dotnet.js';", adapter, StringComparison.Ordinal);
            Assert.DoesNotContain("async scheduled(event, env, ctx)", adapter, StringComparison.Ordinal);
            Assert.DoesNotContain("async durableObjectFetch(exportName, state, request, env)", adapter, StringComparison.Ordinal);
            Assert.Contains("\"entryAssembly\": \"Example.dll\"", manifestJson, StringComparison.Ordinal);
            Assert.Contains("\"kind\": \"Fetch\"", manifestJson, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CanWriteCoreAdapterWithoutPlatformBindingApis()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "workers-tests", Guid.NewGuid().ToString("N"));
        var manifest = new BuildManifest(
            "Example.dll",
            "worker.js",
            "Example.wasm",
            [
                new Entrypoint(EntrypointKind.Fetch, "Example.Worker", "FetchAsync")
            ]);

        try
        {
            var bundle = await BundleWriter.WriteAsync(
                manifest,
                new BundleOptions(
                    outputDirectory,
                    RuntimeAdapterOptions: new RuntimeAdapterOptions { IncludePlatformApis = false }));

            var adapter = await File.ReadAllTextAsync(bundle.RuntimeAdapterPath);

            Assert.Contains("async fetch(request, env, ctx)", adapter, StringComparison.Ordinal);
            Assert.Contains("case 'runtime.console':", adapter, StringComparison.Ordinal);
            Assert.DoesNotContain("case 'kv.getText':", adapter, StringComparison.Ordinal);
            Assert.DoesNotContain("case 'queue.send':", adapter, StringComparison.Ordinal);
            Assert.DoesNotContain("async scheduled(event, env, ctx)", adapter, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("../escaped.js")]
    [InlineData("..\\escaped.js")]
    [InlineData("/tmp/worker.js")]
    [InlineData("nested/worker.js")]
    [InlineData(".")]
    [InlineData("..")]
    public async Task RejectsJavaScriptModulePathsOutsideTheBundleRoot(string javaScriptModule)
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "workers-tests", Guid.NewGuid().ToString("N"));
        var manifest = new BuildManifest(
            "Example.dll",
            javaScriptModule,
            "Example.wasm",
            [
                new Entrypoint(EntrypointKind.Fetch, "Example.Worker", "FetchAsync")
            ]);

        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() => BundleWriter.WriteAsync(
                manifest,
                new BundleOptions(outputDirectory)));
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }
}
