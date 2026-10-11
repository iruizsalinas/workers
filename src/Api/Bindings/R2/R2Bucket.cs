namespace Workers;

public sealed class R2Bucket
{
    private R2Bucket() { }

    public Task<R2Object?> HeadAsync(string key) => WorkerApi.NotExecutable<Task<R2Object?>>();
    /// <summary>Reads an object, or null when it does not exist or an <see cref="R2GetOptions.OnlyIf"/> condition fails.</summary>
    public Task<R2ObjectBody?> GetAsync(string key, R2GetOptions? options = null) => WorkerApi.NotExecutable<Task<R2ObjectBody?>>();
    /// <summary>
    /// Stores an object, or returns null when an <see cref="R2PutOptions.OnlyIf"/> condition fails.
    /// A stream must have a known length, such as a request body or the readable side of a <see cref="FixedLengthStream"/>.
    /// </summary>
    public Task<R2Object?> PutAsync(string key, BodyInit? value, R2PutOptions? options = null) => WorkerApi.NotExecutable<Task<R2Object?>>();
    public Task DeleteAsync(string key) => WorkerApi.NotExecutable<Task>();
    public Task DeleteAsync(IEnumerable<string> keys) => WorkerApi.NotExecutable<Task>();
    public Task<R2Objects> ListAsync(R2ListOptions? options = null) => WorkerApi.NotExecutable<Task<R2Objects>>();
    public Task<R2MultipartUpload> CreateMultipartUploadAsync(string key, R2MultipartOptions? options = null) =>
        WorkerApi.NotExecutable<Task<R2MultipartUpload>>();
    public R2MultipartUpload ResumeMultipartUpload(string key, string uploadId) => WorkerApi.NotExecutable<R2MultipartUpload>();
}

public class R2Object
{
    private protected R2Object() { }

    public string Key => WorkerApi.NotExecutable<string>();
    public string Version => WorkerApi.NotExecutable<string>();
    public long Size => WorkerApi.NotExecutable<long>();
    public string Etag => WorkerApi.NotExecutable<string>();
    public string HttpEtag => WorkerApi.NotExecutable<string>();
    public Date Uploaded => WorkerApi.NotExecutable<Date>();
    public string StorageClass => WorkerApi.NotExecutable<string>();
    /// <summary>Null in list results unless <see cref="R2ListOptions.Include"/> requests it.</summary>
    public R2HttpMetadata? HttpMetadata => WorkerApi.NotExecutable<R2HttpMetadata?>();
    /// <summary>Null in list results unless <see cref="R2ListOptions.Include"/> requests it.</summary>
    public IReadOnlyDictionary<string, string>? CustomMetadata => WorkerApi.NotExecutable<IReadOnlyDictionary<string, string>?>();
    public R2Range? Range => WorkerApi.NotExecutable<R2Range?>();

    public void WriteHttpMetadata(Headers headers) => WorkerApi.NotExecutable();
}

public sealed class R2ObjectBody : R2Object
{
    private R2ObjectBody() { }

    public ReadableStream Body => WorkerApi.NotExecutable<ReadableStream>();
    public bool BodyUsed => WorkerApi.NotExecutable<bool>();

    public Task<string> TextAsync() => WorkerApi.NotExecutable<Task<string>>();
    public Task<byte[]> BytesAsync() => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<T?> JsonAsync<T>() => WorkerApi.NotExecutable<Task<T?>>();
    public Task<Blob> BlobAsync() => WorkerApi.NotExecutable<Task<Blob>>();
}

public sealed class R2Objects
{
    private R2Objects() { }

    public IReadOnlyList<R2Object> Objects => WorkerApi.NotExecutable<IReadOnlyList<R2Object>>();
    public bool Truncated => WorkerApi.NotExecutable<bool>();
    /// <summary>The cursor for the next page, or null when the list is not <see cref="Truncated"/>.</summary>
    public string? Cursor => WorkerApi.NotExecutable<string?>();
    public IReadOnlyList<string> DelimitedPrefixes => WorkerApi.NotExecutable<IReadOnlyList<string>>();
}

public sealed class R2GetOptions
{
    public R2Conditional? OnlyIf { get; init; }
    public R2Range? Range { get; init; }
}

public sealed class R2PutOptions
{
    public R2Conditional? OnlyIf { get; init; }
    public R2HttpMetadata? HttpMetadata { get; init; }
    public IReadOnlyDictionary<string, string>? CustomMetadata { get; init; }
    /// <summary>The storage class, such as <c>Standard</c> or <c>InfrequentAccess</c>.</summary>
    public string? StorageClass { get; init; }
    /// <summary>An expected checksum as hex; R2 rejects the upload when the body does not match.</summary>
    public string? Md5 { get; init; }
    public string? Sha1 { get; init; }
    public string? Sha256 { get; init; }
    public string? Sha384 { get; init; }
    public string? Sha512 { get; init; }
}

public sealed class R2MultipartOptions
{
    public R2HttpMetadata? HttpMetadata { get; init; }
    public IReadOnlyDictionary<string, string>? CustomMetadata { get; init; }
    public string? StorageClass { get; init; }
}

public sealed class R2ListOptions
{
    public int? Limit { get; init; }
    public string? Prefix { get; init; }
    public string? Cursor { get; init; }
    public string? Delimiter { get; init; }
    public string? StartAfter { get; init; }
    public IReadOnlyList<R2ListInclude>? Include { get; init; }
}

public enum R2ListInclude
{
    HttpMetadata,
    CustomMetadata
}

public sealed class R2Conditional
{
    public string? EtagMatches { get; init; }
    public string? EtagDoesNotMatch { get; init; }
    public Date? UploadedBefore { get; init; }
    public Date? UploadedAfter { get; init; }
}

/// <summary>A byte range: <see cref="Offset"/> with an optional <see cref="Length"/>, or a <see cref="Suffix"/> of the last bytes.</summary>
public sealed class R2Range
{
    public long? Offset { get; init; }
    public long? Length { get; init; }
    public long? Suffix { get; init; }
}

public sealed class R2HttpMetadata
{
    public string? ContentType { get; init; }
    public string? ContentLanguage { get; init; }
    public string? ContentDisposition { get; init; }
    public string? ContentEncoding { get; init; }
    public string? CacheControl { get; init; }
    public Date? CacheExpiry { get; init; }
}

public sealed class R2MultipartUpload
{
    private R2MultipartUpload() { }

    public string Key => WorkerApi.NotExecutable<string>();
    public string UploadId => WorkerApi.NotExecutable<string>();

    public Task<R2UploadedPart> UploadPartAsync(int partNumber, BodyInit value) => WorkerApi.NotExecutable<Task<R2UploadedPart>>();
    public Task AbortAsync() => WorkerApi.NotExecutable<Task>();
    public Task<R2Object> CompleteAsync(IEnumerable<R2UploadedPart> uploadedParts) => WorkerApi.NotExecutable<Task<R2Object>>();
}

/// <summary>A completed part. Store the part number and etag to complete an upload in a later request.</summary>
public sealed class R2UploadedPart
{
    public required int PartNumber { get; init; }
    public required string Etag { get; init; }
}
