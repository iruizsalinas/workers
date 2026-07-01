using System.Text.Json;

namespace Workers;

/// <summary>A live runtime binding without a dedicated typed wrapper.</summary>
public sealed class RawBinding : IBinding
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    internal RawBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>Reads a JSON-compatible property from the binding.</summary>
    public async Task<JsonElement> GetPropertyAsync(string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var result = await DispatchAsync(
                "binding.getProperty",
                new RawBindingPropertyRequest(propertyName),
                cancellationToken)
            ;

        return JsonSerializer.Deserialize<RawBindingResult>(result, JsonOptions)?.Value
            ?? throw new WorkersException("Raw binding property access returned an empty result.");
    }

    /// <summary>Reads and deserializes a JSON-compatible property from the binding.</summary>
    public async Task<T?> GetPropertyAsync<T>(
        string propertyName,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var value = await GetPropertyAsync(propertyName, cancellationToken);
        return value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? default
            : value.Deserialize<T>(options ?? JsonOptions);
    }

    /// <summary>Invokes a JSON-compatible method on the binding.</summary>
    public async Task<JsonElement> InvokeAsync(
        string methodName,
        IReadOnlyList<object?>? arguments = null,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        options ??= JsonOptions;

        var result = await DispatchAsync(
                "binding.invoke",
                new RawBindingInvokeRequest(
                    methodName,
                    (arguments ?? []).Select(argument => JsonSerializer.SerializeToElement(argument, options)).ToArray()),
                cancellationToken)
            ;

        return JsonSerializer.Deserialize<RawBindingResult>(result, JsonOptions)?.Value
            ?? throw new WorkersException("Raw binding invocation returned an empty result.");
    }

    /// <summary>Invokes a JSON-compatible method and deserializes the result.</summary>
    public async Task<T?> InvokeAsync<T>(
        string methodName,
        IReadOnlyList<object?>? arguments = null,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var value = await InvokeAsync(methodName, arguments, options, cancellationToken);
        return value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? default
            : value.Deserialize<T>(options ?? JsonOptions);
    }

    /// <summary>Invokes a JSON-compatible method and ignores the result.</summary>
    public async Task InvokeVoidAsync(
        string methodName,
        IReadOnlyList<object?>? arguments = null,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _ = await InvokeAsync(methodName, arguments, options, cancellationToken);
    }

    private Task<string> DispatchAsync(string operation, object payload, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            operation,
            JsonSerializer.Serialize(payload, JsonOptions));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private sealed record RawBindingPropertyRequest(string PropertyName);

    private sealed record RawBindingInvokeRequest(string MethodName, IReadOnlyList<JsonElement> Arguments);

    private sealed record RawBindingResult(JsonElement Value);
}
