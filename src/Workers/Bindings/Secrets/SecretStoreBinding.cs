using System.Text.Json;

namespace Workers;

internal sealed class SecretStoreBinding : ISecretStoreBinding
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public SecretStoreBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<string?> GetAsync(CancellationToken cancellationToken = default)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            "secretStore.get",
            "{}");

        var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
        return JsonSerializer.Deserialize<SecretStoreResult>(result, JsonOptions)?.Value;
    }

    private sealed class SecretStoreResult
    {
        public string? Value { get; init; }
    }
}
