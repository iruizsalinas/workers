namespace Workers.Compiler.Tests;

public sealed class CompilerCommandTests
{
    [Fact]
    public void HelpDescribesTheLongFormCommandLine()
    {
        using var output = new StringWriter();

        var exitCode = global::CompilerCommand.Run(["--help"], output);

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", output.ToString());
        Assert.Contains("--project <path>", output.ToString());
        Assert.Contains("--output <path>", output.ToString());
        Assert.Contains("--sources <path>", output.ToString());
        Assert.Contains("--reference <path>", output.ToString());
        Assert.Contains("--define <symbols>", output.ToString());
        Assert.Contains("--version", output.ToString());
        Assert.DoesNotContain("  -o", output.ToString());
    }

    [Fact]
    public void VersionMatchesTheSharedProductVersion()
    {
        using var output = new StringWriter();
        var expected = typeof(global::Workers.Response).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
            .Single()
            .InformationalVersion
            .Split('+', 2)[0];

        var exitCode = global::CompilerCommand.Run(["--version"], output);

        Assert.Equal(0, exitCode);
        Assert.Equal(expected, output.ToString().Trim());
    }

    [Theory]
    [InlineData("--unknown", "value", "Unknown option '--unknown'.")]
    [InlineData("--project", "Missing value for --project.")]
    [InlineData("--project", "first", "--project", "second", "Option '--project' was specified more than once.")]
    public void InvalidOptionsProduceClearErrors(params string[] values)
    {
        var expected = values[^1];
        var arguments = values[..^1];

        var exception = Assert.Throws<ArgumentException>(() => global::CompilerOptions.Parse(arguments));

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public void InvalidCommandLineReturnsAUsageError()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = global::CompilerCommand.Run(["--output"], output, error);

        Assert.Equal(2, exitCode);
        Assert.Empty(output.ToString());
        Assert.Equal(
            $"error: Missing value for --output.{Environment.NewLine}Run 'Workers.Compiler --help' for usage.{Environment.NewLine}",
            error.ToString());
    }

