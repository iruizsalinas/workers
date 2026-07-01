using System.Text.Json;

namespace Workers;

/// <summary>Rows and cursor metadata returned by Durable Object SQL storage.</summary>
public sealed class DurableObjectSqlResult<T>
{
    /// <summary>Rows returned by the query.</summary>
    public IReadOnlyList<T> Rows { get; init; } = [];

    /// <summary>Column names in the order returned by raw row arrays.</summary>
    public IReadOnlyList<string> ColumnNames { get; init; } = [];

    /// <summary>The final number of rows read by the query cursor.</summary>
    public long RowsRead { get; init; }

    /// <summary>The final number of rows written by the query cursor.</summary>
    public long RowsWritten { get; init; }
}

/// <summary>Raw rows and cursor metadata returned by Durable Object SQL storage.</summary>
public sealed class DurableObjectSqlRawResult
{
    /// <summary>Rows returned by the query as arrays in column order.</summary>
    public IReadOnlyList<IReadOnlyList<JsonElement>> Rows { get; init; } = [];

    /// <summary>Column names in the order returned by raw row arrays.</summary>
    public IReadOnlyList<string> ColumnNames { get; init; } = [];

    /// <summary>The final number of rows read by the query cursor.</summary>
    public long RowsRead { get; init; }

    /// <summary>The final number of rows written by the query cursor.</summary>
    public long RowsWritten { get; init; }
}
