namespace Workers;

public static class TextCodec
{
    public static string DecodeUtf8(
        ReadOnlyMemory<byte> bytes,
        bool fatal = false,
        bool ignoreBom = false) => WorkerApi.NotExecutable<string>();
}
