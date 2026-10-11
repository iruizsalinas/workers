namespace Workers;

/// <summary>The cookies a request carries in its <c>Cookie</c> header. Values are percent-decoded.</summary>
public sealed class Cookies : IEnumerable<Cookie>
{
    /// <summary>Returns the first cookie with this name; browsers send the most specific path first.</summary>
    public string? Get(string name) => WorkerApi.NotExecutable<string?>();
    public IReadOnlyList<string> GetAll(string name) => WorkerApi.NotExecutable<IReadOnlyList<string>>();
    public bool Contains(string name) => WorkerApi.NotExecutable<bool>();
    public IEnumerator<Cookie> GetEnumerator() => WorkerApi.NotExecutable<IEnumerator<Cookie>>();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed record Cookie(string Name, string Value);

/// <summary>Writes cookies to a response's headers. Mutation follows the headers' runtime guard.</summary>
public sealed class ResponseCookies
{
    private ResponseCookies() { }

    /// <summary>Appends a Set-Cookie header without replacing other cookies.</summary>
    public void Set(string name, string value, CookieOptions? options = null) => WorkerApi.NotExecutable();
    /// <summary>Appends an expired cookie. Its path, domain and prefix attributes must match the original cookie.</summary>
    public void Delete(string name, CookieOptions? options = null) => WorkerApi.NotExecutable();
}

/// <summary>
/// Attributes of a <c>Set-Cookie</c> header. The defaults are the safe choice for session cookies:
/// <c>HttpOnly</c>, <c>Secure</c>, <c>SameSite=Lax</c> and <c>Path=/</c>.
/// </summary>
public sealed class CookieOptions
{
    public string? Path { get; init; } = "/";
    public string? Domain { get; init; }
    public Date? Expires { get; init; }
    public int? MaxAgeSeconds { get; init; }
    public bool HttpOnly { get; init; } = true;
    public bool Secure { get; init; } = true;
    public SameSite SameSite { get; init; } = SameSite.Lax;
    public bool Partitioned { get; init; }
}

public enum SameSite
{
    Lax,
    Strict,
    None
}
