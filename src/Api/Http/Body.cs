namespace Workers;

public sealed class Body
{
    public static Body Empty => WorkerApi.NotExecutable<Body>();
    public bool IsEmpty => WorkerApi.NotExecutable<bool>();

    public static Body Text(string value) => WorkerApi.NotExecutable<Body>();
    public static Body Json<T>(T value) => WorkerApi.NotExecutable<Body>();
    public static Body FromBytes(ReadOnlySpan<byte> value) => WorkerApi.NotExecutable<Body>();
    public static Body FromStream(ReadableStream value) => WorkerApi.NotExecutable<Body>();
    public static Body FromFormData(FormData value) => WorkerApi.NotExecutable<Body>();
    public static Body FromQueryParameters(QueryParameters value) => WorkerApi.NotExecutable<Body>();
}
