using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workers;

/// <summary>A vector stored in a Vectorize index.</summary>
public sealed class VectorizeVector
{
    /// <summary>The vector ID.</summary>
    public string Id { get; init; } = "";

    /// <summary>The vector values.</summary>
    public IReadOnlyList<double> Values { get; init; } = [];

    /// <summary>An optional namespace partition for the vector.</summary>
    public string? Namespace { get; init; }

    /// <summary>Optional JSON-compatible vector metadata.</summary>
    public JsonElement? Metadata { get; init; }
}

/// <summary>Controls how much metadata a Vectorize query returns.</summary>
public enum VectorizeReturnMetadata
{
    /// <summary>Do not return metadata.</summary>
    None,

    /// <summary>Return only metadata fields indexed for filtering.</summary>
    Indexed,

    /// <summary>Return all metadata stored with each vector.</summary>
    All
}

/// <summary>Options used when querying a Vectorize index.</summary>
public sealed record VectorizeQueryOptions
{
    /// <summary>The maximum number of matches to return.</summary>
    public int? TopK { get; init; }

    /// <summary>Whether vector values should be returned with each match.</summary>
    public bool? ReturnValues { get; init; }

    /// <summary>Controls whether metadata should be returned with each match.</summary>
    public VectorizeReturnMetadata? ReturnMetadata { get; init; }

    /// <summary>An optional Vectorize metadata filter.</summary>
    public JsonElement? Filter { get; init; }

    /// <summary>An optional namespace partition to query.</summary>
    public string? Namespace { get; init; }
}

/// <summary>A Vectorize asynchronous mutation result.</summary>
public sealed class VectorizeMutationResult
{
    /// <summary>The mutation identifier returned by Vectorize.</summary>
    public string? MutationId { get; init; }

    /// <summary>Additional fields returned by the runtime for this mutation.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>One Vectorize query match.</summary>
public sealed class VectorizeMatch
{
    /// <summary>The matched vector ID.</summary>
    public string Id { get; init; } = "";

    /// <summary>The match score.</summary>
    public double Score { get; init; }

    /// <summary>Vector values, when requested.</summary>
    public IReadOnlyList<double>? Values { get; init; }

    /// <summary>Vector metadata, when requested.</summary>
    public JsonElement? Metadata { get; init; }
}

/// <summary>Result returned by a Vectorize query.</summary>
public sealed class VectorizeQueryResult
{
    /// <summary>The returned matches.</summary>
    public IReadOnlyList<VectorizeMatch> Matches { get; init; } = [];

    /// <summary>The number of matches returned, when supplied by the runtime.</summary>
    public int? Count { get; init; }
}

/// <summary>Configuration and status details for a Vectorize index.</summary>
public sealed class VectorizeIndexDetails
{
    /// <summary>The configured vector dimensions.</summary>
    public int? Dimensions { get; init; }

    /// <summary>The configured distance metric.</summary>
    public string? Metric { get; init; }

    /// <summary>The number of vectors in the index, when supplied by the runtime.</summary>
    public long? VectorCount { get; init; }

    /// <summary>Additional fields returned by the runtime.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed class VectorizeIndexBinding : IVectorizeIndex
{
    private const int MaxMutationVectors = 1000;
    private const int MaxIds = 1000;
    private const int MaxTopK = 100;
    private const int MaxExpandedTopK = 50;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public VectorizeIndexBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);

        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public Task<VectorizeMutationResult> InsertAsync(
        IEnumerable<VectorizeVector> vectors,
        CancellationToken cancellationToken = default) =>
        DispatchAsync<VectorizeMutationResult>(
            "vectorize.insert",
            new { vectors = VectorPayloads(vectors) },
            cancellationToken);

    public Task<VectorizeMutationResult> UpsertAsync(
        IEnumerable<VectorizeVector> vectors,
        CancellationToken cancellationToken = default) =>
        DispatchAsync<VectorizeMutationResult>(
            "vectorize.upsert",
            new { vectors = VectorPayloads(vectors) },
            cancellationToken);

    public Task<VectorizeQueryResult> QueryAsync(
        IEnumerable<double> vector,
        VectorizeQueryOptions? options = null,
        CancellationToken cancellationToken = default) =>
        DispatchAsync<VectorizeQueryResult>(
            "vectorize.query",
            new { vector = VectorValues(vector), options = QueryOptionsPayload.From(options) },
            cancellationToken);

