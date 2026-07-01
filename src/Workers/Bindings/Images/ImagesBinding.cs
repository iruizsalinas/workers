using System.Text.Json;

namespace Workers;

/// <summary>Image metadata returned by the Cloudflare Images binding.</summary>
public sealed class ImagesInfo
{
    /// <summary>The decoded image format.</summary>
    public string Format { get; init; } = "";

    /// <summary>The input file size in bytes.</summary>
    public long FileSize { get; init; }

    /// <summary>The image width in pixels.</summary>
    public int Width { get; init; }

    /// <summary>The image height in pixels.</summary>
    public int Height { get; init; }
}

/// <summary>Options passed to Images output.</summary>
public sealed class ImagesOutputOptions
{
    /// <summary>The output MIME type, such as <c>image/webp</c> or <c>image/avif</c>.</summary>
    public required string Format { get; init; }

    /// <summary>Optional output quality for formats that support it.</summary>
    public object? Quality { get; init; }

    /// <summary>Controls whether animation frames are preserved.</summary>
    public bool? Anim { get; init; }
}

/// <summary>A deferred Cloudflare Images transform pipeline.</summary>
public sealed class ImagesPipeline
{
    private readonly IImagesBinding _binding;
    private readonly Body _input;
    private readonly List<ImagesOperation> _operations = [];

    internal ImagesPipeline(IImagesBinding binding, Body input)
    {
        _binding = binding ?? throw new ArgumentNullException(nameof(binding));
        _input = input ?? throw new ArgumentNullException(nameof(input));
    }

    /// <summary>Adds an Images transform operation and returns this pipeline.</summary>
    public ImagesPipeline Transform(object options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _operations.Add(ImagesOperation.Transform(options));
        return this;
    }

    /// <summary>Draws an overlay image over the current image and returns this pipeline.</summary>
    public ImagesPipeline Draw(Body image, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        _operations.Add(ImagesOperation.Draw(image, options));
        return this;
    }

    /// <summary>Produces an optimized image response.</summary>
    public Task<Response> OutputAsync(ImagesOutputOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Format);
        return _binding.RunPipelineAsync(_input, _operations, options, cancellationToken);
    }
}

internal sealed class ImagesBinding : IImagesBinding
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public ImagesBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public ImagesPipeline Input(Body image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new ImagesPipeline(this, image);
    }

    public async Task<ImagesInfo> InfoAsync(Body image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        var result = await DispatchAsync("images.info", ImagesBodyPayload.From(image), cancellationToken);
        return JsonSerializer.Deserialize<ImagesInfo>(result, JsonOptions)
            ?? throw new WorkersException("Images info returned an empty result.");
    }

    public async Task<Response> RunPipelineAsync(
        Body image,
        IReadOnlyList<ImagesOperation> operations,
        ImagesOutputOptions output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrWhiteSpace(output.Format);

        var payload = new ImagesPipelinePayload(
            ImagesBodyPayload.From(image),
            operations.Select(static operation => operation.ToPayload()).ToArray(),
            output);
        var result = await DispatchAsync("images.pipeline", payload, cancellationToken);
        return JsonSerializer.Deserialize<Workers.Interop.ResponseEnvelope>(result, JsonOptions)?.ToResponse(_invocationId, _dispatcher)
            ?? throw new WorkersException("Images pipeline returned an empty response envelope.");
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
}

/// <summary>One operation in an Images transform pipeline.</summary>
public sealed class ImagesOperation
{
    private ImagesOperation(string kind, object? options, Body? image)
    {
        Kind = kind;
        Options = options;
        Image = image;
    }

    /// <summary>The operation kind.</summary>
    public string Kind { get; }

    /// <summary>JSON-compatible operation options.</summary>
    public object? Options { get; }

    /// <summary>The overlay image for draw operations.</summary>
    public Body? Image { get; }

    /// <summary>Creates a transform operation.</summary>
    public static ImagesOperation Transform(object options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new ImagesOperation("transform", options, image: null);
    }

    /// <summary>Creates a draw operation.</summary>
    public static ImagesOperation Draw(Body image, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new ImagesOperation("draw", options, image);
    }

    internal ImagesOperationPayload ToPayload() =>
        new(Kind, Options, Image is null ? null : ImagesBodyPayload.From(Image));
}

internal sealed record ImagesBodyPayload(string BodyBase64, string? ContentType)
{
    public static ImagesBodyPayload From(Body body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return new ImagesBodyPayload(Convert.ToBase64String(body.InternalBytes.Span), body.ContentType);
    }
}

internal sealed record ImagesOperationPayload(string Kind, object? Options, ImagesBodyPayload? Image);

internal sealed record ImagesPipelinePayload(
    ImagesBodyPayload Image,
    IReadOnlyList<ImagesOperationPayload> Operations,
    ImagesOutputOptions Output);
