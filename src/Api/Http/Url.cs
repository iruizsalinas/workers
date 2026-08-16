namespace Workers;

public sealed class Url
{
    public Url(string value) => WorkerApi.NotExecutable();
    public Url(string value, string baseUrl) => WorkerApi.NotExecutable();

    public string Host => WorkerApi.NotExecutable<string>();
    public string Path { get; set; } = "";
    public string Query { get; set; } = "";
    public QueryParameters QueryParameters => WorkerApi.NotExecutable<QueryParameters>();

    public override string ToString() => WorkerApi.NotExecutable<string>();
}
