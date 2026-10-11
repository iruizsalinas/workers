namespace Workers;

public interface IVectorizeIndex : IBinding
{
    Task<VectorizeMutationResult> InsertAsync(IEnumerable<VectorizeVector> vectors);
    Task<VectorizeMutationResult> UpsertAsync(IEnumerable<VectorizeVector> vectors);
    Task<VectorizeQueryResult> QueryAsync(IEnumerable<double> vector, VectorizeQueryOptions? options = null);
    Task<IReadOnlyList<VectorizeVector>> GetByIdsAsync(IEnumerable<string> ids);
    Task<VectorizeMutationResult> DeleteByIdsAsync(IEnumerable<string> ids);
    Task<VectorizeQueryResult> QueryByIdAsync(string id, VectorizeQueryOptions? options = null);
    Task<VectorizeIndexDetails> DescribeAsync();
}

public sealed class VectorizeVector
{
    public string Id { get; init; } = "";
    public IReadOnlyList<double> Values { get; init; } = [];
    public string? Namespace { get; init; }
    public JsonValue? Metadata { get; init; }
}

public sealed class VectorizeQueryOptions
{
    public int? TopK { get; init; }
    public bool? ReturnValues { get; init; }
    public JsonValue? Filter { get; init; }
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
    public JsonValue? Metadata { get; init; }
}

public sealed class VectorizeIndexDetails
{
    public int? Dimensions { get; init; }
    public string? Metric { get; init; }
    public long? VectorCount { get; init; }
}
