namespace Workers;

/// <summary>An intentional HTTP failure. Status must be 400–599. The router returns an empty response by default;
/// custom error handlers can expose the message to clients.</summary>
public sealed class HttpError : Exception
{
    public HttpError(int status, string message) : base(message) => WorkerApi.NotExecutable();
    public int Status => WorkerApi.NotExecutable<int>();
}

/// <summary>A router failure with a public message and the original exception for server-side logging.</summary>
public sealed class RouterError
{
    private RouterError() { }
    public int Status => WorkerApi.NotExecutable<int>();
    public string Message => WorkerApi.NotExecutable<string>();
    public Exception Exception => WorkerApi.NotExecutable<Exception>();
}
