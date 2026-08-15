namespace Workers;

public sealed class HtmlContentOptions
{
    public static HtmlContentOptions Text => WorkerApi.NotExecutable<HtmlContentOptions>();
    public static HtmlContentOptions Html => WorkerApi.NotExecutable<HtmlContentOptions>();
}

public sealed class HtmlRewriter
{
    public HtmlRewriter() { }

    public HtmlRewriter On(string selector, HtmlElementHandler handler) => WorkerApi.NotExecutable<HtmlRewriter>();
    public HtmlRewriter OnDocument(HtmlDocumentHandler handler) => WorkerApi.NotExecutable<HtmlRewriter>();
    public Response Transform(Response response) => WorkerApi.NotExecutable<Response>();
}
