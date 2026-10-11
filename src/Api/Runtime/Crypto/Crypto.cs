namespace Workers;

public enum DigestAlgorithm
{
    Sha1,
    Sha256,
    Sha384,
    Sha512
}

public static class Crypto
{
    public static string RandomUuid() => WorkerApi.NotExecutable<string>();
    public static byte[] RandomBytes(int count) => WorkerApi.NotExecutable<byte[]>();
    public static bool TimingSafeEqual(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right) =>
        WorkerApi.NotExecutable<bool>();
    public static DigestStream CreateDigestStream(DigestAlgorithm algorithm) =>
        WorkerApi.NotExecutable<DigestStream>();
    public static Task<byte[]> DigestTextAsync(
        DigestAlgorithm algorithm, string value) =>
        WorkerApi.NotExecutable<Task<byte[]>>();
    public static Task<byte[]> DigestAsync(
        DigestAlgorithm algorithm, Body body) =>
        WorkerApi.NotExecutable<Task<byte[]>>();
    public static Task<byte[]> DigestBytesAsync(
        DigestAlgorithm algorithm, ReadOnlyMemory<byte> value) =>
        WorkerApi.NotExecutable<Task<byte[]>>();
    public static Task<bool> VerifyHmacSha256Async(
        string secret, ReadOnlyMemory<byte> signature, ReadOnlyMemory<byte> payload) => WorkerApi.NotExecutable<Task<bool>>();
}

/// <summary>A JavaScript <c>crypto.DigestStream</c>: write bytes through <see cref="WritableStream.GetWriter"/> or a pipe, then read the digest.</summary>
public sealed class DigestStream : WritableStream
{
    private DigestStream() { }
    public Task<byte[]> DigestAsync() => WorkerApi.NotExecutable<Task<byte[]>>();
}
