namespace Workers;

/// <summary>Metadata for an object stored in an R2 bucket.</summary>
public sealed record R2Object(
    string Key,
    string Version,
    ulong Size,
    string Etag,
    string HttpEtag,
    DateTimeOffset? Uploaded,
    R2HttpMetadata HttpMetadata,
    IReadOnlyDictionary<string, string> CustomMetadata,
    R2Checksums Checksums,
    R2Range? Range)
{
    internal static R2Object FromEnvelope(R2ObjectEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return new R2Object(
            envelope.Key,
            envelope.Version,
            envelope.Size,
            envelope.Etag,
            envelope.HttpEtag,
            ParseDate(envelope.Uploaded),
            R2HttpMetadata.FromEnvelope(envelope.HttpMetadata),
            CopyCustomMetadata(envelope.CustomMetadata),
            R2Checksums.FromEnvelope(envelope.Checksums),
            R2Range.FromEnvelope(envelope.Range));
    }

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;

    internal static IReadOnlyDictionary<string, string> CopyCustomMetadata(IReadOnlyDictionary<string, string>? value) =>
        value is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(value, StringComparer.Ordinal));
}

/// <summary>HTTP metadata associated with an R2 object.</summary>
public sealed record R2HttpMetadata(
    string? ContentType,
    string? ContentLanguage,
    string? ContentDisposition,
    string? ContentEncoding,
    string? CacheControl,
    DateTimeOffset? CacheExpiry)
{
    internal static R2HttpMetadata FromEnvelope(R2HttpMetadataEnvelope? envelope)
    {
        if (envelope is null)
            return Empty;

        return new R2HttpMetadata(
            envelope.ContentType,
            envelope.ContentLanguage,
            envelope.ContentDisposition,
            envelope.ContentEncoding,
            envelope.CacheControl,
            DateTimeOffset.TryParse(envelope.CacheExpiry, out var parsed) ? parsed : null);
    }

    /// <summary>An empty HTTP metadata value.</summary>
    public static R2HttpMetadata Empty { get; } = new(null, null, null, null, null, null);
}

/// <summary>Checksum values associated with an R2 object.</summary>
public sealed class R2Checksums
{
    private readonly byte[]? _md5;
    private readonly byte[]? _sha1;
    private readonly byte[]? _sha256;
    private readonly byte[]? _sha384;
    private readonly byte[]? _sha512;

    /// <summary>Creates checksum values associated with an R2 object.</summary>
    public R2Checksums(byte[]? Md5, byte[]? Sha1, byte[]? Sha256, byte[]? Sha384, byte[]? Sha512)
    {
        _md5 = Copy(Md5);
        _sha1 = Copy(Sha1);
        _sha256 = Copy(Sha256);
        _sha384 = Copy(Sha384);
        _sha512 = Copy(Sha512);
    }

    /// <summary>The MD5 checksum bytes.</summary>
    public byte[]? Md5 => Copy(_md5);

    /// <summary>The SHA-1 checksum bytes.</summary>
    public byte[]? Sha1 => Copy(_sha1);

    /// <summary>The SHA-256 checksum bytes.</summary>
    public byte[]? Sha256 => Copy(_sha256);

    /// <summary>The SHA-384 checksum bytes.</summary>
    public byte[]? Sha384 => Copy(_sha384);

    /// <summary>The SHA-512 checksum bytes.</summary>
    public byte[]? Sha512 => Copy(_sha512);

    internal byte[]? InternalMd5 => _md5;

    internal byte[]? InternalSha1 => _sha1;

    internal byte[]? InternalSha256 => _sha256;

    internal byte[]? InternalSha384 => _sha384;

    internal byte[]? InternalSha512 => _sha512;

    internal static R2Checksums FromEnvelope(R2ChecksumsEnvelope? envelope)
    {
        if (envelope is null)
            return Empty;

        return new R2Checksums(
            Decode(envelope.Md5),
            Decode(envelope.Sha1),
            Decode(envelope.Sha256),
            Decode(envelope.Sha384),
            Decode(envelope.Sha512));
    }

