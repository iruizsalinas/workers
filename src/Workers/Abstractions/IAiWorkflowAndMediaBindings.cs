namespace Workers;

/// <summary>Represents a Workers AI binding.</summary>
public interface IAiBinding : IBinding
{
    /// <summary>Runs a model with JSON-serializable input and output.</summary>
    Task<TOutput?> RunAsync<TInput, TOutput>(
        string model,
        TInput input,
        CancellationToken cancellationToken = default);

    /// <summary>Runs a model that returns binary data.</summary>
    Task<Body> RunBytesAsync<TInput>(
        string model,
        TInput input,
        CancellationToken cancellationToken = default);
}

/// <summary>Represents a Workflows binding.</summary>
public interface IWorkflowBinding : IBinding
{
    /// <summary>Creates a Workflow instance with default options.</summary>
    Task<WorkflowInstance> CreateAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a Workflow instance.</summary>
    Task<WorkflowInstance> CreateAsync(
        WorkflowInstanceCreateOptions? options,
        CancellationToken cancellationToken = default);

    /// <summary>Creates multiple Workflow instances, up to 100 at a time.</summary>
    Task<IReadOnlyList<WorkflowInstance>> CreateBatchAsync(
        IEnumerable<WorkflowInstanceCreateOptions> batch,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a Workflow instance by ID.</summary>
    Task<WorkflowInstance> GetAsync(string id, CancellationToken cancellationToken = default);
}

/// <summary>Represents a Cloudflare Images binding.</summary>
public interface IImagesBinding : IBinding
{
    /// <summary>Creates a transform pipeline from image bytes.</summary>
    ImagesPipeline Input(Body image);

    /// <summary>Reads metadata for image bytes.</summary>
    Task<ImagesInfo> InfoAsync(Body image, CancellationToken cancellationToken = default);

    /// <summary>Runs a complete transform pipeline and returns the output response.</summary>
    Task<Response> RunPipelineAsync(
        Body image,
        IReadOnlyList<ImagesOperation> operations,
        ImagesOutputOptions output,
        CancellationToken cancellationToken = default);
}

/// <summary>Represents a Media Transformations binding.</summary>
public interface IMediaBinding : IBinding
{
    /// <summary>Creates a transform pipeline from media bytes.</summary>
    MediaPipeline Input(Body media);

    /// <summary>Runs a complete media pipeline and returns a response.</summary>
    Task<Response> RunResponseAsync(
        Body media,
        bool hasTransform,
        object? transformOptions,
        MediaOutputOptions output,
        CancellationToken cancellationToken = default);

    /// <summary>Runs a complete media pipeline and returns bytes with a content type.</summary>
    Task<Body> RunMediaAsync(
        Body media,
        bool hasTransform,
        object? transformOptions,
        MediaOutputOptions output,
        CancellationToken cancellationToken = default);

    /// <summary>Runs a complete media pipeline and returns the output content type.</summary>
    Task<string> RunContentTypeAsync(
        Body media,
        bool hasTransform,
        object? transformOptions,
        MediaOutputOptions output,
        CancellationToken cancellationToken = default);
}

/// <summary>Represents a Vectorize index binding.</summary>
public interface IVectorizeIndex : IBinding
{
    /// <summary>Inserts vectors into the index. Existing IDs are left unchanged.</summary>
    Task<VectorizeMutationResult> InsertAsync(
        IEnumerable<VectorizeVector> vectors,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts or replaces vectors in the index.</summary>
    Task<VectorizeMutationResult> UpsertAsync(
        IEnumerable<VectorizeVector> vectors,
        CancellationToken cancellationToken = default);

    /// <summary>Queries the index with a vector and returns nearest matches.</summary>
    Task<VectorizeQueryResult> QueryAsync(
        IEnumerable<double> vector,
        VectorizeQueryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Queries the index using the vector stored under the provided ID.</summary>
    Task<VectorizeQueryResult> QueryByIdAsync(
        string id,
        VectorizeQueryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves vectors by ID.</summary>
    Task<IReadOnlyList<VectorizeVector>> GetByIdsAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes vectors by ID.</summary>
    Task<VectorizeMutationResult> DeleteByIdsAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves index configuration and status details.</summary>
    Task<VectorizeIndexDetails> DescribeAsync(CancellationToken cancellationToken = default);
}
