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
            : method.Parameters[1].Type.ToDisplayString() == "Workers.Body"
                ? $"((crypto, algorithm, body) => new Response(body).arrayBuffer().then(value => crypto.subtle.digest(algorithm, value)).then(value => new Uint8Array(value)))({receiver}, {algorithm}, {data})"
                : $"{receiver}.subtle.digest({algorithm}, {data}).then(value => new Uint8Array(value))";
    }

    // The native API returns either a key or a key pair. Check the result instead of silently
    // treating an algorithm from the wrong family as the statically declared C# return type.
    private static string EmitCryptoGenerateKey(string receiver, IMethodSymbol method,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        var pair = method.Parameters[0].Type.ToDisplayString() == "Workers.CryptoKeyPairAlgorithm";
        var check = pair ? "!value.publicKey || !value.privateKey" : "value.publicKey || value.privateKey";
        var expected = pair ? "a key pair" : "a secret key";
        return $"{receiver}.generateKey({string.Join(", ", arguments.Select(item => item.Value))})"
            + $".then(value => {{ if ({check}) throw new TypeError(\"The algorithm must generate {expected}.\"); return value; }})";
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
            "Workers.CryptoKeyUsage" => LowerFirst(name),
            "Workers.CryptoBinaryKeyFormat" or "Workers.CryptoKeyFormat" => name switch
            {
                "RawPublic" => "raw-public", "RawPrivate" => "raw-private", "RawSeed" => "raw-seed",
                "RawSecret" => "raw-secret", _ => name.ToLowerInvariant()
            },
            _ => null
        };
        return value is null ? null : JsonSerializer.Serialize(value);
    }
}
