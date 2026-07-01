using System.Text.Json;
using System.Text.Json.Nodes;

namespace Workers;

/// <summary>Options for storing an object in R2.</summary>
public sealed class R2PutOptions
{
    /// <summary>HTTP metadata to store with the object.</summary>
    public R2HttpMetadata? HttpMetadata { get; init; }

    /// <summary>Custom metadata to store with the object.</summary>
    public IReadOnlyDictionary<string, string>? CustomMetadata { get; init; }

    /// <summary>Checksum values used by R2 to verify the uploaded object.</summary>
    public R2Checksums? Checksums { get; init; }

    /// <summary>Only stores the object when these conditions are satisfied.</summary>
    public R2Conditional? OnlyIf { get; init; }
}

internal static class R2PutRequest
{
    public static JsonObject From(string key, Body body, R2PutOptions? options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(body);

        return new JsonObject
        {
            ["key"] = key,
            ["bodyBase64"] = Convert.ToBase64String(body.InternalBytes.Span),
            ["contentType"] = body.ContentType,
            ["options"] = R2PutOptionsEnvelope.From(options)?.ToJsonObject()
        };
    }
}

internal sealed class R2PutOptionsEnvelope
{
    public R2HttpMetadataEnvelope? HttpMetadata { get; init; }

    public IReadOnlyDictionary<string, string>? CustomMetadata { get; init; }

    public R2ChecksumsEnvelope? Checksums { get; init; }

    public JsonObject? OnlyIf { get; init; }

    public static R2PutOptionsEnvelope? From(R2PutOptions? options)
    {
        if (options is null)
            return null;

        return new R2PutOptionsEnvelope
        {
            HttpMetadata = R2HttpMetadataEnvelope.From(options.HttpMetadata),
            CustomMetadata = R2Object.CopyCustomMetadata(options.CustomMetadata),
            Checksums = R2ChecksumsEnvelope.From(options.Checksums),
            OnlyIf = R2ConditionalEnvelope.From(options.OnlyIf)
        };
    }

    public JsonObject ToJsonObject()
    {
        var json = new JsonObject
        {
            ["httpMetadata"] = HttpMetadata is null
                ? null
                : new JsonObject
                {
                    ["contentType"] = HttpMetadata.ContentType,
                    ["contentLanguage"] = HttpMetadata.ContentLanguage,
                    ["contentDisposition"] = HttpMetadata.ContentDisposition,
                    ["contentEncoding"] = HttpMetadata.ContentEncoding,
                    ["cacheControl"] = HttpMetadata.CacheControl,
                    ["cacheExpiry"] = HttpMetadata.CacheExpiry
                },
            ["customMetadata"] = CustomMetadata is null ? null : JsonSerializer.SerializeToNode(CustomMetadata),
            ["checksums"] = Checksums is null
                ? null
                : new JsonObject
                {
                    ["md5"] = Checksums.Md5,
                    ["sha1"] = Checksums.Sha1,
                    ["sha256"] = Checksums.Sha256,
                    ["sha384"] = Checksums.Sha384,
                    ["sha512"] = Checksums.Sha512
                },
            ["onlyIf"] = OnlyIf
        };

        return json;
    }
}
