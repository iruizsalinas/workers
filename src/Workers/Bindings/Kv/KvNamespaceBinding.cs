using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Workers;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "KV binding proxy payloads and envelopes are SDK-defined JSON shapes; browser-wasm workers keep reflection JSON enabled by default.")]
internal sealed class KvNamespaceBinding : IKvNamespace
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public KvNamespaceBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<string?> GetTextAsync(string key, CancellationToken cancellationToken = default)
    {
        return await GetTextAsync(key, options: null, cancellationToken);
    }

    public async Task<string?> GetTextAsync(string key, KvGetOptions? options, CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("kv.getText", KvPayloads.Get(key, options), cancellationToken);
        using var document = JsonDocument.Parse(result);
        return document.RootElement.TryGetProperty("value", out var value) && value.ValueKind is not JsonValueKind.Null
            ? value.GetString()
            : null;
    }

    public Task PutTextAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        return PutTextAsync(key, value, options: null, cancellationToken);
    }

    public Task PutTextAsync(string key, string value, KvPutOptions? options, CancellationToken cancellationToken = default)
    {
        return DispatchAsync("kv.putText", KvPutTextRequest.From(key, value, options), cancellationToken);
    }

    public async Task<KvValueWithMetadata<string>> GetTextWithMetadataAsync(
        string key,
        KvGetOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("kv.getTextWithMetadata", KvPayloads.Get(key, options), cancellationToken)
            ;
        var envelope = JsonSerializer.Deserialize<KvTextWithMetadataEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("KV metadata read returned an empty result.");

        return new KvValueWithMetadata<string>(envelope.Value, envelope.Metadata?.Clone());
    }

    public async Task<IReadOnlyDictionary<string, string?>> GetTextBulkAsync(
        IEnumerable<string> keys,
        KvGetOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("kv.getTextBulk", KvGetBulkRequest.From(keys, options), cancellationToken)
            ;
        var envelope = JsonSerializer.Deserialize<KvTextBulkEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("KV bulk read returned an empty result.");

        return new Dictionary<string, string?>(envelope.Values, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyDictionary<string, KvValueWithMetadata<string>>> GetTextBulkWithMetadataAsync(
        IEnumerable<string> keys,
        KvGetOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("kv.getTextBulkWithMetadata", KvGetBulkRequest.From(keys, options), cancellationToken)
            ;
        var envelope = JsonSerializer.Deserialize<KvTextBulkWithMetadataEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("KV bulk metadata read returned an empty result.");

        return envelope.Values.ToDictionary(
            static pair => pair.Key,
            static pair => new KvValueWithMetadata<string>(pair.Value.Value, pair.Value.Metadata?.Clone()),
            StringComparer.Ordinal);
    }

    public async Task<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default)
    {
        return await GetBytesAsync(key, options: null, cancellationToken);
    }

    public async Task<byte[]?> GetBytesAsync(string key, KvGetOptions? options, CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("kv.getBytes", KvPayloads.Get(key, options), cancellationToken);
        var envelope = JsonSerializer.Deserialize<KvBytesEnvelope>(result, JsonOptions);
        return envelope?.BodyBase64 is null
            ? null
            : Convert.FromBase64String(envelope.BodyBase64);
    }

    public async Task<KvValueWithMetadata<byte[]>> GetBytesWithMetadataAsync(
        string key,
        KvGetOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("kv.getBytesWithMetadata", KvPayloads.Get(key, options), cancellationToken)
            ;
        var envelope = JsonSerializer.Deserialize<KvBytesWithMetadataEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("KV metadata read returned an empty result.");
        var value = envelope.BodyBase64 is null
            ? null
            : Convert.FromBase64String(envelope.BodyBase64);

        return new KvValueWithMetadata<byte[]>(value, envelope.Metadata?.Clone());
    }

    public Task PutBytesAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        return PutBytesAsync(key, value, options: null, cancellationToken);
    }

    public Task PutBytesAsync(string key, ReadOnlyMemory<byte> value, KvPutOptions? options, CancellationToken cancellationToken = default)
    {
        return DispatchAsync("kv.putBytes", KvPutBytesRequest.From(key, value, options), cancellationToken);
    }

    public async Task<T?> GetJsonAsync<T>(
        string key,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<T>(key, options: null, jsonOptions, cancellationToken);
    }

    public async Task<T?> GetJsonAsync<T>(
        string key,
        KvGetOptions? options,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("kv.getJson", KvPayloads.Get(key, options), cancellationToken);
        using var document = JsonDocument.Parse(result);
        var value = document.RootElement.TryGetProperty("value", out var element)
            ? element
            : (JsonElement?)null;
        return DeserializeJsonValue<T>(value, jsonOptions);
    }

    public async Task<KvValueWithMetadata<T>> GetJsonWithMetadataAsync<T>(
        string key,
        KvGetOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("kv.getJsonWithMetadata", KvPayloads.Get(key, options), cancellationToken)
            ;
        using var document = JsonDocument.Parse(result);
        if (!document.RootElement.TryGetProperty("value", out var value))
            throw new WorkersException("KV metadata read returned an empty result.");

        var metadata = document.RootElement.TryGetProperty("metadata", out var metadataElement) &&
            metadataElement.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? metadataElement.Clone()
            : (JsonElement?)null;

        return new KvValueWithMetadata<T>(
            DeserializeJsonValue<T>(value, jsonOptions),
            metadata);
    }

    public Task PutJsonAsync<T>(
        string key,
        T value,
        KvPutOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchAsync("kv.putJson", KvPutJsonRequest.From(key, value, options, jsonOptions), cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, T?>> GetJsonBulkAsync<T>(
        IEnumerable<string> keys,
        KvGetOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("kv.getJsonBulk", KvGetBulkRequest.From(keys, options), cancellationToken)
            ;
        var envelope = JsonSerializer.Deserialize<KvJsonBulkEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("KV bulk read returned an empty result.");

        return envelope.Values.ToDictionary(
            static pair => pair.Key,
            pair => DeserializeJsonValue<T>(pair.Value, jsonOptions),
            StringComparer.Ordinal);
    }

    public async Task<IReadOnlyDictionary<string, KvValueWithMetadata<T>>> GetJsonBulkWithMetadataAsync<T>(
        IEnumerable<string> keys,
        KvGetOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("kv.getJsonBulkWithMetadata", KvGetBulkRequest.From(keys, options), cancellationToken)
            ;
        var envelope = JsonSerializer.Deserialize<KvJsonBulkWithMetadataEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("KV bulk metadata read returned an empty result.");

        return envelope.Values.ToDictionary(
            static pair => pair.Key,
            pair => new KvValueWithMetadata<T>(
                DeserializeJsonValue<T>(pair.Value.Value, jsonOptions),
                pair.Value.Metadata?.Clone()),
            StringComparer.Ordinal);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return DispatchAsync("kv.delete", new { key }, cancellationToken);
    }

    public async Task<KvListResult> ListAsync(KvListOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("kv.list", KvListRequest.From(options), cancellationToken);
        return ParseListResult(result);
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

    private static T? DeserializeJsonValue<T>(JsonElement? value, JsonSerializerOptions? jsonOptions)
    {
        if (value is not { } element || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return default;

        return element.Deserialize<T>(jsonOptions);
    }

    private static KvListResult ParseListResult(string result)
    {
        using var document = JsonDocument.Parse(result);
        var root = document.RootElement;

        var keys = root.TryGetProperty("keys", out var keysElement) && keysElement.ValueKind == JsonValueKind.Array
            ? keysElement.EnumerateArray().Select(ParseKey).ToArray()
            : [];
        var listComplete = root.TryGetProperty("listComplete", out var listCompleteElement)
            && listCompleteElement.ValueKind == JsonValueKind.True;
        var cursor = root.TryGetProperty("cursor", out var cursorElement) && cursorElement.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? cursorElement.GetString()
            : null;

        return new KvListResult(keys, listComplete, cursor);
    }

    private static KvKey ParseKey(JsonElement element)
    {
        var name = element.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString()
            : null;
        if (string.IsNullOrEmpty(name))
            throw new WorkersException("KV list returned a key without a name.");

        var expiration = element.TryGetProperty("expiration", out var expirationElement) &&
            expirationElement.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? expirationElement.GetUInt64()
            : (ulong?)null;
        var metadata = element.TryGetProperty("metadata", out var metadataElement) &&
            metadataElement.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? metadataElement.Clone()
            : (JsonElement?)null;

        return new KvKey(name, expiration, metadata);
    }
}
