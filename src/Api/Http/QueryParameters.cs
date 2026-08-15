namespace Workers;

public sealed class QueryParameters : IEnumerable<QueryParameter>
{
    public string? Get(string name) => WorkerApi.NotExecutable<string?>();
    public IReadOnlyList<string> GetAll(string name) => WorkerApi.NotExecutable<IReadOnlyList<string>>();
    public bool Contains(string name) => WorkerApi.NotExecutable<bool>();
    public T As<T>() => WorkerApi.NotExecutable<T>();
    public IEnumerator<QueryParameter> GetEnumerator() => WorkerApi.NotExecutable<IEnumerator<QueryParameter>>();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed record QueryParameter(string Name, string Value);
