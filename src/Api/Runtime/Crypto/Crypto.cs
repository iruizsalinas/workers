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
    /// <summary>Fills and returns the array with cryptographically random bytes, at most 65,536 per call.</summary>
    public static byte[] GetRandomValues(byte[] array) => WorkerApi.NotExecutable<byte[]>();
    public static Task<byte[]> DigestAsync(DigestAlgorithm algorithm, ReadOnlyMemory<byte> value) => WorkerApi.NotExecutable<Task<byte[]>>();
    /// <summary>Hashes the UTF-8 encoding of the text.</summary>
    public static Task<byte[]> DigestAsync(DigestAlgorithm algorithm, string value) => WorkerApi.NotExecutable<Task<byte[]>>();
    /// <summary>Buffers the complete body before hashing. Use a DigestStream for incremental hashing.</summary>
    public static Task<byte[]> DigestAsync(DigestAlgorithm algorithm, BodyInit body) => WorkerApi.NotExecutable<Task<byte[]>>();

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

/// <summary>Hashes the bytes written to it, as <c>crypto.DigestStream</c>. Write through <see cref="WritableStream.GetWriter"/>
/// or a pipe, then await <see cref="Digest"/>.</summary>
public sealed class DigestStream : WritableStream, IDisposable
{
    public DigestStream(DigestStreamAlgorithm algorithm, DigestStreamOptions? options = null) => WorkerApi.NotExecutable();

    /// <summary>Resolves with the digest when the stream closes, or rejects if it is aborted or disposed.</summary>
    public Task<byte[]> Digest => WorkerApi.NotExecutable<Task<byte[]>>();
    public long BytesWritten => WorkerApi.NotExecutable<long>();
    public void Dispose() => WorkerApi.NotExecutable();
}
