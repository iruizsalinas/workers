using System.Diagnostics.CodeAnalysis;

namespace Workers;

/// <summary>A request or response body. Conversions pass the value to the runtime without serialization or buffering.</summary>
public sealed class BodyInit
{
    private BodyInit() { }

    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator BodyInit?(string? value) => WorkerApi.NotExecutable<BodyInit?>();
    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator BodyInit?(byte[]? value) => WorkerApi.NotExecutable<BodyInit?>();
    public static implicit operator BodyInit(ReadOnlyMemory<byte> value) => WorkerApi.NotExecutable<BodyInit>();
    public static implicit operator BodyInit(ReadOnlySpan<byte> value) => WorkerApi.NotExecutable<BodyInit>();
    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator BodyInit?(ReadableStream? value) => WorkerApi.NotExecutable<BodyInit?>();
    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator BodyInit?(Blob? value) => WorkerApi.NotExecutable<BodyInit?>();
    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator BodyInit?(FormData? value) => WorkerApi.NotExecutable<BodyInit?>();
    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator BodyInit?(UrlSearchParams? value) => WorkerApi.NotExecutable<BodyInit?>();
}
