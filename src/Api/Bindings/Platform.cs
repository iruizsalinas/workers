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
    public Task<TOutput?> RunAsync<TInput, TOutput>(string model, TInput inputs, AiOptions? options = null) =>
        WorkerApi.NotExecutable<Task<TOutput?>>();
    /// <summary>Runs a model whose output is a stream, such as text generation with <c>stream: true</c> or image generation.</summary>
    public Task<ReadableStream> RunStreamAsync<TInput>(string model, TInput inputs, AiOptions? options = null) =>
        WorkerApi.NotExecutable<Task<ReadableStream>>();
    /// <summary>Searches the web through an AI Gateway and returns the provider's JSON response.</summary>
    public Task<Response> WebSearchAsync(AiWebSearchRequest request) => WorkerApi.NotExecutable<Task<Response>>();

    /// <summary>The AI Gateway log ID of the latest <see cref="RunAsync{TInput, TOutput}"/> sent through a gateway, or null.</summary>
    public string? AiGatewayLogId => WorkerApi.NotExecutable<string?>();
    /// <summary>An AI Gateway, to read and annotate its logs or send requests to other providers through it.</summary>
    public AiGateway Gateway(string gatewayId) => WorkerApi.NotExecutable<AiGateway>();
    /// <summary>Searches the Workers AI model catalog.</summary>
    public Task<IReadOnlyList<AiModelsSearchObject>> ModelsAsync(AiModelsSearchParams? search = null) =>
        WorkerApi.NotExecutable<Task<IReadOnlyList<AiModelsSearchObject>>>();
    /// <summary>The Markdown conversion service, which also lists the supported file formats.</summary>
    public ToMarkdownService ToMarkdown() => WorkerApi.NotExecutable<ToMarkdownService>();
    /// <summary>Converts documents such as PDF, HTML, Office files and images to Markdown, one result per document.</summary>
    public Task<IReadOnlyList<ConversionResponse>> ToMarkdownAsync(IEnumerable<MarkdownDocument> files, ConversionRequestOptions? options = null) =>
        WorkerApi.NotExecutable<Task<IReadOnlyList<ConversionResponse>>>();
    public Task<ConversionResponse> ToMarkdownAsync(MarkdownDocument file, ConversionRequestOptions? options = null) =>
        WorkerApi.NotExecutable<Task<ConversionResponse>>();
}

public sealed class AiGateway
{
    private AiGateway() { }

    /// <summary>Adds a score, feedback or metadata to a logged request.</summary>
    public Task PatchLogAsync(string logId, AiGatewayPatchLog data) => WorkerApi.NotExecutable<Task>();
    public Task<AiGatewayLog> GetLogAsync(string logId) => WorkerApi.NotExecutable<Task<AiGatewayLog>>();
    /// <summary>Sends a request to a provider through the gateway's universal endpoint.</summary>
    public Task<Response> RunAsync(AIGatewayUniversalRequest request, AiGatewayRunOptions? options = null) =>
        WorkerApi.NotExecutable<Task<Response>>();
    /// <summary>Sends requests that the gateway tries in order, falling back to the next when one fails.</summary>
    public Task<Response> RunAsync(IEnumerable<AIGatewayUniversalRequest> requests, AiGatewayRunOptions? options = null) =>
        WorkerApi.NotExecutable<Task<Response>>();
    /// <summary>The gateway's base URL, or its URL for a provider such as <c>openai</c> or <c>anthropic</c>.</summary>
    public Task<string> GetUrlAsync(string? provider = null) => WorkerApi.NotExecutable<Task<string>>();
}

public sealed class AiGatewayPatchLog
{
    public double? Score { get; init; }
    /// <summary>1 for positive feedback, -1 for negative.</summary>
    public int? Feedback { get; init; }
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }
}

public sealed class AiGatewayLog
{
    private AiGatewayLog() { }

