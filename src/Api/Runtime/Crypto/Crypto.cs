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
    public static byte[] RandomBytes(int count) => WorkerApi.NotExecutable<byte[]>();
    public static bool TimingSafeEqual(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right) =>
        WorkerApi.NotExecutable<bool>();
    public static DigestStream CreateDigestStream(DigestAlgorithm algorithm) =>
        WorkerApi.NotExecutable<DigestStream>();
    public static Task<byte[]> DigestTextAsync(
        DigestAlgorithm algorithm, string value, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<byte[]>>();
    public static Task<byte[]> DigestAsync(
        DigestAlgorithm algorithm, Body body, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<byte[]>>();
    public static Task<byte[]> DigestBytesAsync(
        DigestAlgorithm algorithm, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<byte[]>>();
}

public sealed class DigestStream
{
    public Task WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task WriteTextAsync(string value, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task CloseAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task<byte[]> DigestAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<byte[]>>();
}
