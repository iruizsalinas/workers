namespace Workers.Compiler.Tests;

public sealed class ControlFlowTests
{
    [Fact]
    public void RethrowsTheActiveJavaScriptException()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    try { throw new Exception("failure"); }
                    catch { throw; }
                }
            }
            """);

        Assert.Contains("catch ($workers$exception)", module);
        Assert.Contains("throw $workers$exception;", module);
        Assert.DoesNotContain("throw;", module);
    }

    [Fact]
    public void RejectsMultipleOrSpecificCatchClauses()
    {
        var multiple = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    try { throw new InvalidOperationException(); }
                    catch (InvalidOperationException) { return Response.Text("specific"); }
                    catch (Exception) { return Response.Text("general"); }
                }
            }
            """));
        var specific = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    try { throw new InvalidOperationException(); }
                    catch (InvalidOperationException) { return Response.Text("specific"); }
                }
            }
            """));

        Assert.StartsWith("WRK108:", multiple.Message);
        Assert.StartsWith("WRK108:", specific.Message);
    }

    [Fact]
    public void LowersTryCatchSwitchThrowAndDoWhile()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var count = 0;
                    try
                    {
                        do { count = count + 1; } while (count < 1);
                        switch (request.Method)
                        {
                            case "GET": return Response.Text("ok");
                            default: throw new InvalidOperationException("bad method");
                        }
                    }
                    catch (Exception exception)
                    {
                        return Response.Text(exception.Message, 500);
                    }
                }
            }
            """);

        Assert.Contains("try {", module);
        Assert.Contains("do {", module);
        Assert.Contains("switch (request.method)", module);
        Assert.Contains("throw new Error(\"bad method\")", module);
        Assert.Contains("catch (exception)", module);
        Assert.Contains("exception.message", module);
    }
}
