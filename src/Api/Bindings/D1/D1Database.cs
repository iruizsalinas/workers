namespace Workers;

public interface ID1Database : IBinding
{
    D1PreparedStatement Prepare(string query);
    D1DatabaseSession WithSession(D1SessionMode mode = D1SessionMode.FirstUnconstrained);
    D1DatabaseSession WithSession(string bookmark);
    Task<D1ExecResult> ExecAsync(string query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<D1Result<T>>> BatchAsync<T>(IEnumerable<D1PreparedStatement> statements, CancellationToken cancellationToken = default);
    Task<Body> DumpAsync(CancellationToken cancellationToken = default);
}

public sealed class D1PreparedStatement
{
    public string Query => WorkerApi.NotExecutable<string>();

    public D1PreparedStatement Bind(params object?[] values) => WorkerApi.NotExecutable<D1PreparedStatement>();
    public Task<D1Result> RunAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<D1Result>>();
    public Task<D1Result<T>> AllAsync<T>(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<D1Result<T>>>();
    public Task<IReadOnlyList<IReadOnlyList<JsonElement>>> RawAsync(
        D1RawOptions? options = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<IReadOnlyList<IReadOnlyList<JsonElement>>>>();
    public Task<T?> FirstAsync<T>(string? columnName = null, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<T?>>();
}

public sealed class D1Result
{
    public bool Success { get; init; }
    public string? Error { get; init; }
}

public sealed class D1Result<T>
{
    public IReadOnlyList<T> Results { get; init; } = [];
    public bool Success { get; init; }
    public string? Error { get; init; }
}

public sealed class D1ExecResult
{
    public int Count { get; init; }
    public double Duration { get; init; }
}

public sealed record D1RawOptions
{
    public bool? ColumnNames { get; init; }
}

public enum D1SessionMode
{
    FirstPrimary,
    FirstUnconstrained
}

public sealed class D1DatabaseSession
{
    public D1PreparedStatement Prepare(string query) => WorkerApi.NotExecutable<D1PreparedStatement>();
    public Task<IReadOnlyList<D1Result<T>>> BatchAsync<T>(
        IEnumerable<D1PreparedStatement> statements, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<IReadOnlyList<D1Result<T>>>>();
    public Task<string?> GetBookmarkAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<string?>>();
}

public sealed class D1ResultMetadata;
public readonly record struct D1Value(object? Value);
