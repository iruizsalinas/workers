namespace Workers;

public interface IVectorizeIndex : IBinding
{
    Task<VectorizeMutationResult> InsertAsync(IEnumerable<VectorizeVector> vectors, CancellationToken cancellationToken = default);
    Task<VectorizeMutationResult> UpsertAsync(IEnumerable<VectorizeVector> vectors, CancellationToken cancellationToken = default);
    Task<VectorizeQueryResult> QueryAsync(IEnumerable<double> vector, VectorizeQueryOptions? options = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VectorizeVector>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);
    Task<VectorizeMutationResult> DeleteByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);
    Task<VectorizeQueryResult> QueryByIdAsync(string id, VectorizeQueryOptions? options = null, CancellationToken cancellationToken = default);
    Task<VectorizeIndexDetails> DescribeAsync(CancellationToken cancellationToken = default);
}

public sealed class VectorizeVector
{
    public string Id { get; init; } = "";
    public IReadOnlyList<double> Values { get; init; } = [];
    public string? Namespace { get; init; }
    public JsonElement? Metadata { get; init; }
}

public sealed class VectorizeQueryOptions
{
    public int? TopK { get; init; }
    public bool? ReturnValues { get; init; }
    public JsonElement? Filter { get; init; }
}

public sealed class VectorizeMutationResult
{
    public string? MutationId { get; init; }
}

public sealed class VectorizeQueryResult
{
    public IReadOnlyList<VectorizeMatch> Matches { get; init; } = [];
}

public sealed class VectorizeMatch
{
    public string Id { get; init; } = "";
    public double Score { get; init; }
    public IReadOnlyList<double>? Values { get; init; }
    public JsonElement? Metadata { get; init; }
}

public sealed class VectorizeIndexDetails
{
    public int? Dimensions { get; init; }
    public string? Metric { get; init; }
    public long? VectorCount { get; init; }
}

public enum VectorizeReturnMetadata
{
    None,
    Indexed,
    All
}
