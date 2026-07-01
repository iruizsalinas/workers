using System.Text.Json;

namespace Workers;

/// <summary>Version metadata for the deployed Worker script.</summary>
public sealed record VersionMetadata(
    string Id,
    string Tag,
    string Timestamp);

internal sealed class VersionMetadataBinding : IVersionMetadataBinding
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public VersionMetadataBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<VersionMetadata> GetAsync(CancellationToken cancellationToken = default)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            "versionMetadata.get",
            "{}");

        var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
        return JsonSerializer.Deserialize<VersionMetadata>(result, JsonOptions)
            ?? throw new WorkersException("Version Metadata binding returned an empty result.");
    }
}
