namespace Workers;

/// <summary>Cloudflare's native SubtleCrypto. Validation, key usage restrictions, and errors follow the runtime.</summary>
public sealed class SubtleCrypto
{
    private SubtleCrypto() { }

    public Task<byte[]> DigestAsync(DigestAlgorithm algorithm, ReadOnlyMemory<byte> data) => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<byte[]> EncryptAsync(CryptoAlgorithm algorithm, CryptoKey key, ReadOnlyMemory<byte> data) => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<byte[]> DecryptAsync(CryptoAlgorithm algorithm, CryptoKey key, ReadOnlyMemory<byte> data) => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<byte[]> SignAsync(CryptoAlgorithm algorithm, CryptoKey key, ReadOnlyMemory<byte> data) => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<bool> VerifyAsync(CryptoAlgorithm algorithm, CryptoKey key, ReadOnlyMemory<byte> signature, ReadOnlyMemory<byte> data) => WorkerApi.NotExecutable<Task<bool>>();

    public Task<CryptoKey> GenerateKeyAsync(
        CryptoSecretKeyAlgorithm algorithm, bool extractable, IReadOnlyList<CryptoKeyUsage> keyUsages) =>
        WorkerApi.NotExecutable<Task<CryptoKey>>();
    public Task<CryptoKeyPair> GenerateKeyAsync(
        CryptoKeyPairAlgorithm algorithm, bool extractable, IReadOnlyList<CryptoKeyUsage> keyUsages) =>
        WorkerApi.NotExecutable<Task<CryptoKeyPair>>();
    public Task<CryptoKey> ImportKeyAsync(
        CryptoBinaryKeyFormat format, ReadOnlyMemory<byte> keyData, CryptoAlgorithm algorithm,
        bool extractable, IReadOnlyList<CryptoKeyUsage> keyUsages) => WorkerApi.NotExecutable<Task<CryptoKey>>();
    public Task<CryptoKey> ImportKeyAsync(
        JsonWebKey keyData, CryptoAlgorithm algorithm, bool extractable, IReadOnlyList<CryptoKeyUsage> keyUsages) =>
        WorkerApi.NotExecutable<Task<CryptoKey>>();
    public Task<byte[]> ExportKeyAsync(CryptoBinaryKeyFormat format, CryptoKey key) => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<JsonWebKey> ExportJwkAsync(CryptoKey key) => WorkerApi.NotExecutable<Task<JsonWebKey>>();

    /// <summary>Length is in bits; null requests the full shared secret where the algorithm permits it.</summary>
    public Task<byte[]> DeriveBitsAsync(CryptoAlgorithm algorithm, CryptoKey baseKey, int? length = null) => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<CryptoKey> DeriveKeyAsync(
        CryptoAlgorithm algorithm, CryptoKey baseKey, CryptoAlgorithm derivedKeyAlgorithm,
        bool extractable, IReadOnlyList<CryptoKeyUsage> keyUsages) => WorkerApi.NotExecutable<Task<CryptoKey>>();
    public Task<byte[]> WrapKeyAsync(
        CryptoKeyFormat format, CryptoKey key, CryptoKey wrappingKey, CryptoAlgorithm wrapAlgorithm) =>
        WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<CryptoKey> UnwrapKeyAsync(
        CryptoKeyFormat format, ReadOnlyMemory<byte> wrappedKey, CryptoKey unwrappingKey,
        CryptoAlgorithm unwrapAlgorithm, CryptoAlgorithm unwrappedKeyAlgorithm,
        bool extractable, IReadOnlyList<CryptoKeyUsage> keyUsages) => WorkerApi.NotExecutable<Task<CryptoKey>>();
    public bool TimingSafeEqual(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right) => WorkerApi.NotExecutable<bool>();

    /// <summary>Requires webcrypto_modern_algorithms. Returns false for unsupported algorithms or parameters.</summary>
    public static bool Supports(string operation, CryptoAlgorithm algorithm, int? length = null) => WorkerApi.NotExecutable<bool>();
    /// <summary>Requires webcrypto_modern_algorithms; queries operations involving a second algorithm.</summary>
    public static bool Supports(string operation, CryptoAlgorithm algorithm, CryptoAlgorithm additionalAlgorithm) =>
        WorkerApi.NotExecutable<bool>();
    /// <summary>Requires webcrypto_modern_algorithms; queries support for a digest algorithm.</summary>
    public static bool Supports(string operation, DigestAlgorithm algorithm) => WorkerApi.NotExecutable<bool>();

    /// <summary>Requires webcrypto_modern_algorithms.</summary>
    public Task<CryptoKey> GetPublicKeyAsync(CryptoKey key, IReadOnlyList<CryptoKeyUsage> keyUsages) => WorkerApi.NotExecutable<Task<CryptoKey>>();
    /// <summary>Requires webcrypto_modern_algorithms.</summary>
    public Task<EncapsulatedBits> EncapsulateBitsAsync(CryptoAlgorithm algorithm, CryptoKey key) =>
        WorkerApi.NotExecutable<Task<EncapsulatedBits>>();
    /// <summary>Requires webcrypto_modern_algorithms.</summary>
    public Task<EncapsulatedKey> EncapsulateKeyAsync(
        CryptoAlgorithm algorithm, CryptoKey key, CryptoAlgorithm sharedKeyAlgorithm,
        bool extractable, IReadOnlyList<CryptoKeyUsage> keyUsages) => WorkerApi.NotExecutable<Task<EncapsulatedKey>>();
    /// <summary>Requires webcrypto_modern_algorithms.</summary>
    public Task<byte[]> DecapsulateBitsAsync(CryptoAlgorithm algorithm, CryptoKey key, ReadOnlyMemory<byte> ciphertext) =>
        WorkerApi.NotExecutable<Task<byte[]>>();
    /// <summary>Requires webcrypto_modern_algorithms.</summary>
    public Task<CryptoKey> DecapsulateKeyAsync(
        CryptoAlgorithm algorithm, CryptoKey key, ReadOnlyMemory<byte> ciphertext,
        CryptoAlgorithm sharedKeyAlgorithm, bool extractable, IReadOnlyList<CryptoKeyUsage> keyUsages) =>
        WorkerApi.NotExecutable<Task<CryptoKey>>();
}
