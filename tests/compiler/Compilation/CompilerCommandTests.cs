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
}
