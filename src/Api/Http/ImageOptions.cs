namespace Workers;

public enum FetchImageCompression
{
    Fast,
    Slow
}

public enum FetchImageFit
{
    ScaleDown,
    Contain,
    Cover,
    Crop,
    Pad
}

public enum FetchImageFlip
{
    Horizontal,
    Vertical,
    Both
}

public enum FetchImageFormat
{
    Auto,
    Avif,
    Webp,
    Json,
    Jpeg,
    Png
}

public enum FetchImageGravitySide
{
    Auto,
    Center,
    Top,
    Bottom,
    Left,
    Right
}

public enum FetchImageMetadata
{
    Keep,
    Copyright,
    None
}

public enum FetchImageOriginAuth
{
    SharePublicly,
    KeepPrivate
}

public enum FetchImageOnError
{
    Origin
}

public enum FetchImageQualityLevel
{
    Low,
    MediumLow,
    MediumHigh,
    High
}

public sealed class FetchImageQuality;

public sealed class FetchImageGravity;

public sealed class FetchImageDrawRepeat;

public sealed class FetchImageBorder
{
    public int? Width { get; init; }
    public string? Color { get; init; }
}

public sealed class FetchImageDraw
{
    public string? Url { get; init; }
    public int? Opacity { get; init; }
}

public sealed class FetchImageTrim
{
    public int? Top { get; init; }
    public int? Right { get; init; }
    public int? Bottom { get; init; }
    public int? Left { get; init; }
}

public sealed class FetchImageOptions
{
    public int? Width { get; init; }
    public int? Height { get; init; }
    public FetchImageFit? Fit { get; init; }
    public FetchImageFormat? Format { get; init; }
    public FetchImageQuality? Quality { get; init; }
}
