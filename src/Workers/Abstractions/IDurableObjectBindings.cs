using System.Text.Json;

namespace Workers;

/// <summary>Represents a Durable Object namespace binding.</summary>
public interface IDurableObjectNamespace : IBinding
{
    /// <summary>Derives a stable object ID from a name.</summary>
    Task<DurableObjectId> IdFromNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Parses a stringified object ID in this namespace.</summary>
    Task<DurableObjectId> IdFromStringAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Creates a unique object ID.</summary>
    Task<DurableObjectId> NewUniqueIdAsync(
        DurableObjectIdOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a stub for an object ID.</summary>
    IDurableObjectStub Get(DurableObjectId id, DurableObjectGetOptions? options = null);

    /// <summary>Gets a stub for an object name.</summary>
    IDurableObjectStub GetByName(string name, DurableObjectGetOptions? options = null);
}

/// <summary>Represents a Durable Object stub.</summary>
public interface IDurableObjectStub : IBinding
{
    /// <summary>Sends a request to the Durable Object.</summary>
    Task<Response> FetchAsync(Request request, CancellationToken cancellationToken = default);

    /// <summary>Sends a GET request to the Durable Object URL.</summary>
    Task<Response> FetchAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>Invokes a JSON-compatible RPC method on the Durable Object.</summary>
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
