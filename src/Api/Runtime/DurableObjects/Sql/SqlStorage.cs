namespace Workers;

/// <summary>SQLite storage for a Durable Object. Queries run synchronously when <see cref="Exec{T}"/> is called.</summary>
public sealed class DurableObjectSqlStorage
{
    public double DatabaseSize => WorkerApi.NotExecutable<double>();

    public DurableObjectSqlCursor<T> Exec<T>(string query, params object?[] bindings) =>
        WorkerApi.NotExecutable<DurableObjectSqlCursor<T>>();
}

/// <summary>Rows returned by <c>sql.exec()</c>. Read them with <see cref="Next"/>, <see cref="One"/> or <see cref="ToArray"/>.</summary>
public sealed class DurableObjectSqlCursor<T>
{
    public IReadOnlyList<string> ColumnNames => WorkerApi.NotExecutable<IReadOnlyList<string>>();
    public double RowsRead => WorkerApi.NotExecutable<double>();
    public double RowsWritten => WorkerApi.NotExecutable<double>();

    public T? Next() => WorkerApi.NotExecutable<T?>();
    public T One() => WorkerApi.NotExecutable<T>();
    public IReadOnlyList<T> ToArray() => WorkerApi.NotExecutable<IReadOnlyList<T>>();
    public IReadOnlyList<IReadOnlyList<JsonValue>> Raw() => WorkerApi.NotExecutable<IReadOnlyList<IReadOnlyList<JsonValue>>>();
}
