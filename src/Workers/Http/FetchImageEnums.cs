namespace Workers;

/// <summary>Cloudflare image compression modes.</summary>
public enum FetchImageCompression
{
    /// <summary>Prioritizes lower encoding latency over output size and quality.</summary>
    Fast
}

/// <summary>Cloudflare image fit modes.</summary>
public enum FetchImageFit
{
    /// <summary>Fit within bounds without upscaling.</summary>
    ScaleDown,

    /// <summary>Fit within bounds, allowing upscaling.</summary>
    Contain,

    /// <summary>Cover the requested bounds, cropping as needed.</summary>
    Cover,

    /// <summary>Crop to fill bounds without upscaling.</summary>
    Crop,

    /// <summary>Pad within the requested bounds.</summary>
    Pad,

    /// <summary>Stretch to exact dimensions.</summary>
    Squeeze
}

/// <summary>Cloudflare image flip modes.</summary>
public enum FetchImageFlip
{
    /// <summary>Flip horizontally.</summary>
    Horizontal,

    /// <summary>Flip vertically.</summary>
    Vertical,

    /// <summary>Flip horizontally and vertically.</summary>
    Both
}

/// <summary>Cloudflare image output formats.</summary>
public enum FetchImageFormat
{
    /// <summary>Choose an efficient format based on caller logic or Cloudflare support.</summary>
    Auto,

    /// <summary>AVIF output.</summary>
    Avif,

    /// <summary>WebP output.</summary>
    Webp,

    /// <summary>JSON image metadata output.</summary>
    Json,

    /// <summary>JPEG output.</summary>
    Jpeg,

    /// <summary>PNG output.</summary>
    Png,

    /// <summary>Baseline JPEG output.</summary>
    BaselineJpeg,

    /// <summary>Force PNG output.</summary>
    PngForce,

    /// <summary>SVG output.</summary>
    Svg
}

/// <summary>Named Cloudflare image gravity positions.</summary>
public enum FetchImageGravitySide
{
    /// <summary>Choose an automatic focal point.</summary>
    Auto,

    /// <summary>Use face detection as the focal point.</summary>
    Face,

    /// <summary>Crop toward the left edge.</summary>
    Left,

    /// <summary>Crop toward the right edge.</summary>
    Right,

    /// <summary>Crop toward the top edge.</summary>
    Top,

    /// <summary>Crop toward the bottom edge.</summary>
    Bottom
}

/// <summary>Cloudflare image metadata preservation modes.</summary>
public enum FetchImageMetadata
{
    /// <summary>Preserve metadata.</summary>
    Keep,

    /// <summary>Preserve copyright metadata.</summary>
    Copyright,

    /// <summary>Strip metadata.</summary>
    None
}

/// <summary>Cloudflare image origin authentication modes.</summary>
public enum FetchImageOriginAuth
{
    /// <summary>Share the source image publicly.</summary>
    SharePublicly
}

/// <summary>Cloudflare image error handling modes.</summary>
public enum FetchImageOnError
{
    /// <summary>Redirect to the original source image on fatal transformation errors.</summary>
    Redirect
}

/// <summary>Named Cloudflare image quality levels.</summary>
public enum FetchImageQualityLevel
{
    /// <summary>Low quality.</summary>
    Low,

    /// <summary>Medium-low quality.</summary>
    MediumLow,

    /// <summary>Medium-high quality.</summary>
    MediumHigh,

    /// <summary>High quality.</summary>
    High
}
