using System.Text.Json;

namespace Workers;

/// <summary>Options for creating an R2 multipart upload.</summary>
public sealed class R2MultipartUploadOptions
{
    /// <summary>HTTP metadata to store with the completed object.</summary>
    public R2HttpMetadata? HttpMetadata { get; init; }

    /// <summary>Custom metadata to store with the completed object.</summary>
    public IReadOnlyDictionary<string, string>? CustomMetadata { get; init; }
}

/// <summary>An uploaded R2 multipart-upload part.</summary>
public sealed record R2UploadedPart(int PartNumber, string Etag);

/// <summary>An R2 multipart upload.</summary>
public sealed class R2MultipartUpload
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    internal R2MultipartUpload(
        string invocationId,
        string bindingName,
        string key,
        string uploadId,
        IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadId);

        _invocationId = invocationId;
        _bindingName = bindingName;
        Key = key;
        UploadId = uploadId;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>The object key being uploaded.</summary>
    public string Key { get; }

    /// <summary>The multipart upload id.</summary>
    public string UploadId { get; }

    /// <summary>Uploads one multipart part.</summary>
    public async Task<R2UploadedPart> UploadPartAsync(
        int partNumber,
        Body body,
        CancellationToken cancellationToken = default)
    {
        if (partNumber is < 1 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(partNumber), partNumber, "R2 multipart part numbers must be from 1 through 10000.");

        ArgumentNullException.ThrowIfNull(body);

        var result = await DispatchAsync(
            "r2.multipart.uploadPart",
            new MultipartUploadPartRequest(Key, UploadId, partNumber, Convert.ToBase64String(body.InternalBytes.Span)),
            cancellationToken);

        return JsonSerializer.Deserialize<R2UploadedPart>(result, JsonOptions)
            ?? throw new WorkersException("R2 multipart upload part returned an empty result.");
    }

    /// <summary>Completes the multipart upload and returns the completed object metadata.</summary>
    public async Task<R2Object> CompleteAsync(
        IEnumerable<R2UploadedPart> parts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var partArray = parts.ToArray();
        if (partArray.Length == 0)
            throw new ArgumentException("At least one uploaded part is required.", nameof(parts));

        var result = await DispatchAsync(
            "r2.multipart.complete",
            new MultipartCompleteRequest(Key, UploadId, partArray),
            cancellationToken);

        var envelope = JsonSerializer.Deserialize<R2ObjectEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("R2 multipart complete returned an empty result.");
        return R2Object.FromEnvelope(envelope);
    }

    /// <summary>Aborts the multipart upload.</summary>
    public Task AbortAsync(CancellationToken cancellationToken = default) =>
        DispatchAsync("r2.multipart.abort", new MultipartUploadRequest(Key, UploadId), cancellationToken);

    private Task<string> DispatchAsync(string operation, object payload, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            operation,
            JsonSerializer.Serialize(payload, JsonOptions));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private sealed record MultipartUploadRequest(string Key, string UploadId);

    private sealed record MultipartUploadPartRequest(string Key, string UploadId, int PartNumber, string BodyBase64);

    private sealed record MultipartCompleteRequest(string Key, string UploadId, IReadOnlyList<R2UploadedPart> Parts);
}

internal sealed record R2MultipartUploadEnvelope(string Key, string UploadId);

internal sealed record R2MultipartUploadOptionsEnvelope(
    R2HttpMetadataEnvelope? HttpMetadata,
    IReadOnlyDictionary<string, string>? CustomMetadata)
{
    public static R2MultipartUploadOptionsEnvelope? From(R2MultipartUploadOptions? options)
    {
        if (options is null)
            return null;

        return new R2MultipartUploadOptionsEnvelope(
            R2HttpMetadataEnvelope.From(options.HttpMetadata),
            R2Object.CopyCustomMetadata(options.CustomMetadata));
    }
}
