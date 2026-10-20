namespace Workers.Compiler.Tests;

public sealed class Base64DefaultArgumentRegressionTests
{
    [Theory]
    [InlineData("Base64.Encode(bytes, omitPadding: true)")]
    [InlineData("Base64.Encode(bytes, default, true)")]
    [InlineData("Base64.Encode(bytes, new Defaults().Alphabet, true)")]
    [InlineData("Base64.Encode(bytes, (new Base64Alphabet[1])[0], true)")]
    public void DefaultAlphabetUsesItsNativeValue(string expression)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public sealed class Defaults { public Base64Alphabet Alphabet { get; set; } }
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request)
                {
                    byte[] bytes = [251];
                    return Response.Json(new {
                        omitted = {{expression}},
                        explicitDefault = Base64.Encode(bytes, Base64Alphabet.Base64, true),
                        url = Base64.Encode(bytes, Base64Alphabet.Base64Url, true)
                    });
                }
            }
            """);
        Assert.Equal("+w", result.GetProperty("omitted").GetString());
        Assert.Equal("+w", result.GetProperty("explicitDefault").GetString());
        Assert.Equal("-w", result.GetProperty("url").GetString());
    }

    [Fact]
    public void TypedJsonRecordDefaultsUseNativeEnumValues()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public sealed record Options(Base64Alphabet Alphabet = Base64Alphabet.Base64Url);
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request)
                {
                    var options = Json.Parse<Options>("{}")!;
                    return Response.Json(Base64.Encode(new byte[] { 251 }, options.Alphabet, true));
                }
            }
            """);
        Assert.Equal("-w", result.GetString());
    }
}
