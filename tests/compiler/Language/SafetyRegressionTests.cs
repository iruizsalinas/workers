namespace Workers.Compiler.Tests;

public sealed class SafetyRegressionTests
{
    [Fact]
    public void PreservesAwaitForeachAndIntegerMutationSemantics()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var total = int.MaxValue;
                    bool? missing = null;
                    total++;
                    await foreach (var value in Values()) total += value;
                    return Response.Text($"{total}:{true}:{missing}");
                }
                private static async IAsyncEnumerable<int> Values()
                {
                    await Task.CompletedTask;
                    yield return 1;
                }
            }
            """);

        Assert.Contains("for await (const value of", module);
        Assert.Contains("async function* $workers$cs$Worker$Values$0()", module);
        Assert.Contains("| 0", module);
        Assert.Contains("\"True\" : \"False\"", module);
    }

    [Fact]
    public void EmitsSynchronousIteratorMethodsAsGenerators()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var total = 0;
                    foreach (var value in Values()) total += value;
                    return Response.Json(new { total });
                }

                private static IEnumerable<int> Values()
                {
                    yield return 1;
                    yield return 2;
                }
            }
            """);

        Assert.Contains("function* $workers$cs$Worker$Values$0()", module);
        Assert.Contains("yield 1;", module);
        Assert.Contains("yield 2;", module);
    }

    [Fact]
    public void RejectsUsingUntilDisposalSemanticsAreImplemented()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    using var stream = new MemoryStream();
                    return Response.Text("ok");
                }
            }
            """));

        Assert.Contains("WRK108", error.Message);
    }

    [Theory]
    [InlineData("Regex.IsMatch(\"x\", \"x\", RegexOptions.IgnoreCase)")]
    [InlineData("new Uri(\"relative\", UriKind.Relative)")]
    [InlineData("new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)")]
    [InlineData("Convert.ToBase64String([1, 2, 3], 1, 2)")]
    [InlineData("System.Text.Encoding.Unicode.GetBytes(\"text\")")]
    public void RejectsOverloadsThatDoNotHaveEquivalentJavascriptSemantics(string expression)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            using System.Text.RegularExpressions;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var value = {{expression}};
                    return Response.Text("ok");
                }
            }
            """));

        Assert.Contains("WRK105", error.Message);
    }

    [Fact]
    public void RejectsInterpolationWhoseDotnetFormattingCannotBePreserved()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Text($"{DateTimeOffset.UtcNow}");
            }
            """));

        Assert.Contains("WRK108", error.Message);
    }
}
