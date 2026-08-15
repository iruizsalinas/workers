namespace Workers;

public sealed class ReadableStream
{
    public IReadOnlyList<ReadableStream> Tee() =>
        WorkerApi.NotExecutable<IReadOnlyList<ReadableStream>>();
    public Task PipeToAsync(DigestStream destination, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task>();
    public static ReadableStream FromAsyncEnumerable(
        IAsyncEnumerable<ReadOnlyMemory<byte>> chunks, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<ReadableStream>();
    public Task<ReadableStreamReadResult> ReadAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<ReadableStreamReadResult>>();
    public Task<ReadOnlyMemory<byte>> ReadAllBytesAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<ReadOnlyMemory<byte>>>();
    public Task CancelAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
}

public sealed record ReadableStreamReadResult(bool Done, ReadOnlyMemory<byte> Bytes);
