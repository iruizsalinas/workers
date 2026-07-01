using System.Text.Json;

namespace Workers;

internal sealed class FetchBinding
{
    private static readonly FetchBindingJsonContext JsonContext = new(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private readonly string _invocationId;
    private readonly IBindingDispatcher _dispatcher;

    public FetchBinding(string invocationId, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        _invocationId = invocationId;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public Task<Response> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        return FetchAsync(url, options: null, cancellationToken);
    }

    public Task<Response> FetchAsync(
        string url,
        FetchOptions? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        return FetchAsync(Request.Get(url), options, cancellationToken);
    }

    public async Task<Response> FetchAsync(Request request, CancellationToken cancellationToken = default)
    {
        return await FetchAsync(request, options: null, cancellationToken);
    }

    public async Task<Response> FetchAsync(
        Request request,
        FetchOptions? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invocation = new BindingInvocation(
            _invocationId,
            "$fetch",
            "fetch.global",
            JsonSerializer.Serialize(FetchBindingRequest.From(request, options), JsonContext.FetchBindingRequest));

        var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
        return JsonSerializer.Deserialize(result, JsonContext.ResponseEnvelope)?.ToResponse(_invocationId, _dispatcher)
            ?? throw new WorkersException("Fetch returned an empty response envelope.");
    }
}
