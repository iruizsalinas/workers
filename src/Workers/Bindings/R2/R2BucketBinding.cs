using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Workers;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "R2 binding proxy payloads and envelopes are SDK-defined JSON shapes; browser-wasm workers keep reflection JSON enabled by default.")]
internal sealed class R2BucketBinding : IR2Bucket
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public R2BucketBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<R2Object?> HeadAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var result = await DispatchAsync("r2.head", new { key }, cancellationToken);
        if (string.Equals(result, "null", StringComparison.Ordinal))
            return null;

        var envelope = JsonSerializer.Deserialize<R2ObjectEnvelope>(result, JsonOptions);
        return envelope is null ? null : R2Object.FromEnvelope(envelope);
    }

    public async Task<Body?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        return await GetAsync(key, options: null, cancellationToken);
    }

    public async Task<Body?> GetAsync(
        string key,
        R2GetOptions? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var result = await DispatchAsync("r2.get", R2Payloads.Get(key, options), cancellationToken);
        using var document = JsonDocument.Parse(result);
        if (document.RootElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
            !document.RootElement.TryGetProperty("bodyBase64", out var bodyBase64Element) ||
            bodyBase64Element.ValueKind is JsonValueKind.Null)
            return null;

        var contentType = document.RootElement.TryGetProperty("contentType", out var contentTypeElement) &&
            contentTypeElement.ValueKind is not JsonValueKind.Null
                ? contentTypeElement.GetString()
                : null;

        return Body.FromBytes(
            Convert.FromBase64String(bodyBase64Element.GetString() ?? string.Empty),
            contentType ?? "application/octet-stream");
    }

    public Task PutAsync(string key, Body body, CancellationToken cancellationToken = default)
    {
        return PutAsync(key, body, options: null, cancellationToken);
    }

    public async Task PutAsync(
        string key,
        Body body,
        R2PutOptions? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(body);

        _ = await DispatchAsync(
            "r2.put",
            R2PutRequest.From(key, body, options),
            cancellationToken);
    }

    public async Task<R2Object?> PutObjectAsync(
        string key,
        Body body,
        R2PutOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(body);

        var result = await DispatchAsync(
            "r2.put",
            R2PutRequest.From(key, body, options),
            cancellationToken);

        if (string.Equals(result, "null", StringComparison.Ordinal))
            return null;

        var envelope = JsonSerializer.Deserialize<R2ObjectEnvelope>(result, JsonOptions);
        return envelope is null ? null : R2Object.FromEnvelope(envelope);
    }

    public async Task<R2MultipartUpload> CreateMultipartUploadAsync(
        string key,
        R2MultipartUploadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var result = await DispatchAsync(
            "r2.multipart.create",
            new { key, options = R2MultipartUploadOptionsEnvelope.From(options) },
            cancellationToken);

        var envelope = JsonSerializer.Deserialize<R2MultipartUploadEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("R2 multipart upload creation returned an empty result.");
        return MultipartUpload(envelope.Key, envelope.UploadId);
    }

    public R2MultipartUpload ResumeMultipartUpload(string key, string uploadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadId);
        return MultipartUpload(key, uploadId);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return DispatchAsync("r2.delete", new { key }, cancellationToken);
    }

    public Task DeleteAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var keyArray = keys.ToArray();
        if (keyArray.Length is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(keys), keyArray.Length, "R2 multi-delete requires from 1 through 1000 keys.");

        foreach (var key in keyArray)
            ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(keys));

        return DispatchAsync("r2.deleteMany", new { keys = keyArray }, cancellationToken);
    }

    public async Task<R2Objects> ListAsync(R2ListOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("r2.list", R2ListRequest.From(options), cancellationToken);
        var envelope = JsonSerializer.Deserialize<R2ObjectsEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("R2 list returned an empty result.");

        return new R2Objects(
            envelope.Objects.Select(R2Object.FromEnvelope).ToArray(),
            envelope.Truncated,
            envelope.Cursor,
            envelope.DelimitedPrefixes);
    }

    private Task<string> DispatchAsync(string operation, object payload, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            operation,
            JsonSerializer.Serialize(payload, JsonOptions));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private R2MultipartUpload MultipartUpload(string key, string uploadId) =>
        new(_invocationId, _bindingName, key, uploadId, _dispatcher);
}
