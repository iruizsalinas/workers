using System.Text.Json;
using System.Text.Json.Serialization;
using Workers.Interop;

namespace Workers;

/// <summary>A matched HTML element passed to an HTMLRewriter handler.</summary>
public sealed class HtmlElement
{
    private readonly List<HtmlRewriterAction> _actions = [];
    private readonly Dictionary<string, string> _attributes;
    private string _tagName;

    internal HtmlElement(HtmlElementSnapshot snapshot)
    {
        _tagName = snapshot.TagName;
        NamespaceUri = snapshot.NamespaceUri;
        Removed = snapshot.Removed;
        _attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attribute in snapshot.Attributes)
            _attributes[attribute.Name] = attribute.Value;
    }

    /// <summary>The element tag name. Assigning a value renames the element.</summary>
    public string TagName
    {
        get => _tagName;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _tagName = value;
            _actions.Add(HtmlRewriterAction.WithValue("setTagName", value));
        }
    }

    /// <summary>The namespace URI for this element, if any.</summary>
    public string? NamespaceUri { get; }

    /// <summary>True when the element has already been removed.</summary>
    public bool Removed { get; private set; }

    /// <summary>The element attributes visible when the callback ran, including later C# changes in this callback.</summary>
    public IReadOnlyDictionary<string, string> Attributes => _attributes;

    /// <summary>Gets an attribute value, or null when it is missing.</summary>
    public string? GetAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _attributes.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>Returns true when the attribute exists.</summary>
    public bool HasAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _attributes.ContainsKey(name);
    }

    /// <summary>Sets an attribute value.</summary>
    public HtmlElement SetAttribute(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        _attributes[name] = value;
        _actions.Add(HtmlRewriterAction.WithNameValue("setAttribute", name, value));
        return this;
    }

    /// <summary>Removes an attribute.</summary>
    public HtmlElement RemoveAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _attributes.Remove(name);
        _actions.Add(HtmlRewriterAction.WithName("removeAttribute", name));
        return this;
    }

    /// <summary>Inserts content before the element.</summary>
    public HtmlElement Before(string content, HtmlContentOptions? options = null) => AddContent("before", content, options);

    /// <summary>Inserts response body content before the element.</summary>
    public HtmlElement Before(Response content, HtmlContentOptions? options = null) => AddContent("before", content, options);

    /// <summary>Inserts stream content before the element.</summary>
    public HtmlElement Before(ReadableStream content, HtmlContentOptions? options = null) => AddContent("before", content, options);

    /// <summary>Inserts content after the element.</summary>
    public HtmlElement After(string content, HtmlContentOptions? options = null) => AddContent("after", content, options);

    /// <summary>Inserts response body content after the element.</summary>
    public HtmlElement After(Response content, HtmlContentOptions? options = null) => AddContent("after", content, options);

    /// <summary>Inserts stream content after the element.</summary>
    public HtmlElement After(ReadableStream content, HtmlContentOptions? options = null) => AddContent("after", content, options);

    /// <summary>Prepends content inside the element.</summary>
    public HtmlElement Prepend(string content, HtmlContentOptions? options = null) => AddContent("prepend", content, options);

    /// <summary>Prepends response body content inside the element.</summary>
    public HtmlElement Prepend(Response content, HtmlContentOptions? options = null) => AddContent("prepend", content, options);

    /// <summary>Prepends stream content inside the element.</summary>
    public HtmlElement Prepend(ReadableStream content, HtmlContentOptions? options = null) => AddContent("prepend", content, options);

    /// <summary>Appends content inside the element.</summary>
    public HtmlElement Append(string content, HtmlContentOptions? options = null) => AddContent("append", content, options);

    /// <summary>Appends response body content inside the element.</summary>
    public HtmlElement Append(Response content, HtmlContentOptions? options = null) => AddContent("append", content, options);

    /// <summary>Appends stream content inside the element.</summary>
    public HtmlElement Append(ReadableStream content, HtmlContentOptions? options = null) => AddContent("append", content, options);

    /// <summary>Replaces the element and its content.</summary>
    public HtmlElement Replace(string content, HtmlContentOptions? options = null) => AddContent("replace", content, options);

    /// <summary>Replaces the element and its content with response body content.</summary>
    public HtmlElement Replace(Response content, HtmlContentOptions? options = null) => AddContent("replace", content, options);

    /// <summary>Replaces the element and its content with stream content.</summary>
    public HtmlElement Replace(ReadableStream content, HtmlContentOptions? options = null) => AddContent("replace", content, options);

    /// <summary>Replaces the element's inner content.</summary>
    public HtmlElement SetInnerContent(string content, HtmlContentOptions? options = null) => AddContent("setInnerContent", content, options);

    /// <summary>Replaces the element's inner content with response body content.</summary>
    public HtmlElement SetInnerContent(Response content, HtmlContentOptions? options = null) => AddContent("setInnerContent", content, options);

    /// <summary>Replaces the element's inner content with stream content.</summary>
    public HtmlElement SetInnerContent(ReadableStream content, HtmlContentOptions? options = null) => AddContent("setInnerContent", content, options);

    /// <summary>Removes the element and its content.</summary>
    public HtmlElement Remove()
    {
        Removed = true;
        _actions.Add(HtmlRewriterAction.Simple("remove"));
        return this;
    }

    /// <summary>Removes the element tag while keeping its content.</summary>
    public HtmlElement RemoveAndKeepContent()
    {
        Removed = true;
        _actions.Add(HtmlRewriterAction.Simple("removeAndKeepContent"));
        return this;
    }

    /// <summary>Registers a callback for this element's end tag.</summary>
    public void OnEndTag(Action<HtmlEndTag> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var id = HtmlRewriterRegistry.RegisterEndTag(tag =>
        {
            handler(tag);
            return ValueTask.CompletedTask;
        });
        _actions.Add(HtmlRewriterAction.Handler("onEndTag", id));
    }

    /// <summary>Registers an async callback for this element's end tag.</summary>
    public void OnEndTag(Func<HtmlEndTag, ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var id = HtmlRewriterRegistry.RegisterEndTag(handler);
        _actions.Add(HtmlRewriterAction.Handler("onEndTag", id));
    }

    internal IReadOnlyList<HtmlRewriterAction> Actions => _actions;

    private HtmlElement AddContent(string type, string content, HtmlContentOptions? options)
    {
        ArgumentNullException.ThrowIfNull(content);
        _actions.Add(HtmlRewriterAction.WithContent(type, content, options?.IsHtml ?? false));
        return this;
    }

    private HtmlElement AddContent(string type, Response content, HtmlContentOptions? options)
    {
        ArgumentNullException.ThrowIfNull(content);
        _actions.Add(HtmlRewriterAction.WithResponse(type, HtmlContent.ResponseEnvelope(content), options?.IsHtml ?? false, options is not null));
        return this;
    }

    private HtmlElement AddContent(string type, ReadableStream content, HtmlContentOptions? options)
    {
        ArgumentNullException.ThrowIfNull(content);
        _actions.Add(HtmlRewriterAction.WithStream(type, HtmlContent.StreamSource(content), content.Handle, options?.IsHtml ?? false, options is not null));
        return this;
    }
}

