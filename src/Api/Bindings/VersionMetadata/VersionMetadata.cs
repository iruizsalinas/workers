namespace Workers;

public interface IVersionMetadataBinding : IBinding
{
    string Id { get; }
    string Tag { get; }
    string Timestamp { get; }
}
