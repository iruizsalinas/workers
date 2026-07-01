using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;

namespace Workers;

/// <summary>An incremental Durable Object SQL cursor.</summary>
public sealed class DurableObjectSqlCursor<T> : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DurableObjectStorageJsonContext JsonContext = new(new JsonSerializerOptions(JsonOptions));

    private readonly string _invocationId;
    private readonly string _handle;
    private readonly IBindingDispatcher _dispatcher;
    private readonly JsonSerializerOptions? _rowJsonOptions;
    private readonly bool _nativeCursor;

    private bool _disposed;

    internal DurableObjectSqlCursor(
        string invocationId,
        string handle,
        IReadOnlyList<string> columnNames,
        long rowsRead,
        long rowsWritten,
        IBindingDispatcher dispatcher,
        JsonSerializerOptions? rowJsonOptions,
        bool nativeCursor = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        ArgumentNullException.ThrowIfNull(columnNames);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _invocationId = invocationId;
        _handle = handle;
        _dispatcher = dispatcher;
        _rowJsonOptions = rowJsonOptions;
        _nativeCursor = nativeCursor;
        ColumnNames = columnNames;
        RowsRead = rowsRead;
        RowsWritten = rowsWritten;
    }

    /// <summary>Column names in the order returned by raw row arrays.</summary>
    public IReadOnlyList<string> ColumnNames { get; private set; }

    /// <summary>The number of rows read so far by the query cursor.</summary>
    public long RowsRead { get; private set; }

    /// <summary>The number of rows written so far by the query cursor.</summary>
    public long RowsWritten { get; private set; }

    /// <summary>Returns the next row object, or <see langword="null"/> after the cursor is consumed.</summary>
    public async Task<T?> NextAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return default;

        var envelope = await NextCoreAsync(cancellationToken);
        if (envelope.Done)
            return default;

        return envelope.Value.Deserialize<T>(_rowJsonOptions ?? JsonOptions);
    }

    /// <summary>Returns the next row as an array in column order, or <see langword="null"/> after the cursor is consumed.</summary>
    public async Task<IReadOnlyList<JsonElement>?> NextRawAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return null;

        var result = _nativeCursor && OperatingSystem.IsBrowser()
            ? NativeDurableObjectSqlCursor.RawNext(_handle)
            : await DispatchAsync("durable.storage.sql.cursor.rawNext", cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.DurableStorageSqlCursorRawNextEnvelope)
            ?? throw new WorkersException("Durable Object SQL raw cursor next returned an empty result.");

        ApplyMetadata(envelope.ColumnNames, envelope.RowsRead, envelope.RowsWritten);
        if (envelope.Done)
            await DisposeAsync();

        return envelope.Done ? null : envelope.Value;
    }

    /// <summary>
    /// Reads typed rows until the cursor is consumed.
    /// Cloudflare recommends consuming SQL cursors before the next unrelated await for predictable snapshots.
    /// </summary>
    public async IAsyncEnumerable<T> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            while (true)
            {
                if (_disposed)
                    yield break;

                var envelope = await NextCoreAsync(cancellationToken);
                if (envelope.Done)
                    yield break;

                yield return envelope.Value.Deserialize<T>(_rowJsonOptions ?? JsonOptions)
                    ?? throw new WorkersException("Durable Object SQL cursor row could not be deserialized.");
            }
        }
        finally
        {
            await DisposeAsync();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_nativeCursor && OperatingSystem.IsBrowser())
            NativeDurableObjectSqlCursor.Dispose(_handle);
        else
            await DispatchAsync("durable.storage.sql.cursor.dispose", CancellationToken.None);
    }

    private void ApplyMetadata(IReadOnlyList<string> columnNames, long rowsRead, long rowsWritten)
    {
        ColumnNames = columnNames;
        RowsRead = rowsRead;
        RowsWritten = rowsWritten;
    }

    private async Task<DurableStorageSqlCursorNextEnvelope> NextCoreAsync(CancellationToken cancellationToken)
    {
        var result = _nativeCursor && OperatingSystem.IsBrowser()
            ? NativeDurableObjectSqlCursor.Next(_handle)
            : await DispatchAsync("durable.storage.sql.cursor.next", cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.DurableStorageSqlCursorNextEnvelope)
            ?? throw new WorkersException("Durable Object SQL cursor next returned an empty result.");

        ApplyMetadata(envelope.ColumnNames, envelope.RowsRead, envelope.RowsWritten);
        if (envelope.Done)
            await DisposeAsync();

        return envelope;
    }

    private Task<string> DispatchAsync(string operation, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            DurableObjectStorage.BindingName,
            operation,
            JsonSerializer.Serialize(new DurableStorageSqlCursorHandlePayload { Handle = _handle }, JsonContext.DurableStorageSqlCursorHandlePayload));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }
}

[SupportedOSPlatform("browser")]
internal static partial class NativeDurableObjectSqlCursor
{
    [JSImport("cloudflareWorkers.durableStorage.sqlCursorNext", "dotnet.js")]
    internal static partial string Next(string handle);

    [JSImport("cloudflareWorkers.durableStorage.sqlCursorRawNext", "dotnet.js")]
    internal static partial string RawNext(string handle);

    [JSImport("cloudflareWorkers.durableStorage.sqlCursorDispose", "dotnet.js")]
    internal static partial void Dispose(string handle);
}

internal sealed class DurableStorageSqlCursorHandlePayload
{
    public string Handle { get; set; } = "";
}

internal sealed class DurableStorageSqlCursorNextEnvelope
{
    public bool Done { get; set; }

    public JsonElement Value { get; set; }

    public IReadOnlyList<string> ColumnNames { get; set; } = [];

    public long RowsRead { get; set; }

    public long RowsWritten { get; set; }
}

internal sealed class DurableStorageSqlCursorRawNextEnvelope
{
    public bool Done { get; set; }

    public IReadOnlyList<JsonElement>? Value { get; set; }

    public IReadOnlyList<string> ColumnNames { get; set; } = [];

    public long RowsRead { get; set; }

    public long RowsWritten { get; set; }
}
