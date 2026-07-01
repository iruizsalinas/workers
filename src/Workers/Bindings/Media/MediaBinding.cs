using System.Text.Json;

namespace Workers;

/// <summary>Options passed to Media Transformations output.</summary>
public sealed class MediaOutputOptions
{
    /// <summary>The output mode: <c>video</c>, <c>frame</c>, <c>spritesheet</c>, or <c>audio</c>.</summary>
    public required string Mode { get; init; }

    /// <summary>Start timestamp for extraction, such as <c>2s</c> or <c>1m</c>.</summary>
    public string? Time { get; init; }

    /// <summary>Duration for video, audio, or spritesheet outputs.</summary>
    public string? Duration { get; init; }

    /// <summary>Number of frames to include in a spritesheet output.</summary>
    public int? ImageCount { get; init; }

    /// <summary>Output format for frame or audio outputs.</summary>
    public string? Format { get; init; }

    /// <summary>Controls whether audio is included for video outputs.</summary>
    public bool? Audio { get; init; }
}

/// <summary>A deferred Media Transformations pipeline.</summary>
public sealed class MediaPipeline
{
    private readonly IMediaBinding _binding;
    private readonly Body _input;
    private bool _hasTransform;
    private object? _transformOptions;

    internal MediaPipeline(IMediaBinding binding, Body input)
    {
        _binding = binding ?? throw new ArgumentNullException(nameof(binding));
        _input = input ?? throw new ArgumentNullException(nameof(input));
    }

    /// <summary>Adds an optional resize or crop transform and returns this pipeline.</summary>
    public MediaPipeline Transform(object? options = null)
    {
        _hasTransform = true;
        _transformOptions = options;
        return this;
    }

    /// <summary>Sets the output options and returns result helpers.</summary>
    public MediaOutput Output(MediaOutputOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Mode);
        return new MediaOutput(_binding, _input, _hasTransform, _transformOptions, options);
    }
}

/// <summary>Result helpers for a Media Transformations output.</summary>
public sealed class MediaOutput
{
    private readonly IMediaBinding _binding;
    private readonly Body _input;
    private readonly bool _hasTransform;
    private readonly object? _transformOptions;
    private readonly MediaOutputOptions _output;

    internal MediaOutput(
        IMediaBinding binding,
        Body input,
        bool hasTransform,
        object? transformOptions,
        MediaOutputOptions output)
    {
        _binding = binding ?? throw new ArgumentNullException(nameof(binding));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _hasTransform = hasTransform;
        _transformOptions = transformOptions;
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    /// <summary>Returns the transformed media as a response.</summary>
    public Task<Response> ResponseAsync(CancellationToken cancellationToken = default) =>
        _binding.RunResponseAsync(_input, _hasTransform, _transformOptions, _output, cancellationToken);

    /// <summary>Returns the transformed media bytes and content type.</summary>
    public Task<Body> MediaAsync(CancellationToken cancellationToken = default) =>
        _binding.RunMediaAsync(_input, _hasTransform, _transformOptions, _output, cancellationToken);

    /// <summary>Returns the transformed media content type.</summary>
    public Task<string> ContentTypeAsync(CancellationToken cancellationToken = default) =>
        _binding.RunContentTypeAsync(_input, _hasTransform, _transformOptions, _output, cancellationToken);
}

internal sealed class MediaBinding : IMediaBinding
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public MediaBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public MediaPipeline Input(Body media)
    {
        ArgumentNullException.ThrowIfNull(media);
        return new MediaPipeline(this, media);
    }

    public async Task<Response> RunResponseAsync(
        Body media,
        bool hasTransform,
        object? transformOptions,
        MediaOutputOptions output,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "media.response",
            PipelinePayload(media, hasTransform, transformOptions, output),
            cancellationToken);

        return JsonSerializer.Deserialize<Workers.Interop.ResponseEnvelope>(result, JsonOptions)?.ToResponse(_invocationId, _dispatcher)
            ?? throw new WorkersException("Media Transformations returned an empty response envelope.");
    }

    public async Task<Body> RunMediaAsync(
        Body media,
        bool hasTransform,
        object? transformOptions,
        MediaOutputOptions output,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "media.media",
            PipelinePayload(media, hasTransform, transformOptions, output),
            cancellationToken);
        var payload = JsonSerializer.Deserialize<MediaBodyResult>(result, JsonOptions)
            ?? throw new WorkersException("Media Transformations returned an empty media result.");

        return payload.BodyBase64 is null
            ? Body.Empty
            : Body.FromBytes(Convert.FromBase64String(payload.BodyBase64), payload.ContentType ?? "application/octet-stream");
    }

    public async Task<string> RunContentTypeAsync(
        Body media,
        bool hasTransform,
        object? transformOptions,
        MediaOutputOptions output,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "media.contentType",
            PipelinePayload(media, hasTransform, transformOptions, output),
            cancellationToken);
        var payload = JsonSerializer.Deserialize<MediaContentTypeResult>(result, JsonOptions)
            ?? throw new WorkersException("Media Transformations returned an empty content type result.");

        return string.IsNullOrWhiteSpace(payload.ContentType)
            ? throw new WorkersException("Media Transformations returned an empty content type.")
            : payload.ContentType;
    }

    private Task<string> DispatchAsync(string operation, object payload, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            operation,
            JsonSerializer.Serialize(payload, JsonOptions));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private static MediaPipelinePayload PipelinePayload(
        Body media,
        bool hasTransform,
        object? transformOptions,
        MediaOutputOptions output)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrWhiteSpace(output.Mode);

        return new MediaPipelinePayload(MediaBodyPayload.From(media), hasTransform, transformOptions, output);
    }
}

internal sealed record MediaBodyPayload(string BodyBase64, string? ContentType)
{
    public static MediaBodyPayload From(Body body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return new MediaBodyPayload(Convert.ToBase64String(body.InternalBytes.Span), body.ContentType);
    }
}

internal sealed record MediaPipelinePayload(
    MediaBodyPayload Media,
    bool HasTransform,
    object? TransformOptions,
    MediaOutputOptions Output);

internal sealed class MediaBodyResult
{
    public string? BodyBase64 { get; init; }

    public string? ContentType { get; init; }
}

internal sealed class MediaContentTypeResult
{
    public string ContentType { get; init; } = "";
}
