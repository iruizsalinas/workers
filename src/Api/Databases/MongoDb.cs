namespace Workers;

/// <summary>
/// A MongoDB client backed by the official <c>mongodb</c> driver. Install it with
/// <c>npm install mongodb</c> and enable the <c>nodejs_compat</c> compatibility flag.
/// Filters, updates, projections and pipelines are plain objects: anonymous objects, records or
/// <c>Dictionary&lt;string, object?&gt;</c> for operator keys such as <c>$gt</c>.
/// </summary>
public sealed class MongoClient : IAsyncDisposable
{
    public static Task<MongoClient> ConnectAsync(string connectionString, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<MongoClient>>();

    /// <summary>Creates an ObjectId value for filters and documents from its hexadecimal form.</summary>
    public static object ObjectId(string value) => WorkerApi.NotExecutable<object>();

    public MongoDatabase Database(string name) => WorkerApi.NotExecutable<MongoDatabase>();

    public ValueTask DisposeAsync() => WorkerApi.NotExecutable<ValueTask>();
}

public sealed class MongoDatabase
{
    public MongoCollection<T> Collection<T>(string name) => WorkerApi.NotExecutable<MongoCollection<T>>();
}

public sealed class MongoCollection<T>
{
    public Task<IReadOnlyList<T>> FindAsync(
        object? filter = null, MongoFindOptions? options = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<IReadOnlyList<T>>>();
    public Task<T?> FindOneAsync(object? filter = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<T?>>();
    public Task<long> CountDocumentsAsync(object? filter = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<long>>();
    public Task<IReadOnlyList<TResult>> AggregateAsync<TResult>(
        IEnumerable<object> pipeline, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<IReadOnlyList<TResult>>>();
    public Task<MongoInsertOneResult> InsertOneAsync(T document, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<MongoInsertOneResult>>();
    public Task<MongoInsertManyResult> InsertManyAsync(IEnumerable<T> documents, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<MongoInsertManyResult>>();
    public Task<MongoUpdateResult> UpdateOneAsync(
        object filter, object update, MongoUpdateOptions? options = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<MongoUpdateResult>>();
    public Task<MongoUpdateResult> UpdateManyAsync(
        object filter, object update, MongoUpdateOptions? options = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<MongoUpdateResult>>();
    public Task<MongoUpdateResult> ReplaceOneAsync(
        object filter, T replacement, MongoUpdateOptions? options = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<MongoUpdateResult>>();
    public Task<MongoDeleteResult> DeleteOneAsync(object filter, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<MongoDeleteResult>>();
    public Task<MongoDeleteResult> DeleteManyAsync(object filter, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<MongoDeleteResult>>();
}

public sealed class MongoFindOptions
{
    public object? Sort { get; init; }
    public object? Projection { get; init; }
    public int? Skip { get; init; }
    public int? Limit { get; init; }
}

public sealed class MongoUpdateOptions
{
    public bool? Upsert { get; init; }
}

public sealed class MongoInsertOneResult
{
    public string InsertedId { get; init; } = "";
}

public sealed class MongoInsertManyResult
{
    public int InsertedCount { get; init; }
    public IReadOnlyList<string> InsertedIds { get; init; } = [];
}

public sealed class MongoUpdateResult
{
    public long MatchedCount { get; init; }
    public long ModifiedCount { get; init; }
    public long UpsertedCount { get; init; }
    public string? UpsertedId { get; init; }
}

public sealed class MongoDeleteResult
{
    public long DeletedCount { get; init; }
}
