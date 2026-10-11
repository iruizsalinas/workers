namespace Workers;

public sealed class ReadableStream
{
    public static ReadableStream FromAsyncEnumerable(IAsyncEnumerable<ReadOnlyMemory<byte>> chunks) =>
        WorkerApi.NotExecutable<ReadableStream>();

    public bool Locked => WorkerApi.NotExecutable<bool>();
    public ReadableStreamReader GetReader() => WorkerApi.NotExecutable<ReadableStreamReader>();
    public IReadOnlyList<ReadableStream> Tee() => WorkerApi.NotExecutable<IReadOnlyList<ReadableStream>>();
    public Task PipeToAsync(WritableStream destination) => WorkerApi.NotExecutable<Task>();
    public ReadableStream Compress(CompressionFormat format) => WorkerApi.NotExecutable<ReadableStream>();
    public ReadableStream Decompress(CompressionFormat format) => WorkerApi.NotExecutable<ReadableStream>();
    public Task CancelAsync(object? reason = null) => WorkerApi.NotExecutable<Task>();

    /// <summary>Reads a stream that has not been read yet, as <c>new Response(stream).bytes()</c>.</summary>
    public Task<byte[]> BytesAsync() => WorkerApi.NotExecutable<Task<byte[]>>();
    /// <summary>Reads a stream that has not been read yet as UTF-8 text, as <c>new Response(stream).text()</c>.</summary>
    public Task<string> TextAsync() => WorkerApi.NotExecutable<Task<string>>();
}

/// <summary>A JavaScript <c>ReadableStreamDefaultReader</c>. It locks the stream until released.</summary>
public sealed class ReadableStreamReader
{
    public Task<ReadableStreamReadResult> ReadAsync() => WorkerApi.NotExecutable<Task<ReadableStreamReadResult>>();
    public Task CancelAsync(object? reason = null) => WorkerApi.NotExecutable<Task>();
    public void ReleaseLock() => WorkerApi.NotExecutable();
    public Task Closed => WorkerApi.NotExecutable<Task>();
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
    public WritableStreamWriter GetWriter() => WorkerApi.NotExecutable<WritableStreamWriter>();
    public Task CloseAsync() => WorkerApi.NotExecutable<Task>();
    public Task AbortAsync(object? reason = null) => WorkerApi.NotExecutable<Task>();
}

/// <summary>A JavaScript <c>WritableStreamDefaultWriter</c>. It locks the stream until released.</summary>
public sealed class WritableStreamWriter
{
    public Task WriteAsync(ReadOnlyMemory<byte> chunk) => WorkerApi.NotExecutable<Task>();
    public Task WriteAsync(string chunk) => WorkerApi.NotExecutable<Task>();
    public Task CloseAsync() => WorkerApi.NotExecutable<Task>();
    public Task AbortAsync(object? reason = null) => WorkerApi.NotExecutable<Task>();
    public void ReleaseLock() => WorkerApi.NotExecutable();
    public Task Ready => WorkerApi.NotExecutable<Task>();
    public Task Closed => WorkerApi.NotExecutable<Task>();
}

public enum CompressionFormat
{
    Gzip,
    Deflate,
    DeflateRaw
}
