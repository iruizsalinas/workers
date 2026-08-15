namespace Workers;

public interface IVersionMetadataBinding : IBinding
{
    Task<VersionMetadata> GetAsync(CancellationToken cancellationToken = default);
}

public sealed record VersionMetadata(string Id, string? Tag, DateTimeOffset Timestamp);
