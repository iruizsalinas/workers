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
