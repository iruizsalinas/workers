namespace Workers.Compiler.Tests;

public sealed class HtmlConstructorScopeRegressionTests
{
    [Fact]
    public void HtmlConstructorsCanDeclareExpressionOutLocals()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public sealed class Handler : HtmlElementHandler
            {
                public Handler() => Worker.Capture(out var ignored);
            }
            public static class Worker
            {
                private static int calls;
                public static void Capture(out int value) { calls++; value = 7; }
                [Fetch]
                public static Response Fetch(Request request)
                {
                    var handler = new Handler();
                    return Response.Json(new { calls });
                }
            }
            """);

        Assert.Equal(1, result.GetProperty("calls").GetInt32());
    }
}
