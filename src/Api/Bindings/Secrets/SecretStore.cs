namespace Workers;

public interface ISecretStoreBinding : IBinding
{
    Task<string?> GetAsync();
}
