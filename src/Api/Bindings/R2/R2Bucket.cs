namespace Workers;

public interface IR2Bucket : IBinding
{
    Task<R2Object?> HeadAsync(string key);
    Task<R2ObjectBody?> GetAsync(string key, R2GetOptions? options = null);
    Task<R2Object?> PutAsync(string key, Body body, R2PutOptions? options = null);
    Task<R2MultipartUpload> CreateMultipartUploadAsync(string key, R2MultipartUploadOptions? options = null);
    R2MultipartUpload ResumeMultipartUpload(string key, string uploadId);
    Task DeleteAsync(string key);
    Task DeleteAsync(IEnumerable<string> keys);
    Task<R2Objects> ListAsync(R2ListOptions? options = null);
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

public sealed record R2Conditional(Date? UploadedBefore = null, Date? UploadedAfter = null, string? EtagMatches = null, string? EtagDoesNotMatch = null);
public sealed record R2Range(ulong? Offset, ulong? Length, ulong? Suffix);
public sealed record R2HttpMetadata(
    string? ContentType = null,
    string? ContentLanguage = null,
    string? ContentDisposition = null,
    string? ContentEncoding = null,
    string? CacheControl = null,
    Date? CacheExpiry = null);
public sealed record R2Object(
    string Key,
    string Version,
    ulong Size,
    string Etag,
    string HttpEtag,
    Date Uploaded,
    R2HttpMetadata HttpMetadata,
    IReadOnlyDictionary<string, string> CustomMetadata,
    R2Range? Range);
public sealed record R2Objects(IReadOnlyList<R2Object> Objects, bool Truncated, string? Cursor, IReadOnlyList<string> DelimitedPrefixes);
public sealed record R2ObjectBody(
    string Key,
    string Version,
    ulong Size,
    string Etag,
    string HttpEtag,
    Date Uploaded,
    R2HttpMetadata HttpMetadata,
    IReadOnlyDictionary<string, string> CustomMetadata,
    R2Range? Range,
    ReadableStream Body)
{
    public bool BodyUsed => WorkerApi.NotExecutable<bool>();
    public void WriteHttpMetadata(Headers headers) => WorkerApi.NotExecutable();
    public Task<string> TextAsync() => WorkerApi.NotExecutable<Task<string>>();
    public Task<byte[]> BytesAsync() => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<T?> JsonAsync<T>() => WorkerApi.NotExecutable<Task<T?>>();
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
        int partNumber, Body body) =>
        WorkerApi.NotExecutable<Task<R2UploadedPart>>();
    public Task<R2Object> CompleteAsync(
        IEnumerable<R2UploadedPart> parts) =>
        WorkerApi.NotExecutable<Task<R2Object>>();
    public Task AbortAsync() => WorkerApi.NotExecutable<Task>();
}
