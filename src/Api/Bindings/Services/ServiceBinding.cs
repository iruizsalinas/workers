namespace Workers;

public interface IFetcherBinding : IBinding
{
    Task<Response> FetchAsync(string url, CancellationToken cancellationToken = default);
    Task<Response> FetchAsync(Request request, CancellationToken cancellationToken = default);
    Task<Response> FetchAsync(string url, FetchOptions? options, CancellationToken cancellationToken = default);
    Task<Response> FetchAsync(Request request, FetchOptions? options, CancellationToken cancellationToken = default);
}

public interface IServiceBinding : IFetcherBinding
{
    Task<JsonElement> InvokeAsync(string methodName, IEnumerable<object?>? arguments = null, CancellationToken cancellationToken = default);
    Task<TResult?> InvokeAsync<TResult>(string methodName, IEnumerable<object?>? arguments = null, CancellationToken cancellationToken = default);
    Task<RpcStub> InvokeStubAsync(string methodName, IEnumerable<object?>? arguments = null, CancellationToken cancellationToken = default);
    Task InvokeVoidAsync(string methodName, IEnumerable<object?>? arguments = null, CancellationToken cancellationToken = default);
}
