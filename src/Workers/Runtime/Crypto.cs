using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Workers;

/// <summary>Digest algorithms supported by the Workers Web Crypto API.</summary>
public enum DigestAlgorithm
{
    /// <summary>SHA-1.</summary>
    Sha1,

    /// <summary>SHA-256.</summary>
    Sha256,

    /// <summary>SHA-384.</summary>
    Sha384,

    /// <summary>SHA-512.</summary>
    Sha512
}

/// <summary>Helpers for Workers platform cryptography APIs.</summary>
public sealed partial class Crypto
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly CryptoJsonContext JsonContext = new(new JsonSerializerOptions(JsonOptions));

    private readonly string _invocationId;
    private readonly IBindingDispatcher _dispatcher;

    internal Crypto(string invocationId, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        _invocationId = invocationId;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>Generates a Workers runtime random version 4 UUID.</summary>
    public async Task<string> RandomUuidAsync(CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "crypto.randomUUID",
            EmptyPayload.Instance,
            JsonContext.EmptyPayload,
            cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.RandomUuidResult)
            ?? throw new WorkersException("Crypto random UUID returned an empty result.");

        return envelope.Value;
    }

    /// <summary>Generates cryptographically secure random bytes using the Workers runtime.</summary>
    public async Task<byte[]> GetRandomBytesAsync(int count, CancellationToken cancellationToken = default)
    {
        if (count is < 0 or > 65536)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Random byte count must be between 0 and 65,536.");

        var result = await DispatchAsync(
            "crypto.getRandomValues",
            new RandomBytesRequest(count),
            JsonContext.RandomBytesRequest,
            cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.RandomBytesResult)
            ?? throw new WorkersException("Crypto random bytes returned an empty result.");

        return Convert.FromBase64String(envelope.BodyBase64);
    }

    /// <summary>Compares two byte sequences using the Workers runtime timing-safe comparison.</summary>
    public async Task<bool> TimingSafeEqualAsync(
        ReadOnlyMemory<byte> left,
        ReadOnlyMemory<byte> right,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "crypto.timingSafeEqual",
            new TimingSafeEqualRequest(
                Convert.ToBase64String(left.Span),
                Convert.ToBase64String(right.Span)),
            JsonContext.TimingSafeEqualRequest,
            cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.TimingSafeEqualResult)
            ?? throw new WorkersException("Crypto timing-safe comparison returned an empty result.");

        return envelope.Equal;
    }

    /// <summary>Creates a streaming digest writer for data that should not be retained in memory by the runtime.</summary>
    public async Task<DigestStream> CreateDigestStreamAsync(
        DigestAlgorithm algorithm,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "crypto.digestStream.create",
            JsonSerializer.Serialize(
                new DigestStreamCreateRequest(ToAlgorithmName(algorithm)),
                JsonContext.DigestStreamCreateRequest),
            cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.DigestStreamEnvelope)
            ?? throw new WorkersException("Crypto digest stream creation returned an empty result.");

        return new DigestStream(_invocationId, envelope.Handle, _dispatcher);
    }

    /// <summary>Computes a digest for a UTF-8 string.</summary>
    public Task<byte[]> DigestTextAsync(
        DigestAlgorithm algorithm,
        string value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return DigestBytesAsync(algorithm, Encoding.UTF8.GetBytes(value), cancellationToken);
    }

    /// <summary>Computes a digest for a body.</summary>
    public Task<byte[]> DigestAsync(
        DigestAlgorithm algorithm,
        Body body,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);
        return DigestBytesAsync(algorithm, body.InternalBytes, cancellationToken);
    }

    /// <summary>Computes a digest for bytes.</summary>
    public async Task<byte[]> DigestBytesAsync(
        DigestAlgorithm algorithm,
        ReadOnlyMemory<byte> value,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "crypto.digest",
            JsonSerializer.Serialize(
                new DigestRequest(ToAlgorithmName(algorithm), Convert.ToBase64String(value.Span)),
                JsonContext.DigestRequest),
            cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.DigestResult)
            ?? throw new WorkersException("Crypto digest returned an empty result.");

        return Convert.FromBase64String(envelope.BodyBase64);
    }

    internal static string ToAlgorithmName(DigestAlgorithm algorithm) =>
        algorithm switch
        {
            DigestAlgorithm.Sha1 => "SHA-1",
            DigestAlgorithm.Sha256 => "SHA-256",
            DigestAlgorithm.Sha384 => "SHA-384",
            DigestAlgorithm.Sha512 => "SHA-512",
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported digest algorithm.")
        };

    private Task<string> DispatchAsync<TPayload>(
        string operation,
        TPayload payload,
        JsonTypeInfo<TPayload> typeInfo,
        CancellationToken cancellationToken) =>
        DispatchAsync(operation, JsonSerializer.Serialize(payload, typeInfo), cancellationToken);

    private Task<string> DispatchAsync(string operation, string payloadJson, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            "$crypto",
            operation,
            payloadJson);

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private sealed record EmptyPayload
    {
        public static readonly EmptyPayload Instance = new();
    }

    private sealed record RandomUuidResult(string Value);

    private sealed record RandomBytesRequest(int Count);

    private sealed record RandomBytesResult(string BodyBase64);

    private sealed record TimingSafeEqualRequest(string LeftBase64, string RightBase64);

    private sealed record TimingSafeEqualResult(bool Equal);

    private sealed record DigestRequest(string Algorithm, string BodyBase64);

    private sealed record DigestResult(string BodyBase64);

    private sealed record DigestStreamCreateRequest(string Algorithm);

    private sealed record DigestStreamEnvelope(string Handle);

    [JsonSerializable(typeof(EmptyPayload))]
    [JsonSerializable(typeof(RandomUuidResult))]
    [JsonSerializable(typeof(RandomBytesRequest))]
    [JsonSerializable(typeof(RandomBytesResult))]
    [JsonSerializable(typeof(TimingSafeEqualRequest))]
    [JsonSerializable(typeof(TimingSafeEqualResult))]
    [JsonSerializable(typeof(DigestRequest))]
    [JsonSerializable(typeof(DigestResult))]
    [JsonSerializable(typeof(DigestStreamCreateRequest))]
    [JsonSerializable(typeof(DigestStreamEnvelope))]
    private sealed partial class CryptoJsonContext : JsonSerializerContext
    {
    }
}

