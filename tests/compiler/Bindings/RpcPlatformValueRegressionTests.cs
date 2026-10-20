namespace Workers.Compiler.Tests;

public sealed class RpcPlatformValueRegressionTests
{
    [Theory]
    [InlineData("Blob", "new Blob(\"payload\")", "await data.TextAsync()")]
    [InlineData("Response", "new Response(\"payload\")", "await data.TextAsync()")]
    [InlineData("Headers", "new Headers { [\"x-value\"] = \"payload\" }", "data.Get(\"x-value\")")]
    public void AttachmentConversionPreservesPlatformObjectsInsideUserClasses(string type, string value, string read)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public sealed class Payload
            {
                public object Data { get; set; } = null!;
            }
            public static class Worker
            {
                [Fetch] public static async Task<Response> Fetch(Request request)
                {
                    object captured = null!;
                    var socket = JsonValue.From(new {
                        serializeAttachment = (Action<object>)(value => captured = value)
                    }).As<WebSocket>()!;
                    var payload = new Payload { Data = {{value}} };
                    socket.SerializeAttachment(payload);
                    var data = JsonValue.From(captured)["data"].As<{{type}}>()!;
                    return Response.Json(new {
                        text = {{read}},
                        converted = (object)payload != captured,
                        sameData = payload.Data == (object)data
                    });
                }
            }
            """);
        Assert.Equal("payload", result.GetProperty("text").GetString());
        Assert.True(result.GetProperty("converted").GetBoolean());
        Assert.True(result.GetProperty("sameData").GetBoolean());
    }
}
