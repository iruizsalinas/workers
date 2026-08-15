namespace Workers;

public interface IImagesBinding : IBinding
{
    ImagesPipeline Input(Body image);
    Task<ImagesInfo> InfoAsync(Body image, CancellationToken cancellationToken = default);
}

public sealed class ImagesInfo
{
    public string? Format { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
}

public sealed class ImagesPipeline
{
    public ImagesPipeline Transform(object options) => WorkerApi.NotExecutable<ImagesPipeline>();
    public ImagesPipeline Draw(Body image, object? options = null) => WorkerApi.NotExecutable<ImagesPipeline>();
    public Task<Response> OutputAsync(ImagesOutputOptions options, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<Response>>();
}

public sealed class ImagesOutputOptions
{
    public required string Format { get; init; }
    public int? Quality { get; init; }
}

public sealed class ImagesOperation;
