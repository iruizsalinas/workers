using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Workers;

/// <summary>A value that can be bound to a D1 prepared statement.</summary>
public readonly record struct D1Value
{
    private const long MinSafeInteger = -9_007_199_254_740_991;
    private const long MaxSafeInteger = 9_007_199_254_740_991;

    private D1Value(string type, object? value, string? bodyBase64)
    {
        Type = type;
        Value = value;
        BodyBase64 = bodyBase64;
    }

    /// <summary>The D1 value type.</summary>
    [JsonPropertyName("type")]
    public string Type { get; }

    /// <summary>The scalar JSON value, when this is not a blob.</summary>
    [JsonPropertyName("value")]
    public object? Value { get; }

    /// <summary>The base64-encoded blob value.</summary>
    [JsonPropertyName("bodyBase64")]
    public string? BodyBase64 { get; }

    /// <summary>A SQL NULL value.</summary>
    public static D1Value Null => new("null", null, null);

    /// <summary>Creates a floating-point value.</summary>
    public static D1Value Real(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "D1 real values must be finite.");

        return new D1Value("real", value, null);
    }

    /// <summary>Creates an integer value within JavaScript's safe integer range.</summary>
    public static D1Value Integer(long value)
    {
        if (value is < MinSafeInteger or > MaxSafeInteger)
            throw new ArgumentOutOfRangeException(nameof(value), value, "D1 integers must fit JavaScript's safe integer range.");

        return new D1Value("integer", value, null);
    }

    /// <summary>Creates a text value.</summary>
    public static D1Value Text(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new D1Value("text", value, null);
    }

    /// <summary>Creates a boolean value.</summary>
    public static D1Value Boolean(bool value) => new("boolean", value, null);

    /// <summary>Creates a blob value.</summary>
    public static D1Value Blob(ReadOnlySpan<byte> value) => new("blob", null, Convert.ToBase64String(value));

    /// <summary>Converts a supported CLR value to a D1 value.</summary>
    public static D1Value From(object? value) =>
        value switch
        {
            null => Null,
            D1Value typed => typed,
            string text => Text(text),
            bool boolean => Boolean(boolean),
            byte[] bytes => Blob(bytes),
            ReadOnlyMemory<byte> bytes => Blob(bytes.Span),
            Memory<byte> bytes => Blob(bytes.Span),
            int integer => Integer(integer),
            long integer => Integer(integer),
            short integer => Integer(integer),
            byte integer => Integer(integer),
            float real => Real(real),
            double real => Real(real),
            decimal real => Real((double)real),
            _ => throw new ArgumentException($"Values of type '{value.GetType().FullName}' cannot be bound to a D1 statement.", nameof(value))
        };

    internal JsonObject ToJsonObject() =>
        new()
        {
            ["type"] = Type,
            ["value"] = Value is null ? null : JsonSerializer.SerializeToNode(Value),
            ["bodyBase64"] = BodyBase64
        };
}

/// <summary>A prepared D1 statement.</summary>
public sealed class D1PreparedStatement
{
    private readonly ID1StatementExecutor _executor;
    private readonly IReadOnlyList<D1Value> _values;

