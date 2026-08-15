namespace Workers.Compiler.Tests;

public sealed class CompilerCommandTests
{
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
                "--sources-file", sources,
                "--workers-reference", typeof(global::Workers.Response).Assembly.Location,
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
