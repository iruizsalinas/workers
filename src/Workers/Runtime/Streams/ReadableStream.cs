using System.Text.Json;

namespace Workers;

/// <summary>A native Workers readable byte stream.</summary>
public sealed class ReadableStream
{
    private readonly string? _invocationId;
    private readonly IBindingDispatcher? _dispatcher;

    internal ReadableStream(string invocationId, NativeStreamSource source, string handle, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        _invocationId = invocationId;
        Source = source;
        Handle = handle;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    internal ReadableStream(string managedHandle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedHandle);
        Source = NativeStreamSource.Managed;
        Handle = managedHandle;
    }

    internal NativeStreamSource Source { get; }

    internal string Handle { get; }

    /// <summary>Creates a readable stream from C#-produced byte chunks.</summary>
    public static ReadableStream FromAsyncEnumerable(
        IAsyncEnumerable<ReadOnlyMemory<byte>> chunks,
        CancellationToken cancellationToken = default)
    {
        var handle = ManagedReadableStreamRegistry.Register(chunks, cancellationToken);
        return new ReadableStream(handle);
    }

    /// <summary>Reads the next byte chunk from the stream.</summary>
    public async Task<ReadableStreamReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (Source == NativeStreamSource.Managed)
            return await ManagedReadableStreamRegistry.PullAsync(Handle);

        var result = await DispatchAsync("stream.read", cancellationToken);
        var read = JsonSerializer.Deserialize(result, NativeBodyJsonContext.Default.NativeStreamReadResult)
            ?? throw new WorkersException("Native stream read returned an empty result.");

        return new ReadableStreamReadResult(
            read.Done,
            read.BodyBase64 is null ? ReadOnlyMemory<byte>.Empty : Convert.FromBase64String(read.BodyBase64));
    }

    /// <summary>Reads all remaining chunks from the stream.</summary>
    public async Task<ReadOnlyMemory<byte>> ReadAllBytesAsync(CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        while (true)
        {
            var read = await ReadAsync(cancellationToken);
            if (read.Done)
                break;

            buffer.Write(read.Bytes.Span);
        }

        return buffer.ToArray();
    }

    /// <summary>Cancels the stream reader.</summary>
    public async Task CancelAsync(CancellationToken cancellationToken = default)
    {
        if (Source == NativeStreamSource.Managed)
        {
            await ManagedReadableStreamRegistry.CancelAsync(Handle);
            return;
        }

        await DispatchAsync("stream.cancel", cancellationToken);
    }

    private Task<string> DispatchAsync(string operation, CancellationToken cancellationToken)
    {
        if (_invocationId is null || _dispatcher is null)
            throw new WorkersException("Native stream operations require a live Worker invocation.");

        var invocation = new BindingInvocation(
            _invocationId,
            "$stream",
            operation,
            JsonSerializer.Serialize(
                new NativeStreamRequest(SourceName(Source), Handle),
                NativeBodyJsonContext.Default.NativeStreamRequest));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private static string SourceName(NativeStreamSource source) =>
        source switch
        {
            NativeStreamSource.Request => "request",
            NativeStreamSource.Response => "response",
            NativeStreamSource.Managed => "managed",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unsupported native stream source.")
        };
}

/// <summary>A single readable stream chunk.</summary>
public sealed record ReadableStreamReadResult(bool Done, ReadOnlyMemory<byte> Bytes);

internal enum NativeStreamSource
{
    Request,
    Response,
    Managed
}