    internal D1PreparedStatement(ID1StatementExecutor executor, string query, IReadOnlyList<D1Value>? values = null)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        Query = query;
        _values = values ?? [];
    }

    /// <summary>The SQL query text.</summary>
    public string Query { get; }

    /// <summary>Returns a new statement with the provided D1 values bound.</summary>
    public D1PreparedStatement Bind(params D1Value[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new D1PreparedStatement(_executor, Query, values.ToArray());
    }

    /// <summary>Returns a new statement with supported CLR values converted and bound.</summary>
    public D1PreparedStatement Bind(params object?[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Bind(values.Select(D1Value.From).ToArray());
    }

    /// <summary>Executes the query and returns metadata.</summary>
    public Task<D1Result> RunAsync(CancellationToken cancellationToken = default) =>
        _executor.RunAsync(this, cancellationToken);

    /// <summary>Executes the query and returns all result rows.</summary>
    public Task<D1Result<T>> AllAsync<T>(CancellationToken cancellationToken = default) =>
        _executor.AllAsync<T>(this, cancellationToken);

    /// <summary>Executes the query and returns rows as arrays in column order.</summary>
    public Task<IReadOnlyList<IReadOnlyList<JsonElement>>> RawAsync(
        D1RawOptions? options = null,
        CancellationToken cancellationToken = default) =>
        _executor.RawAsync(this, options, cancellationToken);

    /// <summary>Executes the query and returns the first result row or value.</summary>
    public async Task<T?> FirstAsync<T>(string? columnName = null, CancellationToken cancellationToken = default) =>
        await _executor.FirstAsync<T>(this, columnName, cancellationToken);

    internal ID1StatementExecutor Executor => _executor;

    internal IReadOnlyList<D1Value> Values => _values;
}

/// <summary>Options used when reading raw D1 rows.</summary>
public sealed record D1RawOptions
{
    /// <summary>When true, includes column names as the first row.</summary>
    public bool? ColumnNames { get; init; }
}

/// <summary>Metadata returned by D1 query execution.</summary>
public sealed class D1ResultMetadata
{
    /// <summary>Whether the query changed the database.</summary>
    [JsonPropertyName("changed_db")]
    public bool? ChangedDatabase { get; init; }

    /// <summary>The number of changed rows.</summary>
    public int? Changes { get; init; }

    /// <summary>The execution duration in milliseconds.</summary>
    public double? Duration { get; init; }

    /// <summary>The last inserted row id.</summary>
    [JsonPropertyName("last_row_id")]
    public long? LastRowId { get; init; }

    /// <summary>The number of rows read.</summary>
    [JsonPropertyName("rows_read")]
    public int? RowsRead { get; init; }

    /// <summary>The number of rows written.</summary>
    [JsonPropertyName("rows_written")]
    public int? RowsWritten { get; init; }

    /// <summary>The database size after the query.</summary>
    [JsonPropertyName("size_after")]
    public int? SizeAfter { get; init; }
}

/// <summary>A D1 query result without typed rows.</summary>
public sealed class D1Result
{
    /// <summary>Whether the query succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>The error message, when execution failed.</summary>
    public string? Error { get; init; }

    /// <summary>Execution metadata.</summary>
    public D1ResultMetadata? Meta { get; init; }
}

/// <summary>A D1 query result with typed rows.</summary>
public sealed class D1Result<T>
{
    /// <summary>The result rows.</summary>
    public IReadOnlyList<T> Results { get; init; } = [];

    /// <summary>Whether the query succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>The error message, when execution failed.</summary>
    public string? Error { get; init; }

    /// <summary>Execution metadata.</summary>
    public D1ResultMetadata? Meta { get; init; }
}

/// <summary>The result of executing raw SQL through D1.</summary>
public sealed class D1ExecResult
{
    /// <summary>The number of statements executed.</summary>
    public int Count { get; init; }

    /// <summary>The total execution duration in milliseconds.</summary>
    public double Duration { get; init; }
}

/// <summary>The first-query policy for a D1 session.</summary>
public enum D1SessionMode
{
    /// <summary>Starts with any database instance and prioritizes lower latency.</summary>
    FirstUnconstrained,

    /// <summary>Starts with the primary database instance for the freshest data.</summary>
    FirstPrimary
}

/// <summary>Options used when starting a D1 session.</summary>
public sealed record D1SessionOptions
{
    /// <summary>The first-query routing mode.</summary>
    public D1SessionMode? Mode { get; init; }

    /// <summary>A bookmark from a previous D1 session.</summary>
    public string? Bookmark { get; init; }

    /// <summary>Starts a session from a previous bookmark.</summary>
    public static D1SessionOptions FromBookmark(string bookmark)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookmark);
        return new D1SessionOptions { Bookmark = bookmark };
    }

    internal string? ToParameter()
    {
        if (!string.IsNullOrWhiteSpace(Bookmark))
        {
            if (Mode is not null)
                throw new ArgumentException("D1 session options cannot specify both a mode and a bookmark.", nameof(Bookmark));

            return Bookmark;
        }

        return Mode switch
        {
            null => null,
            D1SessionMode.FirstUnconstrained => "first-unconstrained",
            D1SessionMode.FirstPrimary => "first-primary",
            _ => throw new ArgumentOutOfRangeException(nameof(Mode), Mode, "Unsupported D1 session mode.")
        };
    }
}