    [Fact]
    public void SourceManifestControlsCompilationInputsAndSupportsLinkedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"workers-compiler-{Guid.NewGuid():N}");
        var project = Path.Combine(root, "project");
        var linked = Path.Combine(root, "linked");
        var output = Path.Combine(project, "dist", "worker.js");
        var sources = Path.Combine(project, "obj", "WorkerSources.txt");

        try
        {
            Directory.CreateDirectory(project);
            Directory.CreateDirectory(linked);
            Directory.CreateDirectory(Path.GetDirectoryName(sources)!);

            var worker = Path.Combine(linked, "Worker.cs");
            File.WriteAllText(worker, """
                using Workers;

                public static class Worker
                {
                    [Fetch]
                    public static Response Fetch(Request request, Env env, Context context) =>
                        Response.Text("linked");
                }
                """);
            File.WriteAllText(Path.Combine(project, "Excluded.cs"), "this is deliberately not valid C#");
            File.WriteAllLines(sources, [worker]);

            var exitCode = global::WorkerCompiler.Run(
            [
                "--project", project,
                "--sources", sources,
                "--reference", typeof(global::Workers.Response).Assembly.Location,
                "--output", output
            ]);

            Assert.Equal(0, exitCode);
            Assert.Contains("new Response(\"linked\")", File.ReadAllText(output));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(null, "'pg' is not installed")]
    [InlineData("8.1.0", "'pg' 8.1.0 is older than the supported 8.16.3")]
    public void ReportsMissingOrOutdatedNpmPackagesWithoutWritingTheWorker(string? installedVersion, string problem)
    {
        using var workspace = NpmWorkspace.Create(installedVersion);

        var (exitCode, error) = workspace.Compile();

        Assert.Equal(1, exitCode);
        Assert.Contains($"error WRK121: The Worker imports npm packages that Wrangler cannot resolve: {problem}. "
            + $"Run 'npm install pg@^8.16.3' in '{workspace.Project}'.", error);
        Assert.False(File.Exists(workspace.Output));
    }

    [Fact]
    public void ListsInstallArgumentsAndHoldsBackTheWorkerUntilPackagesResolve()
    {
        using var workspace = NpmWorkspace.Create(installedVersion: null);

        var (exitCode, _) = workspace.Compile(installArguments: true);

        Assert.Equal(0, exitCode);
        Assert.Equal(["--prefix", workspace.Project, "pg@^8.16.3"], File.ReadAllLines(workspace.InstallArguments));
        Assert.False(File.Exists(workspace.Output), "A Worker whose packages are not installed must not be deployable.");
    }

    [Fact]
    public void InstallsIntoTheNearestPackageJsonAboveTheWorker()
    {
        using var workspace = NpmWorkspace.Create(installedVersion: null);
        File.WriteAllText(Path.Combine(workspace.Root, "package.json"), "{}");

        workspace.Compile(installArguments: true);

        Assert.Equal(["--prefix", workspace.Root, "pg@^8.16.3"], File.ReadAllLines(workspace.InstallArguments));
    }

    [Fact]
    public void RefusesToInstallWhereTheWorkerCannotResolvePackages()
    {
        using var workspace = NpmWorkspace.Create(installedVersion: null, output: Path.Combine("..", "out", "worker.js"));

        var (exitCode, error) = workspace.Compile(installArguments: true);

        Assert.Equal(1, exitCode);
        Assert.Contains($"in '{Path.Combine(workspace.Root, "out")}' or a directory above it.", error);
        Assert.Empty(File.ReadAllLines(workspace.InstallArguments));
        Assert.False(File.Exists(workspace.Output));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WritesTheWorkerWhenInstalledPackagesSatisfyItsImports(bool installArguments)
    {
        // Installed in an ancestor, as in a workspace that shares node_modules.
        using var workspace = NpmWorkspace.Create(installedVersion: "8.23.1");

        var (exitCode, _) = workspace.Compile(installArguments);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(workspace.Output));
        if (installArguments)
            Assert.Empty(File.ReadAllLines(workspace.InstallArguments));
    }

    [Fact]
    public void WorkersWithoutNpmImportsNeedNoPackages()
    {
        Assert.Empty(global::NpmDependencies.Unsatisfied(["cloudflare:workers", "cloudflare:sockets"], Path.GetTempPath()));
    }

    private sealed class NpmWorkspace : IDisposable
    {
        private NpmWorkspace(string root, string output)
        {
            Root = root;
            Project = Path.Combine(root, "project");
            Output = Path.GetFullPath(output, Project);
            InstallArguments = Path.Combine(Project, "obj", "worker-npm-install.txt");
        }

        public string Root { get; }
        public string Project { get; }
        public string Output { get; }
        public string InstallArguments { get; }

        public static NpmWorkspace Create(string? installedVersion, string? output = null)
        {
            var workspace = new NpmWorkspace(
                Path.Combine(Path.GetTempPath(), $"workers-npm-{Guid.NewGuid():N}"),
                output ?? Path.Combine("dist", "worker.js"));
            Directory.CreateDirectory(workspace.Project);
            File.WriteAllText(Path.Combine(workspace.Project, "Worker.cs"), """
                using Workers;
                using System.Threading.Tasks;

                public static class Worker
                {
                    [Fetch]
                    public static async Task<Response> Fetch(Request request, Env env, Context context)
                    {
                        await using var db = await PostgresClient.ConnectAsync(env.Hyperdrive("HYPERDRIVE"));
                        return Response.Text("ok");
                    }
                }
                """);
            if (installedVersion is not null)
            {
                var package = Path.Combine(workspace.Root, "node_modules", "pg");
                Directory.CreateDirectory(package);
                File.WriteAllText(Path.Combine(package, "package.json"), $$"""{ "name": "pg", "version": "{{installedVersion}}" }""");
            }
            return workspace;
        }

        public (int ExitCode, string Error) Compile(bool installArguments = false)
        {
            using var error = new StringWriter();
            var exitCode = global::CompilerCommand.Run(
            [
                "--project", Project,
                "--reference", typeof(global::Workers.Response).Assembly.Location,
                "--output", Output,
                .. installArguments ? new[] { "--npm-install-arguments", InstallArguments } : []
            ], TextWriter.Null, error);
            return (exitCode, error.ToString());
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
