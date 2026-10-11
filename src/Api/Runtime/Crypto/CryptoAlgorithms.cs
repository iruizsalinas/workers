namespace Workers;

// Each parameter type is a Web Crypto algorithm dictionary for one algorithm or algorithm family.
// The interfaces it implements are the operations whose native validation accepts it, so passing
// AES-GCM parameters to SignAsync is a compile error rather than a runtime TypeError. Types with a
// single algorithm name supply it when compiled; family types take the name as an enum.

/// <summary>A Web Crypto algorithm dictionary accepted by at least one SubtleCrypto operation.</summary>
public interface ICryptoAlgorithm;

/// <summary>Parameters accepted by encrypt and decrypt.</summary>
public interface IEncryptionAlgorithm : ICryptoAlgorithm;

/// <summary>Parameters accepted by wrapKey and unwrapKey.</summary>
public interface IKeyWrapAlgorithm : ICryptoAlgorithm;

/// <summary>Parameters accepted by sign and verify.</summary>
public interface ISignatureAlgorithm : ICryptoAlgorithm;

/// <summary>Parameters for which generateKey returns a single secret key.</summary>
public interface ISecretKeyGenerationAlgorithm : ICryptoAlgorithm;

/// <summary>Parameters for which generateKey returns a public and private key pair.</summary>
public interface IKeyPairGenerationAlgorithm : ICryptoAlgorithm;

/// <summary>Parameters describing a key to import, derive, unwrap, or decapsulate.</summary>
public interface IKeyImportAlgorithm : ICryptoAlgorithm;

/// <summary>Parameters accepted by deriveBits and deriveKey.</summary>
public interface IKeyDerivationAlgorithm : ICryptoAlgorithm;

/// <summary>Parameters accepted by the encapsulate and decapsulate operations. Requires webcrypto_modern_algorithms.</summary>
public interface IKeyEncapsulationAlgorithm : ICryptoAlgorithm;

/// <summary>AES-GCM authenticated encryption.</summary>
public sealed class AesGcmParams : IEncryptionAlgorithm, IKeyWrapAlgorithm
{
    public string Name => "AES-GCM";
    public required byte[] Iv { get; init; }
    public byte[]? AdditionalData { get; init; }
    /// <summary>Authentication tag length in bits; the runtime defaults to 128.</summary>
    public int? TagLength { get; init; }
}

/// <summary>AES-CBC encryption with PKCS#7 padding.</summary>
public sealed class AesCbcParams : IEncryptionAlgorithm, IKeyWrapAlgorithm
{
    public string Name => "AES-CBC";
    /// <summary>A 16-byte initialization vector.</summary>
    public required byte[] Iv { get; init; }
}

/// <summary>AES-CTR encryption.</summary>
public sealed class AesCtrParams : IEncryptionAlgorithm, IKeyWrapAlgorithm
{
    public string Name => "AES-CTR";
    /// <summary>The 16-byte initial counter block.</summary>
    public required byte[] Counter { get; init; }
    /// <summary>The number of rightmost counter block bits that are incremented.</summary>
    public required int Length { get; init; }
}

/// <summary>AES-KW key wrapping (RFC 3394).</summary>
public sealed class AesKwParams : IKeyWrapAlgorithm
{
    public string Name => "AES-KW";
}

/// <summary>RSA-OAEP encryption. The hash is fixed when the key is generated or imported.</summary>
public sealed class RsaOaepParams : IEncryptionAlgorithm, IKeyWrapAlgorithm
{
    public string Name => "RSA-OAEP";
    public byte[]? Label { get; init; }
}

/// <summary>HMAC signing. The hash is fixed when the key is generated or imported.</summary>
public sealed class HmacParams : ISignatureAlgorithm
{
    public string Name => "HMAC";
}

/// <summary>RSASSA-PKCS1-v1_5 signing. The hash is fixed when the key is generated or imported.</summary>
public sealed class RsaPkcs1Params : ISignatureAlgorithm
{
    public string Name => "RSASSA-PKCS1-v1_5";
}

/// <summary>RSA-PSS signing. The hash is fixed when the key is generated or imported.</summary>
public sealed class RsaPssParams : ISignatureAlgorithm
{
    public string Name => "RSA-PSS";
    /// <summary>Salt length in bytes, commonly the hash output length.</summary>
    public required int SaltLength { get; init; }
}

/// <summary>ECDSA signing.</summary>
public sealed class EcdsaParams : ISignatureAlgorithm
{
    public string Name => "ECDSA";
    public required DigestAlgorithm Hash { get; init; }
}

/// <summary>Ed25519 as defined by the Secure Curves specification, for signing, key generation, and import.</summary>
public sealed class Ed25519Params : ISignatureAlgorithm, IKeyPairGenerationAlgorithm, IKeyImportAlgorithm
{
    public string Name => "Ed25519";
}

/// <summary>Cloudflare's legacy NODE-ED25519 algorithm. Prefer <see cref="Ed25519Params"/> for new keys.</summary>
public sealed class NodeEd25519Params : ISignatureAlgorithm, IKeyPairGenerationAlgorithm, IKeyImportAlgorithm
{
    public string Name => "NODE-ED25519";
}

/// <summary>X25519 key generation and import. Derive with <see cref="EcdhKeyDeriveParams"/>.</summary>
public sealed class X25519Params : IKeyPairGenerationAlgorithm, IKeyImportAlgorithm
{
    public string Name => "X25519";
}

