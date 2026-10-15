namespace Workers.Compiler.Tests;

public sealed class ByteMaterializationTests
{
    [Theory]
    [InlineData("new byte[] { 65, 66 }.Where(value => value > 0).ToArray()", "AB")]
    [InlineData("new List<byte> { 65, 66 }.ToArray()", "AB")]
    [InlineData("new Queue<byte>(new byte[] { 65, 66 }).ToArray()", "AB")]
    [InlineData("new Stack<byte>(new byte[] { 65, 66 }).ToArray()", "BA")]
    [InlineData("new byte[] { 65, 66 }.Chunk(1).First()", "A")]
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
