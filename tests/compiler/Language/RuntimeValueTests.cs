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
                    DateTimeOffset? value = null;
                    return Response.Json(new { offset = value?.Offset });
                }
            }
            """));

        Assert.StartsWith("WRK105:", error.Message);
        Assert.Contains("System.DateTimeOffset.Offset", error.Message);
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
    public void UsesNativeLoggingUuidAndRandomOperations()
    {
        var module = Compile("""
            using Workers;
            using System;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    Log.Info("hello");
                    Log.Error("failure");
                    var id = Crypto.RandomUuid();
                    var whole = (int)Numbers.Floor(Numbers.Random() * 6) + 2;
                    var fraction = Numbers.Random();
                    return Response.Json(new { id, whole, fraction });
                }
            }
            """);

        Assert.Contains("console.info(\"hello\")", module);
        Assert.Contains("console.error(\"failure\")", module);
        Assert.Contains("globalThis.crypto.randomUUID()", module);
        Assert.Contains("Math.floor(Math.random() * 6)", module);
        Assert.Contains("Math.random()", module);
    }
}
