using System.Diagnostics;
using System.IO.Compression;
using Xunit;

namespace Workers.Package.Tests;

public sealed class PackageTests
{
    [Fact]
    public void Package_publishes_a_consumer_and_never_leaves_a_stale_worker()
    {
        using var workspace = TemporaryDirectory.Create();
        var repository = FindRepository();

        var packageDirectory = Environment.GetEnvironmentVariable("WORKERS_PACKAGE_DIRECTORY");
        if (string.IsNullOrEmpty(packageDirectory))
        {
            RunDotNet(repository, "pack", "src/Workers.csproj", "-c", "Release", "-o", workspace.Path);
        }
        else
        {
            var artifact = Directory.GetFiles(packageDirectory, "Workers.*.nupkg").Single();
            Directory.CreateDirectory(workspace.Path);
            File.Copy(artifact, Path.Combine(workspace.Path, Path.GetFileName(artifact)));
        }

        var package = Directory.GetFiles(workspace.Path, "Workers.*.nupkg").Single();
        using (var archive = ZipFile.OpenRead(package))
        {
            var entries = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.Ordinal);
            Assert.Contains("buildTransitive/Workers.targets", entries);
            Assert.Contains("lib/net10.0/Workers.dll", entries);
            Assert.Contains("tools/net10.0/Workers.Compiler.dll", entries);
            Assert.Contains("tools/net10.0/Workers.Compiler.deps.json", entries);
            Assert.Contains("tools/net10.0/Workers.Compiler.runtimeconfig.json", entries);
            Assert.Contains("tools/net10.0/Microsoft.CodeAnalysis.dll", entries);
            Assert.Contains("tools/net10.0/Microsoft.CodeAnalysis.CSharp.dll", entries);
            Assert.DoesNotContain(entries, entry => entry.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase));
        }

        var consumer = Directory.CreateDirectory(Path.Combine(workspace.Path, "consumer")).FullName;
        File.WriteAllText(Path.Combine(consumer, "Consumer.csproj"), Project(package, workspace.Path));
        File.WriteAllText(Path.Combine(consumer, "Worker.cs"), Source);
        File.WriteAllText(Path.Combine(consumer, ".editorconfig"), """
            root = true
            [*.cs]
            dotnet_diagnostic.CS0168.severity = none
            """);

        var packages = Path.Combine(workspace.Path, "packages");
        RunDotNet(consumer, "publish", "-c", "Release", "--packages", packages);

        var module = Path.Combine(consumer, "dist", "worker.js");
        Assert.True(File.Exists(module));
        Assert.Contains("Hello from package", File.ReadAllText(module), StringComparison.Ordinal);
        Assert.Contains("$workers$notNull($workers$receiver).generateKey", File.ReadAllText(module), StringComparison.Ordinal);
        Assert.Contains(")(globalThis.crypto.subtle,", File.ReadAllText(module), StringComparison.Ordinal);

        var compilerArguments = Directory.GetFiles(Path.Combine(consumer, "obj"), "worker-compiler-arguments.rsp", SearchOption.AllDirectories).Single();
        File.Delete(compilerArguments);
        RunDotNet(consumer, "build", "-c", "Release", "--packages", packages);
        Assert.True(File.Exists(compilerArguments), "An incremental build must recover a missing compiler argument manifest.");
        Assert.True(File.Exists(module));

        File.WriteAllText(Path.Combine(consumer, "Worker.cs"), Source.Replace("new Response", "new MissingResponse", StringComparison.Ordinal));
        RunDotNetFailure(consumer, "build", "-c", "Release", "--packages", packages);
        Assert.False(File.Exists(module), "A failed compilation must not leave the previous Worker artifact deployable.");

        File.WriteAllText(Path.Combine(consumer, "Worker.cs"), Source);
        RunDotNet(consumer, "build", "-c", "Release", "--packages", packages);
        Assert.True(File.Exists(module));
        RunDotNet(consumer, "clean", "-c", "Release");
        Assert.False(File.Exists(module), "Cleaning the consumer must remove its generated Worker artifact.");
    }

    private static string Project(string package, string source) => $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
            <RestoreSources>{{source}}</RestoreSources>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Workers" Version="{{Path.GetFileNameWithoutExtension(package)[8..]}}" />
          </ItemGroup>
        </Project>
        """;

    private const string Source = """
        using Workers;

        public static class Worker
        {
            [Fetch]
            public static async Task<Response> FetchAsync(Request request, Env environment, ExecutionContext context)
            {
                int unused;
                var key = await Crypto.Subtle.GenerateKeyAsync(
                    new AesKeyParams { Name = AesAlgorithm.AesGcm, Length = 256 },
                    false, [CryptoKeyUsage.Encrypt, CryptoKeyUsage.Decrypt]);
                return new Response("Hello from package") { Headers = { ["x-key-type"] = key.Algorithm.Name } };
            }
        }
        """;

    private static string FindRepository()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Workers.slnx")))
                return directory.FullName;

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static void RunDotNet(string directory, params string[] arguments)
    {
        var (exitCode, output) = ExecuteDotNet(directory, arguments);
        Assert.True(exitCode == 0, $"dotnet {string.Join(' ', arguments)} failed:{Environment.NewLine}{output}");
    }

    private static void RunDotNetFailure(string directory, params string[] arguments)
    {
        var (exitCode, output) = ExecuteDotNet(directory, arguments);
        Assert.True(exitCode != 0, $"dotnet {string.Join(' ', arguments)} unexpectedly succeeded:{Environment.NewLine}{output}");
    }

    private static (int ExitCode, string Output) ExecuteDotNet(string directory, params string[] arguments) =>
        Execute("dotnet", directory, arguments);

    private static (int ExitCode, string Output) Execute(string fileName, string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = directory,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {fileName}.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, output + error);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create() =>
            new(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"workers-package-{Guid.NewGuid():N}"));

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
