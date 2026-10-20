namespace Workers.Compiler.Tests;

public sealed class OptionalWebArgumentRegressionTests
{
    [Fact]
    public void NullQueryValueMeansNoValueFilter()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request)
                {
                    var query = new UrlSearchParams("x=one&x=two");
                    var found = query.Has("x", null);
                    var conditional = new UrlSearchParams("x=one").Has("x", request.Url.Length > 0 ? null : "missing");
                    query.Delete("x", null);
                    var filtered = new UrlSearchParams("x=one&x=null");
                    filtered.Delete("x", "null");
                    return Response.Json(new { found, conditional, remaining = query.Size, filtered = filtered.ToString() });
                }
            }
            """);
        Assert.True(result.GetProperty("found").GetBoolean());
        Assert.True(result.GetProperty("conditional").GetBoolean());
        Assert.Equal(0, result.GetProperty("remaining").GetInt32());
        Assert.Equal("x=one", result.GetProperty("filtered").GetString());
    }

    [Fact]
    public void NullBlobEndUsesTheRestOfTheBody()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch] public static async Task<Response> Fetch(Request request)
                {
                    var blob = new Blob("payload");
                    var slice = blob.Slice(1, null, null);
                    return Response.Json(new { text = await slice.TextAsync(), slice.Type, empty = blob.Slice(0, 0).Size });
                }
            }
            """);
        Assert.Equal("ayload", result.GetProperty("text").GetString());
        Assert.Equal("", result.GetProperty("type").GetString());
        Assert.Equal(0, result.GetProperty("empty").GetInt64());
    }

    [Theory]
    [InlineData("Append")]
    [InlineData("Set")]
    public void NullFormFilenamePreservesTheFileName(string method)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request)
                {
                    var form = new FormData();
                    form.{{method}}("file", new File("payload", "kept.txt"), null);
                    return Response.Json(form.Get("file")!.File!.Name);
                }
            }
            """);
        Assert.Equal("kept.txt", result.GetString());
    }
}
