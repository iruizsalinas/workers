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
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    var total = int.MaxValue;
                    bool? missing = null;
                    total++;
                    await foreach (var value in Values()) total += value;
                    return new Response($"{total}:{true}:{missing}");
                }
                private static async IAsyncEnumerable<int> Values()
                {
                    await Task.CompletedTask;
                    yield return 1;
                }
            }
            """);

        Assert.Contains("for await (const value of", module);
        Assert.Contains("function $workers$cs$Worker$Values$0()", module);
        Assert.Contains("return { [Symbol.asyncIterator]: (async function* ()", module);
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
                public static Response Fetch(Request request, Env env, ExecutionContext context)
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

        Assert.Contains("function $workers$cs$Worker$Values$0()", module);
        Assert.Contains("return { [Symbol.iterator]: (function* ()", module);
        Assert.Contains("yield 1;", module);
        Assert.Contains("yield 2;", module);
    }

    [Fact]
    public void RejectsUsingResourcesWithoutAMappedDisposal()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using System;
            using Workers;
            public sealed class Lease : IDisposable
            {
                public void Dispose() { }
            }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context)
                {
                    using IDisposable lease = new Lease();
                    return new Response("ok");
                }
            }
            """));

        Assert.Contains("WRK108", error.Message);
    }

    [Theory]
    [InlineData("new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)")]
    [InlineData("new HashSet<string>(StringComparer.OrdinalIgnoreCase)")]
    public void RejectsCollectionComparersWithoutAJavaScriptEquivalent(string expression)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context)
                {
                    var value = {{expression}};
                    return new Response("ok");
                }
            }
            """));

        Assert.Contains("WRK105", error.Message);
    }
}