    public string Id => WorkerApi.NotExecutable<string>();
    public string Provider => WorkerApi.NotExecutable<string>();
    public string Model => WorkerApi.NotExecutable<string>();
    public string? ModelType => WorkerApi.NotExecutable<string?>();
    public string Path => WorkerApi.NotExecutable<string>();
    /// <summary>Milliseconds.</summary>
    public double Duration => WorkerApi.NotExecutable<double>();
    public string? RequestType => WorkerApi.NotExecutable<string?>();
    public string? RequestContentType => WorkerApi.NotExecutable<string?>();
    public int StatusCode => WorkerApi.NotExecutable<int>();
    public string? ResponseContentType => WorkerApi.NotExecutable<string?>();
    public bool Success => WorkerApi.NotExecutable<bool>();
    public bool Cached => WorkerApi.NotExecutable<bool>();
    public long? TokensIn => WorkerApi.NotExecutable<long?>();
    public long? TokensOut => WorkerApi.NotExecutable<long?>();
    public JsonValue? Metadata => WorkerApi.NotExecutable<JsonValue?>();
    public int? Step => WorkerApi.NotExecutable<int?>();
    public double? Cost => WorkerApi.NotExecutable<double?>();
    public bool? CustomCost => WorkerApi.NotExecutable<bool?>();
    public long RequestSize => WorkerApi.NotExecutable<long>();
    public string? RequestHead => WorkerApi.NotExecutable<string?>();
    public bool RequestHeadComplete => WorkerApi.NotExecutable<bool>();
    public long ResponseSize => WorkerApi.NotExecutable<long>();
    public string? ResponseHead => WorkerApi.NotExecutable<string?>();
    public bool ResponseHeadComplete => WorkerApi.NotExecutable<bool>();
    public Date CreatedAt => WorkerApi.NotExecutable<Date>();
}

/// <summary>A request to a provider through an AI Gateway's universal endpoint.</summary>
public sealed class AIGatewayUniversalRequest
{
    /// <summary>The provider, such as <c>workers-ai</c>, <c>openai</c> or <c>anthropic</c>.</summary>
    public required string Provider { get; init; }
    /// <summary>The provider's endpoint path, such as <c>chat/completions</c>.</summary>
    public required string Endpoint { get; init; }
    /// <summary>Request headers, such as <c>Authorization</c> or gateway headers like <c>cf-aig-cache-ttl</c>.</summary>
    public required IReadOnlyDictionary<string, object> Headers { get; init; }
    /// <summary>The JSON request body sent to the provider.</summary>
    public required object? Query { get; init; }
}

public sealed class AiGatewayRunOptions
{
    public UniversalGatewayOptions? Gateway { get; init; }
    public IReadOnlyDictionary<string, string>? ExtraHeaders { get; init; }
    public AbortSignal? Signal { get; init; }
}

/// <summary>Gateway settings for a universal request; the gateway itself comes from <see cref="Ai.Gateway"/>.</summary>
public sealed class UniversalGatewayOptions
{
    public string? CacheKey { get; init; }
    /// <summary>Seconds to cache the response.</summary>
    public int? CacheTtl { get; init; }
    public bool? SkipCache { get; init; }
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }
    public bool? CollectLog { get; init; }
    public string? EventId { get; init; }
    public int? RequestTimeoutMs { get; init; }
    public GatewayRetries? Retries { get; init; }
}

public sealed class AiModelsSearchParams
{
    public string? Author { get; init; }
    public bool? HideExperimental { get; init; }
    public int? Page { get; init; }
    public int? PerPage { get; init; }
    public string? Search { get; init; }
    public int? Source { get; init; }
    /// <summary>A task name, such as <c>Text Generation</c>.</summary>
    public string? Task { get; init; }
}

public sealed class AiModelsSearchObject
{
    private AiModelsSearchObject() { }

