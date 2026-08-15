namespace Workers;

public sealed class Body
{
    public static Body Empty => WorkerApi.NotExecutable<Body>();
    public string? ContentType => WorkerApi.NotExecutable<string?>();
    public bool IsEmpty => WorkerApi.NotExecutable<bool>();
    public ReadOnlyMemory<byte> Bytes => WorkerApi.NotExecutable<ReadOnlyMemory<byte>>();

    public static Body Text(string value, string contentType = "text/plain; charset=utf-8") => WorkerApi.NotExecutable<Body>();
    public static Body Json<T>(T value, object? options = null) => WorkerApi.NotExecutable<Body>();
    public static Body FromBytes(ReadOnlySpan<byte> value, string contentType = "application/octet-stream") => WorkerApi.NotExecutable<Body>();
    public string AsText() => WorkerApi.NotExecutable<string>();
    public T? AsJson<T>() => WorkerApi.NotExecutable<T?>();
}