/// <summary>An element end tag passed to an HTMLRewriter end-tag handler.</summary>
public sealed class HtmlEndTag
{
    private readonly List<HtmlRewriterAction> _actions = [];
    private string _name;

    internal HtmlEndTag(HtmlEndTagSnapshot snapshot)
    {
        _name = snapshot.Name;
    }

    /// <summary>The end tag name. Assigning a value renames the tag.</summary>
    public string Name
    {
        get => _name;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _name = value;
            _actions.Add(HtmlRewriterAction.WithValue("setName", value));
        }
    }

    /// <summary>Inserts content before the end tag.</summary>
    public HtmlEndTag Before(string content, HtmlContentOptions? options = null) => AddContent("before", content, options);

    /// <summary>Inserts response body content before the end tag.</summary>
    public HtmlEndTag Before(Response content, HtmlContentOptions? options = null) => AddContent("before", content, options);

    /// <summary>Inserts stream content before the end tag.</summary>
    public HtmlEndTag Before(ReadableStream content, HtmlContentOptions? options = null) => AddContent("before", content, options);

    /// <summary>Inserts content after the end tag.</summary>
    public HtmlEndTag After(string content, HtmlContentOptions? options = null) => AddContent("after", content, options);

    /// <summary>Inserts response body content after the end tag.</summary>
    public HtmlEndTag After(Response content, HtmlContentOptions? options = null) => AddContent("after", content, options);

    /// <summary>Inserts stream content after the end tag.</summary>
    public HtmlEndTag After(ReadableStream content, HtmlContentOptions? options = null) => AddContent("after", content, options);

    /// <summary>Removes the end tag.</summary>
    public HtmlEndTag Remove()
    {
        _actions.Add(HtmlRewriterAction.Simple("remove"));
        return this;
    }

    internal IReadOnlyList<HtmlRewriterAction> Actions => _actions;

    private HtmlEndTag AddContent(string type, string content, HtmlContentOptions? options)
    {
        ArgumentNullException.ThrowIfNull(content);
        _actions.Add(HtmlRewriterAction.WithContent(type, content, options?.IsHtml ?? false));
        return this;
    }

    private HtmlEndTag AddContent(string type, Response content, HtmlContentOptions? options)
    {
        ArgumentNullException.ThrowIfNull(content);
        _actions.Add(HtmlRewriterAction.WithResponse(type, HtmlContent.ResponseEnvelope(content), options?.IsHtml ?? false, options is not null));
        return this;
    }

    private HtmlEndTag AddContent(string type, ReadableStream content, HtmlContentOptions? options)
    {
        ArgumentNullException.ThrowIfNull(content);
        _actions.Add(HtmlRewriterAction.WithStream(type, HtmlContent.StreamSource(content), content.Handle, options?.IsHtml ?? false, options is not null));
        return this;
    }
}

