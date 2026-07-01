namespace Workers;

/// <summary>Controls how HTMLRewriter inserts supplied content.</summary>
public sealed class HtmlContentOptions
{
    private HtmlContentOptions(bool html)
    {
        IsHtml = html;
    }

    /// <summary>Inserts content as escaped text.</summary>
    public static HtmlContentOptions Text { get; } = new(html: false);

    /// <summary>Inserts content as trusted raw HTML.</summary>
    public static HtmlContentOptions Html { get; } = new(html: true);

    internal bool IsHtml { get; }
}
