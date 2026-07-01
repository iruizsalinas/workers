using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace Workers.Tests;

public sealed class ReadableStreamTests
{
    [Fact]
    public async Task ManagedReadableStreamReadsAsyncEnumerableChunks()
    {
        var stream = ReadableStream.FromAsyncEnumerable(Chunks("he"u8.ToArray(), "llo"u8.ToArray()));

        var first = await stream.ReadAsync();
        var second = await stream.ReadAsync();
        var done = await stream.ReadAsync();

        Assert.False(first.Done);
        Assert.False(second.Done);
        Assert.True(done.Done);
        Assert.Equal("he", Encoding.UTF8.GetString(first.Bytes.Span));
        Assert.Equal("llo", Encoding.UTF8.GetString(second.Bytes.Span));
        Assert.Empty(done.Bytes.ToArray());
    }

    [Fact]
    public async Task ManagedReadableStreamCanBeCancelled()
    {
        var stream = ReadableStream.FromAsyncEnumerable(Chunks("first"u8.ToArray(), "second"u8.ToArray()));

        var read = await stream.ReadAsync();
        await stream.CancelAsync();

        Assert.False(read.Done);
        Assert.Equal("first", Encoding.UTF8.GetString(read.Bytes.Span));
        await Assert.ThrowsAsync<WorkersException>(() => stream.ReadAsync());
    }

    [Fact]
    public async Task ManagedReadableStreamReleasesProducerWhenItThrows()
    {
        var stream = ReadableStream.FromAsyncEnumerable(ThrowingChunks());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => stream.ReadAsync());

        Assert.Equal("producer failed", ex.Message);
        await Assert.ThrowsAsync<WorkersException>(() => stream.ReadAsync());
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> Chunks(params byte[][] chunks)
    {
        foreach (var chunk in chunks)
        {
            await Task.Yield();
            yield return chunk;
        }
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> ThrowingChunks([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("producer failed");
#pragma warning disable CS0162
        yield return ReadOnlyMemory<byte>.Empty;
#pragma warning restore CS0162
    }
}
