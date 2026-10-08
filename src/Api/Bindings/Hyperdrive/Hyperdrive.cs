namespace Workers;

public interface IHyperdriveBinding : IBinding
{
    Task<HyperdriveConnectionInfo> GetConnectionInfoAsync(CancellationToken cancellationToken = default);
    TcpSocket Connect();
}

public sealed record HyperdriveConnectionInfo(string ConnectionString, string Host, int Port, string User, string Password, string Database)
{
    public string Ip { get; init; } = "";
}
