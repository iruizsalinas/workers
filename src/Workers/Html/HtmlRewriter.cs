using System.Text.Json;
using System.Text.Json.Serialization;
using Workers.Interop;

namespace Workers;

/// <summary>Transforms HTML responses using the native Cloudflare Workers HTMLRewriter API.</summary>
public sealed class HtmlRewriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly List<HtmlSelectorHandler> _selectorHandlers = [];
    private readonly string _invocationId;
    private readonly IBindingDispatcher _dispatcher;
    private HtmlDocumentHandler? _documentHandler;

    internal HtmlRewriter(string invocationId, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        _invocationId = invocationId;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>Adds an element handler for a CSS selector.</summary>
    public HtmlRewriter On(string selector, HtmlElementHandler handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        ArgumentNullException.ThrowIfNull(handler);
        _selectorHandlers.Add(new HtmlSelectorHandler(selector, handler));
        return this;
    }

    /// <summary>Adds a document handler.</summary>
    public HtmlRewriter OnDocument(HtmlDocumentHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _documentHandler = handler;
        return this;
    }

    /// <summary>Transforms a response using the configured native HTMLRewriter handlers.</summary>
    public async Task<Response> TransformAsync(Response response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (_selectorHandlers.Count == 0 && _documentHandler is null)
            return response;

        var registration = HtmlRewriterRegistry.Register(
            _invocationId,
            _dispatcher,
            _selectorHandlers,
            _documentHandler);

        try
        {
            var payload = new HtmlRewriterTransformRequest(
                ResponseEnvelope.FromResponse(response),
                registration.Id,
                registration.Selectors,
                registration.DocumentHandlerId);

            var invocation = new BindingInvocation(
                _invocationId,
                "$htmlRewriter",
                "htmlRewriter.transform",
                JsonSerializer.Serialize(payload, HtmlRewriterJsonContext.Default.HtmlRewriterTransformRequest));

            var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
            var envelope = JsonSerializer.Deserialize(result, HtmlRewriterJsonContext.Default.ResponseEnvelope)
                ?? throw new WorkersException("HTMLRewriter transform returned an empty response.");

            return envelope.ToResponse(_invocationId, _dispatcher);
        }
        catch
        {
            HtmlRewriterRegistry.Release(registration.Id);
            throw;
        }
    }
}

internal sealed record HtmlSelectorHandler(string Selector, HtmlElementHandler Handler);

internal sealed record HtmlRewriterTransformRequest(
    ResponseEnvelope Response,
    string RegistryId,
    IReadOnlyList<HtmlRewriterSelectorRegistration> Selectors,
    string? DocumentHandlerId);

internal sealed record HtmlRewriterSelectorRegistration(string Selector, string HandlerId);

[JsonSerializable(typeof(HtmlRewriterTransformRequest))]
[JsonSerializable(typeof(ResponseEnvelope))]
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
internal sealed partial class HtmlRewriterJsonContext : JsonSerializerContext;
