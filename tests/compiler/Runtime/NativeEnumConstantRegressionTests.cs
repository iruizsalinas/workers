namespace Workers.Compiler.Tests;

public sealed class NativeEnumConstantRegressionTests
{
    [Theory]
    [InlineData("0", "+w")]
    [InlineData("(Base64Alphabet)0", "+w")]
    [InlineData("(Base64Alphabet)1", "-w")]
    public void ConvertedConstantsUseTheNativeEnumValue(string alphabet, string expected)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request) =>
                    Response.Json(Base64.Encode(new byte[] { 251 }, {{alphabet}}, true));
            }
            """);
        Assert.Equal(expected, result.GetString());
    }

    [Fact]
    public void ConstantAliasesKeepTheirNativeEnumRepresentation()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public enum LocalKind { First, Second }
            public static class Worker
            {
                private const Base64Alphabet Alphabet = Base64Alphabet.Base64Url;
                private const DigestAlgorithm Algorithm = DigestAlgorithm.Sha256;
                private const LocalKind Kind = LocalKind.Second;
                [Fetch] public static Response Fetch(Request request)
                {
                    byte[] bytes = [251];
                    return Response.Json(new {
                        alphabet = Alphabet,
                        algorithm = Algorithm,
                        kind = Kind,
                        encoded = Base64.Encode(bytes, Alphabet, true)
                    });
                }
            }
            """);
        Assert.Equal("base64url", result.GetProperty("alphabet").GetString());
        Assert.Equal("SHA-256", result.GetProperty("algorithm").GetString());
        Assert.Equal(1, result.GetProperty("kind").GetInt32());
        Assert.Equal("-w", result.GetProperty("encoded").GetString());
    }
}
