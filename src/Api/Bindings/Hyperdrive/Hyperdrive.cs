namespace Workers;

public interface IHyperdriveBinding : IBinding
{
    string ConnectionString { get; }
    string Host { get; }
    int Port { get; }
    string User { get; }
    string Password { get; }
    string Database { get; }
    TcpSocket Connect();
}
