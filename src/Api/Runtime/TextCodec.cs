namespace Workers;

public static class TextCodec
{
    public static byte[] EncodeUtf8(string text) => WorkerApi.NotExecutable<byte[]>();
    public static string EncodeBase64(ReadOnlyMemory<byte> bytes) => WorkerApi.NotExecutable<string>();
    public static byte[] DecodeBase64(string text) => WorkerApi.NotExecutable<byte[]>();
    public static string EncodeHex(ReadOnlyMemory<byte> bytes) => WorkerApi.NotExecutable<string>();
    public static byte[] DecodeHex(string text) => WorkerApi.NotExecutable<byte[]>();
    public static string DecodeUtf8(
        ReadOnlyMemory<byte> bytes,
        bool fatal = false,
        bool ignoreBom = false) => WorkerApi.NotExecutable<string>();
}
