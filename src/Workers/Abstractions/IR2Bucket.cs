namespace Workers;

/// <summary>Represents a Workers R2 bucket binding.</summary>
public interface IR2Bucket : IBinding
{
    /// <summary>Gets object metadata by key without reading the body.</summary>
    Task<R2Object?> HeadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Gets an object body by key.</summary>
    Task<Body?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Gets an object body by key.</summary>
    Task<Body?> GetAsync(string key, R2GetOptions? options, CancellationToken cancellationToken = default);

    /// <summary>Stores an object body by key.</summary>
    Task PutAsync(string key, Body body, CancellationToken cancellationToken = default);

    /// <summary>Stores an object body by key.</summary>
    Task PutAsync(string key, Body body, R2PutOptions? options, CancellationToken cancellationToken = default);

    /// <summary>Stores an object body by key and returns stored object metadata.</summary>
    Task<R2Object?> PutObjectAsync(string key, Body body, R2PutOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Creates a multipart upload for an object key.</summary>
    Task<R2MultipartUpload> CreateMultipartUploadAsync(
        string key,
        R2MultipartUploadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Resumes a multipart upload by key and upload id.</summary>
    R2MultipartUpload ResumeMultipartUpload(string key, string uploadId);

    /// <summary>Deletes an object by key.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Deletes multiple objects by key.</summary>
    Task DeleteAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>Lists objects in the bucket.</summary>
    Task<R2Objects> ListAsync(R2ListOptions? options = null, CancellationToken cancellationToken = default);
}
