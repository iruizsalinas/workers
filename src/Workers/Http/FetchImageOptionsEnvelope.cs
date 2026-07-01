using System.Text.Json.Serialization;

namespace Workers;

internal sealed record FetchImageOptionsEnvelope(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? Anim,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Background,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Blur,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    FetchImageBorder? Border,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? Brightness,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Compression,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? Contrast,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? Dpr,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    FetchImageDrawEnvelope? Draw,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Fit,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Flip,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Format,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? Gamma,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    object? Gravity,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Height,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Metadata,
    [property: JsonPropertyName("origin-auth")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? OriginAuth,
    [property: JsonPropertyName("onerror")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? OnError,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    object? Quality,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Rotate,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? Saturation,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? Sharpen,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    FetchImageTrim? Trim,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Width)
{
    public static FetchImageOptionsEnvelope? From(FetchImageOptions? options)
    {
        if (options is null)
            return null;

        Validate(options);
        return new FetchImageOptionsEnvelope(
            options.Anim,
            options.Background,
            options.Blur,
            options.Border,
            options.Brightness,
            CompressionName(options.Compression),
            options.Contrast,
            options.Dpr,
            FetchImageDrawEnvelope.From(options.Draw),
            FitName(options.Fit),
            FlipName(options.Flip),
            FormatName(options.Format),
            options.Gamma,
            GravityValue(options.Gravity),
            options.Height,
            MetadataName(options.Metadata),
            OriginAuthName(options.OriginAuth),
            OnErrorName(options.OnError),
            QualityValue(options.Quality),
            options.Rotate,
            options.Saturation,
            options.Sharpen,
            options.Trim,
            options.Width);
    }

    private static void Validate(FetchImageOptions options)
    {
        ValidateRange(options.Blur, 0, 250, nameof(options.Blur), "Image blur must be from 0 through 250.");
        ValidatePositive(options.Height, nameof(options.Height), "Image height must be a positive integer.");
        ValidatePositive(options.Width, nameof(options.Width), "Image width must be a positive integer.");
        ValidateDoubleRange(options.Dpr, 0, 2, nameof(options.Dpr), "Image DPR must be greater than 0 and no more than 2.", excludeMinimum: true);
        ValidateDoubleRange(options.Sharpen, 0, 10, nameof(options.Sharpen), "Image sharpen value must be from 0 through 10.");

        if (options.Rotate is not null and not (90 or 180 or 270))
            throw new ArgumentOutOfRangeException(nameof(options.Rotate), options.Rotate, "Image rotation must be 90, 180, or 270 degrees.");
    }

    private static void ValidatePositive(int? value, string parameterName, string message)
    {
        if (value is <= 0)
            throw new ArgumentOutOfRangeException(parameterName, value, message);
    }

    private static void ValidateRange(int? value, int minimum, int maximum, string parameterName, string message)
    {
        if (value is null)
            return;

        if (value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(parameterName, value, message);
    }

    private static void ValidateDoubleRange(
        double? value,
        double minimum,
        double maximum,
        string parameterName,
        string message,
        bool excludeMinimum = false)
    {
        if (value is null)
            return;

        if (!double.IsFinite(value.Value) ||
            (excludeMinimum ? value <= minimum : value < minimum) ||
            value > maximum)
            throw new ArgumentOutOfRangeException(parameterName, value, message);
    }

    private static string? CompressionName(FetchImageCompression? compression) =>
        compression switch
        {
            null => null,
            FetchImageCompression.Fast => "fast",
            _ => throw new ArgumentOutOfRangeException(nameof(compression), compression, "Unsupported image compression mode.")
        };

    private static string? FitName(FetchImageFit? fit) =>
        fit switch
        {
            null => null,
            FetchImageFit.ScaleDown => "scale-down",
            FetchImageFit.Contain => "contain",
            FetchImageFit.Cover => "cover",
            FetchImageFit.Crop => "crop",
            FetchImageFit.Pad => "pad",
            FetchImageFit.Squeeze => "squeeze",
            _ => throw new ArgumentOutOfRangeException(nameof(fit), fit, "Unsupported image fit mode.")
        };

    private static string? FlipName(FetchImageFlip? flip) =>
        flip switch
        {
            null => null,
            FetchImageFlip.Horizontal => "h",
            FetchImageFlip.Vertical => "v",
            FetchImageFlip.Both => "hv",
            _ => throw new ArgumentOutOfRangeException(nameof(flip), flip, "Unsupported image flip mode.")
        };

    private static string? FormatName(FetchImageFormat? format) =>
        format switch
        {
            null => null,
            FetchImageFormat.Auto => "auto",
            FetchImageFormat.Avif => "avif",
            FetchImageFormat.Webp => "webp",
            FetchImageFormat.Json => "json",
            FetchImageFormat.Jpeg => "jpeg",
            FetchImageFormat.Png => "png",
            FetchImageFormat.BaselineJpeg => "baseline-jpeg",
            FetchImageFormat.PngForce => "png-force",
            FetchImageFormat.Svg => "svg",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported image format.")
        };

    private static object? GravityValue(FetchImageGravity? gravity)
    {
        if (gravity is null)
            return null;

        return gravity.Side is { } side
            ? GravitySideName(side)
            : new FetchImageGravityCoordinates(gravity.X.GetValueOrDefault(), gravity.Y.GetValueOrDefault());
    }

    private static string GravitySideName(FetchImageGravitySide side) =>
        side switch
        {
            FetchImageGravitySide.Auto => "auto",
            FetchImageGravitySide.Face => "face",
            FetchImageGravitySide.Left => "left",
            FetchImageGravitySide.Right => "right",
            FetchImageGravitySide.Top => "top",
            FetchImageGravitySide.Bottom => "bottom",
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unsupported image gravity side.")
        };

    private static string? MetadataName(FetchImageMetadata? metadata) =>
        metadata switch
        {
            null => null,
            FetchImageMetadata.Keep => "keep",
            FetchImageMetadata.Copyright => "copyright",
            FetchImageMetadata.None => "none",
            _ => throw new ArgumentOutOfRangeException(nameof(metadata), metadata, "Unsupported image metadata mode.")
        };

    private static string? OriginAuthName(FetchImageOriginAuth? originAuth) =>
        originAuth switch
        {
            null => null,
            FetchImageOriginAuth.SharePublicly => "share-publicly",
            _ => throw new ArgumentOutOfRangeException(nameof(originAuth), originAuth, "Unsupported image origin auth mode.")
        };

    private static string? OnErrorName(FetchImageOnError? onError) =>
        onError switch
        {
            null => null,
            FetchImageOnError.Redirect => "redirect",
            _ => throw new ArgumentOutOfRangeException(nameof(onError), onError, "Unsupported image error mode.")
        };

    private static object? QualityValue(FetchImageQuality? quality)
    {
        if (quality is null)
            return null;

        return quality.Value is { } value
            ? value
            : QualityLevelName(quality.Level.GetValueOrDefault());
    }

    private static string QualityLevelName(FetchImageQualityLevel level) =>
        level switch
        {
            FetchImageQualityLevel.Low => "low",
            FetchImageQualityLevel.MediumLow => "medium-low",
            FetchImageQualityLevel.MediumHigh => "medium-high",
            FetchImageQualityLevel.High => "high",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unsupported image quality level.")
        };
}

internal sealed record FetchImageDrawEnvelope(
    string Url,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? Opacity,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    object? Repeat,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Top,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Bottom,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Left,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Right)
{
    public static FetchImageDrawEnvelope? From(FetchImageDraw? draw)
    {
        if (draw is null)
            return null;

        return new FetchImageDrawEnvelope(
            draw.Url,
            draw.Opacity,
            RepeatValue(draw.Repeat),
            draw.Top,
            draw.Bottom,
            draw.Left,
            draw.Right);
    }

    private static object? RepeatValue(FetchImageDrawRepeat? repeat)
    {
        if (repeat is null)
            return null;

        return repeat.Enabled is { } enabled ? enabled : repeat.Axis;
    }
}

internal sealed record FetchImageGravityCoordinates(double X, double Y);
