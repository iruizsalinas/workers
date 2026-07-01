namespace Workers;

/// <summary>Represents a Workers Secret Store binding for one configured secret.</summary>
public interface ISecretStoreBinding : IBinding
{
    /// <summary>Gets the secret value, or null when the secret does not exist.</summary>
    Task<string?> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>Represents a Workers Hyperdrive binding.</summary>
public interface IHyperdriveBinding : IBinding
{
    /// <summary>Reads the Hyperdrive connection metadata exposed by the runtime binding.</summary>
    Task<HyperdriveConnectionInfo> GetConnectionInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>Connects directly to Hyperdrive and returns an unauthenticated database TCP socket.</summary>
    Task<Socket> ConnectAsync(CancellationToken cancellationToken = default);
}
