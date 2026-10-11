namespace Workers;

/// <summary>Cloudflare's native SubtleCrypto. Validation, key usage restrictions, and errors follow the runtime.</summary>
public sealed class SubtleCrypto
{
    private SubtleCrypto() { }

    public Task<byte[]> DigestAsync(DigestAlgorithm algorithm, ReadOnlyMemory<byte> data) => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<byte[]> EncryptAsync(IEncryptionAlgorithm algorithm, CryptoKey key, ReadOnlyMemory<byte> data) => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<byte[]> DecryptAsync(IEncryptionAlgorithm algorithm, CryptoKey key, ReadOnlyMemory<byte> data) => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<byte[]> SignAsync(ISignatureAlgorithm algorithm, CryptoKey key, ReadOnlyMemory<byte> data) => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<bool> VerifyAsync(ISignatureAlgorithm algorithm, CryptoKey key, ReadOnlyMemory<byte> signature, ReadOnlyMemory<byte> data) => WorkerApi.NotExecutable<Task<bool>>();

    public Task<CryptoKey> GenerateKeyAsync(
        ISecretKeyGenerationAlgorithm algorithm, bool extractable, IReadOnlyList<CryptoKeyUsage> keyUsages) =>
        WorkerApi.NotExecutable<Task<CryptoKey>>();
    public Task<CryptoKeyPair> GenerateKeyAsync(
        IKeyPairGenerationAlgorithm algorithm, bool extractable, IReadOnlyList<CryptoKeyUsage> keyUsages) =>
        WorkerApi.NotExecutable<Task<CryptoKeyPair>>();
    public Task<CryptoKey> ImportKeyAsync(
        CryptoBinaryKeyFormat format, ReadOnlyMemory<byte> keyData, IKeyImportAlgorithm algorithm,
        bool extractable, IReadOnlyList<CryptoKeyUsage> keyUsages) => WorkerApi.NotExecutable<Task<CryptoKey>>();
    public Task<CryptoKey> ImportKeyAsync(
        JsonWebKey keyData, IKeyImportAlgorithm algorithm, bool extractable, IReadOnlyList<CryptoKeyUsage> keyUsages) =>
        WorkerApi.NotExecutable<Task<CryptoKey>>();
    public Task<byte[]> ExportKeyAsync(CryptoBinaryKeyFormat format, CryptoKey key) => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<JsonWebKey> ExportJwkAsync(CryptoKey key) => WorkerApi.NotExecutable<Task<JsonWebKey>>();

    /// <summary>Length is in bits; null requests the full shared secret where the algorithm permits it.</summary>
    public Task<byte[]> DeriveBitsAsync(IKeyDerivationAlgorithm algorithm, CryptoKey baseKey, int? length = null) => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<CryptoKey> DeriveKeyAsync(
        IKeyDerivationAlgorithm algorithm, CryptoKey baseKey, IKeyImportAlgorithm derivedKeyAlgorithm,
        bool extractable, IReadOnlyList<CryptoKeyUsage> keyUsages) => WorkerApi.NotExecutable<Task<CryptoKey>>();
    public Task<byte[]> WrapKeyAsync(
        CryptoKeyFormat format, CryptoKey key, CryptoKey wrappingKey, IKeyWrapAlgorithm wrapAlgorithm) =>
        WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<CryptoKey> UnwrapKeyAsync(
        CryptoKeyFormat format, ReadOnlyMemory<byte> wrappedKey, CryptoKey unwrappingKey,
        IKeyWrapAlgorithm unwrapAlgorithm, IKeyImportAlgorithm unwrappedKeyAlgorithm,
        bool extractable, IReadOnlyList<CryptoKeyUsage> keyUsages) => WorkerApi.NotExecutable<Task<CryptoKey>>();

    /// <summary>Requires webcrypto_modern_algorithms. Returns false for unsupported algorithms or parameters.</summary>
    public static bool Supports(CryptoOperation operation, ICryptoAlgorithm algorithm, int? length = null) => WorkerApi.NotExecutable<bool>();
    /// <summary>Requires webcrypto_modern_algorithms; queries operations involving a second algorithm, such as deriveKey or unwrapKey.</summary>
    public static bool Supports(CryptoOperation operation, ICryptoAlgorithm algorithm, IKeyImportAlgorithm additionalAlgorithm) =>
        WorkerApi.NotExecutable<bool>();
    /// <summary>Requires webcrypto_modern_algorithms; queries support for a digest algorithm.</summary>
    public static bool Supports(CryptoOperation operation, DigestAlgorithm algorithm) => WorkerApi.NotExecutable<bool>();

    /// <summary>Requires webcrypto_modern_algorithms.</summary>
    public Task<CryptoKey> GetPublicKeyAsync(CryptoKey key, IReadOnlyList<CryptoKeyUsage> keyUsages) => WorkerApi.NotExecutable<Task<CryptoKey>>();
    /// <summary>Requires webcrypto_modern_algorithms.</summary>
    public Task<EncapsulatedBits> EncapsulateBitsAsync(IKeyEncapsulationAlgorithm algorithm, CryptoKey key) =>
        WorkerApi.NotExecutable<Task<EncapsulatedBits>>();
    /// <summary>Requires webcrypto_modern_algorithms.</summary>
    public Task<EncapsulatedKey> EncapsulateKeyAsync(
        IKeyEncapsulationAlgorithm algorithm, CryptoKey key, IKeyImportAlgorithm sharedKeyAlgorithm,
        bool extractable, IReadOnlyList<CryptoKeyUsage> keyUsages) => WorkerApi.NotExecutable<Task<EncapsulatedKey>>();
    /// <summary>Requires webcrypto_modern_algorithms.</summary>
    public Task<byte[]> DecapsulateBitsAsync(IKeyEncapsulationAlgorithm algorithm, CryptoKey key, ReadOnlyMemory<byte> ciphertext) =>
        WorkerApi.NotExecutable<Task<byte[]>>();
    /// <summary>Requires webcrypto_modern_algorithms.</summary>
    public Task<CryptoKey> DecapsulateKeyAsync(
        IKeyEncapsulationAlgorithm algorithm, CryptoKey key, ReadOnlyMemory<byte> ciphertext,
        IKeyImportAlgorithm sharedKeyAlgorithm, bool extractable, IReadOnlyList<CryptoKeyUsage> keyUsages) =>
        WorkerApi.NotExecutable<Task<CryptoKey>>();
}