    public string Id => WorkerApi.NotExecutable<string>();
    public int Source => WorkerApi.NotExecutable<int>();
    /// <summary>The model name to pass to <see cref="Ai.RunAsync{TInput, TOutput}"/>, such as <c>@cf/meta/llama-3.1-8b-instruct</c>.</summary>
    public string Name => WorkerApi.NotExecutable<string>();
    public string Description => WorkerApi.NotExecutable<string>();
    public AiModelsSearchTask Task => WorkerApi.NotExecutable<AiModelsSearchTask>();
    public IReadOnlyList<string> Tags => WorkerApi.NotExecutable<IReadOnlyList<string>>();
    public IReadOnlyList<AiModelsSearchProperty> Properties => WorkerApi.NotExecutable<IReadOnlyList<AiModelsSearchProperty>>();
}

public sealed class AiModelsSearchTask
{
    private AiModelsSearchTask() { }

    public string Id => WorkerApi.NotExecutable<string>();
    public string Name => WorkerApi.NotExecutable<string>();
    public string Description => WorkerApi.NotExecutable<string>();
}

public sealed class AiModelsSearchProperty
{
    private AiModelsSearchProperty() { }

    public string PropertyId => WorkerApi.NotExecutable<string>();
    public string Value => WorkerApi.NotExecutable<string>();
}

public sealed class ToMarkdownService
{
    private ToMarkdownService() { }

    public Task<IReadOnlyList<ConversionResponse>> TransformAsync(IEnumerable<MarkdownDocument> files, ConversionRequestOptions? options = null) =>
        WorkerApi.NotExecutable<Task<IReadOnlyList<ConversionResponse>>>();
    public Task<ConversionResponse> TransformAsync(MarkdownDocument file, ConversionRequestOptions? options = null) =>
        WorkerApi.NotExecutable<Task<ConversionResponse>>();
    public Task<IReadOnlyList<SupportedFileFormat>> SupportedAsync() => WorkerApi.NotExecutable<Task<IReadOnlyList<SupportedFileFormat>>>();
}

public sealed class SupportedFileFormat
{
    private SupportedFileFormat() { }

    public string MimeType => WorkerApi.NotExecutable<string>();
    /// <summary>The file extension, such as <c>.pdf</c>.</summary>
    public string Extension => WorkerApi.NotExecutable<string>();
}

/// <summary>A document to convert; the file name's extension and the blob's type identify its format.</summary>
public sealed class MarkdownDocument
{
    public required string Name { get; init; }
    public required Blob Blob { get; init; }
}

public sealed class ConversionRequestOptions
{
    public GatewayOptions? Gateway { get; init; }
    public IReadOnlyDictionary<string, string>? ExtraHeaders { get; init; }
    public ConversionOptions? ConversionOptions { get; init; }
}

public sealed class ConversionOptions
{
    public ConversionOutputOptions? Output { get; init; }
    public ConversionHtmlOptions? Html { get; init; }
    public ConversionEmbeddedImagesOptions? Docx { get; init; }
    /// <summary>Options for image files, which are converted to a description.</summary>
    public ImageConversionOptions? Image { get; init; }
    public ConversionPdfOptions? Pdf { get; init; }
}

public sealed class ConversionOutputOptions
{
    public ConversionOutputFormat? Format { get; init; }
}

public enum ConversionOutputFormat
{
    Markdown,
    Text
}

public sealed class ConversionHtmlOptions
{
    public EmbeddedImageConversionOptions? Images { get; init; }
    /// <summary>The host name used to resolve relative URLs in the page.</summary>
    public string? Hostname { get; init; }
    /// <summary>Converts only the elements that match this CSS selector.</summary>
    public string? CssSelector { get; init; }
}

public sealed class ConversionEmbeddedImagesOptions
{
    public EmbeddedImageConversionOptions? Images { get; init; }
}

public sealed class ConversionPdfOptions
{
    public EmbeddedImageConversionOptions? Images { get; init; }
    /// <summary>Whether to include the document's metadata in the output.</summary>
    public bool? Metadata { get; init; }
}

public sealed class ImageConversionOptions
{
    /// <summary>The language of generated image descriptions: <c>en</c>, <c>es</c>, <c>fr</c>, <c>it</c>, <c>pt</c> or <c>de</c>.</summary>
    public string? DescriptionLanguage { get; init; }
}

