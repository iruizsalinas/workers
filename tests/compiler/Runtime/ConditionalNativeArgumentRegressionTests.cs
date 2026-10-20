namespace Workers.Compiler.Tests;

public sealed class ConditionalNativeArgumentRegressionTests
{
    [Theory]
    [InlineData("Response.Json(\"payload\", 200, absent ? null : new Headers())")]
    [InlineData("Response.Json(\"payload\", 200, null, absent ? null : \"custom\")")]
    public void ConditionalNullResponseOptionsUseNativeDefaults(string expression)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request)
                {
                    var absent = request.Url.Length > 0;
                    var response = {{expression}};
                    return Response.Json(response.StatusText);
                }
            }
            """);
        Assert.Equal("", result.GetString());
    }

    [Theory]
    [InlineData("Url.Parse(\"https://example.com/\", absent ? null : \"https://base.test/\") is not null")]
    [InlineData("Url.CanParse(\"https://example.com/\", absent ? null : \"https://base.test/\")")]
    [InlineData("new Url(\"https://example.com/\", absent ? null : \"https://base.test/\").Href == \"https://example.com/\"")]
    public void ConditionalNullBaseUsesTheNativeDefault(string expression)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request)
                {
                    var absent = request.Url.Length > 0;
                    return Response.Json({{expression}});
                }
            }
            """);
        Assert.True(result.GetBoolean());
    }

    [Theory]
    [InlineData("new Blob(\"payload\", absent ? null : \"text/plain\")")]
    [InlineData("new File(\"payload\", \"file.txt\", absent ? null : \"text/plain\")")]
    public void ConditionalNullMediaTypeUsesAnEmptyType(string expression)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request)
                {
                    var absent = request.Url.Length > 0;
                    var value = {{expression}};
                    return Response.Json(value.Type);
                }
            }
            """);
        Assert.Equal("", result.GetString());
    }
}
