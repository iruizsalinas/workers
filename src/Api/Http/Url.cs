namespace Workers;

public sealed class Url
{
    public static string EncodeComponent(string text) => WorkerApi.NotExecutable<string>();
    public static string DecodeComponent(string text) => WorkerApi.NotExecutable<string>();
    public Url(string value) => WorkerApi.NotExecutable();
    public Url(string value, string baseUrl) => WorkerApi.NotExecutable();

    public string Origin => WorkerApi.NotExecutable<string>();
    public string Protocol => WorkerApi.NotExecutable<string>();
    public string Host => WorkerApi.NotExecutable<string>();
    public string Hostname => WorkerApi.NotExecutable<string>();
    public string Port => WorkerApi.NotExecutable<string>();
    public string Username => WorkerApi.NotExecutable<string>();
    public string Password => WorkerApi.NotExecutable<string>();
    public string Path { get; set; } = "";
    public string Query { get; set; } = "";
    public string Fragment { get; set; } = "";
    public QueryParameters QueryParameters => WorkerApi.NotExecutable<QueryParameters>();

    public override string ToString() => WorkerApi.NotExecutable<string>();
}
