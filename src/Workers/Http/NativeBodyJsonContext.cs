using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workers;

internal sealed record NativeBodyRequest(string? Handle);

internal sealed record NativeStreamRequest(string Source, string Handle);

internal sealed record NativeStreamReadResult(bool Done, string? BodyBase64);

internal sealed record NativeTextResult(string Value);

internal sealed record NativeBytesResult(string? BodyBase64);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(NativeBodyRequest))]
[JsonSerializable(typeof(NativeStreamRequest))]
[JsonSerializable(typeof(NativeStreamReadResult))]
[JsonSerializable(typeof(NativeTextResult))]
[JsonSerializable(typeof(NativeBytesResult))]
internal sealed partial class NativeBodyJsonContext : JsonSerializerContext;
