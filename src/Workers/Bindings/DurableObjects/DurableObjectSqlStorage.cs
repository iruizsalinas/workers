using System.Text.Json;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Workers;

/// <summary>SQLite storage attached to a SQLite-backed Durable Object instance.</summary>
public sealed class DurableObjectSqlStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DurableObjectStorageJsonContext JsonContext = new(new JsonSerializerOptions(JsonOptions));

    private readonly string _invocationId;
    private readonly IBindingDispatcher _dispatcher;
    private readonly JSObject? _nativeState;

    internal DurableObjectSqlStorage(string invocationId, IBindingDispatcher dispatcher, JSObject? nativeState = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        _invocationId = invocationId;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _nativeState = nativeState;
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
            return new DurableStorageSqlStatementPayload
            {
                Query = statement.Query,
                Values = statement.Values
            };
        }).ToArray();
        if (payload.Length == 0)
            throw new ArgumentException("At least one SQL statement is required.", nameof(statements));

        var payloadJson = JsonSerializer.Serialize(
            new DurableStorageSqlStatementsPayload { Statements = payload },
            JsonContext.DurableStorageSqlStatementsPayload);
        var result = _nativeState is not null && OperatingSystem.IsBrowser()
            ? NativeDurableObjectSqlStorage.TransactionSyncRaw(_nativeState, payloadJson)
            : await DispatchAsync(
                "durable.storage.sql.transactionSync.raw",
                payloadJson,
                cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.DurableStorageSqlTransactionRawEnvelope)
            ?? throw new WorkersException("Durable Object SQL transactionSync returned an empty result.");

        return envelope.Results;
    }

    /// <summary>Reads the current SQLite database size in bytes.</summary>
    public async Task<long> GetDatabaseSizeAsync(CancellationToken cancellationToken = default)
    {
        var result = _nativeState is not null && OperatingSystem.IsBrowser()
            ? NativeDurableObjectSqlStorage.GetDatabaseSize(_nativeState)
            : await DispatchAsync(
                "durable.storage.sql.databaseSize",
                EmptyPayload(),
                cancellationToken);
        return JsonSerializer.Deserialize(result, JsonContext.DurableStorageSqlDatabaseSizeEnvelope)?.DatabaseSize
            ?? throw new WorkersException("Durable Object SQL storage returned an empty database size result.");
    }

    internal async Task<DurableObjectSqlResult<T>> AllAsync<T>(
        DurableObjectSqlStatement statement,
        CancellationToken cancellationToken)
    {
        var result = _nativeState is not null && OperatingSystem.IsBrowser()
            ? NativeDurableObjectSqlStorage.All(_nativeState, StatementJson(statement))
            : await DispatchStatementAsync(
                "durable.storage.sql.all",
                statement,
                cancellationToken);

        var envelope = JsonSerializer.Deserialize(result, JsonContext.DurableStorageSqlRowsEnvelope)
            ?? throw new WorkersException("Durable Object SQL all returned an empty result.");

        return ToSqlResult<T>(envelope);
    }

    internal async Task<T> OneAsync<T>(
        DurableObjectSqlStatement statement,
        CancellationToken cancellationToken)
    {
        var result = _nativeState is not null && OperatingSystem.IsBrowser()
            ? NativeDurableObjectSqlStorage.One(_nativeState, StatementJson(statement))
            : await DispatchStatementAsync(
                "durable.storage.sql.one",
                statement,
                cancellationToken);

        var envelope = JsonSerializer.Deserialize(result, JsonContext.DurableStorageSqlOneEnvelope)
            ?? throw new WorkersException("Durable Object SQL one returned an empty result.");

        return envelope.Value.Deserialize<T>(JsonOptions)
            ?? throw new WorkersException("Durable Object SQL row could not be deserialized.");
    }

    internal async Task<DurableObjectSqlRawResult> RawAsync(
        DurableObjectSqlStatement statement,
        CancellationToken cancellationToken)
    {
        var result = _nativeState is not null && OperatingSystem.IsBrowser()
            ? NativeDurableObjectSqlStorage.Raw(_nativeState, StatementJson(statement))
            : await DispatchStatementAsync(
                "durable.storage.sql.raw",
                statement,
                cancellationToken);

        return JsonSerializer.Deserialize(result, JsonContext.DurableObjectSqlRawResult)
            ?? throw new WorkersException("Durable Object SQL raw returned an empty result.");
    }

    internal async Task<DurableObjectSqlCursor<T>> OpenCursorAsync<T>(
        DurableObjectSqlStatement statement,
        JsonSerializerOptions? jsonOptions,
        CancellationToken cancellationToken)
    {
        string result;
        var nativeCursor = false;
        if (_nativeState is not null && OperatingSystem.IsBrowser())
        {
            result = NativeDurableObjectSqlStorage.OpenCursor(_nativeState, StatementJson(statement));
            nativeCursor = true;
        }
        else
        {
            result = await DispatchStatementAsync(
                "durable.storage.sql.cursor.open",
                statement,
                cancellationToken);
        }
        var envelope = JsonSerializer.Deserialize(result, JsonContext.DurableStorageSqlCursorOpenEnvelope)
            ?? throw new WorkersException("Durable Object SQL cursor open returned an empty result.");

        ArgumentException.ThrowIfNullOrWhiteSpace(envelope.Handle);
        return new DurableObjectSqlCursor<T>(
            _invocationId,
            envelope.Handle,
            envelope.ColumnNames,
            envelope.RowsRead,
            envelope.RowsWritten,
            _dispatcher,
            jsonOptions,
            nativeCursor);
    }

    private Task<string> DispatchStatementAsync(
        string operation,
        DurableObjectSqlStatement statement,
        CancellationToken cancellationToken)
    {
        return DispatchAsync(
            operation,
            StatementJson(statement),
            cancellationToken);
    }

    private Task<string> DispatchAsync(string operation, string payloadJson, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            DurableObjectStorage.BindingName,
            operation,
            payloadJson);

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private static string EmptyPayload() =>
        JsonSerializer.Serialize(new DurableStorageEmptyPayload(), JsonContext.DurableStorageEmptyPayload);

    private static string StatementJson(DurableObjectSqlStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);
        return JsonSerializer.Serialize(
            new DurableStorageSqlStatementPayload
            {
                Query = statement.Query,
                Values = statement.Values
            },
            JsonContext.DurableStorageSqlStatementPayload);
    }

    private static DurableObjectSqlResult<T> ToSqlResult<T>(DurableStorageSqlRowsEnvelope envelope)
    {
        var rows = new List<T>(envelope.Rows.Count);
        foreach (var row in envelope.Rows)
        {
            rows.Add(row.Deserialize<T>(JsonOptions)
                ?? throw new WorkersException("Durable Object SQL row could not be deserialized."));
        }

        return new DurableObjectSqlResult<T>
        {
            Rows = rows,
            ColumnNames = envelope.ColumnNames,
            RowsRead = envelope.RowsRead,
            RowsWritten = envelope.RowsWritten
        };
    }
}

