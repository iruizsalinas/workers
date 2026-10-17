namespace Workers.Compiler.Tests;

public sealed class TypedBindingRegressionTests
{
    [Fact]
    public void RegularExpressionAdaptersEvaluateReceiverBeforeArguments()
    {
        var actual = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                private static string trace = "";
                private static string Pattern() { trace += "p"; return "a"; }
                private static string Text() { trace += "t"; return "a,a"; }
                private static string Replacement() { trace += "r"; return "x"; }
                [Fetch]
                public static Response Fetch(Request request) {
                    var replaced = new RegExp(Pattern(), "g").Replace(Text(), Replacement());
                    var replaceTrace = trace;
                    trace = "";
                    var split = new RegExp(Pattern()).Split(Text());
                    return Response.Json(new { replaced, replaceTrace, splitTrace = trace, parts = split.Length });
                }
            }
            """);

        Assert.Equal("x,x", actual.GetProperty("replaced").GetString());
        Assert.Equal("ptr", actual.GetProperty("replaceTrace").GetString());
        Assert.Equal("pt", actual.GetProperty("splitTrace").GetString());
        Assert.Equal(3, actual.GetProperty("parts").GetInt32());
    }

    [Fact]
    public void WebSocketMessageConversionsEvaluateTheirReceiverOnce()
    {
        var actual = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                private static int calls;
                private static WebSocketMessage Next() {
                    calls++;
                    return JsonValue.From("hello").As<WebSocketMessage>()!;
                }
                [Fetch]
                public static Response Fetch(Request request) {
                    var text = Next().AsText();
                    var textCalls = calls;
                    calls = 0;
                    var bytes = Next().AsBytes();
                    return Response.Json(new { text, textCalls, byteCalls = calls, byteLength = bytes.Length });
                }
            }
            """);

        Assert.Equal("hello", actual.GetProperty("text").GetString());
        Assert.Equal(1, actual.GetProperty("textCalls").GetInt32());
        Assert.Equal(1, actual.GetProperty("byteCalls").GetInt32());
        Assert.Equal(5, actual.GetProperty("byteLength").GetInt32());
    }

    [Theory]
    [InlineData("ExecutionContext")]
    [InlineData("DurableObjectState")]
    public void TypedPropsMaterializeWorkerClasses(string contextType)
    {
        var actual = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public sealed class Tenant
            {
                [JsonName("tenant_id")] public string Id { get; init; } = "default";
                public string Format() => "tenant:" + Id;
            }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request)
                {
                    var native = JsonValue.From(new { props = new { tenant_id = "acme" } }).As<{{contextType}}>();
                    return Response.Json(native!.Props<Tenant>().Format());
                }
            }
            """);
        Assert.Equal("tenant:acme", actual.GetString());
    }
}
