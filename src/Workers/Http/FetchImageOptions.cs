namespace Workers;

/// <summary>Cloudflare image transformation options for a fetch request.</summary>
public sealed class FetchImageOptions
{
    /// <summary>Controls whether animation frames are preserved.</summary>
    public bool? Anim { get; init; }

    /// <summary>Background color used for transparent pixels or padded output.</summary>
    public string? Background { get; init; }

    /// <summary>Blur radius for the output image.</summary>
    public int? Blur { get; init; }

    /// <summary>Border added around the transformed image.</summary>
    public FetchImageBorder? Border { get; init; }

    /// <summary>Brightness multiplier.</summary>
    public double? Brightness { get; init; }

    /// <summary>Compression mode.</summary>
    public FetchImageCompression? Compression { get; init; }

    /// <summary>Contrast multiplier.</summary>
    public double? Contrast { get; init; }

    /// <summary>Device pixel ratio multiplier.</summary>
    public double? Dpr { get; init; }

    /// <summary>Overlay drawing operation.</summary>
    public FetchImageDraw? Draw { get; init; }

    /// <summary>Resize fit mode.</summary>
    public FetchImageFit? Fit { get; init; }

    /// <summary>Flip mode.</summary>
    public FetchImageFlip? Flip { get; init; }

    /// <summary>Output image format.</summary>
    public FetchImageFormat? Format { get; init; }

    /// <summary>Gamma multiplier.</summary>
    public double? Gamma { get; init; }

    /// <summary>Crop gravity.</summary>
    public FetchImageGravity? Gravity { get; init; }

    /// <summary>Maximum output height in pixels.</summary>
    public int? Height { get; init; }

    /// <summary>Metadata preservation mode.</summary>
    public FetchImageMetadata? Metadata { get; init; }

    /// <summary>Authentication mode for the origin image.</summary>
    public FetchImageOriginAuth? OriginAuth { get; init; }

    /// <summary>Error handling mode.</summary>
    public FetchImageOnError? OnError { get; init; }

    /// <summary>Output quality.</summary>
    public FetchImageQuality? Quality { get; init; }

    /// <summary>Clockwise rotation in degrees.</summary>
    public int? Rotate { get; init; }

    /// <summary>Saturation multiplier.</summary>
    public double? Saturation { get; init; }

    /// <summary>Sharpening strength.</summary>
    public double? Sharpen { get; init; }

    /// <summary>Trim rectangle or edges.</summary>
    public FetchImageTrim? Trim { get; init; }

    /// <summary>Maximum output width in pixels.</summary>
    public int? Width { get; init; }
}
