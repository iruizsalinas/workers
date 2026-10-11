namespace Workers;

/// <summary>A Cloudflare Images binding.</summary>
public sealed class ImagesBinding
{
    private ImagesBinding() { }

    public Task<ImageInfoResponse> InfoAsync(ReadableStream stream) => WorkerApi.NotExecutable<Task<ImageInfoResponse>>();
    public ImageTransformer Input(ReadableStream stream) => WorkerApi.NotExecutable<ImageTransformer>();
}

public sealed class ImageInfoResponse
{
    private ImageInfoResponse() { }

    public string Format => WorkerApi.NotExecutable<string>();
    /// <summary>The file size in bytes; null for SVG images.</summary>
    public long? FileSize => WorkerApi.NotExecutable<long?>();
    public int? Width => WorkerApi.NotExecutable<int?>();
    public int? Height => WorkerApi.NotExecutable<int?>();
}

public sealed class ImageTransformer
{
    private ImageTransformer() { }

    /// <summary>Applies a transform object such as <c>new { width = 256, fit = "cover" }</c>.</summary>
    public ImageTransformer Transform(object transform) => WorkerApi.NotExecutable<ImageTransformer>();
    /// <summary>Draws another image over this one, with options such as <c>new { top = 0, opacity = 0.5 }</c>.</summary>
    public ImageTransformer Draw(ReadableStream image, object? options = null) => WorkerApi.NotExecutable<ImageTransformer>();
    public ImageTransformer Draw(ImageTransformer image, object? options = null) => WorkerApi.NotExecutable<ImageTransformer>();
    public Task<ImageTransformationResult> OutputAsync(ImageOutputOptions options) => WorkerApi.NotExecutable<Task<ImageTransformationResult>>();
}

public sealed class ImageOutputOptions
{
    /// <summary>A MIME type such as <c>image/webp</c>, <c>image/avif</c>, <c>image/png</c> or <c>image/jpeg</c>.</summary>
    public required string Format { get; init; }
    public int? Quality { get; init; }
    public string? Background { get; init; }
}

public sealed class ImageTransformationResult
{
    private ImageTransformationResult() { }

    public Response Response() => WorkerApi.NotExecutable<Response>();
    public string ContentType() => WorkerApi.NotExecutable<string>();
    public ReadableStream Image() => WorkerApi.NotExecutable<ReadableStream>();
}

/// <summary>A Media Transformations binding.</summary>
public sealed class MediaBinding
{
    private MediaBinding() { }

    public MediaTransformer Input(ReadableStream media) => WorkerApi.NotExecutable<MediaTransformer>();
}

public sealed class MediaTransformer
{
    private MediaTransformer() { }

    /// <summary>Applies a transform object such as <c>new { width = 640, fit = "contain" }</c>.</summary>
    public MediaTransformationGenerator Transform(object? transform = null) => WorkerApi.NotExecutable<MediaTransformationGenerator>();
    public MediaTransformationResult Output(MediaTransformationOutputOptions? output = null) => WorkerApi.NotExecutable<MediaTransformationResult>();
}

public sealed class MediaTransformationGenerator
{
    private MediaTransformationGenerator() { }

    public MediaTransformationResult Output(MediaTransformationOutputOptions? output = null) => WorkerApi.NotExecutable<MediaTransformationResult>();
}

public sealed class MediaTransformationOutputOptions
{
    /// <summary><c>video</c>, <c>spritesheet</c>, <c>frame</c> or <c>audio</c>.</summary>
    public string? Mode { get; init; }
    public bool? Audio { get; init; }
    public string? Time { get; init; }
    public string? Duration { get; init; }
    public int? ImageCount { get; init; }
    /// <summary><c>jpg</c>, <c>png</c> or <c>m4a</c>.</summary>
    public string? Format { get; init; }
}

public sealed class MediaTransformationResult
{
    private MediaTransformationResult() { }

    public Task<ReadableStream> MediaAsync() => WorkerApi.NotExecutable<Task<ReadableStream>>();
    public Task<Response> ResponseAsync() => WorkerApi.NotExecutable<Task<Response>>();
    public Task<string> ContentTypeAsync() => WorkerApi.NotExecutable<Task<string>>();
}
