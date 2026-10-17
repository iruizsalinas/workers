namespace Workers.Compiler.Tests;

public sealed class ByteMaterializationTests
{
    [Fact]
    public void TypedBodyReadsAndNestedJsonUseNativeByteBuffers()
    {
        var actual = JavaScriptRunner.FetchJson("""
            using Workers;
            public sealed record Payload(byte[] Data);
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request)
                {
                    var response = await new Response("[65,66]").JsonAsync<byte[]>();
                    var input = new Request("https://worker.test/", new RequestInit { Method = "POST", Body = "[67,68]" });
                    var body = await input.JsonAsync<byte[]>();
                    var nested = Json.Parse<Payload>("{\"data\":[69,70]}");
                    var original = new byte[] { 71, 72 };
                    var reused = JsonValue.From(original).As<byte[]>();
                    return Response.Json(new {
                        response = new TextDecoder().Decode(response!),
                        body = new TextDecoder().Decode(body!),
                        nested = new TextDecoder().Decode(nested!.Data),
                        sameBuffer = original == reused
                    });
                }
            }
            """);
        Assert.Equal("AB", actual.GetProperty("response").GetString());
        Assert.Equal("CD", actual.GetProperty("body").GetString());
        Assert.Equal("EF", actual.GetProperty("nested").GetString());
        Assert.True(actual.GetProperty("sameBuffer").GetBoolean());
    }

    [Theory]
    [InlineData("new byte[] { 65, 66 }.Where(value => value > 0).ToArray()", "AB")]
    [InlineData("new List<byte> { 65, 66 }.ToArray()", "AB")]
    [InlineData("new Queue<byte>(new byte[] { 65, 66 }).ToArray()", "AB")]
    [InlineData("new Stack<byte>(new byte[] { 65, 66 }).ToArray()", "BA")]
    [InlineData("new byte[] { 65, 66 }.Chunk(1).First()", "A")]
    [InlineData("Json.Parse<byte[]>(\"[65,66]\")!", "AB")]
    [InlineData("JsonValue.From(new int[] { 65, 66 }).As<byte[]>()!", "AB")]
    public void MaterializedByteArraysWorkWithNativeByteApis(string expression, string expected)
    {
        var actual = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request)
                {
                    byte[] bytes = {{expression}};
                    return Response.Json(new TextDecoder().Decode(bytes));
                }
            }
            """);
        Assert.Equal(expected, actual.GetString());
    }
}
