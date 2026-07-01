using System.Text.Json;

namespace Workers;

/// <summary>Represents a service binding to another Worker.</summary>
public interface IServiceBinding : IFetcherBinding
{
    /// <summary>Invokes a JSON-compatible RPC method on the bound Worker.</summary>
    Task<JsonElement> InvokeAsync(
        string methodName,
        IReadOnlyList<object?>? arguments = null,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Invokes a JSON-compatible RPC method and deserializes the result.</summary>
    Task<TResult?> InvokeAsync<TResult>(
        string methodName,
        IReadOnlyList<object?>? arguments = null,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Invokes an RPC method that returns an object-capability stub.</summary>
    Task<RpcStub> InvokeStubAsync(
        string methodName,
        IReadOnlyList<object?>? arguments = null,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Invokes a JSON-compatible RPC method and ignores the result.</summary>
    Task InvokeVoidAsync(
        string methodName,
        IReadOnlyList<object?>? arguments = null,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default);
}
