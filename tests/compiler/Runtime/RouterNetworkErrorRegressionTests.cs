namespace Workers.Compiler.Tests;

public sealed class RouterNetworkErrorRegressionTests
{
    [Theory]
    [InlineData("GET", false)]
    [InlineData("GET", true)]
    [InlineData("HEAD", false)]
    [InlineData("HEAD", true)]
    public void RouterPreservesNativeNetworkErrorResponses(string method, bool cors)
    {
        var middleware = cors ? ".Use(Cors.Allow([\"https://app.test\"]))" : "";
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                [Fetch] public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    var router = new Router(){{middleware}}.Any("/", (request, route) => Response.Error());
                    var response = await router.HandleAsync(new Request(request.Url, new RequestInit { Method = "{{method}}" }), env, context);
                    return Response.Json(new { response.Status, response.Type });
                }
            }
            """);
        Assert.Equal(0, result.GetProperty("status").GetInt32());
        Assert.Equal("error", result.GetProperty("type").GetString());
    }
}