/// <summary>A D1 session that maintains sequential consistency across queries.</summary>
public sealed class D1DatabaseSession : ID1StatementExecutor
{
    private readonly D1DatabaseBinding _database;
    private readonly string _handle;
    private readonly string? _parameter;

    internal D1DatabaseSession(D1DatabaseBinding database, string handle, string? parameter)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        _handle = handle;
        _parameter = parameter;
    }

    /// <summary>Prepares a SQL statement within this session.</summary>
    public D1PreparedStatement Prepare(string query) => new(this, query);

    /// <summary>Executes prepared statements sequentially inside this session.</summary>
    public Task<IReadOnlyList<D1Result<JsonElement>>> BatchAsync(
        IEnumerable<D1PreparedStatement> statements,
        CancellationToken cancellationToken = default) =>
        BatchAsync<JsonElement>(statements, cancellationToken);

    /// <summary>Executes prepared statements sequentially inside this session.</summary>
    public async Task<IReadOnlyList<D1Result<T>>> BatchAsync<T>(
        IEnumerable<D1PreparedStatement> statements,
        CancellationToken cancellationToken = default)
    {
        var result = await _database.DispatchSessionAsync(
            "d1.session.batch",
            _handle,
            _parameter,
            new JsonObject { ["statements"] = StatementPayloads(statements) },
            cancellationToken);

        return D1DatabaseBinding.ReadResults<T>(result);
    }

    /// <summary>Gets the latest bookmark seen by this session, or null before the first query.</summary>
    public async Task<string?> GetBookmarkAsync(CancellationToken cancellationToken = default)
    {
        var result = await _database.DispatchSessionAsync(
            "d1.session.getBookmark",
            _handle,
            _parameter,
            new JsonObject(),
            cancellationToken);

        var payload = JsonSerializer.Deserialize<D1BookmarkResult>(result, D1DatabaseBinding.JsonOptions)
            ?? throw new WorkersException("D1 session bookmark returned an empty result.");
        return payload.Bookmark;
    }

    Task<D1Result> ID1StatementExecutor.RunAsync(D1PreparedStatement statement, CancellationToken cancellationToken) =>
        _database.SessionRunAsync(this, statement, cancellationToken);

    Task<D1Result<T>> ID1StatementExecutor.AllAsync<T>(D1PreparedStatement statement, CancellationToken cancellationToken) =>
        _database.SessionAllAsync<T>(this, statement, cancellationToken);

    Task<IReadOnlyList<IReadOnlyList<JsonElement>>> ID1StatementExecutor.RawAsync(
        D1PreparedStatement statement,
        D1RawOptions? options,
        CancellationToken cancellationToken) =>
        _database.SessionRawAsync(this, statement, options, cancellationToken);

    async Task<T> ID1StatementExecutor.FirstAsync<T>(
        D1PreparedStatement statement,
        string? columnName,
        CancellationToken cancellationToken) =>
        (await _database.SessionFirstAsync<T>(this, statement, columnName, cancellationToken))!;

    internal string Handle => _handle;

    internal string? Parameter => _parameter;

    internal JsonArray StatementPayloads(IEnumerable<D1PreparedStatement> statements)
    {
        ArgumentNullException.ThrowIfNull(statements);

        var payload = statements.Select(statement =>
        {
            ArgumentNullException.ThrowIfNull(statement);
            if (!ReferenceEquals(statement.Executor, this))
                throw new ArgumentException("All D1 session batch statements must be prepared by this session.", nameof(statements));

            return D1StatementPayload.From(statement);
        }).ToArray();

        if (payload.Length == 0)
            throw new ArgumentException("At least one D1 statement is required.", nameof(statements));

        var array = new JsonArray();
        foreach (var statement in payload)
            array.Add(statement);

        return array;
    }

    private sealed record D1BookmarkResult(string? Bookmark);
}

