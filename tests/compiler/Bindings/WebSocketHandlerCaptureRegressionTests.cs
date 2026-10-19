namespace Workers.Compiler.Tests;

public sealed class WebSocketHandlerCaptureRegressionTests
{
    [Theory]
    [InlineData("Action<WebSocketMessage>", "")]
    [InlineData("Func<WebSocketMessage, Task>", "return Task.CompletedTask;")]
    public void MessageRegistrationCapturesTheCurrentHandler(string handlerType, string completion)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request)
                {
                    Action<JsonValue>? dispatch = null;
                    var socket = JsonValue.From(new
                    {
                        addEventListener = (Action<string, Action<JsonValue>>)((name, listener) => dispatch = listener)
                    }).As<WebSocket>()!;
                    string trace = "";
                    {{handlerType}} handler = message => { trace += "original:" + message.AsText() + ";"; {{completion}} };
                    socket.OnMessage(handler);
                    handler = message => { trace += "replacement;"; {{completion}} };
                    dispatch!(JsonValue.From(new { data = "hello" }));
                    dispatch!(JsonValue.From(new { data = "again" }));
                    return Response.Json(trace);
                }
            }
            """);
        Assert.Equal("original:hello;original:again;", result.GetString());
    }
}
