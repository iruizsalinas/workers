namespace Workers;

/// <summary>An opaque native Web Crypto key. Key material is only available through export when extractable.</summary>
public sealed class CryptoKey
{
    private CryptoKey() { }
    public CryptoKeyType Type => WorkerApi.NotExecutable<CryptoKeyType>();
    public bool Extractable => WorkerApi.NotExecutable<bool>();
    public CryptoKeyAlgorithm Algorithm => WorkerApi.NotExecutable<CryptoKeyAlgorithm>();
    public IReadOnlyList<CryptoKeyUsage> Usages => WorkerApi.NotExecutable<IReadOnlyList<CryptoKeyUsage>>();
}

public enum CryptoKeyType
{
    Secret, Public, Private
}

public sealed class CryptoKeyPair
{
    private CryptoKeyPair() { }
    public CryptoKey PublicKey => WorkerApi.NotExecutable<CryptoKey>();
    public CryptoKey PrivateKey => WorkerApi.NotExecutable<CryptoKey>();
}

/// <summary>Native key algorithm metadata. Algorithm-specific fields may be absent.</summary>
public sealed class CryptoKeyAlgorithm
{
    private CryptoKeyAlgorithm() { }
    public string Name => WorkerApi.NotExecutable<string>();
    public DigestAlgorithm? Hash => WorkerApi.NotExecutable<DigestAlgorithm?>();
    public int? Length => WorkerApi.NotExecutable<int?>();
    public int? ModulusLength => WorkerApi.NotExecutable<int?>();
    public byte[]? PublicExponent => WorkerApi.NotExecutable<byte[]?>();
    public string? NamedCurve => WorkerApi.NotExecutable<string?>();
}

/// <summary>RFC 7517/7518 JSON Web Key fields, including modern AKP key material.</summary>
public sealed class JsonWebKey
{
    public required string Kty { get; init; }
    public string? Use { get; init; }
    public IReadOnlyList<CryptoKeyUsage>? KeyOps { get; init; }
    public string? Alg { get; init; }
    public bool? Ext { get; init; }
    public string? Crv { get; init; }
    public string? X { get; init; }
    public string? Y { get; init; }
    public string? D { get; init; }
    public string? N { get; init; }
    public string? E { get; init; }
    public string? P { get; init; }
    public string? Q { get; init; }
    public string? Dp { get; init; }
    public string? Dq { get; init; }
    public string? Qi { get; init; }
    public IReadOnlyList<RsaOtherPrimesInfo>? Oth { get; init; }
    public string? K { get; init; }
    public string? Pub { get; init; }
    public string? Priv { get; init; }
}

public sealed class RsaOtherPrimesInfo
{
    public required string R { get; init; }
    public required string D { get; init; }
    public required string T { get; init; }
}

public sealed class EncapsulatedBits
{
    private EncapsulatedBits() { }
    public byte[] SharedKey => WorkerApi.NotExecutable<byte[]>();
    public byte[] Ciphertext => WorkerApi.NotExecutable<byte[]>();
}

public sealed class EncapsulatedKey
{
    private EncapsulatedKey() { }
    public CryptoKey SharedKey => WorkerApi.NotExecutable<CryptoKey>();
    public byte[] Ciphertext => WorkerApi.NotExecutable<byte[]>();
}