[SupportedOSPlatform("browser")]
internal static partial class NativeDurableObjectSqlStorage
{
    [JSImport("cloudflareWorkers.durableStorage.sqlAll", "dotnet.js")]
    internal static partial string All(JSObject state, string statementJson);

    [JSImport("cloudflareWorkers.durableStorage.sqlOne", "dotnet.js")]
    internal static partial string One(JSObject state, string statementJson);

    [JSImport("cloudflareWorkers.durableStorage.sqlRaw", "dotnet.js")]
    internal static partial string Raw(JSObject state, string statementJson);

    [JSImport("cloudflareWorkers.durableStorage.sqlTransactionSyncRaw", "dotnet.js")]
    internal static partial string TransactionSyncRaw(JSObject state, string statementsJson);

    [JSImport("cloudflareWorkers.durableStorage.sqlCursorOpen", "dotnet.js")]
    internal static partial string OpenCursor(JSObject state, string statementJson);

    [JSImport("cloudflareWorkers.durableStorage.sqlDatabaseSize", "dotnet.js")]
    internal static partial string GetDatabaseSize(JSObject state);
}

internal sealed class DurableStorageSqlStatementPayload
{
    public string Query { get; set; } = "";

    public IReadOnlyList<D1Value> Values { get; set; } = [];
}

internal sealed class DurableStorageSqlStatementsPayload
{
    public IReadOnlyList<DurableStorageSqlStatementPayload> Statements { get; set; } = [];
}

internal sealed class DurableStorageSqlDatabaseSizeEnvelope
{
    public long DatabaseSize { get; set; }
}

internal sealed class DurableStorageSqlOneEnvelope
{
    public JsonElement Value { get; set; }
}

internal sealed class DurableStorageSqlRowsEnvelope
{
    public IReadOnlyList<JsonElement> Rows { get; set; } = [];

    public IReadOnlyList<string> ColumnNames { get; set; } = [];

    public long RowsRead { get; set; }

    public long RowsWritten { get; set; }
}

internal sealed class DurableStorageSqlTransactionRawEnvelope
{
    public IReadOnlyList<DurableObjectSqlRawResult> Results { get; set; } = [];
}

internal sealed class DurableStorageSqlCursorOpenEnvelope
{
    public string Handle { get; set; } = "";

    public IReadOnlyList<string> ColumnNames { get; set; } = [];

    public long RowsRead { get; set; }

    public long RowsWritten { get; set; }
}
