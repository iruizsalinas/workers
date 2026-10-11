namespace Workers;

/// <summary>Encodes text as UTF-8.</summary>
public sealed class TextEncoder
{
    public TextEncoder() => WorkerApi.NotExecutable();

    public byte[] Encode(string input = "") => WorkerApi.NotExecutable<byte[]>();
}

/// <summary>Decodes text from bytes. Invalid sequences become U+FFFD unless <c>fatal</c> is set, which makes them throw.</summary>
public sealed class TextDecoder
{
    public TextDecoder(string label = "utf-8", bool fatal = false, bool ignoreBom = false) => WorkerApi.NotExecutable();

    public string Encoding => WorkerApi.NotExecutable<string>();
    public bool Fatal => WorkerApi.NotExecutable<bool>();
    public bool IgnoreBom => WorkerApi.NotExecutable<bool>();

    public string Decode(ReadOnlyMemory<byte> input) => WorkerApi.NotExecutable<string>();
}

/// <summary>Base64 encoding of bytes, as <c>Uint8Array.prototype.toBase64</c> and <c>Uint8Array.fromBase64</c>.</summary>
public static class Base64
{
    public static string Encode(ReadOnlyMemory<byte> bytes, Base64Alphabet alphabet = Base64Alphabet.Base64, bool omitPadding = false) =>
        WorkerApi.NotExecutable<string>();
    /// <summary>Decodes base64 text; throws a SyntaxError when it is malformed.</summary>
    public static byte[] Decode(string text, Base64Alphabet alphabet = Base64Alphabet.Base64) => WorkerApi.NotExecutable<byte[]>();
}

public enum Base64Alphabet
{
    Base64,
    /// <summary>The URL- and filename-safe alphabet, used by JWTs.</summary>
    Base64Url
}

/// <summary>Hexadecimal encoding of bytes, as <c>Uint8Array.prototype.toHex</c> and <c>Uint8Array.fromHex</c>.</summary>
public static class Hex
{
    public static string Encode(ReadOnlyMemory<byte> bytes) => WorkerApi.NotExecutable<string>();
    /// <summary>Decodes hex text; throws a SyntaxError when it is malformed.</summary>
    public static byte[] Decode(string text) => WorkerApi.NotExecutable<byte[]>();
}
