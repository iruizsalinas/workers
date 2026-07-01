using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workers;

internal static class HtmlRewriterRegistry
{
    private static readonly ConcurrentDictionary<string, RegistryEntry> Registries = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Func<HtmlEndTag, ValueTask>> EndTagHandlers = new(StringComparer.Ordinal);
    private static readonly AsyncLocal<string?> CurrentRegistryId = new();
    private static long nextRegistryId;
    private static long nextHandlerId;
    private static long nextEndTagHandlerId;

    public static HtmlRewriterRegistration Register(
        string invocationId,
        IBindingDispatcher dispatcher,
        IReadOnlyList<HtmlSelectorHandler> selectorHandlers,
        HtmlDocumentHandler? documentHandler)
    {
        var registryId = "html:" + Interlocked.Increment(ref nextRegistryId).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var handlers = new Dictionary<string, HtmlElementHandler>(StringComparer.Ordinal);
        var selectors = new List<HtmlRewriterSelectorRegistration>(selectorHandlers.Count);

        foreach (var selectorHandler in selectorHandlers)
        {
            var handlerId = "handler:" + Interlocked.Increment(ref nextHandlerId).ToString(System.Globalization.CultureInfo.InvariantCulture);
            handlers.Add(handlerId, selectorHandler.Handler);
            selectors.Add(new HtmlRewriterSelectorRegistration(selectorHandler.Selector, handlerId));
        }

        string? documentHandlerId = null;
        if (documentHandler is not null)
            documentHandlerId = "document:" + Interlocked.Increment(ref nextHandlerId).ToString(System.Globalization.CultureInfo.InvariantCulture);

        Registries[registryId] = new RegistryEntry(invocationId, dispatcher, handlers, documentHandlerId, documentHandler);
        return new HtmlRewriterRegistration(registryId, selectors, documentHandlerId);
    }

    public static string RegisterEndTag(Func<HtmlEndTag, ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var registryId = CurrentRegistryId.Value
            ?? throw new WorkersException("HTMLRewriter end tag handlers can only be registered during an element callback.");
        var id = registryId + ":endtag:" + Interlocked.Increment(ref nextEndTagHandlerId).ToString(System.Globalization.CultureInfo.InvariantCulture);
        EndTagHandlers[id] = handler;
        return id;
    }

    public static void Release(string registryId)
    {
        if (Registries.TryRemove(registryId, out _))
        {
            foreach (var pair in EndTagHandlers)
            {
                if (pair.Key.StartsWith(registryId + ":", StringComparison.Ordinal))
                    EndTagHandlers.TryRemove(pair.Key, out _);
            }
        }
    }

    public static async Task<string> InvokeCallbackAsync(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize(payloadJson, HtmlRewriterCallbackJsonContext.Default.HtmlRewriterCallbackPayload)
            ?? throw new WorkersException("HTMLRewriter callback payload was empty.");

        if (!Registries.TryGetValue(payload.RegistryId, out var entry))
            throw new WorkersException($"HTMLRewriter registry '{payload.RegistryId}' is not active.");

        using var _ = BindingDispatcher.Use(entry.Dispatcher);
        var previousRegistryId = CurrentRegistryId.Value;
        CurrentRegistryId.Value = payload.RegistryId;
        try
        {
            var actions = await InvokeCallbackCoreAsync(entry, payload);
            return JsonSerializer.Serialize(actions, HtmlRewriterMutationJsonContext.Default.IReadOnlyListHtmlRewriterAction);
        }
        finally
        {
            CurrentRegistryId.Value = previousRegistryId;
        }
    }