/// <summary>How images inside a document are converted.</summary>
public sealed class EmbeddedImageConversionOptions
{
    /// <summary>Whether to describe embedded images in the output.</summary>
    public bool? Convert { get; init; }
    public int? MaxConvertedImages { get; init; }
    /// <summary>The language of generated image descriptions: <c>en</c>, <c>es</c>, <c>fr</c>, <c>it</c>, <c>pt</c> or <c>de</c>.</summary>
    public string? DescriptionLanguage { get; init; }
    /// <summary>For HTML: whether to describe the page's Open Graph image.</summary>
    public bool? ConvertOGImage { get; init; }
}

/// <summary>A converted document, or the reason it could not be converted when <see cref="Format"/> is <c>error</c>.</summary>
public sealed class ConversionResponse
{
    private ConversionResponse() { }

    public string Id => WorkerApi.NotExecutable<string>();
    public string Name => WorkerApi.NotExecutable<string>();
    public string MimeType => WorkerApi.NotExecutable<string>();
    /// <summary><c>markdown</c>, <c>text</c> or <c>error</c>.</summary>
    public string Format => WorkerApi.NotExecutable<string>();
    /// <summary>The token count of <see cref="Data"/>; null on error.</summary>
    public int? Tokens => WorkerApi.NotExecutable<int?>();
    /// <summary>The converted content; null on error.</summary>
    public string? Data => WorkerApi.NotExecutable<string?>();
    /// <summary>Why the conversion failed; null on success.</summary>
    public string? Error => WorkerApi.NotExecutable<string?>();
}

public sealed class AiOptions
{
    /// <summary>Routes the request through an AI Gateway for caching, logging and retries.</summary>
    public GatewayOptions? Gateway { get; init; }
    /// <summary>Up to 5 tags of at most 50 characters, to group requests in the dashboard.</summary>
    public IReadOnlyList<string>? Tags { get; init; }
    public string? Prefix { get; init; }
    /// <summary>Extra request headers, such as <c>new Dictionary&lt;string, string&gt; { ["x-session-id"] = id }</c>.</summary>
    public IReadOnlyDictionary<string, string>? ExtraHeaders { get; init; }
    public AbortSignal? Signal { get; init; }
}

public sealed class GatewayOptions
{
    /// <summary>The AI Gateway ID.</summary>
    public required string Id { get; init; }
    public string? CacheKey { get; init; }
    /// <summary>Seconds to cache the response.</summary>
    public int? CacheTtl { get; init; }
    public bool? SkipCache { get; init; }
    /// <summary>Values logged with the request, such as <c>new Dictionary&lt;string, object?&gt; { ["user"] = id }</c>.</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }
    public bool? CollectLog { get; init; }
    public string? EventId { get; init; }
    public int? RequestTimeoutMs { get; init; }
    public GatewayRetries? Retries { get; init; }
}

public sealed class GatewayRetries
{
    /// <summary>From 1 to 5.</summary>
    public int? MaxAttempts { get; init; }
    public int? RetryDelayMs { get; init; }
    /// <summary><c>constant</c>, <c>linear</c> or <c>exponential</c>.</summary>
    public string? Backoff { get; init; }
}

public sealed class AiWebSearchRequest
{
    /// <summary>The AI Gateway whose web search configuration is used.</summary>
    public required string GatewayId { get; init; }
    public required string Query { get; init; }
    /// <summary>The maximum number of results; defaults to 10 and is capped at 20.</summary>
    public int? Limit { get; init; }
    /// <summary>A bring-your-own-key provider configured on the gateway.</summary>
    public string? Provider { get; init; }
    /// <summary>The bring-your-own-key alias; defaults to <c>default</c>.</summary>
    public string? ByokAlias { get; init; }
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
    /// <summary>The resolved IP address of the Hyperdrive endpoint, for drivers that need an address instead of a host name.</summary>
    public string Ip => WorkerApi.NotExecutable<string>();
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
