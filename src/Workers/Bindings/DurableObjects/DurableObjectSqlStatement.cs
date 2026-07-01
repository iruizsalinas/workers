using System.Text.Json;

namespace Workers;

/// <summary>A prepared SQLite statement for Durable Object SQL storage.</summary>
public sealed class DurableObjectSqlStatement
{
    private readonly DurableObjectSqlStorage _storage;
    private readonly IReadOnlyList<D1Value> _values;

    internal DurableObjectSqlStatement(
        DurableObjectSqlStorage storage,
        string query,
        IReadOnlyList<D1Value>? values = null)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        Query = query;
        _values = values ?? [];
    }

    /// <summary>The SQL query text.</summary>
    public string Query { get; }

    /// <summary>Returns a new statement with the provided SQL values bound to positional placeholders.</summary>
    public DurableObjectSqlStatement Bind(params D1Value[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new DurableObjectSqlStatement(_storage, Query, values.ToArray());
    }

    /// <summary>Returns a new statement with supported CLR values converted and bound to positional placeholders.</summary>
    public DurableObjectSqlStatement Bind(params object?[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Bind(values.Select(D1Value.From).ToArray());
    }

    /// <summary>Executes the query and returns all rows as typed objects.</summary>
    public Task<DurableObjectSqlResult<T>> AllAsync<T>(CancellationToken cancellationToken = default) =>
        _storage.AllAsync<T>(this, cancellationToken);

    /// <summary>Executes the query and returns the single row. The runtime throws if zero or multiple rows are returned.</summary>
    public Task<T> OneAsync<T>(CancellationToken cancellationToken = default) =>
        _storage.OneAsync<T>(this, cancellationToken);

    /// <summary>Executes the query and returns rows as arrays in column order.</summary>
    public Task<DurableObjectSqlRawResult> RawAsync(CancellationToken cancellationToken = default) =>
        _storage.RawAsync(this, cancellationToken);

    /// <summary>
    /// Executes the query and opens a cursor for incremental iteration.
    /// Cloudflare cursors do not provide snapshot isolation when held across awaits; prefer <see cref="AllAsync{T}"/>
    /// or fully consume the cursor promptly when predictable snapshots matter.
    /// </summary>
    public Task<DurableObjectSqlCursor<T>> OpenCursorAsync<T>(
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default) =>
        _storage.OpenCursorAsync<T>(this, jsonOptions, cancellationToken);

    internal IReadOnlyList<D1Value> Values => _values;
}
