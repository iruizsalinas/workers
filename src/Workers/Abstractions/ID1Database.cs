using System.Text.Json;

namespace Workers;

/// <summary>Represents a D1 database binding.</summary>
public interface ID1Database : IBinding
{
    /// <summary>Prepares a SQL statement.</summary>
    D1PreparedStatement Prepare(string query);

    /// <summary>Starts a D1 session for sequentially consistent queries.</summary>
    D1DatabaseSession WithSession(D1SessionOptions? options = null);

    /// <summary>Executes one or more SQL statements directly.</summary>
    Task<D1ExecResult> ExecAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>Executes prepared statements sequentially as a single D1 batch transaction.</summary>
    Task<IReadOnlyList<D1Result<JsonElement>>> BatchAsync(
        IEnumerable<D1PreparedStatement> statements,
        CancellationToken cancellationToken = default);

    /// <summary>Executes prepared statements sequentially as a single D1 batch transaction.</summary>
    Task<IReadOnlyList<D1Result<T>>> BatchAsync<T>(
        IEnumerable<D1PreparedStatement> statements,
        CancellationToken cancellationToken = default);

    /// <summary>Dumps the database as a binary SQLite payload.</summary>
    Task<Body> DumpAsync(CancellationToken cancellationToken = default);
}
