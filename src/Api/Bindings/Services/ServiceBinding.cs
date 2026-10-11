namespace Workers;

public interface IFetcherBinding : IBinding
{
    Task<Response> FetchAsync(string url, FetchOptions? options = null);
    Task<Response> FetchAsync(Request request, FetchOptions? options = null);
}

public interface IServiceBinding : IFetcherBinding
{
    Task<JsonValue> InvokeAsync(string methodName, IEnumerable<object?>? arguments = null);
    Task<TResult?> InvokeAsync<TResult>(string methodName, IEnumerable<object?>? arguments = null);
    Task<RpcStub> InvokeStubAsync(string methodName, IEnumerable<object?>? arguments = null);
    Task InvokeVoidAsync(string methodName, IEnumerable<object?>? arguments = null);
}
