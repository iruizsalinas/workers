using System.Text.Json;

namespace Workers;

/// <summary>SQLite storage attached to a SQLite-backed Durable Object instance.</summary>
public sealed class DurableObjectSqlStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly IBindingDispatcher _dispatcher;

    internal DurableObjectSqlStorage(string invocationId, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        _invocationId = invocationId;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>Prepares a SQLite statement for execution against this Durable Object's private database.</summary>
    public DurableObjectSqlStatement Prepare(string query) => new(this, query);

    /// <summary>
    /// Executes SQL statements inside Cloudflare's synchronous <c>transactionSync</c> boundary
    /// and returns each result as raw rows in column order.
    /// </summary>
    public async Task<IReadOnlyList<DurableObjectSqlRawResult>> TransactionSyncRawAsync(
        IEnumerable<DurableObjectSqlStatement> statements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statements);

        var payload = statements.Select(statement =>
        {
            ArgumentNullException.ThrowIfNull(statement);
            return new SqlStatementPayload(statement.Query, statement.Values);
        }).ToArray();
        if (payload.Length == 0)
            throw new ArgumentException("At least one SQL statement is required.", nameof(statements));

        var result = await DispatchAsync(
            "durable.storage.sql.transactionSync.raw",
            new { statements = payload },
            cancellationToken);
        var envelope = JsonSerializer.Deserialize<SqlTransactionRawEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("Durable Object SQL transactionSync returned an empty result.");

        return envelope.Results;
    }

    /// <summary>Reads the current SQLite database size in bytes.</summary>
    public async Task<long> GetDatabaseSizeAsync(CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "durable.storage.sql.databaseSize",
            new { },
            cancellationToken);
        return JsonSerializer.Deserialize<DatabaseSizeEnvelope>(result, JsonOptions)?.DatabaseSize
            ?? throw new WorkersException("Durable Object SQL storage returned an empty database size result.");
    }

    internal async Task<DurableObjectSqlResult<T>> AllAsync<T>(
        DurableObjectSqlStatement statement,
        CancellationToken cancellationToken)
    {
        var result = await DispatchStatementAsync(
            "durable.storage.sql.all",
            statement,
            cancellationToken);

        return JsonSerializer.Deserialize<DurableObjectSqlResult<T>>(result, JsonOptions)
            ?? throw new WorkersException("Durable Object SQL all returned an empty result.");
    }

    internal async Task<T> OneAsync<T>(
        DurableObjectSqlStatement statement,
        CancellationToken cancellationToken)
    {
        var result = await DispatchStatementAsync(
            "durable.storage.sql.one",
            statement,
            cancellationToken);

        var envelope = JsonSerializer.Deserialize<DurableObjectSqlOneResult<T>>(result, JsonOptions)
            ?? throw new WorkersException("Durable Object SQL one returned an empty result.");

        return envelope.Value;
    }

    internal async Task<DurableObjectSqlRawResult> RawAsync(
        DurableObjectSqlStatement statement,
        CancellationToken cancellationToken)
    {
        var result = await DispatchStatementAsync(
            "durable.storage.sql.raw",
            statement,
            cancellationToken);

        return JsonSerializer.Deserialize<DurableObjectSqlRawResult>(result, JsonOptions)
            ?? throw new WorkersException("Durable Object SQL raw returned an empty result.");
    }

    internal async Task<DurableObjectSqlCursor<T>> OpenCursorAsync<T>(
        DurableObjectSqlStatement statement,
        JsonSerializerOptions? jsonOptions,
        CancellationToken cancellationToken)
    {
        var result = await DispatchStatementAsync(
            "durable.storage.sql.cursor.open",
            statement,
            cancellationToken);
        var envelope = JsonSerializer.Deserialize<SqlCursorOpenEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("Durable Object SQL cursor open returned an empty result.");

        ArgumentException.ThrowIfNullOrWhiteSpace(envelope.Handle);
        return new DurableObjectSqlCursor<T>(
            _invocationId,
            envelope.Handle,
            envelope.ColumnNames,
            envelope.RowsRead,
            envelope.RowsWritten,
            _dispatcher,
            jsonOptions);
    }

    private Task<string> DispatchStatementAsync(
        string operation,
        DurableObjectSqlStatement statement,
        CancellationToken cancellationToken)
    {
        return DispatchAsync(
            operation,
            new SqlStatementPayload(statement.Query, statement.Values),
            cancellationToken);
    }

    private Task<string> DispatchAsync(string operation, object payload, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            DurableObjectStorage.BindingName,
            operation,
            JsonSerializer.Serialize(payload, JsonOptions));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private sealed record SqlStatementPayload(string Query, IReadOnlyList<D1Value> Values);

    private sealed record DatabaseSizeEnvelope(long DatabaseSize);

    private sealed record DurableObjectSqlOneResult<T>(T Value);

    private sealed record SqlTransactionRawEnvelope(IReadOnlyList<DurableObjectSqlRawResult> Results);

    private sealed record SqlCursorOpenEnvelope(
        string Handle,
        IReadOnlyList<string> ColumnNames,
        long RowsRead,
        long RowsWritten);
}
