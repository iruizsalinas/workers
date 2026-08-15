namespace Workers.Compiler.Tests;

public sealed class UnsupportedApiTests
{
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
