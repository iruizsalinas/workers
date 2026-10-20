namespace Workers.Compiler.Tests;

public sealed class StreamPullRegressionTests
{
    [Fact]
    public void FromDoesNotAdvanceTheSequenceBeforeTheStreamIsRead()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                private static async IAsyncEnumerable<ReadOnlyMemory<byte>> Chunks(int[] visits)
                {
                    visits[0]++;
                    yield return new byte[] { 65 };
                    visits[0]++;
                    yield return new byte[] { 66 };
                }
                [Fetch] public static async Task<Response> Fetch(Request request)
                {
                    int[] visits = [0];
                    var stream = ReadableStream.From(Chunks(visits));
                    await Scheduler.WaitAsync(0);
                    var before = visits[0];
                    var reader = stream.GetReader();
                    var first = await reader.ReadAsync();
                    await reader.CancelAsync();
                    return Response.Json(new { before, after = visits[0], value = first.Value![0] });
                }
            }
            """);
        Assert.Equal(0, result.GetProperty("before").GetInt32());
        Assert.Equal(1, result.GetProperty("after").GetInt32());
        Assert.Equal(65, result.GetProperty("value").GetInt32());
    }
}
