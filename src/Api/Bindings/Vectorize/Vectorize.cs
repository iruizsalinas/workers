namespace Workers;

/// <summary>A Vectorize index.</summary>
public sealed class Vectorize
{
    private Vectorize() { }

    public Task<VectorizeIndexInfo> DescribeAsync() => WorkerApi.NotExecutable<Task<VectorizeIndexInfo>>();
    public Task<VectorizeMatches> QueryAsync(IEnumerable<double> vector, VectorizeQueryOptions? options = null) =>
        WorkerApi.NotExecutable<Task<VectorizeMatches>>();
    public Task<VectorizeMatches> QueryByIdAsync(string vectorId, VectorizeQueryOptions? options = null) =>
        WorkerApi.NotExecutable<Task<VectorizeMatches>>();
    public Task<VectorizeAsyncMutation> InsertAsync(IEnumerable<VectorizeVector> vectors) => WorkerApi.NotExecutable<Task<VectorizeAsyncMutation>>();
    public Task<VectorizeAsyncMutation> UpsertAsync(IEnumerable<VectorizeVector> vectors) => WorkerApi.NotExecutable<Task<VectorizeAsyncMutation>>();
    public Task<VectorizeAsyncMutation> DeleteByIdsAsync(IEnumerable<string> ids) => WorkerApi.NotExecutable<Task<VectorizeAsyncMutation>>();
    public Task<IReadOnlyList<VectorizeVector>> GetByIdsAsync(IEnumerable<string> ids) => WorkerApi.NotExecutable<Task<IReadOnlyList<VectorizeVector>>>();
}

public sealed class VectorizeVector
{
    public required string Id { get; init; }
    public required IReadOnlyList<double> Values { get; init; }
    public string? Namespace { get; init; }
    /// <summary>Metadata stored with the vector. Build it with <see cref="JsonValue.From{T}"/>.</summary>
    public JsonValue? Metadata { get; init; }
}

public enum VectorizeMetadataRetrievalLevel
{
    None,
    Indexed,
    All
}

public sealed class VectorizeQueryOptions
{
    public int? TopK { get; init; }
    public string? Namespace { get; init; }
    public bool? ReturnValues { get; init; }
    public VectorizeMetadataRetrievalLevel? ReturnMetadata { get; init; }
    /// <summary>A metadata filter such as <c>JsonValue.From(new { genre = "drama" })</c>.</summary>
    public JsonValue? Filter { get; init; }
}

public sealed class VectorizeMatches
{
    private VectorizeMatches() { }

    public IReadOnlyList<VectorizeMatch> Matches => WorkerApi.NotExecutable<IReadOnlyList<VectorizeMatch>>();
    public int Count => WorkerApi.NotExecutable<int>();
}

public sealed class VectorizeMatch
{
    private VectorizeMatch() { }

    public string Id => WorkerApi.NotExecutable<string>();
    public double Score => WorkerApi.NotExecutable<double>();
    public IReadOnlyList<double>? Values => WorkerApi.NotExecutable<IReadOnlyList<double>?>();
    public string? Namespace => WorkerApi.NotExecutable<string?>();
    public JsonValue? Metadata => WorkerApi.NotExecutable<JsonValue?>();
}

public sealed class VectorizeAsyncMutation
{
    private VectorizeAsyncMutation() { }

    public string MutationId => WorkerApi.NotExecutable<string>();
}

public sealed class VectorizeIndexInfo
{
    private VectorizeIndexInfo() { }

    public long VectorCount => WorkerApi.NotExecutable<long>();
    public int Dimensions => WorkerApi.NotExecutable<int>();
    public double ProcessedUpToDatetime => WorkerApi.NotExecutable<double>();
    public double ProcessedUpToMutation => WorkerApi.NotExecutable<double>();
}
