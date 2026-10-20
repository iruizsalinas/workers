using Microsoft.CodeAnalysis;
using System.Text.Json;

internal sealed partial class JavaScriptEmitter
{
    private static string EmitCryptoDigest(string receiver, IMethodSymbol method,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        var algorithm = arguments[0].Value;
        var data = arguments[1].Value;
        return method.Parameters[1].Type.SpecialType == SpecialType.System_String
            ? $"{receiver}.subtle.digest({algorithm}, new TextEncoder().encode({data})).then(value => new Uint8Array(value))"
            : method.Parameters[1].Type.ToDisplayString() == "Workers.BodyInit"
                ? $"((crypto, algorithm, body) => new Response(body).arrayBuffer().then(value => crypto.subtle.digest(algorithm, value)).then(value => new Uint8Array(value)))({receiver}, {algorithm}, {data})"
                : $"{receiver}.subtle.digest({algorithm}, {data}).then(value => new Uint8Array(value))";
    }

    // Imports the raw key for the single operation; string keys are UTF-8 encoded first.
    private static string EmitCryptoHmac(string receiver, IMethodSymbol method,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments, bool verify)
    {
        var key = method.Parameters[1].Type.SpecialType == SpecialType.System_String
            ? $"new TextEncoder().encode({arguments[1].Value})"
            : arguments[1].Value;
        var import = $"crypto.subtle.importKey(\"raw\", raw, {{ name: \"HMAC\", hash }}, false, [\"{(verify ? "verify" : "sign")}\"])";
        // Key import is asynchronous; capture the bytes before callers can reuse their buffers.
        return verify
            ? $"((crypto, hash, raw, signature, payload) => {{ signature = signature.slice(); payload = payload.slice(); return {import}.then(key => crypto.subtle.verify(\"HMAC\", key, signature, payload)); }})"
              + $"({receiver}, {arguments[0].Value}, {key}, {arguments[2].Value}, {arguments[3].Value})"
            : $"((crypto, hash, raw, payload) => {{ payload = payload.slice(); return {import}.then(key => crypto.subtle.sign(\"HMAC\", key, payload)).then(value => new Uint8Array(value)); }})"
              + $"({receiver}, {arguments[0].Value}, {key}, {arguments[2].Value})";
    }

    // Parameter types that stand for one algorithm carry its name, which C# callers never write.
    internal static IEnumerable<string> CryptoFixedProperties(string type)
    {
        var name = type switch
        {
            "Workers.AesGcmParams" => "AES-GCM",
            "Workers.AesCbcParams" => "AES-CBC",
            "Workers.AesCtrParams" => "AES-CTR",
            "Workers.AesKwParams" => "AES-KW",
            "Workers.RsaOaepParams" => "RSA-OAEP",
            "Workers.HmacParams" or "Workers.HmacKeyParams" => "HMAC",
            "Workers.RsaPkcs1Params" => "RSASSA-PKCS1-v1_5",
            "Workers.RsaPssParams" => "RSA-PSS",
            "Workers.EcdsaParams" => "ECDSA",
            "Workers.Ed25519Params" => "Ed25519",
            "Workers.NodeEd25519Params" => "NODE-ED25519",
            "Workers.X25519Params" => "X25519",
            "Workers.Pbkdf2Params" or "Workers.Pbkdf2KeyParams" => "PBKDF2",
            "Workers.HkdfParams" or "Workers.HkdfKeyParams" => "HKDF",
            _ => null
        };
        if (name is null) yield break;
        yield return $"name: {JsonSerializer.Serialize(name)}";
        // The legacy algorithm also names itself as its curve.
        if (name == "NODE-ED25519") yield return $"namedCurve: {JsonSerializer.Serialize(name)}";
    }

    private static string? CryptoEnumValue(string type, string name)
    {
        var value = type switch
        {
            "Workers.DigestAlgorithm" or "Workers.DigestStreamAlgorithm" => name switch
            {
                "Sha1" => "SHA-1", "Sha256" => "SHA-256", "Sha384" => "SHA-384", "Sha512" => "SHA-512",
                "Md5" => "MD5", "Crc32" => "CRC32", "Crc32C" => "CRC32C", "Crc64Nvme" => "CRC64NVME",
                _ => null
            },
            "Workers.CryptoKeyUsage" or "Workers.CryptoOperation" or "Workers.CryptoKeyType" => LowerFirst(name),
            "Workers.CryptoBinaryKeyFormat" or "Workers.CryptoKeyFormat" => name switch
            {
                "RawPublic" => "raw-public", "RawPrivate" => "raw-private", "RawSeed" => "raw-seed",
                "RawSecret" => "raw-secret", _ => name.ToLowerInvariant()
            },
            "Workers.AesAlgorithm" => name switch
            {
                "AesCtr" => "AES-CTR", "AesCbc" => "AES-CBC", "AesGcm" => "AES-GCM", "AesKw" => "AES-KW", _ => null
            },
            "Workers.RsaAlgorithm" => name switch
            {
                "RsassaPkcs1V15" => "RSASSA-PKCS1-v1_5", "RsaPss" => "RSA-PSS", "RsaOaep" => "RSA-OAEP", _ => null
            },
            "Workers.EcAlgorithm" or "Workers.KeyAgreementAlgorithm" => name.ToUpperInvariant(),
            "Workers.EcNamedCurve" => name switch { "P256" => "P-256", "P384" => "P-384", "P521" => "P-521", _ => null },
            "Workers.MlDsaAlgorithm" => name.StartsWith("MlDsa", StringComparison.Ordinal) ? "ML-DSA-" + name[5..] : null,
            "Workers.MlKemAlgorithm" => name.StartsWith("MlKem", StringComparison.Ordinal) ? "ML-KEM-" + name[5..] : null,
            _ => null
        };
        return value is null ? null : JsonSerializer.Serialize(value);
    }
}
