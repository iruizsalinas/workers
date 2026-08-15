namespace Workers;

public interface IR2Bucket : IBinding
{
    Task<R2Object?> HeadAsync(string key, CancellationToken cancellationToken = default);
    Task<Body?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task<Body?> GetAsync(string key, R2GetOptions? options, CancellationToken cancellationToken = default);
    Task<R2ObjectBody?> GetObjectAsync(string key, R2GetOptions? options = null, CancellationToken cancellationToken = default);
    Task PutAsync(string key, Body body, R2PutOptions? options = null, CancellationToken cancellationToken = default);
    Task<R2Object?> PutObjectAsync(string key, Body body, R2PutOptions? options = null, CancellationToken cancellationToken = default);
    Task<R2Object?> PutObjectAsync(string key, ReadableStream body, R2PutOptions? options = null, CancellationToken cancellationToken = default);
    Task<R2MultipartUpload> CreateMultipartUploadAsync(string key, R2MultipartUploadOptions? options = null, CancellationToken cancellationToken = default);
    R2MultipartUpload ResumeMultipartUpload(string key, string uploadId);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
    Task<R2Objects> ListAsync(R2ListOptions? options = null, CancellationToken cancellationToken = default);
}

public sealed class R2GetOptions
{
    public R2Conditional? OnlyIf { get; init; }
    public R2Range? Range { get; init; }
}

public sealed class R2PutOptions
{
    public R2HttpMetadata? HttpMetadata { get; init; }
    public IReadOnlyDictionary<string, string>? CustomMetadata { get; init; }
    public R2Checksums? Checksums { get; init; }
    public R2Conditional? OnlyIf { get; init; }
}

public sealed class R2ListOptions
{
    public int? Limit { get; init; }
    public string? Prefix { get; init; }
    public string? StartAfter { get; init; }
    public string? Cursor { get; init; }
    public string? Delimiter { get; init; }
    public IReadOnlyList<string>? Include { get; init; }
}

public sealed record R2Conditional(DateTimeOffset? UploadedBefore = null, DateTimeOffset? UploadedAfter = null, string? EtagMatches = null, string? EtagDoesNotMatch = null);
public sealed record R2Range(ulong? Offset, ulong? Length, ulong? Suffix);
public sealed record R2HttpMetadata(
    string? ContentType = null,
    string? ContentLanguage = null,
    string? ContentDisposition = null,
    string? ContentEncoding = null,
    string? CacheControl = null,
    DateTimeOffset? CacheExpiry = null);
public sealed class R2Checksums;
public sealed record R2Object(
    string Key,
    string Version,
    ulong Size,
    string Etag,
    string HttpEtag,
    DateTimeOffset Uploaded,
    R2HttpMetadata HttpMetadata,
    IReadOnlyDictionary<string, string> CustomMetadata,
    R2Range? Range,
    R2Checksums Checksums);
public sealed record R2Objects(IReadOnlyList<R2Object> Objects, bool Truncated, string? Cursor, IReadOnlyList<string> DelimitedPrefixes);
public sealed record R2ObjectBody(
    string Key,
    string Version,
    ulong Size,
    string Etag,
    string HttpEtag,
    DateTimeOffset Uploaded,
    R2HttpMetadata HttpMetadata,
    IReadOnlyDictionary<string, string> CustomMetadata,
    R2Range? Range,
    R2Checksums Checksums,
    ReadableStream Body)
{
    public void WriteHttpMetadata(Headers headers) => WorkerApi.NotExecutable();
}
public sealed class R2MultipartUploadOptions
{
    public R2HttpMetadata? HttpMetadata { get; init; }
    public IReadOnlyDictionary<string, string>? CustomMetadata { get; init; }
}

public sealed record R2UploadedPart(int PartNumber, string Etag);
public sealed class R2MultipartUpload
{
    public string Key => WorkerApi.NotExecutable<string>();
    public string UploadId => WorkerApi.NotExecutable<string>();

    public Task<R2UploadedPart> UploadPartAsync(
        int partNumber, Body body, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<R2UploadedPart>>();
    public Task<R2Object> CompleteAsync(
        IEnumerable<R2UploadedPart> parts, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<R2Object>>();
    public Task AbortAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
}
