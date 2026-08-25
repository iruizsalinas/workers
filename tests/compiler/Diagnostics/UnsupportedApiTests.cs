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
                public static Response Fetch(Request request, Env env, Context context) => Response.Text("first");
            }
            public static class Second
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) => Response.Text("second");
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
                public static Response Fetch(Request request, Env env, Context context) => Response.Text("ok");
            }
            [WorkerEntrypoint]
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
            [DurableObject("Gateway")]
            public sealed class State;
            [WorkerEntrypoint("Gateway")]
            public sealed class Rpc : WorkerEntrypoint;
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
            [WorkerEntrypoint("Rpc")]
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
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    var values = request.QueryParameters.As<object>();
                    return Response.Text("ok");
                }
            }
            """));

        Assert.StartsWith("WRK105:", error.Message);
        Assert.Contains("Workers.QueryParameters.As", error.Message);
    }
}
