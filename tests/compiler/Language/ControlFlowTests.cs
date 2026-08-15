namespace Workers.Compiler.Tests;

public sealed class ControlFlowTests
{
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
