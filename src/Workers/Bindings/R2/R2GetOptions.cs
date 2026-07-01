using System.Text.Json.Nodes;

namespace Workers;

/// <summary>Options for reading an object from R2.</summary>
public sealed class R2GetOptions
{
    /// <summary>Only returns the object when these conditions are satisfied.</summary>
    public R2Conditional? OnlyIf { get; init; }

    /// <summary>Reads only a byte range from the object.</summary>
    public R2Range? Range { get; init; }
}

/// <summary>Conditional checks for R2 read and write operations.</summary>
public sealed record R2Conditional(
    string? EtagMatches = null,
    string? EtagDoesNotMatch = null,
    DateTimeOffset? UploadedBefore = null,
    DateTimeOffset? UploadedAfter = null);

internal static class R2Payloads
{
    public static JsonObject Get(string key, R2GetOptions? options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new JsonObject
        {
            ["key"] = key,
            ["options"] = R2GetOptionsEnvelope.From(options)
        };
    }
}

internal sealed class R2GetOptionsEnvelope
{
    public static JsonObject? From(R2GetOptions? options)
    {
        if (options is null)
            return null;

        return new JsonObject
        {
            ["onlyIf"] = R2ConditionalEnvelope.From(options.OnlyIf),
            ["range"] = RangeFrom(options.Range)
        };
    }

    private static JsonObject? RangeFrom(R2Range? range)
    {
        if (range is null)
            return null;

        return new JsonObject
        {
            ["offset"] = range.Offset,
            ["length"] = range.Length,
            ["suffix"] = range.Suffix
        };
    }
}

internal sealed class R2ConditionalEnvelope
{
    public static JsonObject? From(R2Conditional? conditional)
    {
        if (conditional is null)
            return null;

        return new JsonObject
        {
            ["etagMatches"] = conditional.EtagMatches,
            ["etagDoesNotMatch"] = conditional.EtagDoesNotMatch,
            ["uploadedBefore"] = FormatDate(conditional.UploadedBefore),
            ["uploadedAfter"] = FormatDate(conditional.UploadedAfter)
        };
    }

    private static string? FormatDate(DateTimeOffset? value) =>
        value?.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
}