/// <summary>ML-DSA signing, key generation, and import. Requires webcrypto_modern_algorithms.</summary>
public sealed class MlDsaParams : ISignatureAlgorithm, IKeyPairGenerationAlgorithm, IKeyImportAlgorithm
{
    public required MlDsaAlgorithm Name { get; init; }
    /// <summary>Signing context of at most 255 bytes. Only sign and verify read it.</summary>
    public byte[]? Context { get; init; }
}

/// <summary>ML-KEM key generation, import, and encapsulation. Requires webcrypto_modern_algorithms.</summary>
public sealed class MlKemParams : IKeyPairGenerationAlgorithm, IKeyImportAlgorithm, IKeyEncapsulationAlgorithm
{
    public required MlKemAlgorithm Name { get; init; }
}

/// <summary>An AES key for generation, import, or use as a derived, unwrapped, or shared key.</summary>
public sealed class AesKeyParams : ISecretKeyGenerationAlgorithm, IKeyImportAlgorithm
{
    public required AesAlgorithm Name { get; init; }
    /// <summary>Key length in bits (128, 192, or 256). Required to generate or derive; import reads it from the key data.</summary>
    public int? Length { get; init; }
}

/// <summary>An HMAC key for generation, import, or use as a derived, unwrapped, or shared key.</summary>
public sealed class HmacKeyParams : ISecretKeyGenerationAlgorithm, IKeyImportAlgorithm
{
    public string Name => "HMAC";
    public required DigestAlgorithm Hash { get; init; }
    /// <summary>Key length in bits; defaults to the hash block size when generating and to the key data length when importing.</summary>
    public int? Length { get; init; }
}

/// <summary>RSA key pair generation.</summary>
public sealed class RsaKeyGenParams : IKeyPairGenerationAlgorithm
{
    public required RsaAlgorithm Name { get; init; }
    public required DigestAlgorithm Hash { get; init; }
    /// <summary>Modulus length in bits, for example 2048 or 4096.</summary>
    public required int ModulusLength { get; init; }
    /// <summary>Big-endian public exponent; [1, 0, 1] is 65537.</summary>
    public required byte[] PublicExponent { get; init; }
}

/// <summary>RSA key import.</summary>
public sealed class RsaKeyImportParams : IKeyImportAlgorithm
{
    public required RsaAlgorithm Name { get; init; }
    public required DigestAlgorithm Hash { get; init; }
}

/// <summary>ECDSA or ECDH key generation and import.</summary>
public sealed class EcKeyParams : IKeyPairGenerationAlgorithm, IKeyImportAlgorithm
{
    public required EcAlgorithm Name { get; init; }
    public required EcNamedCurve NamedCurve { get; init; }
}

/// <summary>A PBKDF2 base key, imported from raw password bytes.</summary>
public sealed class Pbkdf2KeyParams : IKeyImportAlgorithm
{
    public string Name => "PBKDF2";
}

/// <summary>An HKDF base key, imported from raw input keying material.</summary>
public sealed class HkdfKeyParams : IKeyImportAlgorithm
{
    public string Name => "HKDF";
}

/// <summary>PBKDF2 derivation from a key imported with <see cref="Pbkdf2KeyParams"/>.</summary>
public sealed class Pbkdf2Params : IKeyDerivationAlgorithm
{
    public string Name => "PBKDF2";
    public required byte[] Salt { get; init; }
    public required int Iterations { get; init; }
    public required DigestAlgorithm Hash { get; init; }
}

/// <summary>HKDF derivation from a key imported with <see cref="HkdfKeyParams"/>.</summary>
public sealed class HkdfParams : IKeyDerivationAlgorithm
{
    public string Name => "HKDF";
    public required DigestAlgorithm Hash { get; init; }
    public required byte[] Salt { get; init; }
    public required byte[] Info { get; init; }
}

/// <summary>ECDH or X25519 key agreement with a peer's public key.</summary>
public sealed class EcdhKeyDeriveParams : IKeyDerivationAlgorithm
{
    public required KeyAgreementAlgorithm Name { get; init; }
    public required CryptoKey Public { get; init; }
}

public enum AesAlgorithm
{
    AesCtr, AesCbc, AesGcm, AesKw
}

public enum RsaAlgorithm
{
    RsassaPkcs1V15, RsaPss, RsaOaep
}

public enum EcAlgorithm
{
    Ecdsa, Ecdh
}

public enum EcNamedCurve
{
    P256, P384, P521
}

public enum KeyAgreementAlgorithm
{
    Ecdh, X25519
}

public enum MlDsaAlgorithm
{
    MlDsa44, MlDsa65, MlDsa87
}

/// <summary>ML-KEM parameter sets the runtime implements; ML-KEM-512 is not available.</summary>
public enum MlKemAlgorithm
{
    MlKem768, MlKem1024
}

/// <summary>SubtleCrypto operation names for <see cref="SubtleCrypto.Supports(CryptoOperation, ICryptoAlgorithm, int?)"/>.</summary>
public enum CryptoOperation
{
    Encrypt, Decrypt, Sign, Verify, Digest, GenerateKey, DeriveKey, DeriveBits, ImportKey, ExportKey,
    WrapKey, UnwrapKey, EncapsulateKey, EncapsulateBits, DecapsulateKey, DecapsulateBits, GetPublicKey
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
