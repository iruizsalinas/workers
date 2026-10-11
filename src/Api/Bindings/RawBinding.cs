namespace Workers;

/// <summary>A binding without a typed API, used through its JavaScript properties and methods.</summary>
public sealed class RawBinding
{
    private RawBinding() { }

    public JsonValue GetProperty(string name) => WorkerApi.NotExecutable<JsonValue>();
    public Task<JsonValue> InvokeAsync(string methodName, params object?[] arguments) => WorkerApi.NotExecutable<Task<JsonValue>>();
    /// <summary>Calls a method and maps its result to a C# class or record.</summary>
    public Task<TResult?> InvokeAsync<TResult>(string methodName, params object?[] arguments) => WorkerApi.NotExecutable<Task<TResult?>>();
}
