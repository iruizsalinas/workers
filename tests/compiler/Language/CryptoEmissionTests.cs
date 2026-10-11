namespace Workers.Compiler.Tests;

public sealed class CryptoEmissionTests
{
    [Fact]
    public void OptionalParametersOmitNullFromEitherConditionalBranch()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var first = new CryptoSignAlgorithm { Name = "ML-DSA-44", Context = request.Path == "/" ? null : new byte[] { 1 } };
                    var second = new CryptoSignAlgorithm { Name = "ML-DSA-44", Context = request.Path != "/" ? new byte[] { 1 } : null };
                    return Response.Json(new { first = Json.Stringify(first), second = Json.Stringify(second) });
                }
            }
            """);
        Assert.Equal("{\"name\":\"ML-DSA-44\"}", result.GetProperty("first").GetString());
        Assert.Equal("{\"name\":\"ML-DSA-44\"}", result.GetProperty("second").GetString());
    }

    [Fact]
    public void StringAndCharacterLiteralsPreserveLoneSurrogatesForNativeEncoding()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                private const string Value = "\ud800";
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) => Response.Json(new
                {
                    literal = Json.Stringify("\ud800"), constant = Json.Stringify(Value), character = Json.Stringify('\ud800')
                });
            }
            """);
        foreach (var property in result.EnumerateObject())
            Assert.Equal("\"\\ud800\"", property.Value.GetString());
    }

    [Fact]
    public void NamedArgumentsPreserveEvaluationOrderAndNativeKeys()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                private static string trace = "";
                private static CryptoAlgorithm Algorithm() { trace += "a"; return new CryptoAlgorithm { Name = "HMAC" }; }
                private static byte[] Bytes(string name) { trace += name; return TextCodec.EncodeUtf8("payload"); }
                private static CryptoKey Key(CryptoKey key) { trace += "k"; return key; }
                private static SubtleCrypto Subtle() { trace += "r"; return Crypto.Subtle; }
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var key = await Crypto.Subtle.ImportKeyAsync(CryptoBinaryKeyFormat.Raw, TextCodec.EncodeUtf8("secret"),
                        new CryptoImportAlgorithm { Name = "HMAC", Hash = DigestAlgorithm.Sha256 }, true,
                        [CryptoKeyUsage.Sign, CryptoKeyUsage.Verify]);
                    var signature = await Subtle().SignAsync(data: Bytes("d"), key: Key(key), algorithm: Algorithm());
                    var order = trace;
                    var valid = await Crypto.Subtle.VerifyAsync(data: Bytes("v"), signature: signature, key: key, algorithm: new CryptoAlgorithm { Name = "HMAC" });
                    return Response.Json(new { order, valid, hex = TextCodec.EncodeHex(signature) });
                }
            }
            """);

        Assert.Equal("rdka", result.GetProperty("order").GetString());
        Assert.True(result.GetProperty("valid").GetBoolean());
        Assert.Equal("b82fcb791acec57859b989b430a826488ce2e479fdf92326bd0a2e8375a42ba4", result.GetProperty("hex").GetString());
    }

    [Fact]
    public void JwkInitializersAndReadsUseNativeFieldNames()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            using static Workers.CryptoKeyUsage;
            using static Workers.Crypto;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var jwk = new JsonWebKey { Kty = "oct", K = "c2VjcmV0", Alg = "HS256", Ext = true, KeyOps = [Sign, Verify] };
                    var key = await Subtle.ImportKeyAsync(keyUsages: [Sign, Verify], extractable: true,
                        algorithm: new CryptoImportAlgorithm { Name = "HMAC", Hash = DigestAlgorithm.Sha256 }, keyData: jwk);
                    var exported = await Crypto.Subtle.ExportJwkAsync(key);
                    return Response.Json(new { ops = exported.KeyOps, key.Type, hash = key.Algorithm.Hash!.Name });
                }
            }
            """);

        Assert.Equal("secret", result.GetProperty("type").GetString());
        Assert.Equal("SHA-256", result.GetProperty("hash").GetString());
        Assert.Equal(new[] { "sign", "verify" }, result.GetProperty("ops").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public void ByteResultsRemainByteArraysAcrossTypedBoundaries()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var bytes = await Crypto.DigestAsync(DigestAlgorithm.Sha256, "hello");
                    var second = await Crypto.Subtle.DigestAsync(DigestAlgorithm.Sha256, bytes);
                    return Response.Json(new { count = bytes.Length, first = bytes[0], hex = TextCodec.EncodeHex(second) });
                }
            }
            """);

        Assert.Equal(32, result.GetProperty("count").GetInt32());
        Assert.Equal(0x2c, result.GetProperty("first").GetInt32());
        Assert.Equal(64, result.GetProperty("hex").GetString()!.Length);
    }

    [Theory]
    [InlineData("CryptoSecretKeyAlgorithm", "Ed25519", "Sign")]
    [InlineData("CryptoKeyPairAlgorithm", "HMAC", "Verify")]
    public void KeyGenerationRejectsAlgorithmsFromTheWrongReturnFamily(string options, string name, string usage)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    try
                    {
                        await Crypto.Subtle.GenerateKeyAsync(new {{options}} { Name = "{{name}}", Hash = DigestAlgorithm.Sha256 }, false, [CryptoKeyUsage.{{usage}}]);
                        return Response.Json(new { rejected = false });
                    }
                    catch (Exception error) { return Response.Json(new { rejected = true, message = error.Message }); }
                }
            }
            """);
        Assert.True(result.GetProperty("rejected").GetBoolean());
        Assert.Contains("must generate", result.GetProperty("message").GetString());
    }
}
