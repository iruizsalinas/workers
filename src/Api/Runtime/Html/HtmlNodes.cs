namespace Workers;

public sealed class HtmlElement
{
    public string TagName { get => WorkerApi.NotExecutable<string>(); set => WorkerApi.NotExecutable(); }
    public string? NamespaceUri => WorkerApi.NotExecutable<string?>();
    public bool Removed => WorkerApi.NotExecutable<bool>();
    public IReadOnlyDictionary<string, string> Attributes => WorkerApi.NotExecutable<IReadOnlyDictionary<string, string>>();

    public string? GetAttribute(string name) => WorkerApi.NotExecutable<string?>();
    public bool HasAttribute(string name) => WorkerApi.NotExecutable<bool>();
    public HtmlElement SetAttribute(string name, string value) => WorkerApi.NotExecutable<HtmlElement>();
    public HtmlElement RemoveAttribute(string name) => WorkerApi.NotExecutable<HtmlElement>();
    public HtmlElement Before(string content, HtmlContentOptions? options = null) => WorkerApi.NotExecutable<HtmlElement>();
    public HtmlElement After(string content, HtmlContentOptions? options = null) => WorkerApi.NotExecutable<HtmlElement>();
    public HtmlElement Prepend(string content, HtmlContentOptions? options = null) => WorkerApi.NotExecutable<HtmlElement>();
    public HtmlElement Append(string content, HtmlContentOptions? options = null) => WorkerApi.NotExecutable<HtmlElement>();
    public HtmlElement Replace(string content, HtmlContentOptions? options = null) => WorkerApi.NotExecutable<HtmlElement>();
    public HtmlElement SetInnerContent(string content, HtmlContentOptions? options = null) => WorkerApi.NotExecutable<HtmlElement>();
    public HtmlElement Remove() => WorkerApi.NotExecutable<HtmlElement>();
    public HtmlElement RemoveAndKeepContent() => WorkerApi.NotExecutable<HtmlElement>();
    public void OnEndTag(Action<HtmlEndTag> handler) => WorkerApi.NotExecutable();
    public void OnEndTag(Func<HtmlEndTag, ValueTask> handler) => WorkerApi.NotExecutable();
}

public sealed class HtmlEndTag
{
    public string Name { get => WorkerApi.NotExecutable<string>(); set => WorkerApi.NotExecutable(); }

    public HtmlEndTag Before(string content, HtmlContentOptions? options = null) => WorkerApi.NotExecutable<HtmlEndTag>();
    public HtmlEndTag After(string content, HtmlContentOptions? options = null) => WorkerApi.NotExecutable<HtmlEndTag>();
    public HtmlEndTag Remove() => WorkerApi.NotExecutable<HtmlEndTag>();
}

public sealed class HtmlTextChunk
{
    public string Text => WorkerApi.NotExecutable<string>();
    public bool LastInTextNode => WorkerApi.NotExecutable<bool>();
    public bool Removed => WorkerApi.NotExecutable<bool>();

    public HtmlTextChunk Before(string content, HtmlContentOptions? options = null) => WorkerApi.NotExecutable<HtmlTextChunk>();
    public HtmlTextChunk After(string content, HtmlContentOptions? options = null) => WorkerApi.NotExecutable<HtmlTextChunk>();
    public HtmlTextChunk Replace(string content, HtmlContentOptions? options = null) => WorkerApi.NotExecutable<HtmlTextChunk>();
    public HtmlTextChunk Remove() => WorkerApi.NotExecutable<HtmlTextChunk>();
}

public sealed class HtmlComment
{
    public string Text { get => WorkerApi.NotExecutable<string>(); set => WorkerApi.NotExecutable(); }
    public bool Removed => WorkerApi.NotExecutable<bool>();

    public HtmlComment Before(string content, HtmlContentOptions? options = null) => WorkerApi.NotExecutable<HtmlComment>();
    public HtmlComment After(string content, HtmlContentOptions? options = null) => WorkerApi.NotExecutable<HtmlComment>();
    public HtmlComment Replace(string content, HtmlContentOptions? options = null) => WorkerApi.NotExecutable<HtmlComment>();
    public HtmlComment Remove() => WorkerApi.NotExecutable<HtmlComment>();
}

public sealed class HtmlDoctype
{
    public string? Name => WorkerApi.NotExecutable<string?>();
    public string? PublicId => WorkerApi.NotExecutable<string?>();
    public string? SystemId => WorkerApi.NotExecutable<string?>();
}

public sealed class HtmlDocumentEnd
{
    public HtmlDocumentEnd Append(string content, HtmlContentOptions? options = null) => WorkerApi.NotExecutable<HtmlDocumentEnd>();
}
