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
                    var first = new MlDsaParams { Name = MlDsaAlgorithm.MlDsa44, Context = request.Path == "/" ? null : new byte[] { 1 } };
                    var second = new MlDsaParams { Name = MlDsaAlgorithm.MlDsa44, Context = request.Path != "/" ? new byte[] { 1 } : null };
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
                private static ISignatureAlgorithm Algorithm() { trace += "a"; return new HmacParams(); }
                private static byte[] Bytes(string name) { trace += name; return TextCodec.EncodeUtf8("payload"); }
                private static CryptoKey Key(CryptoKey key) { trace += "k"; return key; }
                private static SubtleCrypto Subtle() { trace += "r"; return Crypto.Subtle; }
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var key = await Crypto.Subtle.ImportKeyAsync(CryptoBinaryKeyFormat.Raw, TextCodec.EncodeUtf8("secret"),
                        new HmacKeyParams { Hash = DigestAlgorithm.Sha256 }, true,
                        [CryptoKeyUsage.Sign, CryptoKeyUsage.Verify]);
                    var signature = await Subtle().SignAsync(data: Bytes("d"), key: Key(key), algorithm: Algorithm());
                    var order = trace;
                    var valid = await Crypto.Subtle.VerifyAsync(data: Bytes("v"), signature: signature, key: key, algorithm: new HmacParams());
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
                        algorithm: new HmacKeyParams { Hash = DigestAlgorithm.Sha256 }, keyData: jwk);
                    var exported = await Crypto.Subtle.ExportJwkAsync(key);
                    return Response.Json(new
                    {
                        ops = exported.KeyOps, key.Type, hash = key.Algorithm.Hash,
                        secret = key.Type == CryptoKeyType.Secret, sha256 = key.Algorithm.Hash == DigestAlgorithm.Sha256
                    });
                }
            }
            """);

        Assert.Equal("secret", result.GetProperty("type").GetString());
        Assert.Equal("SHA-256", result.GetProperty("hash").GetString());
        Assert.True(result.GetProperty("secret").GetBoolean());
        Assert.True(result.GetProperty("sha256").GetBoolean());
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

    [Fact]
    public void ParameterTypesSupplyNativeAlgorithmNames()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) => Response.Json(new
                {
                    hmac = Json.Stringify(new HmacParams()),
                    legacy = Json.Stringify(new NodeEd25519Params()),
                    pss = Json.Stringify(new RsaPssParams { SaltLength = 32 }),
                    ec = Json.Stringify(new EcKeyParams { Name = EcAlgorithm.Ecdh, NamedCurve = EcNamedCurve.P521 }),
                    aes = Json.Stringify(new AesKeyParams { Name = AesAlgorithm.AesKw, Length = 256 }),
                    rsa = Json.Stringify(new RsaKeyImportParams { Name = RsaAlgorithm.RsassaPkcs1V15, Hash = DigestAlgorithm.Sha512 }),
                    kem = Json.Stringify(new MlKemParams { Name = MlKemAlgorithm.MlKem1024 }),
                    kdf = Json.Stringify(new Pbkdf2KeyParams())
                });
            }
            """);

        Assert.Equal("{\"name\":\"HMAC\"}", result.GetProperty("hmac").GetString());
        Assert.Equal("{\"name\":\"NODE-ED25519\",\"namedCurve\":\"NODE-ED25519\"}", result.GetProperty("legacy").GetString());
        Assert.Equal("{\"name\":\"RSA-PSS\",\"saltLength\":32}", result.GetProperty("pss").GetString());
        Assert.Equal("{\"name\":\"ECDH\",\"namedCurve\":\"P-521\"}", result.GetProperty("ec").GetString());
        Assert.Equal("{\"name\":\"AES-KW\",\"length\":256}", result.GetProperty("aes").GetString());
        Assert.Equal("{\"name\":\"RSASSA-PKCS1-v1_5\",\"hash\":\"SHA-512\"}", result.GetProperty("rsa").GetString());
        Assert.Equal("{\"name\":\"ML-KEM-1024\"}", result.GetProperty("kem").GetString());
        Assert.Equal("{\"name\":\"PBKDF2\"}", result.GetProperty("kdf").GetString());
    }

    // A single-algorithm type's Name is what the compiler injects, and every family type takes its name as an enum.
    [Fact]
    public void EveryAlgorithmTypeEmitsTheNameItDeclares()
    {
        var algorithms = typeof(global::Workers.ICryptoAlgorithm).Assembly.GetExportedTypes()
            .Where(type => type.IsClass && typeof(global::Workers.ICryptoAlgorithm).IsAssignableFrom(type));
        foreach (var type in algorithms)
        {
            var name = type.GetProperty("Name")!;
            var fixedProperties = global::JavaScriptEmitter.CryptoFixedProperties(type.FullName!).ToArray();
            if (name.PropertyType == typeof(string))
            {
                var instance = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
                Assert.Equal($"name: {System.Text.Json.JsonSerializer.Serialize(name.GetValue(instance))}", fixedProperties.FirstOrDefault());
            }
            else
            {
                Assert.True(name.PropertyType.IsEnum, type.FullName);
                Assert.Empty(fixedProperties);
            }
            Assert.True(global::BindingClassification.IsStructural(type.FullName!), type.FullName);
        }
    }

    [Fact]
    public void HmacHelpersSignAndVerifyWithTextAndByteKeys()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                private static string trace = "";
                private static byte[] Bytes(string name, string text) { trace += name; return TextCodec.EncodeUtf8(text); }
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var signature = await Crypto.SignHmacAsync(DigestAlgorithm.Sha256, "secret", Bytes("p", "payload"));
                    var bytes = await Crypto.SignHmacAsync(DigestAlgorithm.Sha1, Bytes("k", "secret"), Bytes("p", "payload"));
                    var valid = await Crypto.VerifyHmacAsync(DigestAlgorithm.Sha256, Bytes("k", "secret"), Bytes("s", "x").Length == 1 ? signature : [], Bytes("p", "payload"));
                    var invalid = await Crypto.VerifyHmacAsync(DigestAlgorithm.Sha256, "secret", signature, Bytes("p", "tampered"));
                    return Response.Json(new { hex = TextCodec.EncodeHex(signature), sha1 = TextCodec.EncodeHex(bytes), valid, invalid, trace });
                }
            }
            """);

        Assert.Equal("b82fcb791acec57859b989b430a826488ce2e479fdf92326bd0a2e8375a42ba4", result.GetProperty("hex").GetString());
        Assert.Equal(40, result.GetProperty("sha1").GetString()!.Length);
        Assert.True(result.GetProperty("valid").GetBoolean());
        Assert.False(result.GetProperty("invalid").GetBoolean());
        Assert.Equal("pkpkspp", result.GetProperty("trace").GetString());
    }
}
