namespace Workers;

/// <summary>Cloudflare image quality, either numeric or named.</summary>
public sealed class FetchImageQuality
{
    private FetchImageQuality(int? value, FetchImageQualityLevel? level)
    {
        Value = value;
        Level = level;
    }

    internal int? Value { get; }

    internal FetchImageQualityLevel? Level { get; }

    /// <summary>Creates a numeric quality value.</summary>
    public static FetchImageQuality FromValue(int value)
    {
        if (value is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Image quality must be from 1 through 100.");

        return new(value, level: null);
    }

    /// <summary>Creates a named quality value.</summary>
    public static FetchImageQuality FromLevel(FetchImageQualityLevel level) => new(value: null, level);
}

/// <summary>Cloudflare image gravity, either named or coordinate-based.</summary>
public sealed class FetchImageGravity
{
    private FetchImageGravity(FetchImageGravitySide? side, double? x, double? y)
    {
        Side = side;
        X = x;
        Y = y;
    }

    internal FetchImageGravitySide? Side { get; }

    internal double? X { get; }

    internal double? Y { get; }

    /// <summary>Creates named gravity.</summary>
    public static FetchImageGravity FromSide(FetchImageGravitySide side) => new(side, x: null, y: null);

    /// <summary>Creates coordinate-based gravity.</summary>
    public static FetchImageGravity FromCoordinates(double x, double y)
    {
        if (!double.IsFinite(x) || x is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(x), x, "Image gravity X coordinate must be from 0.0 through 1.0.");

        if (!double.IsFinite(y) || y is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(y), y, "Image gravity Y coordinate must be from 0.0 through 1.0.");

        return new(side: null, x, y);
    }
}

/// <summary>Cloudflare image overlay repeat mode, either boolean or axis-based.</summary>
public sealed class FetchImageDrawRepeat
{
    private FetchImageDrawRepeat(bool? enabled, string? axis)
    {
        Enabled = enabled;
        Axis = axis;
    }

    internal bool? Enabled { get; }

    internal string? Axis { get; }

    /// <summary>Creates a boolean repeat mode.</summary>
    public static FetchImageDrawRepeat FromBoolean(bool enabled) => new(enabled, axis: null);

    /// <summary>Creates an axis repeat mode.</summary>
    public static FetchImageDrawRepeat FromAxis(string axis)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(axis);
        return new(enabled: null, axis);
    }
}
