namespace Workers.Compiler.Tests;

public sealed class ByteParamsRegressionTests
{
    [Theory]
    [InlineData("Bytes(65, 66)", "AB")]
    [InlineData("Bytes()", "")]
    [InlineData("Identity<byte>(65, 66)", "AB")]
    [InlineData("Local(65, 66)", "AB")]
    [InlineData("read(65, 66)", "AB")]
    [InlineData("new Packet(65, 66).Bytes", "AB")]
    [InlineData("new ByteBatch(65, 66).Bytes", "AB")]
    [InlineData("prefix.Pack(65, 66)", "AB")]
    [InlineData("Bytes(new byte[] { 65, 66 })", "AB")]
    public void ExpandedByteParamsWorkWithNativeByteApis(string expression, string expected)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public delegate byte[] ByteReader(params byte[] bytes);
            public sealed class Packet(params byte[] bytes) { public byte[] Bytes => bytes; }
            public sealed record ByteBatch(params byte[] Bytes);
            public static class Extensions
            {
                public static byte[] Pack(this byte first, params byte[] bytes) => bytes;
            }
            public static class Worker
            {
                private static byte[] Bytes(params byte[] bytes) => bytes;
                private static T[] Identity<T>(params T[] values) => values;
                [Fetch]
                public static Response Fetch(Request request)
                {
                    byte[] Local(params byte[] bytes) => bytes;
                    ByteReader read = Bytes;
                    byte prefix = 65;
                    return Response.Json(new TextDecoder().Decode({{expression}}));
                }
            }
            """);
        Assert.Equal(expected, result.GetString());
    }
}
