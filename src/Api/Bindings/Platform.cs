namespace Workers;

/// <summary>A rate limiting binding.</summary>
public sealed class RateLimit
{
    private RateLimit() { }

    public Task<RateLimitOutcome> LimitAsync(RateLimitOptions options) => WorkerApi.NotExecutable<Task<RateLimitOutcome>>();
}

public sealed class RateLimitOptions
{
    /// <summary>The key to count requests against, such as a user ID or API key.</summary>
    public required string Key { get; init; }
}

public sealed class RateLimitOutcome
{
    private RateLimitOutcome() { }

    public bool Success => WorkerApi.NotExecutable<bool>();
}

/// <summary>A Workers Analytics Engine dataset.</summary>
public sealed class AnalyticsEngineDataset
{
    private AnalyticsEngineDataset() { }

    /// <summary>Queues a data point without waiting; the write cannot fail the request.</summary>
    public void WriteDataPoint(AnalyticsEngineDataPoint? dataPoint = null) => WorkerApi.NotExecutable();
}

public sealed class AnalyticsEngineDataPoint
{
    /// <summary>At most one index, used for sampling.</summary>
    public IReadOnlyList<string>? Indexes { get; init; }
    public IReadOnlyList<double>? Doubles { get; init; }
    public IReadOnlyList<string>? Blobs { get; init; }
}

/// <summary>A Workers AI binding.</summary>
public sealed class Ai
{
    private Ai() { }

    /// <summary>Runs a model and maps its JSON output to <typeparamref name="TOutput"/>.</summary>
    public Task<TOutput?> RunAsync<TInput, TOutput>(string model, TInput inputs) => WorkerApi.NotExecutable<Task<TOutput?>>();
    /// <summary>Runs a model whose output is a stream, such as text generation with <c>stream: true</c> or image generation.</summary>
    public Task<ReadableStream> RunStreamAsync<TInput>(string model, TInput inputs) => WorkerApi.NotExecutable<Task<ReadableStream>>();
}

/// <summary>A Secrets Store secret.</summary>
public sealed class SecretsStoreSecret
{
    private SecretsStoreSecret() { }

    public Task<string> GetAsync() => WorkerApi.NotExecutable<Task<string>>();
}

/// <summary>A Hyperdrive configuration. Pass the connection details to a database driver, or connect a socket.</summary>
public sealed class Hyperdrive
{
    private Hyperdrive() { }

    public string ConnectionString => WorkerApi.NotExecutable<string>();
    public string Host => WorkerApi.NotExecutable<string>();
    public int Port => WorkerApi.NotExecutable<int>();
    public string User => WorkerApi.NotExecutable<string>();
    public string Password => WorkerApi.NotExecutable<string>();
    public string Database => WorkerApi.NotExecutable<string>();
    public Socket Connect() => WorkerApi.NotExecutable<Socket>();
}

/// <summary>Metadata about the deployed version of this Worker.</summary>
public sealed class WorkerVersionMetadata
{
    private WorkerVersionMetadata() { }

    public string Id => WorkerApi.NotExecutable<string>();
    public string Tag => WorkerApi.NotExecutable<string>();
    public string Timestamp => WorkerApi.NotExecutable<string>();
}
