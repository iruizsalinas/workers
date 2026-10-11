namespace Workers;

public interface IImagesBinding : IBinding
{
    ImagesPipeline Input(ReadableStream image);
    Task<ImagesInfo> InfoAsync(ReadableStream image);
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
    public ImagesPipeline Draw(ReadableStream image, object? options = null) => WorkerApi.NotExecutable<ImagesPipeline>();
    public Task<Response> OutputAsync(ImagesOutputOptions options) => WorkerApi.NotExecutable<Task<Response>>();
}

public sealed class ImagesOutputOptions
{
    public required string Format { get; init; }
    public int? Quality { get; init; }
}
