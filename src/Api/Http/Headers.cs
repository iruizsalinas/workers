namespace Workers;

public sealed class Headers : IEnumerable<KeyValuePair<string, string>>
{
    public int Count => WorkerApi.NotExecutable<int>();

    public string? Get(string name) => WorkerApi.NotExecutable<string?>();
    public IReadOnlyList<string> GetAll(string name) => WorkerApi.NotExecutable<IReadOnlyList<string>>();
    public bool Contains(string name) => WorkerApi.NotExecutable<bool>();
    public Headers Set(string name, string value) => WorkerApi.NotExecutable<Headers>();
    public Headers Append(string name, string value) => WorkerApi.NotExecutable<Headers>();
    public bool Delete(string name) => WorkerApi.NotExecutable<bool>();
    public Headers Clone() => WorkerApi.NotExecutable<Headers>();
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => WorkerApi.NotExecutable<IEnumerator<KeyValuePair<string, string>>>();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed record Header(string Name, string Value);