/// <summary>A text chunk passed to an HTMLRewriter handler.</summary>
public sealed class HtmlTextChunk
{
    private readonly List<HtmlRewriterAction> _actions = [];

    internal HtmlTextChunk(HtmlTextSnapshot snapshot)
    {
        Text = snapshot.Text;
        LastInTextNode = snapshot.LastInTextNode;
        Removed = snapshot.Removed;
    }

    /// <summary>The text contained in this chunk.</summary>
    public string Text { get; }

    /// <summary>True when this is the last chunk for the current text node.</summary>
    public bool LastInTextNode { get; }

    /// <summary>True when this text chunk has already been removed.</summary>
    public bool Removed { get; private set; }

    /// <summary>Inserts content before the text chunk.</summary>
    public HtmlTextChunk Before(string content, HtmlContentOptions? options = null) => AddContent("before", content, options);

    /// <summary>Inserts response body content before the text chunk.</summary>
    public HtmlTextChunk Before(Response content, HtmlContentOptions? options = null) => AddContent("before", content, options);

    /// <summary>Inserts stream content before the text chunk.</summary>
    public HtmlTextChunk Before(ReadableStream content, HtmlContentOptions? options = null) => AddContent("before", content, options);

    /// <summary>Inserts content after the text chunk.</summary>
    public HtmlTextChunk After(string content, HtmlContentOptions? options = null) => AddContent("after", content, options);

    /// <summary>Inserts response body content after the text chunk.</summary>
    public HtmlTextChunk After(Response content, HtmlContentOptions? options = null) => AddContent("after", content, options);

    /// <summary>Inserts stream content after the text chunk.</summary>
    public HtmlTextChunk After(ReadableStream content, HtmlContentOptions? options = null) => AddContent("after", content, options);

    /// <summary>Replaces the text chunk.</summary>
    public HtmlTextChunk Replace(string content, HtmlContentOptions? options = null) => AddContent("replace", content, options);

    /// <summary>Replaces the text chunk with response body content.</summary>
    public HtmlTextChunk Replace(Response content, HtmlContentOptions? options = null) => AddContent("replace", content, options);

    /// <summary>Replaces the text chunk with stream content.</summary>
    public HtmlTextChunk Replace(ReadableStream content, HtmlContentOptions? options = null) => AddContent("replace", content, options);

    /// <summary>Removes the text chunk.</summary>
    public HtmlTextChunk Remove()
    {
        Removed = true;
        _actions.Add(HtmlRewriterAction.Simple("remove"));
        return this;
    }

    internal IReadOnlyList<HtmlRewriterAction> Actions => _actions;

    private HtmlTextChunk AddContent(string type, string content, HtmlContentOptions? options)
    {
        ArgumentNullException.ThrowIfNull(content);
        _actions.Add(HtmlRewriterAction.WithContent(type, content, options?.IsHtml ?? false));
        return this;
    }

    private HtmlTextChunk AddContent(string type, Response content, HtmlContentOptions? options)
    {
        ArgumentNullException.ThrowIfNull(content);
        _actions.Add(HtmlRewriterAction.WithResponse(type, HtmlContent.ResponseEnvelope(content), options?.IsHtml ?? false, options is not null));
        return this;
    }

