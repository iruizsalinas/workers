namespace Workers;

/// <summary>A native Web Crypto algorithm identifier. Names follow Cloudflare's Web Crypto API.</summary>
public class CryptoAlgorithm
{
    public required string Name { get; init; }
}

public sealed class CryptoEncryptAlgorithm : CryptoAlgorithm
{
    public byte[]? Iv { get; init; }
    public byte[]? AdditionalData { get; init; }
    public int? TagLength { get; init; }
    public byte[]? Counter { get; init; }
    public int? Length { get; init; }
    public byte[]? Label { get; init; }
}

public sealed class CryptoSignAlgorithm : CryptoAlgorithm
{
    public DigestAlgorithm? Hash { get; init; }
    public int? SaltLength { get; init; }
    /// <summary>Non-standard signing parameter accepted by the runtime.</summary>
    public int? DataLength { get; init; }
    /// <summary>ML-DSA context bytes; requires webcrypto_modern_algorithms.</summary>
    public byte[]? Context { get; init; }
}

/// <summary>Generation parameters for AES or HMAC. GenerateKeyAsync returns a single key.</summary>
public sealed class CryptoSecretKeyAlgorithm : CryptoAlgorithm
{
    public DigestAlgorithm? Hash { get; init; }
    public int? Length { get; init; }
}

/// <summary>Generation parameters for RSA, EC, Ed25519, X25519, ML-DSA, or ML-KEM. GenerateKeyAsync returns a key pair.</summary>
public sealed class CryptoKeyPairAlgorithm : CryptoAlgorithm
{
    public DigestAlgorithm? Hash { get; init; }
    public int? ModulusLength { get; init; }
    public byte[]? PublicExponent { get; init; }
    public string? NamedCurve { get; init; }
}

public sealed class CryptoImportAlgorithm : CryptoAlgorithm
{
    public DigestAlgorithm? Hash { get; init; }
    public int? Length { get; init; }
    public string? NamedCurve { get; init; }
    public bool? Compressed { get; init; }
}

public sealed class CryptoDeriveAlgorithm : CryptoAlgorithm
{
    public byte[]? Salt { get; init; }
    public int? Iterations { get; init; }
    public DigestAlgorithm? Hash { get; init; }
    public CryptoKey? Public { get; init; }
    public byte[]? Info { get; init; }
}

public enum CryptoKeyUsage
{
    Encrypt, Decrypt, Sign, Verify, DeriveKey, DeriveBits, WrapKey, UnwrapKey,
    EncapsulateKey, EncapsulateBits, DecapsulateKey, DecapsulateBits
}

/// <summary>Binary key formats. The raw-* formats require webcrypto_modern_algorithms.</summary>
public enum CryptoBinaryKeyFormat
{
    Raw, Spki, Pkcs8, RawPublic, RawPrivate, RawSeed, RawSecret
}

/// <summary>Key serialization formats for wrapping and unwrapping.</summary>
public enum CryptoKeyFormat
{
    Raw, Spki, Pkcs8, Jwk, RawPublic, RawPrivate, RawSeed, RawSecret
}

public enum DigestStreamAlgorithm
{
    Sha1, Sha256, Sha384, Sha512, Md5, Crc32, Crc32C, Crc64Nvme
}

public sealed class DigestStreamOptions
{
    /// <summary>Replace lone surrogates in string chunks with U+FFFD. Requires a runtime supporting this option.</summary>
    public bool? ToWellFormed { get; init; }
}