/// <summary>A Workers crypto DigestStream handle.</summary>
public sealed partial class DigestStream
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DigestStreamJsonContext JsonContext = new(new JsonSerializerOptions(JsonOptions));

    private readonly string _invocationId;
    private readonly IBindingDispatcher _dispatcher;

    internal DigestStream(string invocationId, string handle, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        _invocationId = invocationId;
        Handle = handle;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>The opaque platform handle for this digest stream.</summary>
    internal string Handle { get; }

    /// <summary>Writes bytes into the digest stream.</summary>
    public Task WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) =>
        DispatchAsync(
            "crypto.digestStream.write",
            new DigestStreamWriteRequest(Handle, Convert.ToBase64String(bytes.Span)),
            JsonContext.DigestStreamWriteRequest,
            cancellationToken);

    /// <summary>Writes UTF-8 text into the digest stream.</summary>
    public Task WriteTextAsync(string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return WriteAsync(Encoding.UTF8.GetBytes(value), cancellationToken);
    }

    /// <summary>Closes the writable side of the digest stream.</summary>
    public Task CloseAsync(CancellationToken cancellationToken = default) =>
        DispatchAsync(
            "crypto.digestStream.close",
            new DigestStreamHandleRequest(Handle),
            JsonContext.DigestStreamHandleRequest,
            cancellationToken);

    /// <summary>Waits for and returns the computed digest.</summary>
    public async Task<byte[]> DigestAsync(CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "crypto.digestStream.digest",
            new DigestStreamHandleRequest(Handle),
            JsonContext.DigestStreamHandleRequest,
            cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.DigestStreamResult)
            ?? throw new WorkersException("Crypto digest stream returned an empty result.");

        return Convert.FromBase64String(envelope.BodyBase64);
    }

    private Task<string> DispatchAsync<TPayload>(
        string operation,
        TPayload payload,
        JsonTypeInfo<TPayload> typeInfo,
        CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            "$crypto",
            operation,
            JsonSerializer.Serialize(payload, typeInfo));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private sealed record DigestStreamHandleRequest(string Handle);

    private sealed record DigestStreamWriteRequest(string Handle, string BodyBase64);

    private sealed record DigestStreamResult(string BodyBase64);

    [JsonSerializable(typeof(DigestStreamHandleRequest))]
    [JsonSerializable(typeof(DigestStreamWriteRequest))]
    [JsonSerializable(typeof(DigestStreamResult))]
    private sealed partial class DigestStreamJsonContext : JsonSerializerContext
    {
    }
}
