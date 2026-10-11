namespace Workers;

/// <summary>Handles elements matching an <see cref="HtmlRewriter.On"/> selector, and the text and comments inside them.</summary>
public abstract class HtmlElementHandler
{
    public virtual Task ElementAsync(HtmlElement element) => Task.CompletedTask;
    public virtual Task TextAsync(HtmlText text) => Task.CompletedTask;
    public virtual Task CommentsAsync(HtmlComment comment) => Task.CompletedTask;
}

/// <summary>Handles document-level content for <see cref="HtmlRewriter.OnDocument"/>.</summary>
public abstract class HtmlDocumentHandler
{
    public virtual Task DoctypeAsync(HtmlDoctype doctype) => Task.CompletedTask;
    public virtual Task TextAsync(HtmlText text) => Task.CompletedTask;
    public virtual Task CommentsAsync(HtmlComment comment) => Task.CompletedTask;
    public virtual Task EndAsync(HtmlDocumentEnd end) => Task.CompletedTask;
}
