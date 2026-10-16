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
                public static Response Fetch(Request request, Env env, ExecutionContext context)
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
    public void SupportsMultipleAndSpecificCatchClauses()
    {
        var multiple = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context)
                {
                    try { throw new InvalidOperationException(); }
                    catch (InvalidOperationException) { return new Response("specific"); }
                    catch (Exception) { return new Response("general"); }
                }
            }
            """);
        var specific = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context)
                {
                    try { throw new InvalidOperationException(); }
                    catch (InvalidOperationException) { return new Response("specific"); }
                }
            }
            """);

        Assert.Contains("instanceof ($workers$exceptionType(\"System.InvalidOperationException\"))", multiple);
        Assert.Contains("instanceof ($workers$exceptionType(\"System.InvalidOperationException\"))", specific);
    }

    [Fact]
    public void LowersTryCatchSwitchThrowAndDoWhile()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context)
                {
                    var count = 0;
                    try
                    {
                        do { count = count + 1; } while (count < 1);
                        switch (request.Method)
                        {
                            case "GET": return new Response("ok");
                            default: throw new InvalidOperationException("bad method");
                        }
                    }
                    catch (Exception exception)
                    {
                        return new Response(exception.Message) { Status = 500 };
                    }
                }
            }
            """);

        Assert.Contains("try {", module);
        Assert.Contains("do {", module);
        Assert.Contains("switch ($workers$notNull(request).method)", module);
        Assert.Contains("$workers$exceptionType(\"System.InvalidOperationException\")", module);
        Assert.Contains("(\"bad method\")", module);
        Assert.Contains("catch (exception)", module);
        Assert.Contains("$workers$notNull(exception).message", module);
    }
}
