namespace Workers;

public interface IMediaBinding : IBinding
{
    MediaPipeline Input(ReadableStream media);
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
    public Task<Response> ResponseAsync() => WorkerApi.NotExecutable<Task<Response>>();
    public Task<ReadableStream> MediaAsync() => WorkerApi.NotExecutable<Task<ReadableStream>>();
    public Task<string> ContentTypeAsync() => WorkerApi.NotExecutable<Task<string>>();
}
