namespace Workers;

/// <summary>A stream of bytes. A stream can be read once: read it, pipe it, or tee it into two copies.</summary>
public sealed class ReadableStream
{
    private ReadableStream() { }

    /// <summary>A stream of the chunks an async sequence yields, pulled as the stream is read.</summary>
    public static ReadableStream From(IAsyncEnumerable<ReadOnlyMemory<byte>> chunks) => WorkerApi.NotExecutable<ReadableStream>();

    public bool Locked => WorkerApi.NotExecutable<bool>();
    public ReadableStreamDefaultReader GetReader() => WorkerApi.NotExecutable<ReadableStreamDefaultReader>();
    public ReadableStream PipeThrough(TransformStream transform) => WorkerApi.NotExecutable<ReadableStream>();
    public Task PipeToAsync(WritableStream destination, StreamPipeOptions? options = null) => WorkerApi.NotExecutable<Task>();
    /// <summary>Splits the stream into two that each receive every chunk.</summary>
    public IReadOnlyList<ReadableStream> Tee() => WorkerApi.NotExecutable<IReadOnlyList<ReadableStream>>();
    public Task CancelAsync(object? reason = null) => WorkerApi.NotExecutable<Task>();
}

public sealed class StreamPipeOptions
{
    public bool? PreventClose { get; init; }
    public bool? PreventAbort { get; init; }
    public bool? PreventCancel { get; init; }
    public AbortSignal? Signal { get; init; }
}

/// <summary>A reader that locks its stream until released.</summary>
public sealed class ReadableStreamDefaultReader
{
    private ReadableStreamDefaultReader() { }

    public Task Closed => WorkerApi.NotExecutable<Task>();
    public Task<ReadableStreamReadResult> ReadAsync() => WorkerApi.NotExecutable<Task<ReadableStreamReadResult>>();
    public Task CancelAsync(object? reason = null) => WorkerApi.NotExecutable<Task>();
    public void ReleaseLock() => WorkerApi.NotExecutable();
}

/// <summary>The result of <c>reader.read()</c>: <c>Value</c> is null once <c>Done</c> is true.</summary>
public sealed class ReadableStreamReadResult
{
    private ReadableStreamReadResult() { }

    public bool Done => WorkerApi.NotExecutable<bool>();
    public byte[]? Value => WorkerApi.NotExecutable<byte[]?>();
}

public class WritableStream
{
    private protected WritableStream() { }

    public bool Locked => WorkerApi.NotExecutable<bool>();
    public WritableStreamDefaultWriter GetWriter() => WorkerApi.NotExecutable<WritableStreamDefaultWriter>();
    public Task CloseAsync() => WorkerApi.NotExecutable<Task>();
    public Task AbortAsync(object? reason = null) => WorkerApi.NotExecutable<Task>();
}

/// <summary>A writer that locks its stream until released.</summary>
public sealed class WritableStreamDefaultWriter
{
    private WritableStreamDefaultWriter() { }

    public Task Closed => WorkerApi.NotExecutable<Task>();
    public Task Ready => WorkerApi.NotExecutable<Task>();
    public Task WriteAsync(ReadOnlyMemory<byte> chunk) => WorkerApi.NotExecutable<Task>();
    /// <summary>Writes text to a stream that accepts strings, such as a <see cref="DigestStream"/>.
    /// Byte streams, such as a <see cref="TransformStream"/> feeding a response, need encoded bytes.</summary>
    public Task WriteAsync(string chunk) => WorkerApi.NotExecutable<Task>();
    public Task CloseAsync() => WorkerApi.NotExecutable<Task>();
    public Task AbortAsync(object? reason = null) => WorkerApi.NotExecutable<Task>();
    public void ReleaseLock() => WorkerApi.NotExecutable();
}

/// <summary>A pair of streams: bytes written to <see cref="Writable"/> are read from <see cref="Readable"/>.</summary>
public class TransformStream
{
    /// <summary>An identity transform, useful for streaming a response while it is being produced.</summary>
    public TransformStream() => WorkerApi.NotExecutable();

    public ReadableStream Readable => WorkerApi.NotExecutable<ReadableStream>();
    public WritableStream Writable => WorkerApi.NotExecutable<WritableStream>();
}

/// <summary>An identity transform that only accepts bytes, as Cloudflare's <c>IdentityTransformStream</c>.</summary>
public class IdentityTransformStream : TransformStream
{
    public IdentityTransformStream() => WorkerApi.NotExecutable();
}

/// <summary>An identity transform with a declared length, so its readable side can be an R2 upload or a body with <c>Content-Length</c>.</summary>
public sealed class FixedLengthStream : IdentityTransformStream
{
    public FixedLengthStream(long expectedLength) => WorkerApi.NotExecutable();
}

public sealed class CompressionStream : TransformStream
{
    public CompressionStream(CompressionFormat format) => WorkerApi.NotExecutable();
}

public sealed class DecompressionStream : TransformStream
{
    public DecompressionStream(CompressionFormat format) => WorkerApi.NotExecutable();
}

public enum CompressionFormat
{
    Gzip,
    Deflate,
    DeflateRaw
}
