namespace Workers;

public abstract class HtmlElementHandler
{
    public virtual ValueTask ElementAsync(HtmlElement element) => ValueTask.CompletedTask;
    public virtual ValueTask TextAsync(HtmlTextChunk text) => ValueTask.CompletedTask;
    public virtual ValueTask CommentsAsync(HtmlComment comment) => ValueTask.CompletedTask;
}

public abstract class HtmlDocumentHandler
{
    public virtual ValueTask DoctypeAsync(HtmlDoctype doctype) => ValueTask.CompletedTask;
    public virtual ValueTask TextAsync(HtmlTextChunk text) => ValueTask.CompletedTask;
    public virtual ValueTask CommentsAsync(HtmlComment comment) => ValueTask.CompletedTask;
    public virtual ValueTask EndAsync(HtmlDocumentEnd end) => ValueTask.CompletedTask;
}
