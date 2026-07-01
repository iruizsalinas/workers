using System.Text.Json.Serialization;

namespace Workers;

/// <summary>Border options for Cloudflare image transformations.</summary>
public sealed class FetchImageBorder
{
    /// <summary>Border color.</summary>
    public required string Color { get; init; }

    /// <summary>Uniform border width in pixels.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Width { get; init; }

    /// <summary>Top border width in pixels.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Top { get; init; }

    /// <summary>Right border width in pixels.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Right { get; init; }

    /// <summary>Bottom border width in pixels.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Bottom { get; init; }

    /// <summary>Left border width in pixels.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Left { get; init; }
}

/// <summary>Overlay drawing options for Cloudflare image transformations.</summary>
public sealed class FetchImageDraw
{
    /// <summary>Overlay image URL.</summary>
    public required string Url { get; init; }

    /// <summary>Overlay opacity.</summary>
    public double? Opacity { get; init; }

    /// <summary>Overlay repeat mode.</summary>
    public FetchImageDrawRepeat? Repeat { get; init; }

    /// <summary>Top offset in pixels.</summary>
    public int? Top { get; init; }

    /// <summary>Bottom offset in pixels.</summary>
    public int? Bottom { get; init; }

    /// <summary>Left offset in pixels.</summary>
    public int? Left { get; init; }

    /// <summary>Right offset in pixels.</summary>
    public int? Right { get; init; }
}

/// <summary>Trim options for Cloudflare image transformations.</summary>
public sealed class FetchImageTrim
{
    /// <summary>Top trim in pixels.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Top { get; init; }

    /// <summary>Bottom trim in pixels.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Bottom { get; init; }

    /// <summary>Left trim in pixels.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Left { get; init; }

    /// <summary>Right trim in pixels.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Right { get; init; }

    /// <summary>Trim rectangle width in pixels.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Width { get; init; }

    /// <summary>Trim rectangle height in pixels.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Height { get; init; }
}
