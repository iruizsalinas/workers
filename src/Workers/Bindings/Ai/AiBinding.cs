using System.Text.Json;

namespace Workers;

internal sealed class AiBinding : IAiBinding
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public AiBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<TOutput?> RunAsync<TInput, TOutput>(
        string model,
        TInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            "ai.run",
            JsonSerializer.Serialize(
                new AiRunRequest(model, JsonSerializer.SerializeToElement(input, JsonOptions)),
                JsonOptions));

        var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
        var envelope = JsonSerializer.Deserialize<AiRunResponse>(result, JsonOptions)
            ?? throw new WorkersException("Workers AI binding returned an empty result.");

        return envelope.Output.Deserialize<TOutput>(JsonOptions);
    }

    public async Task<Body> RunBytesAsync<TInput>(
        string model,
        TInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            "ai.runBytes",
            JsonSerializer.Serialize(
                new AiRunRequest(model, JsonSerializer.SerializeToElement(input, JsonOptions)),
                JsonOptions));

        var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
        var envelope = JsonSerializer.Deserialize<AiRunBytesResponse>(result, JsonOptions)
            ?? throw new WorkersException("Workers AI binding returned an empty result.");

        var bytes = envelope.BodyBase64 is null ? [] : Convert.FromBase64String(envelope.BodyBase64);
        return Body.FromBytes(bytes);
    }

    private sealed record AiRunRequest(string Model, JsonElement Input);

    private sealed class AiRunResponse
    {
        public JsonElement Output { get; init; }
    }

    private sealed class AiRunBytesResponse
    {
        public string? BodyBase64 { get; init; }
    }
}