    private HtmlTextChunk AddContent(string type, ReadableStream content, HtmlContentOptions? options)
    {
        ArgumentNullException.ThrowIfNull(content);
        _actions.Add(HtmlRewriterAction.WithStream(type, HtmlContent.StreamSource(content), content.Handle, options?.IsHtml ?? false, options is not null));
        return this;
    }
}

/// <summary>An HTML comment passed to an HTMLRewriter handler.</summary>
public sealed class HtmlComment
{
    private readonly List<HtmlRewriterAction> _actions = [];
    private string _text;

    internal HtmlComment(HtmlCommentSnapshot snapshot)
    {
        _text = snapshot.Text;
        Removed = snapshot.Removed;
    }

    /// <summary>The comment text. Assigning a value updates the comment.</summary>
    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _text = value;
            _actions.Add(HtmlRewriterAction.WithValue("setText", value));
        }
    }

    /// <summary>True when this comment has already been removed.</summary>
    public bool Removed { get; private set; }

    /// <summary>Inserts content before the comment.</summary>
    public HtmlComment Before(string content, HtmlContentOptions? options = null) => AddContent("before", content, options);

    /// <summary>Inserts response body content before the comment.</summary>
    public HtmlComment Before(Response content, HtmlContentOptions? options = null) => AddContent("before", content, options);

    /// <summary>Inserts stream content before the comment.</summary>
    public HtmlComment Before(ReadableStream content, HtmlContentOptions? options = null) => AddContent("before", content, options);

    /// <summary>Inserts content after the comment.</summary>
    public HtmlComment After(string content, HtmlContentOptions? options = null) => AddContent("after", content, options);

    /// <summary>Inserts response body content after the comment.</summary>
    public HtmlComment After(Response content, HtmlContentOptions? options = null) => AddContent("after", content, options);

    /// <summary>Inserts stream content after the comment.</summary>
    public HtmlComment After(ReadableStream content, HtmlContentOptions? options = null) => AddContent("after", content, options);

    /// <summary>Replaces the comment.</summary>
    public HtmlComment Replace(string content, HtmlContentOptions? options = null) => AddContent("replace", content, options);

    /// <summary>Replaces the comment with response body content.</summary>
    public HtmlComment Replace(Response content, HtmlContentOptions? options = null) => AddContent("replace", content, options);

    /// <summary>Replaces the comment with stream content.</summary>
    public HtmlComment Replace(ReadableStream content, HtmlContentOptions? options = null) => AddContent("replace", content, options);

    /// <summary>Removes the comment.</summary>
    public HtmlComment Remove()
    {
        Removed = true;
        _actions.Add(HtmlRewriterAction.Simple("remove"));
        return this;
    }

    internal IReadOnlyList<HtmlRewriterAction> Actions => _actions;

    private HtmlComment AddContent(string type, string content, HtmlContentOptions? options)
    {
        ArgumentNullException.ThrowIfNull(content);
        _actions.Add(HtmlRewriterAction.WithContent(type, content, options?.IsHtml ?? false));
        return this;
    }

    private HtmlComment AddContent(string type, Response content, HtmlContentOptions? options)
    {
        ArgumentNullException.ThrowIfNull(content);
        _actions.Add(HtmlRewriterAction.WithResponse(type, HtmlContent.ResponseEnvelope(content), options?.IsHtml ?? false, options is not null));
        return this;
    }

    private HtmlComment AddContent(string type, ReadableStream content, HtmlContentOptions? options)
    {
        ArgumentNullException.ThrowIfNull(content);
        _actions.Add(HtmlRewriterAction.WithStream(type, HtmlContent.StreamSource(content), content.Handle, options?.IsHtml ?? false, options is not null));
        return this;
    }
}

/// <summary>An HTML doctype passed to a document handler.</summary>
public sealed class HtmlDoctype
{
    internal HtmlDoctype(HtmlDoctypeSnapshot snapshot)
    {
        Name = snapshot.Name;
        PublicId = snapshot.PublicId;
        SystemId = snapshot.SystemId;
    }

    /// <summary>The doctype name.</summary>
    public string? Name { get; }

    /// <summary>The doctype public identifier.</summary>
    public string? PublicId { get; }

    /// <summary>The doctype system identifier.</summary>
    public string? SystemId { get; }
}

/// <summary>The document end callback object.</summary>
public sealed class HtmlDocumentEnd
{
    private readonly List<HtmlRewriterAction> _actions = [];

    /// <summary>Appends content to the end of the document.</summary>
    public HtmlDocumentEnd Append(string content, HtmlContentOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        _actions.Add(HtmlRewriterAction.WithContent("append", content, options?.IsHtml ?? false));
        return this;
    }

