namespace Workers;

/// <summary>A WHATWG URL. Setting a component updates <see cref="Href"/> and <see cref="SearchParams"/>.</summary>
public sealed class Url
{
    /// <summary>Parses an absolute URL, or a relative one against <paramref name="baseUrl"/>; throws a TypeError when invalid.</summary>
    public Url(string url, string? baseUrl = null) => WorkerApi.NotExecutable();

    public static bool CanParse(string url, string? baseUrl = null) => WorkerApi.NotExecutable<bool>();
    /// <summary>Parses a URL, or returns null when it is invalid.</summary>
    public static Url? Parse(string url, string? baseUrl = null) => WorkerApi.NotExecutable<Url?>();

    public string Href { get => WorkerApi.NotExecutable<string>(); set => WorkerApi.NotExecutable(); }
    public string Origin => WorkerApi.NotExecutable<string>();
    public string Protocol { get => WorkerApi.NotExecutable<string>(); set => WorkerApi.NotExecutable(); }
    public string Username { get => WorkerApi.NotExecutable<string>(); set => WorkerApi.NotExecutable(); }
    public string Password { get => WorkerApi.NotExecutable<string>(); set => WorkerApi.NotExecutable(); }
    public string Host { get => WorkerApi.NotExecutable<string>(); set => WorkerApi.NotExecutable(); }
    public string Hostname { get => WorkerApi.NotExecutable<string>(); set => WorkerApi.NotExecutable(); }
    public string Port { get => WorkerApi.NotExecutable<string>(); set => WorkerApi.NotExecutable(); }
    public string Pathname { get => WorkerApi.NotExecutable<string>(); set => WorkerApi.NotExecutable(); }
    /// <summary>The query, including its leading <c>?</c>, or empty.</summary>
    public string Search { get => WorkerApi.NotExecutable<string>(); set => WorkerApi.NotExecutable(); }
    /// <summary>The fragment, including its leading <c>#</c>, or empty.</summary>
    public string Hash { get => WorkerApi.NotExecutable<string>(); set => WorkerApi.NotExecutable(); }
    public UrlSearchParams SearchParams => WorkerApi.NotExecutable<UrlSearchParams>();

    public override string ToString() => WorkerApi.NotExecutable<string>();

    /// <summary>Percent-encodes text for a URL component, as <c>encodeURIComponent</c>.</summary>
    public static string EncodeComponent(string text) => WorkerApi.NotExecutable<string>();
    /// <summary>Decodes a percent-encoded URL component, as <c>decodeURIComponent</c>; throws on malformed input.</summary>
    public static string DecodeComponent(string text) => WorkerApi.NotExecutable<string>();
}

/// <summary>A query string's name-value pairs, in order. Names may repeat.</summary>
public sealed class UrlSearchParams : IEnumerable<KeyValuePair<string, string>>
{
    public UrlSearchParams() => WorkerApi.NotExecutable();
    /// <summary>Parses a query string, with or without its leading <c>?</c>.</summary>
    public UrlSearchParams(string init) => WorkerApi.NotExecutable();
    public UrlSearchParams(IEnumerable<KeyValuePair<string, string>> init) => WorkerApi.NotExecutable();

    public int Size => WorkerApi.NotExecutable<int>();

    public void Append(string name, string value) => WorkerApi.NotExecutable();
    /// <summary>Removes every pair with this name, or only those that also have this value.</summary>
    public void Delete(string name, string? value = null) => WorkerApi.NotExecutable();
    /// <summary>The first value for this name, or null.</summary>
    public string? Get(string name) => WorkerApi.NotExecutable<string?>();
    public IReadOnlyList<string> GetAll(string name) => WorkerApi.NotExecutable<IReadOnlyList<string>>();
    public bool Has(string name, string? value = null) => WorkerApi.NotExecutable<bool>();
    /// <summary>Replaces every pair with this name by a single pair.</summary>
    public void Set(string name, string value) => WorkerApi.NotExecutable();
    public void Sort() => WorkerApi.NotExecutable();
    public IReadOnlyList<string> Keys() => WorkerApi.NotExecutable<IReadOnlyList<string>>();
    public IReadOnlyList<string> Values() => WorkerApi.NotExecutable<IReadOnlyList<string>>();
    /// <summary>Maps the parameters to a C# class or record. Each property reads every value of its name;
    /// scalar properties take the first.</summary>
    public T As<T>() => WorkerApi.NotExecutable<T>();
    /// <summary>The query string without its leading <c>?</c>.</summary>
    public override string ToString() => WorkerApi.NotExecutable<string>();

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => WorkerApi.NotExecutable<IEnumerator<KeyValuePair<string, string>>>();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
