namespace Workers.Compiler.Tests;

public sealed class ControlFlowTests
{
    [Fact]
    public void TypedCatchVariablesCanBeReassigned() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                try { throw new System.InvalidOperationException("original"); }
                catch (System.InvalidOperationException error)
                {
                    error = new System.InvalidOperationException("changed");
                    return error.Message;
                }
            }
        }
        """);

    [Fact]
    public void ExceptionFiltersPreserveAssignmentsToTheCatchVariable() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                try { throw new System.Exception("original"); }
                catch (System.Exception error) when (Accept(error = new System.Exception("changed")))
                {
                    return error.Message;
                }
            }
            private static bool Accept(System.Exception error) => true;
        }
        """);

    [Theory]
    [InlineData("System.Exception")]
    [InlineData("System.InvalidOperationException")]
    public void RethrowKeepsTheOriginalExceptionAfterReassigningTheCatchVariable(string catchType) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            public static object Run()
            {
                try
                {
                    try { throw new System.InvalidOperationException("original"); }
                    catch ({{catchType}} error)
                    {
                        error = new System.InvalidOperationException("changed");
                        throw;
                    }
                }
                catch (System.Exception error) { return error.Message; }
            }
        }
        """);

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
