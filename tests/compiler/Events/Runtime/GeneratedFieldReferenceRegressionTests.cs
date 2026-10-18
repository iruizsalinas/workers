namespace Workers.Compiler.Tests;

public sealed class GeneratedFieldReferenceRegressionTests
{
    [Theory]
    [InlineData("value", "value.Item1 = 9;")]
    [InlineData("value", "Worker.Set(out value);")]
    [InlineData("constructor", "constructor.Item1 = 9;")]
    public void HtmlHandlerFieldsSupportWritableReferences(string name, string mutation)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public sealed class Handler : HtmlElementHandler
            {
                private (int, int) {{name}} = (1, 2);
                public void Change() { {{mutation}} }
                public int Read() => {{name}}.Item1;
            }
            public static class Worker
            {
                public static void Set(out (int, int) value) { value = (9, 2); }
                [Fetch]
                public static Response Fetch(Request request)
                {
                    var handler = new Handler();
                    handler.Change();
                    return Response.Json(new { value = handler.Read() });
                }
            }
            """);

        Assert.Equal(9, result.GetProperty("value").GetInt32());
    }
}
