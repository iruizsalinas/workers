namespace Workers;

/// <summary>Handles elements matched by an HTMLRewriter selector.</summary>
public abstract class HtmlElementHandler
{
    /// <summary>Called when a matched element start tag is found.</summary>
    public virtual ValueTask ElementAsync(HtmlElement element) => ValueTask.CompletedTask;

    /// <summary>Called for text chunks inside a matched element.</summary>
    public virtual ValueTask TextAsync(HtmlTextChunk text) => ValueTask.CompletedTask;

    /// <summary>Called for comments inside a matched element.</summary>
    public virtual ValueTask CommentsAsync(HtmlComment comment) => ValueTask.CompletedTask;
}

/// <summary>Handles document-level HTMLRewriter callbacks.</summary>
public abstract class HtmlDocumentHandler
{
    /// <summary>Called when the document doctype is found.</summary>
    public virtual ValueTask DoctypeAsync(HtmlDoctype doctype) => ValueTask.CompletedTask;

    /// <summary>Called for document-level text chunks.</summary>
    public virtual ValueTask TextAsync(HtmlTextChunk text) => ValueTask.CompletedTask;

    /// <summary>Called for document-level comments.</summary>
    public virtual ValueTask CommentsAsync(HtmlComment comment) => ValueTask.CompletedTask;

    /// <summary>Called at the end of the document.</summary>
    public virtual ValueTask EndAsync(HtmlDocumentEnd end) => ValueTask.CompletedTask;
}
