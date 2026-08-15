namespace Workers;

public interface IMediaBinding : IBinding
{
    MediaPipeline Input(Body media);
}

public sealed class MediaPipeline
{
    public MediaPipeline Transform(object? options = null) => WorkerApi.NotExecutable<MediaPipeline>();
    public MediaOutput Output(MediaOutputOptions options) => WorkerApi.NotExecutable<MediaOutput>();
}

public sealed class MediaOutputOptions
{
    public required string Mode { get; init; }
    public string? Format { get; init; }
}

public sealed class MediaOutput
{
    public Task<Response> ResponseAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<Response>>();
    public Task<Body> MediaAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<Body>>();
    public Task<string> ContentTypeAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<string>>();
}
