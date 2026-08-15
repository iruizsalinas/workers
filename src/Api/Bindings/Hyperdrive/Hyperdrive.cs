namespace Workers;

public interface IHyperdriveBinding : IBinding
{
    Task<HyperdriveConnectionInfo> GetConnectionInfoAsync(CancellationToken cancellationToken = default);
}

public sealed record HyperdriveConnectionInfo(string ConnectionString, string Host, int Port, string User, string Password, string Database);
