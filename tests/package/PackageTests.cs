using System.Diagnostics;
using System.IO.Compression;
using Xunit;

namespace Workers.Package.Tests;

public sealed class PackageTests
{
    [Fact]
    public void Package_builds_a_consumer_worker()
    {
        using var workspace = TemporaryDirectory.Create();
        var repository = FindRepository();

        RunDotNet(repository, "pack", "src/Workers.csproj", "-c", "Release", "--no-restore", "-o", workspace.Path);

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

        RunDotNet(consumer, "build", "-c", "Release", "--packages", Path.Combine(workspace.Path, "packages"));

        var module = Path.Combine(consumer, "dist", "worker.js");
        Assert.True(File.Exists(module));
        Assert.Contains("Hello from package", File.ReadAllText(module), StringComparison.Ordinal);
    }

    private static string Project(string package, string source) => $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
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
            public static Task<Response> FetchAsync(Request request, Env environment, Context context) =>
                Task.FromResult(Response.Text("Hello from package"));
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
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = directory,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"dotnet {string.Join(' ', arguments)} failed:{Environment.NewLine}{output}{error}");
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
