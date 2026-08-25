namespace Workers.Compiler.Tests;

public sealed class RuntimeValueTests
{
    [Fact]
    public void PreservesAsyncExpressionLambdasAndCompletedTasks()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    Func<Task<Response>> noArguments = async () => await BuildAsync("one");
                    Func<string, Task<Response>> oneArgument = async value => await BuildAsync(value);
                    Task completed = Task.CompletedTask;
                    context.WaitUntil(completed);
                    return Response.Text("ok");
                }

                private static Task<Response> BuildAsync(string value) => Task.FromResult(Response.Text(value));
            }
            """);

        Assert.Contains("async () => await", module);
        Assert.Contains("async value => await", module);
        Assert.Contains("let completed = Promise.resolve();", module);
        Assert.Contains("context.waitUntil(completed)", module);
    }

    [Fact]
    public void TreatsNativeUndefinedAsCSharpNull()
    {
        var module = Compile("""
            #nullable enable
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    string? value = request.Headers.Get("x-value");
                    return Response.Json(new { missing = value is null, present = value != null });
                }
            }
            """);

        Assert.Contains("missing: value == null", module);
        Assert.Contains("present: value != null", module);
    }

    [Fact]
    public void RejectsUnclassifiedFrameworkProperties()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    DateTimeOffset? value = DateTimeOffset.UtcNow;
                    return Response.Json(new { day = value?.Day });
                }
            }
            """));

        Assert.StartsWith("WRK105:", error.Message);
        Assert.Contains("System.DateTimeOffset.Day", error.Message);
    }

    [Fact]
    public void ErasesNullableSuppression()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    string? value = "present";
                    return Response.Text(value!);
                }
            }
            """);

        Assert.Contains("return new Response(value);", module);
    }

    [Fact]
    public void LowersExplicitConsoleGuidAndRandomBclIntrinsics()
    {
        var module = Compile("""
            using Workers;
            using System;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    Console.WriteLine("hello");
                    Console.Error.WriteLine("failure");
                    var id = Guid.NewGuid().ToString();
                    var whole = Random.Shared.Next(2, 8);
                    var fraction = Random.Shared.NextDouble();
                    return Response.Json(new { id, whole, fraction });
                }
            }
            """);

        Assert.Contains("console.log(\"hello\")", module);
        Assert.Contains("console.error(\"failure\")", module);
        Assert.Contains("globalThis.crypto.randomUUID()", module);
        Assert.Contains("$workers$randomNext(2, 8)", module);
        Assert.Contains("function $workers$randomNext(minimum, maximum)", module);
        Assert.Contains("Math.random()", module);
    }

    [Fact]
    public void RejectsConsoleCompositeFormatOverloads()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            using System;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    Console.WriteLine("value={0}", 42);
                    return Response.Text("ok");
                }
            }
            """));

        Assert.StartsWith("WRK105:", error.Message);
        Assert.Contains("System.Console.WriteLine", error.Message);
    }

}
