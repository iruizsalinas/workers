namespace Workers.Compiler.Tests;

public sealed class RuntimeInterfaceRegressionTests
{
    [Fact]
    public void HtmlHandlersSupportSourceInterfaceCallsAndTypeTests()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public interface ILabel {
                string Label();
                string Title => "default title";
                int GetCounter();
                void SetCounter(int value);
                int Counter { get => GetCounter(); set => SetCounter(value); }
                int this[int offset] { get => GetCounter() + offset; set => SetCounter(value - offset); }
            }
            public sealed class Handler : HtmlElementHandler, ILabel
            {
                private int _counter;
                public string Label() => "handler";
                public int GetCounter() => _counter;
                public void SetCounter(int value) => _counter = value;
                internal int ReadInternal() => _counter;
            }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request) {
                    var handler = new Handler();
                    ILabel label = handler;
                    label.Counter = 3;
                    label.Counter += 2;
                    label[2] = 7;
                    label[2] += 2;
                    return Response.Json(new { value = label.Label(), title = label.Title, count = label.Counter, indexed = label[2], internalValue = handler.ReadInternal(), matches = handler is ILabel });
                }
            }
            """);

        Assert.Equal("handler", result.GetProperty("value").GetString());
        Assert.Equal("default title", result.GetProperty("title").GetString());
        Assert.Equal(7, result.GetProperty("count").GetInt32());
        Assert.Equal(9, result.GetProperty("indexed").GetInt32());
        Assert.Equal(7, result.GetProperty("internalValue").GetInt32());
        Assert.True(result.GetProperty("matches").GetBoolean());
    }
}
