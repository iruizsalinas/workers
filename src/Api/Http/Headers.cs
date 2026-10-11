using System.Diagnostics.CodeAnalysis;

namespace Workers;

/// <summary>Native headers. Mutation follows runtime guards; construct a copy to obtain writable headers.</summary>
public sealed class Headers : IEnumerable<KeyValuePair<string, string>>
{
    public Headers() => WorkerApi.NotExecutable();
    public Headers(Headers source) => WorkerApi.NotExecutable();
    public Headers(IEnumerable<KeyValuePair<string, string>> entries) => WorkerApi.NotExecutable();

    [DisallowNull]
    public string? this[string name]
    {
        get => WorkerApi.NotExecutable<string?>();
        set => WorkerApi.NotExecutable();
    }

    public int Count => WorkerApi.NotExecutable<int>();

    public string? Get(string name) => WorkerApi.NotExecutable<string?>();
    public IReadOnlyList<string> GetSetCookie() => WorkerApi.NotExecutable<IReadOnlyList<string>>();
    public bool Contains(string name) => WorkerApi.NotExecutable<bool>();
    public void Set(string name, string value) => WorkerApi.NotExecutable();
    public void Append(string name, string value) => WorkerApi.NotExecutable();
    public void Delete(string name) => WorkerApi.NotExecutable();
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => WorkerApi.NotExecutable<IEnumerator<KeyValuePair<string, string>>>();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
