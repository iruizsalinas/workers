namespace Workers.Compiler.Tests;

public sealed class QueueTimestampRegressionTests
{
    [Theory]
    [InlineData("1234")]
    [InlineData("new Date(1234)")]
    public void QueueMetricsExposeTimestampsAsDates(string timestamp)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                private static int calls;
                private static QueueMetrics Metrics()
                {
                    calls++;
                    return JsonValue.From(new { oldestMessageTimestamp = {{timestamp}} }).As<QueueMetrics>()!;
                }
                [Fetch] public static Response Fetch(Request request)
                {
                    var time = Metrics().OldestMessageTimestamp!.GetTime();
                    var empty = Json.Parse("{}").As<QueueMetrics>()!.OldestMessageTimestamp;
                    return Response.Json(new { time, calls, empty });
                }
            }
            """);

        Assert.Equal(1234, result.GetProperty("time").GetDouble());
        Assert.Equal(1, result.GetProperty("calls").GetInt32());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, result.GetProperty("empty").ValueKind);
    }
}
