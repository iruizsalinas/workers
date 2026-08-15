namespace Workers.Compiler.Tests;

public sealed class RuntimeValueTests
{
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
        Assert.Contains("Math.floor(Math.random() * (8 - 2)) + 2", module);
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
