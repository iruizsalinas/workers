namespace Workers;

/// <summary>A D1 database. Typed row reads map columns to C# classes and records.</summary>
public sealed class D1Database
{
    private D1Database() { }

    public D1PreparedStatement Prepare(string query) => WorkerApi.NotExecutable<D1PreparedStatement>();
    public Task<IReadOnlyList<D1Result<T>>> BatchAsync<T>(IEnumerable<D1PreparedStatement> statements) =>
        WorkerApi.NotExecutable<Task<IReadOnlyList<D1Result<T>>>>();
    /// <summary>Runs one or more unbound statements separated by semicolons. Prefer prepared statements for input.</summary>
    public Task<D1ExecResult> ExecAsync(string query) => WorkerApi.NotExecutable<Task<D1ExecResult>>();
    /// <summary>Starts a session that reads from replicas, beginning with the given constraint.</summary>
    public D1DatabaseSession WithSession(D1SessionConstraint constraint = D1SessionConstraint.FirstUnconstrained) =>
        WorkerApi.NotExecutable<D1DatabaseSession>();
    /// <summary>Starts a session that sees at least the writes recorded by a bookmark from an earlier session.</summary>
    public D1DatabaseSession WithSession(string bookmark) => WorkerApi.NotExecutable<D1DatabaseSession>();
}

public enum D1SessionConstraint
{
    FirstPrimary,
    FirstUnconstrained
}

public sealed class D1DatabaseSession
{
    private D1DatabaseSession() { }

    public D1PreparedStatement Prepare(string query) => WorkerApi.NotExecutable<D1PreparedStatement>();
    public Task<IReadOnlyList<D1Result<T>>> BatchAsync<T>(IEnumerable<D1PreparedStatement> statements) =>
        WorkerApi.NotExecutable<Task<IReadOnlyList<D1Result<T>>>>();
    public string? GetBookmark() => WorkerApi.NotExecutable<string?>();
}

public sealed class D1PreparedStatement
{
    private D1PreparedStatement() { }

    /// <summary>Binds positional parameters (<c>?</c> or <c>?NNN</c>) and returns the bound statement.</summary>
    public D1PreparedStatement Bind(params object?[] values) => WorkerApi.NotExecutable<D1PreparedStatement>();
    /// <summary>The first row, or null when the query returns no rows.</summary>
    public Task<T?> FirstAsync<T>() => WorkerApi.NotExecutable<Task<T?>>();
    /// <summary>A column of the first row, or null when the query returns no rows.</summary>
    public Task<T?> FirstAsync<T>(string colName) => WorkerApi.NotExecutable<Task<T?>>();
    /// <summary>Runs a statement whose rows are not needed, such as an INSERT, UPDATE or DELETE.</summary>
    public Task<D1Result<JsonValue>> RunAsync() => WorkerApi.NotExecutable<Task<D1Result<JsonValue>>>();
    public Task<D1Result<T>> RunAsync<T>() => WorkerApi.NotExecutable<Task<D1Result<T>>>();
    public Task<D1Result<T>> AllAsync<T>() => WorkerApi.NotExecutable<Task<D1Result<T>>>();
    public Task<IReadOnlyList<IReadOnlyList<JsonValue>>> RawAsync(D1RawOptions? options = null) =>
        WorkerApi.NotExecutable<Task<IReadOnlyList<IReadOnlyList<JsonValue>>>>();
}

public sealed class D1RawOptions
{
    /// <summary>Returns the column names as the first row.</summary>
    public bool? ColumnNames { get; init; }
}

public sealed class D1Result<T>
{
    private D1Result() { }

    public IReadOnlyList<T> Results => WorkerApi.NotExecutable<IReadOnlyList<T>>();
    public bool Success => WorkerApi.NotExecutable<bool>();
    public D1Meta Meta => WorkerApi.NotExecutable<D1Meta>();
}

public sealed class D1ExecResult
{
    private D1ExecResult() { }

    public int Count => WorkerApi.NotExecutable<int>();
    public double Duration => WorkerApi.NotExecutable<double>();
}

public sealed class D1Meta
{
    private D1Meta() { }

    public double Duration => WorkerApi.NotExecutable<double>();
    public long SizeAfter => WorkerApi.NotExecutable<long>();
    public long RowsRead => WorkerApi.NotExecutable<long>();
    public long RowsWritten => WorkerApi.NotExecutable<long>();
    public long LastRowId => WorkerApi.NotExecutable<long>();
    public bool ChangedDb => WorkerApi.NotExecutable<bool>();
    public int Changes => WorkerApi.NotExecutable<int>();
    public string? ServedByRegion => WorkerApi.NotExecutable<string?>();
    public string? ServedByColo => WorkerApi.NotExecutable<string?>();
    public bool? ServedByPrimary => WorkerApi.NotExecutable<bool?>();
    public int? TotalAttempts => WorkerApi.NotExecutable<int?>();
    public D1Timings? Timings => WorkerApi.NotExecutable<D1Timings?>();
}

public sealed class D1Timings
{
    private D1Timings() { }

    public double SqlDurationMs => WorkerApi.NotExecutable<double>();
}
