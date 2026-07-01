using System.Collections.Concurrent;

namespace Workers;

internal static class ManagedReadableStreamRegistry
{
    private static readonly ConcurrentDictionary<string, IAsyncEnumerator<ReadOnlyMemory<byte>>> Streams = new(StringComparer.Ordinal);
    private static long nextStreamId;

    public static string Register(IAsyncEnumerable<ReadOnlyMemory<byte>> chunks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        var handle = "managed-stream:" + Interlocked.Increment(ref nextStreamId).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Streams[handle] = chunks.GetAsyncEnumerator(cancellationToken);
        return handle;
    }

    public static async Task<ReadableStreamReadResult> PullAsync(string handle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);

        if (!Streams.TryGetValue(handle, out var enumerator))
            throw new WorkersException($"Managed readable stream '{handle}' is not defined.");

        try
        {
            if (await enumerator.MoveNextAsync())
                return new ReadableStreamReadResult(false, enumerator.Current);
        }
        catch
        {
            await ReleaseAsync(handle, enumerator);
            throw;
        }

        await ReleaseAsync(handle, enumerator);
        return new ReadableStreamReadResult(true, ReadOnlyMemory<byte>.Empty);
    }

    public static async Task CancelAsync(string handle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);

        if (Streams.TryRemove(handle, out var enumerator))
            await enumerator.DisposeAsync();
    }

    private static async Task ReleaseAsync(string handle, IAsyncEnumerator<ReadOnlyMemory<byte>> enumerator)
    {
        Streams.TryRemove(handle, out _);
        await enumerator.DisposeAsync();
    }
}