    private static async Task<IReadOnlyList<HtmlRewriterAction>> InvokeCallbackCoreAsync(
        RegistryEntry entry,
        HtmlRewriterCallbackPayload payload)
    {
        switch (payload.Kind)
        {
            case "element":
                {
                    var handler = ElementHandler(entry, payload.HandlerId);
                    var snapshot = payload.Snapshot.Deserialize(HtmlRewriterMutationJsonContext.Default.HtmlElementSnapshot)
                        ?? throw new WorkersException("HTMLRewriter element callback payload was empty.");
                    var element = new HtmlElement(snapshot);
                    await handler.ElementAsync(element);
                    return element.Actions;
                }
            case "text":
                {
                    var snapshot = payload.Snapshot.Deserialize(HtmlRewriterMutationJsonContext.Default.HtmlTextSnapshot)
                        ?? throw new WorkersException("HTMLRewriter text callback payload was empty.");
                    var text = new HtmlTextChunk(snapshot);
                    if (payload.HandlerId == entry.DocumentHandlerId && entry.DocumentHandler is not null)
                        await entry.DocumentHandler.TextAsync(text);
                    else
                        await ElementHandler(entry, payload.HandlerId).TextAsync(text);
                    return text.Actions;
                }
            case "comments":
                {
                    var snapshot = payload.Snapshot.Deserialize(HtmlRewriterMutationJsonContext.Default.HtmlCommentSnapshot)
                        ?? throw new WorkersException("HTMLRewriter comment callback payload was empty.");
                    var comment = new HtmlComment(snapshot);
                    if (payload.HandlerId == entry.DocumentHandlerId && entry.DocumentHandler is not null)
                        await entry.DocumentHandler.CommentsAsync(comment);
                    else
                        await ElementHandler(entry, payload.HandlerId).CommentsAsync(comment);
                    return comment.Actions;
                }
            case "doctype":
                {
                    var documentHandler = DocumentHandler(entry, payload.HandlerId);
                    var snapshot = payload.Snapshot.Deserialize(HtmlRewriterMutationJsonContext.Default.HtmlDoctypeSnapshot)
                        ?? throw new WorkersException("HTMLRewriter doctype callback payload was empty.");
                    await documentHandler.DoctypeAsync(new HtmlDoctype(snapshot));
                    return [];
                }
            case "end":
                {
                    var documentHandler = DocumentHandler(entry, payload.HandlerId);
                    var end = new HtmlDocumentEnd();
                    await documentHandler.EndAsync(end);
                    return end.Actions;
                }
            case "endTag":
                {
                    if (!EndTagHandlers.TryRemove(payload.HandlerId, out var handler))
                        throw new WorkersException($"HTMLRewriter end tag handler '{payload.HandlerId}' is not active.");

                    var snapshot = payload.Snapshot.Deserialize(HtmlRewriterMutationJsonContext.Default.HtmlEndTagSnapshot)
                        ?? throw new WorkersException("HTMLRewriter end tag callback payload was empty.");
                    var endTag = new HtmlEndTag(snapshot);
                    await handler(endTag);
                    return endTag.Actions;
                }
            default:
                throw new WorkersException($"Unsupported HTMLRewriter callback kind '{payload.Kind}'.");
        }
    }

    private static HtmlElementHandler ElementHandler(RegistryEntry entry, string handlerId)
    {
        if (entry.ElementHandlers.TryGetValue(handlerId, out var handler))
            return handler;

        throw new WorkersException($"HTMLRewriter element handler '{handlerId}' is not active.");
    }

    private static HtmlDocumentHandler DocumentHandler(RegistryEntry entry, string handlerId)
    {
        if (handlerId == entry.DocumentHandlerId && entry.DocumentHandler is not null)
            return entry.DocumentHandler;

        throw new WorkersException($"HTMLRewriter document handler '{handlerId}' is not active.");
    }

    private sealed record RegistryEntry(
        string InvocationId,
        IBindingDispatcher Dispatcher,
        IReadOnlyDictionary<string, HtmlElementHandler> ElementHandlers,
        string? DocumentHandlerId,
        HtmlDocumentHandler? DocumentHandler);
}

internal sealed record HtmlRewriterRegistration(
    string Id,
    IReadOnlyList<HtmlRewriterSelectorRegistration> Selectors,
    string? DocumentHandlerId);

internal sealed class HtmlRewriterCallbackPayload
{
    public required string RegistryId { get; init; }
    public required string HandlerId { get; init; }
    public required string Kind { get; init; }
    public required JsonElement Snapshot { get; init; }
}

[JsonSerializable(typeof(HtmlRewriterCallbackPayload))]
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
internal sealed partial class HtmlRewriterCallbackJsonContext : JsonSerializerContext;
