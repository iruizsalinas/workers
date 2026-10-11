namespace Workers.Compiler.Tests;

public sealed class CompilerCommandTests
{
    [Theory]
    [InlineData("/checked+", "return Response.Json(Add(1));", "WRK108")]
    [InlineData("/nullable:enable /warnaserror+", "string value = 1; return new Response(value);", "CS0029")]
    [InlineData("/nullable:enable /warnaserror+", "string value = null; return new Response(value);", "", 0)]
    [InlineData("/langversion:9", "int[] values = [1]; return Response.Json(values);", "error CS")]
    public void CompilerArgumentsPreserveProjectSemantics(string flags, string body, string diagnostic, int expectedCode = 1)
    {
        var root = Directory.CreateTempSubdirectory("workers compiler ");
        try
        {
            var arguments = Path.Combine(root.FullName, "compiler.rsp");
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Append(typeof(global::Workers.Response).Assembly.Location);
            System.IO.File.WriteAllLines(arguments, references.Select(path => $"/reference:\"{path}\"")
                .Prepend(flags).Prepend("Worker.cs").Prepend("/target:library"));
            System.IO.File.WriteAllText(Path.Combine(root.FullName, "Worker.cs"), $$"""
                using Workers;
                public static class Worker
                {
                    [Fetch]
                    public static Response Fetch(Request request, Env env, ExecutionContext context) { {{body}} }
                    private static int Add(int value) => value + 1;
                }
                """);
            using var error = new StringWriter();
            var output = Path.Combine(root.FullName, "worker.js");
            var code = global::CompilerCommand.Run([
                "--project", root.FullName, "--output", output, "--compiler-arguments", arguments
            ], error: error);
            Assert.Equal(expectedCode, code);
            if (expectedCode == 1)
            {
                Assert.Contains(diagnostic, error.ToString());
                Assert.False(System.IO.File.Exists(output));
            }
            else
            {
                Assert.Empty(error.ToString());
                Assert.True(System.IO.File.Exists(output));
            }
        }
        finally { root.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("Dependency.Value", 0, "Response.json(42)")]
    [InlineData("Dependency.Get()", 1, "WRK105")]
    [InlineData("new Dependency()", 1, "WRK105")]
    public void CompilerArgumentsUseProjectReferencesAndPreprocessorSymbols(string expression, int expectedCode, string expected)
    {
        var root = Directory.CreateTempSubdirectory("workers compiler ");
        try
        {
            var dependency = Path.Combine(root.FullName, "Dependency.dll");
            var compilation = global::WorkerCompiler.CreateCompilation([
                Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("public sealed class Dependency { public const int Value = 42; public static int Get() => 42; }")
            ]);
            using (var file = System.IO.File.Create(dependency)) Assert.True(compilation.Emit(file).Success);
            var arguments = Path.Combine(root.FullName, "compiler.rsp");
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Append(typeof(global::Workers.Response).Assembly.Location).Append(dependency);
            System.IO.File.WriteAllLines(arguments, references.Select(path => $"/reference:\"{path}\"")
                .Prepend("Worker.cs").Prepend("/target:library /define:PROJECT_CONFIGURATION"));
            System.IO.File.WriteAllText(Path.Combine(root.FullName, "Worker.cs"), $$"""
                using Workers;
                public static class Worker
                {
                    [Fetch]
                    public static Response Fetch(Request request, Env env, ExecutionContext context)
                    {
                #if PROJECT_CONFIGURATION
                        return Response.Json({{expression}});
                #else
                        return MissingConfiguration;
                #endif
                    }
                }
                """);
            using var error = new StringWriter();
            var output = Path.Combine(root.FullName, "worker.js");
            var code = global::CompilerCommand.Run([
                "--project", root.FullName, "--output", output, "--compiler-arguments", arguments
            ], error: error);
            Assert.Equal(expectedCode, code);
            if (expectedCode == 0)
            {
                Assert.Empty(error.ToString());
                Assert.Contains(expected, System.IO.File.ReadAllText(output));
            }
            else
            {
                Assert.Contains(expected, error.ToString());
                Assert.DoesNotContain("error CS", error.ToString());
                Assert.False(System.IO.File.Exists(output));
            }
        }
        finally { root.Delete(recursive: true); }
    }

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
            System.IO.File.WriteAllText(worker, """
                using Workers;

                public static class Worker
                {
                    [Fetch]
                    public static Response Fetch(Request request, Env env, ExecutionContext context) =>
                        new Response("linked");
                }
                """);
            System.IO.File.WriteAllText(Path.Combine(project, "Excluded.cs"), "this is deliberately not valid C#");
            System.IO.File.WriteAllLines(sources, [worker]);

            var exitCode = global::WorkerCompiler.Run(
            [
                "--project", project,
                "--sources", sources,
                "--reference", typeof(global::Workers.Response).Assembly.Location,
                "--output", output
            ]);

            Assert.Equal(0, exitCode);
            Assert.Contains("new Response(\"linked\")", System.IO.File.ReadAllText(output));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
