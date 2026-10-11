using System.Diagnostics.CodeAnalysis;

namespace Workers;

/// <summary>HTTP headers. Names are case-insensitive. Mutation follows the runtime's guard: headers of a received
/// request or fetched response are immutable, so copy them with <c>new Headers(source)</c> to change them.</summary>
public sealed class Headers : IEnumerable<KeyValuePair<string, string>>
{
    public Headers() => WorkerApi.NotExecutable();
    public Headers(Headers init) => WorkerApi.NotExecutable();
    public Headers(IEnumerable<KeyValuePair<string, string>> init) => WorkerApi.NotExecutable();

    /// <summary>Gets a header as <see cref="Get"/>, or replaces it as <see cref="Set"/>.</summary>
    [DisallowNull]
    public string? this[string name]
    {
        get => WorkerApi.NotExecutable<string?>();
        set => WorkerApi.NotExecutable();
    }

    /// <summary>The header's values joined with <c>, </c>, or null.</summary>
    public string? Get(string name) => WorkerApi.NotExecutable<string?>();
    /// <summary>Each <c>Set-Cookie</c> header separately, since those values cannot be joined.</summary>
    public IReadOnlyList<string> GetSetCookie() => WorkerApi.NotExecutable<IReadOnlyList<string>>();
    public bool Has(string name) => WorkerApi.NotExecutable<bool>();
    public void Set(string name, string value) => WorkerApi.NotExecutable();
    public void Append(string name, string value) => WorkerApi.NotExecutable();
    public void Delete(string name) => WorkerApi.NotExecutable();
    public IReadOnlyList<string> Keys() => WorkerApi.NotExecutable<IReadOnlyList<string>>();
    public IReadOnlyList<string> Values() => WorkerApi.NotExecutable<IReadOnlyList<string>>();

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => WorkerApi.NotExecutable<IEnumerator<KeyValuePair<string, string>>>();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
