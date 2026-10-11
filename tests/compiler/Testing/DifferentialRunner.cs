using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;

namespace Workers.Compiler.Tests;

// Run a pure C# Test.Run method on .NET and through the Workers translator. Platform API
// contracts remain covered by the Workers runtime suite; this checks language semantics.
internal static class DifferentialRunner
{
    public static void Equal(string source)
    {
        const string worker = """
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) => Response.Json(Test.Run());
            }
            """;
        var compilation = global::WorkerCompiler.CreateCompilation([CSharpSyntaxTree.ParseText(source)]);
        using var assembly = new MemoryStream();
        var result = compilation.Emit(assembly);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        assembly.Position = 0;
        var context = new AssemblyLoadContext("workers-differential", isCollectible: true);
        JsonElement expected;
        var isAsync = false;
        try
        {
            var method = context.LoadFromStream(assembly).GetType("Test")!
                .GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;
            var value = method.Invoke(null, null);
            if (value is Task task)
            {
                isAsync = true;
                task.GetAwaiter().GetResult();
                value = task.GetType().GetProperty("Result")!.GetValue(task);
            }
            expected = JsonSerializer.SerializeToElement(value);
        }
        finally
        {
            context.Unload();
        }
        var entrypoint = isAsync ? worker.Replace("static Response Fetch", "static async Task<Response> Fetch")
            .Replace("Response.Json(Test.Run())", "Response.Json(await Test.Run())") : worker;
        var actual = JavaScriptRunner.FetchJson(entrypoint + Environment.NewLine + source);
        Assert.True(JsonElement.DeepEquals(expected, actual), $".NET: {expected}{Environment.NewLine}JavaScript: {actual}");
    }
}
