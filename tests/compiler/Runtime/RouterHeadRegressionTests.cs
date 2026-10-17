namespace Workers.Compiler.Tests;

public sealed class RouterHeadRegressionTests
{
    [Fact]
    public void HeadDoesNotWaitForTheOtherBranchOfATee()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    var router = new Router().Get("/", (request, route) =>
                        new Response(new Response("hello").Body!.Tee()[0]) { Status = 201 });
                    var response = await router.HandleAsync(new Request(request.Url, new RequestInit { Method = "HEAD" }), env, context);
                    return Response.Json(new { status = response.Status, body = await response.TextAsync() });
                }
            }
            """);

        Assert.Equal(201, result.GetProperty("status").GetInt32());
        Assert.Equal("", result.GetProperty("body").GetString());
    }

    [Fact]
    public void HeadPreservesResponseWhenBodyCancellationFails()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    var router = new Router().Get("/", (request, route) =>
                        new Response(ReadableStream.From(Chunks())) { Status = 201 });
                    var response = await router.HandleAsync(new Request(request.Url, new RequestInit { Method = "HEAD" }), env, context);
                    return Response.Json(new { status = response.Status, body = await response.TextAsync() });
                }
                private static async IAsyncEnumerable<ReadOnlyMemory<byte>> Chunks()
                {
                    try { yield return new byte[] { 1 }; }
                    finally { throw new Exception("cleanup failed"); }
                }
            }
            """);

        Assert.Equal(201, result.GetProperty("status").GetInt32());
        Assert.Equal("", result.GetProperty("body").GetString());
    }
}