internal interface ID1StatementExecutor
{
    Task<D1Result> RunAsync(D1PreparedStatement statement, CancellationToken cancellationToken);

    Task<D1Result<T>> AllAsync<T>(D1PreparedStatement statement, CancellationToken cancellationToken);

    Task<IReadOnlyList<IReadOnlyList<JsonElement>>> RawAsync(
        D1PreparedStatement statement,
        D1RawOptions? options,
        CancellationToken cancellationToken);

    Task<T> FirstAsync<T>(
        D1PreparedStatement statement,
        string? columnName,
        CancellationToken cancellationToken);
}

internal sealed class D1DatabaseBinding : ID1Database, ID1StatementExecutor
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static long nextSessionId;

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public D1DatabaseBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public D1PreparedStatement Prepare(string query) => new(this, query);

    public D1DatabaseSession WithSession(D1SessionOptions? options = null) =>
        new(this, "d1-session:" + Interlocked.Increment(ref nextSessionId).ToString(System.Globalization.CultureInfo.InvariantCulture), options?.ToParameter());

    public Task<IReadOnlyList<D1Result<JsonElement>>> BatchAsync(
        IEnumerable<D1PreparedStatement> statements,
        CancellationToken cancellationToken = default) =>
        BatchAsync<JsonElement>(statements, cancellationToken);

    public async Task<IReadOnlyList<D1Result<T>>> BatchAsync<T>(
        IEnumerable<D1PreparedStatement> statements,
        CancellationToken cancellationToken = default)
    {
        var payload = StatementPayloads(statements);

        var result = await DispatchAsync(
            "d1.batch",
            new JsonObject { ["statements"] = payload },
            cancellationToken);

        return ReadResults<T>(result);
    }

    public async Task<D1ExecResult> ExecAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var result = await DispatchAsync("d1.exec", new JsonObject { ["query"] = query }, cancellationToken);
        using var document = JsonDocument.Parse(result);
        return new D1ExecResult
        {
            Count = document.RootElement.TryGetProperty("count", out var count) ? count.GetInt32() : 0,
            Duration = document.RootElement.TryGetProperty("duration", out var duration) ? duration.GetDouble() : 0
        };
    }

    public async Task<Body> DumpAsync(CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("d1.dump", new JsonObject(), cancellationToken);
        var payload = JsonSerializer.Deserialize<D1DumpResult>(result, JsonOptions)
            ?? throw new WorkersException("D1 dump returned an empty result.");

        return Body.FromBytes(Convert.FromBase64String(payload.BodyBase64), "application/octet-stream");
    }

    public async Task<D1Result> RunAsync(D1PreparedStatement statement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var result = await DispatchStatementAsync("d1.run", statement, columnName: null, options: null, cancellationToken);
        return ReadResult(result);
    }

    public async Task<D1Result<T>> AllAsync<T>(D1PreparedStatement statement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var result = await DispatchStatementAsync("d1.all", statement, columnName: null, options: null, cancellationToken);
        return ReadResult<T>(result);
    }

    public async Task<IReadOnlyList<IReadOnlyList<JsonElement>>> RawAsync(
        D1PreparedStatement statement,
        D1RawOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var result = await DispatchStatementAsync("d1.raw", statement, columnName: null, options, cancellationToken)
            ;
        return JsonSerializer.Deserialize<IReadOnlyList<IReadOnlyList<JsonElement>>>(result, JsonOptions)
            ?? throw new WorkersException("D1 raw returned an empty result.");
    }

    public async Task<T?> FirstAsync<T>(
        D1PreparedStatement statement,
        string? columnName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var result = await DispatchStatementAsync("d1.first", statement, columnName, options: null, cancellationToken);
        return ReadFirst<T>(result);
    }

    async Task<T> ID1StatementExecutor.FirstAsync<T>(
        D1PreparedStatement statement,
        string? columnName,
        CancellationToken cancellationToken) =>
        (await FirstAsync<T>(statement, columnName, cancellationToken))!;

    private Task<string> DispatchStatementAsync(
        string operation,
        D1PreparedStatement statement,
        string? columnName,
        D1RawOptions? options,
        CancellationToken cancellationToken)
    {
        return DispatchAsync(
            operation,
            D1StatementPayload.From(statement, columnName, options),
            cancellationToken);
    }

    internal async Task<D1Result> SessionRunAsync(
        D1DatabaseSession session,
        D1PreparedStatement statement,
        CancellationToken cancellationToken)
    {
        var result = await DispatchSessionStatementAsync("d1.session.run", session, statement, columnName: null, options: null, cancellationToken);
        return ReadResult(result);
    }

    internal async Task<D1Result<T>> SessionAllAsync<T>(
        D1DatabaseSession session,
        D1PreparedStatement statement,
        CancellationToken cancellationToken)
    {
        var result = await DispatchSessionStatementAsync("d1.session.all", session, statement, columnName: null, options: null, cancellationToken);
        return ReadResult<T>(result);
    }

    internal async Task<IReadOnlyList<IReadOnlyList<JsonElement>>> SessionRawAsync(
        D1DatabaseSession session,
        D1PreparedStatement statement,
        D1RawOptions? options,
        CancellationToken cancellationToken)
    {
        var result = await DispatchSessionStatementAsync("d1.session.raw", session, statement, columnName: null, options, cancellationToken);
        return JsonSerializer.Deserialize<IReadOnlyList<IReadOnlyList<JsonElement>>>(result, JsonOptions)
            ?? throw new WorkersException("D1 session raw returned an empty result.");
    }

    internal async Task<T?> SessionFirstAsync<T>(
        D1DatabaseSession session,
        D1PreparedStatement statement,
        string? columnName,
        CancellationToken cancellationToken)
    {
        var result = await DispatchSessionStatementAsync("d1.session.first", session, statement, columnName, options: null, cancellationToken);
        return ReadFirst<T>(result);
    }

    private Task<string> DispatchSessionStatementAsync(
        string operation,
        D1DatabaseSession session,
        D1PreparedStatement statement,
        string? columnName,
        D1RawOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(statement);
        if (!ReferenceEquals(statement.Executor, session))
            throw new ArgumentException("D1 session statements must be prepared by the session that executes them.", nameof(statement));

        return DispatchSessionAsync(
            operation,
            session.Handle,
            session.Parameter,
            D1StatementPayload.From(statement, columnName, options),
            cancellationToken);
    }

    private JsonArray StatementPayloads(IEnumerable<D1PreparedStatement> statements)
    {
        ArgumentNullException.ThrowIfNull(statements);

        var payload = statements.Select(statement =>
        {
            ArgumentNullException.ThrowIfNull(statement);
            if (!ReferenceEquals(statement.Executor, this))
                throw new ArgumentException("All D1 batch statements must be prepared by this database binding.", nameof(statements));

            return D1StatementPayload.From(statement);
        }).ToArray();

        if (payload.Length == 0)
            throw new ArgumentException("At least one D1 statement is required.", nameof(statements));

        var array = new JsonArray();
        foreach (var statement in payload)
            array.Add(statement);

        return array;
    }

    internal Task<string> DispatchSessionAsync(
        string operation,
        string handle,
        string? parameter,
        object payload,
        CancellationToken cancellationToken)
    {
        var envelope = new JsonObject
        {
            ["handle"] = handle,
            ["parameter"] = parameter,
            ["payload"] = JsonSerializer.SerializeToNode(payload, JsonOptions)
        };
        return DispatchAsync(operation, envelope, cancellationToken);
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

    internal static IReadOnlyList<D1Result<T>> ReadResults<T>(string json)
    {
        using var document = JsonDocument.Parse(json);
        var results = new List<D1Result<T>>();
        foreach (var element in document.RootElement.EnumerateArray())
            results.Add(ReadResult<T>(element));

        return results;
    }

    private static D1Result ReadResult(string json)
    {
        using var document = JsonDocument.Parse(json);
        var element = document.RootElement;
        return new D1Result
        {
            Success = element.TryGetProperty("success", out var success) && success.GetBoolean(),
            Error = element.TryGetProperty("error", out var error) && error.ValueKind is not JsonValueKind.Null
                ? error.GetString()
                : null,
            Meta = ReadMetadata(element)
        };
    }

    private static D1Result<T> ReadResult<T>(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ReadResult<T>(document.RootElement);
    }

    private static D1Result<T> ReadResult<T>(JsonElement element)
    {
        var rows = element.TryGetProperty("results", out var results) && results.ValueKind is JsonValueKind.Array
            ? results.Deserialize<IReadOnlyList<T>>(JsonOptions) ?? []
            : [];

        return new D1Result<T>
        {
            Results = rows,
            Success = element.TryGetProperty("success", out var success) && success.GetBoolean(),
            Error = element.TryGetProperty("error", out var error) && error.ValueKind is not JsonValueKind.Null
                ? error.GetString()
                : null,
            Meta = ReadMetadata(element)
        };
    }

    private static T? ReadFirst<T>(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("value", out var value) &&
            value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
                ? value.Deserialize<T>(JsonOptions)
                : default;
    }

    private static D1ResultMetadata? ReadMetadata(JsonElement element)
    {
        if (!element.TryGetProperty("meta", out var meta) || meta.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return new D1ResultMetadata
        {
            ChangedDatabase = meta.TryGetProperty("changed_db", out var changedDatabase) && changedDatabase.ValueKind is not JsonValueKind.Null
                ? changedDatabase.GetBoolean()
                : null,
            Changes = meta.TryGetProperty("changes", out var changes) && changes.ValueKind is not JsonValueKind.Null
                ? changes.GetInt32()
                : null,
            Duration = meta.TryGetProperty("duration", out var duration) && duration.ValueKind is not JsonValueKind.Null
                ? duration.GetDouble()
                : null,
            LastRowId = meta.TryGetProperty("last_row_id", out var lastRowId) && lastRowId.ValueKind is not JsonValueKind.Null
                ? lastRowId.GetInt64()
                : null,
            RowsRead = meta.TryGetProperty("rows_read", out var rowsRead) && rowsRead.ValueKind is not JsonValueKind.Null
                ? rowsRead.GetInt32()
                : null,
            RowsWritten = meta.TryGetProperty("rows_written", out var rowsWritten) && rowsWritten.ValueKind is not JsonValueKind.Null
                ? rowsWritten.GetInt32()
                : null,
            SizeAfter = meta.TryGetProperty("size_after", out var sizeAfter) && sizeAfter.ValueKind is not JsonValueKind.Null
                ? sizeAfter.GetInt32()
                : null
        };
    }

    private sealed record D1DumpResult(string BodyBase64);

}

internal static class D1StatementPayload
{
    public static JsonObject From(
        D1PreparedStatement statement,
        string? columnName = null,
        D1RawOptions? options = null)
    {
        var values = new JsonArray();
        foreach (var value in statement.Values)
            values.Add(value.ToJsonObject());

        return new JsonObject
        {
            ["query"] = statement.Query,
            ["values"] = values,
            ["columnName"] = columnName,
            ["options"] = options is null
                ? null
                : new JsonObject { ["columnNames"] = options.ColumnNames }
        };
    }
}
