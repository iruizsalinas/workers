namespace Workers.Compiler.Tests;

public sealed class HtmlHandlerValueRegressionTests
{
    [Fact]
    public void HandlerInitializersAndAsyncExpressionReturnsCopyTupleValues()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public sealed class Handler : HtmlElementHandler
            {
                private (int, int) value = Worker.Source;
                private (int, int)[] values = [(3, 4)];
                public async Task<(int, int)> ReadAsync() => value;
                public async Task<(int, int)> ReadArrayAsync() => values[0];
                public void Change() { values[0].Item1 = 9; }
            }
            public static class Worker
            {
                public static (int, int) Source = (1, 2);
                [Fetch]
                public static async Task<Response> Fetch(Request request)
                {
                    var handler = new Handler();
                    Source.Item1 = 8;
                    var initial = await handler.ReadAsync();
                    var pending = handler.ReadArrayAsync();
                    handler.Change();
                    var captured = await pending;
                    return Response.Json(new { initial = initial.Item1, captured = captured.Item1 });
                }
            }
            """);

        Assert.Equal(1, result.GetProperty("initial").GetInt32());
        Assert.Equal(3, result.GetProperty("captured").GetInt32());
    }
}
