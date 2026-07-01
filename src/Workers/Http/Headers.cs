using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Workers;

/// <summary>A case-insensitive HTTP header collection with Fetch-compatible append and set semantics.</summary>
public sealed class Headers : IEnumerable<KeyValuePair<string, string>>
{
    private readonly Dictionary<string, HeaderEntry> _headers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The number of distinct header names.</summary>
    public int Count => _headers.Count;

    /// <summary>Gets a comma-joined header value by name.</summary>
    public string? Get(string name)
    {
        ValidateName(name);
        return _headers.TryGetValue(name, out var entry) ? string.Join(", ", entry.Values) : null;
    }

    /// <summary>Tries to get a comma-joined header value by name.</summary>
    public bool TryGet(string name, [NotNullWhen(true)] out string? value)
    {
        value = Get(name);
        return value is not null;
    }

    /// <summary>Gets a comma-joined header value by name, throwing when the header is missing.</summary>
    public string GetRequired(string name) =>
        Get(name) ?? throw new WorkersException($"Header '{name}' is not defined.");

    /// <summary>Gets all values for a header name.</summary>
    public IReadOnlyList<string> GetAll(string name)
    {
        ValidateName(name);
        return _headers.TryGetValue(name, out var entry) ? entry.Values.ToArray() : [];
    }

    /// <summary>Returns true when a header exists.</summary>
    public bool Contains(string name)
    {
        ValidateName(name);
        return _headers.ContainsKey(name);
    }

    /// <summary>Sets a header, replacing any existing values.</summary>
    public Headers Set(string name, string value)
    {
        Validate(name, value);
        _headers[name] = new HeaderEntry(name, [value]);
        return this;
    }

    /// <summary>Appends a header value without replacing existing values.</summary>
    public Headers Append(string name, string value)
    {
        Validate(name, value);

        if (_headers.TryGetValue(name, out var entry))
        {
            entry.Values.Add(value);
            return this;
        }

        _headers[name] = new HeaderEntry(name, [value]);
        return this;
    }

    /// <summary>Deletes a header by name.</summary>
    public bool Delete(string name)
    {
        ValidateName(name);
        return _headers.Remove(name);
    }

    /// <summary>Creates an independent copy of this header collection.</summary>
    public Headers Clone() => From(this);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        foreach (var entry in _headers.Values)
        {
            foreach (var value in entry.Values)
                yield return new KeyValuePair<string, string>(entry.Name, value);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Creates a header collection from name-value pairs.</summary>
    public static Headers From(IEnumerable<KeyValuePair<string, string>> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var result = new Headers();
        foreach (var (name, value) in headers)
            result.Append(name, value);

        return result;
    }

    /// <summary>Creates a header collection from name-value tuples.</summary>
    public static Headers Create(params (string Name, string Value)[] headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var result = new Headers();
        foreach (var (name, value) in headers)
            result.Append(name, value);

        return result;
    }

    private static void Validate(string name, string value)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(value);

        if (value.Any(static c => c is '\0' or '\r' or '\n'))
            throw new ArgumentException("Header values cannot contain null, CR, or LF characters.", nameof(value));
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name.Any(static c => c <= 32 || c >= 127 || "()<>@,;:\\\"/[]?={}".Contains(c, StringComparison.Ordinal)))
            throw new ArgumentException("Header names must be valid HTTP tokens.", nameof(name));
    }

    private sealed record HeaderEntry(string Name, List<string> Values);
}
