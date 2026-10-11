namespace Workers;

/// <summary>Digest algorithms for subtle.digest and for the hash parameter of HMAC, RSA, ECDSA, PBKDF2, and HKDF.</summary>
public enum DigestAlgorithm
{
    Sha1,
    Sha256,
    Sha384,
    Sha512,
    /// <summary>Non-standard and cryptographically broken; provided for legacy formats.</summary>
    Md5
}

public static class Crypto
{
    public static SubtleCrypto Subtle => WorkerApi.NotExecutable<SubtleCrypto>();
    public static string RandomUuid() => WorkerApi.NotExecutable<string>();
    /// <summary>Allocates and fills up to 65,536 bytes using native getRandomValues.</summary>
    public static byte[] RandomBytes(int count) => WorkerApi.NotExecutable<byte[]>();
    /// <summary>Fills and returns the supplied byte array. The native 65,536-byte quota applies.</summary>
    public static byte[] GetRandomValues(byte[] buffer) => WorkerApi.NotExecutable<byte[]>();
    public static bool TimingSafeEqual(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right) =>
        WorkerApi.NotExecutable<bool>();
    public static DigestStream CreateDigestStream(DigestStreamAlgorithm algorithm, DigestStreamOptions? options = null) =>
        WorkerApi.NotExecutable<DigestStream>();
    public static Task<byte[]> DigestAsync(DigestAlgorithm algorithm, ReadOnlyMemory<byte> value) => WorkerApi.NotExecutable<Task<byte[]>>();
    /// <summary>Hashes the UTF-8 encoding of the text.</summary>
    public static Task<byte[]> DigestAsync(DigestAlgorithm algorithm, string value) => WorkerApi.NotExecutable<Task<byte[]>>();
    /// <summary>Buffers the complete body before hashing. Use a DigestStream for incremental hashing.</summary>
    public static Task<byte[]> DigestAsync(DigestAlgorithm algorithm, Body body) => WorkerApi.NotExecutable<Task<byte[]>>();

    /// <summary>Computes an HMAC with a raw key. A string key is UTF-8 encoded.</summary>
    public static Task<byte[]> SignHmacAsync(DigestAlgorithm hash, string key, ReadOnlyMemory<byte> payload) =>
        WorkerApi.NotExecutable<Task<byte[]>>();
    /// <summary>Computes an HMAC with a raw key.</summary>
    public static Task<byte[]> SignHmacAsync(DigestAlgorithm hash, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> payload) =>
        WorkerApi.NotExecutable<Task<byte[]>>();
    /// <summary>Verifies an HMAC in constant time with a raw key. A string key is UTF-8 encoded.</summary>
    public static Task<bool> VerifyHmacAsync(
        DigestAlgorithm hash, string key, ReadOnlyMemory<byte> signature, ReadOnlyMemory<byte> payload) =>
        WorkerApi.NotExecutable<Task<bool>>();
    /// <summary>Verifies an HMAC in constant time with a raw key.</summary>
    public static Task<bool> VerifyHmacAsync(
        DigestAlgorithm hash, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> signature, ReadOnlyMemory<byte> payload) =>
        WorkerApi.NotExecutable<Task<bool>>();
}

/// <summary>A JavaScript <c>crypto.DigestStream</c>: write bytes through <see cref="WritableStream.GetWriter"/> or a pipe, then read the digest.</summary>
public sealed class DigestStream : WritableStream, IDisposable
{
    private DigestStream() { }
    public long BytesWritten => WorkerApi.NotExecutable<long>();
    /// <summary>Resolves when the stream closes, or rejects if it is aborted or disposed.</summary>
    public Task<byte[]> DigestAsync() => WorkerApi.NotExecutable<Task<byte[]>>();
    public void Dispose() => WorkerApi.NotExecutable();
}
