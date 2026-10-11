namespace Workers.Compiler.Tests;

public sealed class UnsupportedApiTests
{
    [Fact]
    public void RejectsDuplicateEventEntrypointsDeterministically()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class First
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context) => new Response("first");
            }
            public static class Second
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context) => new Response("second");
            }
            """));

        Assert.Equal("WRK111: Multiple 'fetch' event entrypoints are not supported.", error.Message);
    }

    [Fact]
    public void RejectsConflictingDefaultEntrypoints()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context) => new Response("ok");
            }
            [DefaultExport]
            public sealed class Rpc : WorkerEntrypoint
            {
                public string Ping() => "pong";
            }
            """));

        Assert.Equal("WRK112: Multiple default Worker exports are not supported.", error.Message);
    }

    [Fact]
    public void RejectsDuplicateClassExports()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            namespace State { public sealed class Gateway : DurableObject; }
            namespace Rpc { public sealed class Gateway : WorkerEntrypoint; }
            """));

        Assert.Equal("WRK113: Multiple Worker classes export the name 'Gateway'.", error.Message);
    }

    [Fact]
    public void RejectsInvalidEventSignatures()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public sealed class Worker
            {
                [Fetch]
                public string Fetch(Env environment) => "not a response";
            }
            """));

        Assert.Equal("WRK114: The 'fetch' event entrypoint has an invalid signature.", error.Message);
    }

    [Fact]
    public void RejectsMethodsThatCollapseToTheSameJavascriptName()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public sealed class Rpc : WorkerEntrypoint
            {
                public string Find() => "sync";
                public Task<string> FindAsync() => Task.FromResult("async");
            }
            """));

        Assert.Equal("WRK115: Multiple methods on 'Rpc' compile to 'find'.", error.Message);
    }

    [Fact]
    public void RejectsExplicitlyUnsupportedWorkersMethods()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var values = request.Headers.GetEnumerator();
                    return new Response("ok");
                }
            }
            """));

        Assert.StartsWith("WRK105:", error.Message);
        Assert.Contains("Workers.Headers.GetEnumerator", error.Message);

        var binding = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var values = new Url(request.Url).SearchParams.As<object>();
                    return new Response("ok");
                }
            }
            """));

        Assert.StartsWith("WRK119:", binding.Message);
    }
}
