namespace Workers.Compiler.Tests;

public sealed class HmacBufferRegressionTests
{
    [Fact]
    public void SigningReadsThePayloadWhenTheOperationStarts()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch] public static async Task<Response> Fetch(Request request)
                {
                    var payload = new TextEncoder().Encode("payload");
                    var expected = await Crypto.SignHmacAsync(DigestAlgorithm.Sha256, "secret", payload);
                    var pending = Crypto.SignHmacAsync(DigestAlgorithm.Sha256, "secret", payload);
                    payload[0] = 0;
                    return Response.Json(Hex.Encode(await pending) == Hex.Encode(expected));
                }
            }
            """);
        Assert.True(result.GetBoolean());
    }

    [Theory]
    [InlineData("payload[0] = 0;")]
    [InlineData("signature[0] = 0;")]
    public void VerificationReadsItsBuffersWhenTheOperationStarts(string mutation)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                [Fetch] public static async Task<Response> Fetch(Request request)
                {
                    var payload = new TextEncoder().Encode("payload");
                    var signature = await Crypto.SignHmacAsync(DigestAlgorithm.Sha256, "secret", payload);
                    var pending = Crypto.VerifyHmacAsync(DigestAlgorithm.Sha256, "secret", signature, payload);
                    {{mutation}}
                    return Response.Json(await pending);
                }
            }
            """);
        Assert.True(result.GetBoolean());
    }
}