    /// <summary>An empty checksum value.</summary>
    public static R2Checksums Empty { get; } = new(null, null, null, null, null);

    private static byte[]? Decode(string? value) =>
        value is null ? null : Convert.FromBase64String(value);

    private static byte[]? Copy(byte[]? value) =>
        value is null ? null : value.ToArray();
}

/// <summary>The byte range represented by an R2 object body.</summary>
public sealed record R2Range(ulong? Offset, ulong? Length, ulong? Suffix)
{
    /// <summary>Reads <paramref name="length"/> bytes starting at <paramref name="offset"/>.</summary>
    public static R2Range OffsetWithLength(ulong offset, ulong length) => new(offset, length, Suffix: null);

    /// <summary>Reads from <paramref name="offset"/> through the end of the object.</summary>
    public static R2Range OffsetToEnd(ulong offset) => new(offset, Length: null, Suffix: null);

    /// <summary>Reads <paramref name="length"/> bytes from the beginning of the object.</summary>
    public static R2Range Prefix(ulong length) => new(Offset: null, length, Suffix: null);

    /// <summary>Reads <paramref name="suffix"/> bytes from the end of the object.</summary>
    public static R2Range SuffixBytes(ulong suffix) => new(Offset: null, Length: null, suffix);

    internal static R2Range? FromEnvelope(R2RangeEnvelope? envelope) =>
        envelope is null ? null : new R2Range(envelope.Offset, envelope.Length, envelope.Suffix);
}

internal sealed class R2ObjectEnvelope
{
    public string Key { get; init; } = "";

    public string Version { get; init; } = "";

    public ulong Size { get; init; }

    public string Etag { get; init; } = "";

    public string HttpEtag { get; init; } = "";

    public string? Uploaded { get; init; }

    public R2HttpMetadataEnvelope? HttpMetadata { get; init; }

    public IReadOnlyDictionary<string, string>? CustomMetadata { get; init; }

    public R2ChecksumsEnvelope? Checksums { get; init; }

    public R2RangeEnvelope? Range { get; init; }
}

internal sealed class R2HttpMetadataEnvelope
{
    public static R2HttpMetadataEnvelope? From(R2HttpMetadata? metadata)
    {
        if (metadata is null)
            return null;

        return new R2HttpMetadataEnvelope
        {
            ContentType = metadata.ContentType,
            ContentLanguage = metadata.ContentLanguage,
            ContentDisposition = metadata.ContentDisposition,
            ContentEncoding = metadata.ContentEncoding,
            CacheControl = metadata.CacheControl,
            CacheExpiry = metadata.CacheExpiry?.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    public string? ContentType { get; init; }

    public string? ContentLanguage { get; init; }

    public string? ContentDisposition { get; init; }

    public string? ContentEncoding { get; init; }

    public string? CacheControl { get; init; }

    public string? CacheExpiry { get; init; }
}

internal sealed class R2ChecksumsEnvelope
{
    public static R2ChecksumsEnvelope? From(R2Checksums? checksums)
    {
        if (checksums is null)
            return null;

        return new R2ChecksumsEnvelope
        {
            Md5 = Encode(checksums.InternalMd5),
            Sha1 = Encode(checksums.InternalSha1),
            Sha256 = Encode(checksums.InternalSha256),
            Sha384 = Encode(checksums.InternalSha384),
            Sha512 = Encode(checksums.InternalSha512)
        };
    }

    public string? Md5 { get; init; }

    public string? Sha1 { get; init; }

    public string? Sha256 { get; init; }

    public string? Sha384 { get; init; }

    public string? Sha512 { get; init; }

    private static string? Encode(byte[]? value) =>
        value is null ? null : Convert.ToBase64String(value);
}

internal sealed class R2RangeEnvelope
{
    public static R2RangeEnvelope? From(R2Range? range)
    {
        if (range is null)
            return null;

        return new R2RangeEnvelope
        {
            Offset = range.Offset,
            Length = range.Length,
            Suffix = range.Suffix
        };
    }

    public ulong? Offset { get; init; }

    public ulong? Length { get; init; }

    public ulong? Suffix { get; init; }
}
