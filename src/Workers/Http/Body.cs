using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace Workers;

/// <summary>Immutable in-memory body representation used by the SDK core.</summary>
public sealed class Body
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly byte[] _bytes;

    private Body(byte[] bytes, string? contentType)
    {
        _bytes = bytes;
        ContentType = contentType;
    }

    /// <summary>An empty body.</summary>
    public static Body Empty { get; } = new([], null);

    /// <summary>The content type associated with the body, if known.</summary>
    public string? ContentType { get; }

    /// <summary>True when the body has no bytes.</summary>
    public bool IsEmpty => _bytes.Length == 0;

    /// <summary>A snapshot of the raw body bytes.</summary>
    public ReadOnlyMemory<byte> Bytes => _bytes.ToArray();

    internal ReadOnlyMemory<byte> InternalBytes => _bytes;

    /// <summary>Creates a UTF-8 text body.</summary>
    public static Body Text(string value, string contentType = "text/plain; charset=utf-8")
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Body(Encoding.UTF8.GetBytes(value), contentType);
    }

    /// <summary>Creates a JSON body using <see cref="JsonSerializer"/>.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "This convenience API intentionally uses System.Text.Json reflection serialization; browser-wasm workers keep reflection JSON enabled by default.")]
    public static Body Json<T>(T value, JsonSerializerOptions? options = null)
    {
        return new Body(JsonSerializer.SerializeToUtf8Bytes(value, options ?? JsonOptions), "application/json");
    }

    /// <summary>Creates a body from raw bytes.</summary>
    public static Body FromBytes(ReadOnlySpan<byte> value, string contentType = "application/octet-stream")
    {
        return new Body(value.ToArray(), contentType);
    }

    /// <summary>Decodes the body as UTF-8 text.</summary>
    public string AsText() => Encoding.UTF8.GetString(_bytes);

    /// <summary>Deserializes the body as JSON.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "This convenience API intentionally uses System.Text.Json reflection deserialization; browser-wasm workers keep reflection JSON enabled by default.")]
    public T? AsJson<T>(JsonSerializerOptions? options = null) =>
        JsonSerializer.Deserialize<T>(_bytes, options ?? JsonOptions);
}