    /// <summary>Appends response body content to the end of the document.</summary>
    public HtmlDocumentEnd Append(Response content, HtmlContentOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        _actions.Add(HtmlRewriterAction.WithResponse("append", HtmlContent.ResponseEnvelope(content), options?.IsHtml ?? false, options is not null));
        return this;
    }

    /// <summary>Appends stream content to the end of the document.</summary>
    public HtmlDocumentEnd Append(ReadableStream content, HtmlContentOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        _actions.Add(HtmlRewriterAction.WithStream("append", HtmlContent.StreamSource(content), content.Handle, options?.IsHtml ?? false, options is not null));
        return this;
    }

    internal IReadOnlyList<HtmlRewriterAction> Actions => _actions;
}

internal sealed class HtmlAttributeSnapshot
{
    public required string Name { get; init; }
    public required string Value { get; init; }
}

internal sealed class HtmlElementSnapshot
{
    public required string TagName { get; init; }
    public string? NamespaceUri { get; init; }
    public bool Removed { get; init; }
    public IReadOnlyList<HtmlAttributeSnapshot> Attributes { get; init; } = [];
}

internal sealed class HtmlEndTagSnapshot
{
    public required string Name { get; init; }
}

internal sealed class HtmlTextSnapshot
{
    public required string Text { get; init; }
    public bool LastInTextNode { get; init; }
    public bool Removed { get; init; }
}

internal sealed class HtmlCommentSnapshot
{
    public required string Text { get; init; }
    public bool Removed { get; init; }
}

internal sealed class HtmlDoctypeSnapshot
{
    public string? Name { get; init; }
    public string? PublicId { get; init; }
    public string? SystemId { get; init; }
}

internal sealed record HtmlRewriterAction(
    string Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Name = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Value = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Content = null,
    bool Html = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool HasContentOptions = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? HandlerId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? StreamSource = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? StreamHandle = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ResponseEnvelope? Response = null)
{
    public static HtmlRewriterAction Simple(string type) => new(type);
    public static HtmlRewriterAction WithName(string type, string name) => new(type, Name: name);
    public static HtmlRewriterAction WithValue(string type, string value) => new(type, Value: value);
    public static HtmlRewriterAction WithNameValue(string type, string name, string value) => new(type, Name: name, Value: value);
    public static HtmlRewriterAction WithContent(string type, string content, bool html) => new(type, Content: content, Html: html, HasContentOptions: true);
    public static HtmlRewriterAction WithResponse(string type, ResponseEnvelope response, bool html, bool hasContentOptions) => new(type, Html: html, HasContentOptions: hasContentOptions, Response: response);
    public static HtmlRewriterAction WithStream(string type, string source, string handle, bool html, bool hasContentOptions) => new(type, Html: html, HasContentOptions: hasContentOptions, StreamSource: source, StreamHandle: handle);
    public static HtmlRewriterAction Handler(string type, string handlerId) => new(type, HandlerId: handlerId);
}

internal static class HtmlContent
{
    public static ResponseEnvelope ResponseEnvelope(Response response)
    {
        if (response.WebSocket is not null)
            throw new ArgumentException("WebSocket responses cannot be inserted as HTMLRewriter content.", nameof(response));

        return Workers.Interop.ResponseEnvelope.FromResponse(response);
    }

    public static string StreamSource(ReadableStream stream) =>
        stream.Source switch
        {
            NativeStreamSource.Request => "request",
            NativeStreamSource.Response => "response",
            NativeStreamSource.Managed => "managed",
            _ => throw new ArgumentOutOfRangeException(nameof(stream), stream.Source, "Unsupported native stream source.")
        };
}

[JsonSerializable(typeof(HtmlAttributeSnapshot))]
[JsonSerializable(typeof(HtmlElementSnapshot))]
[JsonSerializable(typeof(HtmlEndTagSnapshot))]
[JsonSerializable(typeof(HtmlTextSnapshot))]
[JsonSerializable(typeof(HtmlCommentSnapshot))]
[JsonSerializable(typeof(HtmlDoctypeSnapshot))]
[JsonSerializable(typeof(HtmlRewriterAction))]
[JsonSerializable(typeof(ResponseEnvelope))]
[JsonSerializable(typeof(IReadOnlyList<HtmlRewriterAction>))]
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
internal sealed partial class HtmlRewriterMutationJsonContext : JsonSerializerContext;