    public Task<VectorizeQueryResult> QueryByIdAsync(
        string id,
        VectorizeQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return DispatchAsync<VectorizeQueryResult>(
            "vectorize.queryById",
            new { id, options = QueryOptionsPayload.From(options) },
            cancellationToken);
    }

    public Task<IReadOnlyList<VectorizeVector>> GetByIdsAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default) =>
        DispatchAsync<IReadOnlyList<VectorizeVector>>(
            "vectorize.getByIds",
            new { ids = Ids(ids) },
            cancellationToken);

    public Task<VectorizeMutationResult> DeleteByIdsAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default) =>
        DispatchAsync<VectorizeMutationResult>(
            "vectorize.deleteByIds",
            new { ids = Ids(ids) },
            cancellationToken);

    public Task<VectorizeIndexDetails> DescribeAsync(CancellationToken cancellationToken = default) =>
        DispatchAsync<VectorizeIndexDetails>("vectorize.describe", new { }, cancellationToken);

    private async Task<T> DispatchAsync<T>(string operation, object payload, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            operation,
            JsonSerializer.Serialize(payload, JsonOptions));

        var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
        return JsonSerializer.Deserialize<T>(result, JsonOptions)
            ?? throw new WorkersException($"Vectorize operation '{operation}' returned an empty result.");
    }

    private static IReadOnlyList<VectorizeVectorPayload> VectorPayloads(IEnumerable<VectorizeVector> vectors)
    {
        ArgumentNullException.ThrowIfNull(vectors);

        var payloads = vectors.Select(vector =>
        {
            ArgumentNullException.ThrowIfNull(vector);
            ArgumentException.ThrowIfNullOrWhiteSpace(vector.Id);

            return new VectorizeVectorPayload(
                vector.Id,
                VectorValues(vector.Values),
                vector.Namespace,
                vector.Metadata);
        }).ToArray();

        if (payloads.Length is < 1 or > MaxMutationVectors)
            throw new ArgumentOutOfRangeException(nameof(vectors), payloads.Length, "Vectorize mutations must contain between 1 and 1000 vectors.");

        return payloads;
    }

    private static IReadOnlyList<double> VectorValues(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var valueArray = values.ToArray();
        if (valueArray.Length == 0)
            throw new ArgumentException("A vector must contain at least one value.", nameof(values));

        foreach (var value in valueArray)
        {
            if (!double.IsFinite(value))
                throw new ArgumentOutOfRangeException(nameof(values), value, "Vector values must be finite.");
        }

        return valueArray;
    }

    private static IReadOnlyList<string> Ids(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idArray = ids.ToArray();
        if (idArray.Length is < 1 or > MaxIds)
            throw new ArgumentOutOfRangeException(nameof(ids), idArray.Length, "Vectorize ID operations must contain between 1 and 1000 IDs.");

        foreach (var id in idArray)
            ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return idArray;
    }

    private sealed record VectorizeVectorPayload(
        string Id,
        IReadOnlyList<double> Values,
        string? Namespace,
        JsonElement? Metadata);

    private sealed record QueryOptionsPayload(
        int? TopK,
        bool? ReturnValues,
        string? ReturnMetadata,
        JsonElement? Filter,
        string? Namespace)
    {
        public static QueryOptionsPayload? From(VectorizeQueryOptions? options)
        {
            if (options is null)
                return null;

            if (options.TopK is < 1 or > MaxTopK)
                throw new ArgumentOutOfRangeException(nameof(options), options.TopK, "Vectorize topK must be between 1 and 100.");

            if (options.TopK > MaxExpandedTopK
                && (options.ReturnValues == true || options.ReturnMetadata == VectorizeReturnMetadata.All))
                throw new ArgumentOutOfRangeException(nameof(options), options.TopK, "Vectorize topK cannot exceed 50 when returning values or all metadata.");

            return new QueryOptionsPayload(
                options.TopK,
                options.ReturnValues,
                options.ReturnMetadata is null ? null : ReturnMetadataValue(options.ReturnMetadata.Value),
                options.Filter,
                options.Namespace);
        }

        private static string ReturnMetadataValue(VectorizeReturnMetadata value) =>
            value switch
            {
                VectorizeReturnMetadata.None => "none",
                VectorizeReturnMetadata.Indexed => "indexed",
                VectorizeReturnMetadata.All => "all",
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported Vectorize metadata return mode.")
            };
    }
}
