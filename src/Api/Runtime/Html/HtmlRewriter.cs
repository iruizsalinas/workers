namespace Workers;

/// <summary>Streams an HTML response through element and document handlers, as <c>HTMLRewriter</c>.</summary>
public sealed class HtmlRewriter
{
    public HtmlRewriter() { }

    public HtmlRewriter On(string selector, HtmlElementHandler handlers) => WorkerApi.NotExecutable<HtmlRewriter>();
    public HtmlRewriter OnDocument(HtmlDocumentHandler handlers) => WorkerApi.NotExecutable<HtmlRewriter>();
    public Response Transform(Response response) => WorkerApi.NotExecutable<Response>();
}

/// <summary>How inserted content is treated; by default it is text and escaped.</summary>
public sealed class HtmlContentOptions
{
    /// <summary>Inserts the content as raw HTML instead of escaped text.</summary>
    public bool Html { get; init; }
}
