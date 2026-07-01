using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Workers;

/// <summary>Parsed URL query parameters from a Worker request URL.</summary>
public sealed class QueryParameters
{
    private readonly ReadOnlyCollection<QueryParameter> _entries;

    private QueryParameters(List<QueryParameter> entries)
    {
        _entries = entries.AsReadOnly();
    }

    /// <summary>All query parameters in URL order.</summary>
    public IReadOnlyList<QueryParameter> Entries => _entries;

    /// <summary>Returns true when at least one parameter with the given name exists.</summary>
    public bool Contains(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _entries.Any(entry => string.Equals(entry.Name, name, StringComparison.Ordinal));
    }

    /// <summary>Gets the first value for a parameter name, or null when absent.</summary>
    public string? Get(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _entries.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.Ordinal))?.Value;
    }

    /// <summary>Tries to get the first value for a parameter name.</summary>
    public bool TryGet(string name, [NotNullWhen(true)] out string? value)
    {
        value = Get(name);
        return value is not null;
    }

    /// <summary>Gets the first value for a parameter name, throwing when the parameter is missing.</summary>
    public string GetRequired(string name) =>
        Get(name) ?? throw new WorkersException($"Query parameter '{name}' is not defined.");

    /// <summary>Gets all values for a parameter name in URL order.</summary>
    public IReadOnlyList<string> GetAll(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _entries
            .Where(entry => string.Equals(entry.Name, name, StringComparison.Ordinal))
            .Select(static entry => entry.Value)
            .ToArray();
    }

    /// <summary>Deserializes query parameters into a typed object.</summary>
    public T As<T>(JsonSerializerOptions? options = null) =>
        QueryObject.Deserialize<T>(_entries, options, "Query parameters");

    internal static QueryParameters Parse(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        var query = url.Query;
        var entries = new List<QueryParameter>();
        if (query.Length <= 1)
            return new QueryParameters(entries);

        foreach (var pair in query[1..].Split('&'))
        {
            if (pair.Length == 0)
                continue;

            var separator = pair.IndexOf('=');
            var name = separator < 0 ? pair : pair[..separator];
            var value = separator < 0 ? "" : pair[(separator + 1)..];
            entries.Add(new QueryParameter(Decode(name), Decode(value)));
        }

        return new QueryParameters(entries);
    }

    private static string Decode(string value) =>
        WebUtility.UrlDecode(value.Replace('+', ' ')) ?? "";
}

/// <summary>A single decoded URL query parameter.</summary>
public sealed record QueryParameter(string Name, string Value);

internal static class QueryObject
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new(JsonSerializerDefaults.Web);

    public static T Deserialize<T>(
        IReadOnlyList<QueryParameter> entries,
        JsonSerializerOptions? options,
        string sourceName)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        options ??= DefaultJsonOptions;

        try
        {
            var value = CreateObject(typeof(T), entries, options);
            return value.Deserialize<T>(options)
                ?? throw new WorkersException($"{sourceName} could not be deserialized as '{typeof(T).FullName}'.");
        }
        catch (WorkersException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException or FormatException or OverflowException)
        {
            throw new WorkersException($"{sourceName} could not be deserialized as '{typeof(T).FullName}'.", ex);
        }
    }

    private static JsonObject CreateObject(
        Type targetType,
        IReadOnlyList<QueryParameter> entries,
        JsonSerializerOptions options)
    {
        var targets = GetTargets(targetType, options);
        var values = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!targets.TryGetValue(entry.Name, out var target))
                continue;

            if (!values.TryGetValue(target.JsonName, out var existing))
            {
                existing = [];
                values[target.JsonName] = existing;
            }

            existing.Add(entry.Value);
        }

        var result = new JsonObject();
        foreach (var (jsonName, rawValues) in values)
        {
            var target = targets[jsonName];
            result[jsonName] = CreateValue(rawValues, target.Type);
        }

        return result;
    }

    private static Dictionary<string, QueryTarget> GetTargets(Type type, JsonSerializerOptions options)
    {
        var comparer = options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var targets = new Dictionary<string, QueryTarget>(comparer);
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetMethod is null || property.GetIndexParameters().Length != 0)
                continue;

            var jsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? options.PropertyNamingPolicy?.ConvertName(property.Name)
                ?? property.Name;
            var target = new QueryTarget(jsonName, property.PropertyType);
            targets.TryAdd(jsonName, target);
            targets.TryAdd(property.Name, target);
        }

        return targets;
    }

    private static JsonNode? CreateValue(IReadOnlyList<string> values, Type targetType)
    {
        if (TryGetCollectionElementType(targetType, out var elementType))
        {
            var array = new JsonArray();
            foreach (var value in values)
                array.Add(CreateScalar(value, elementType));

            return array;
        }

        return CreateScalar(values[0], targetType);
    }

    private static JsonNode? CreateScalar(string value, Type targetType)
    {
        var nullableType = Nullable.GetUnderlyingType(targetType);
        if (nullableType is not null)
        {
            if (value.Length == 0)
                return null;

            targetType = nullableType;
        }

        if (targetType == typeof(string))
            return JsonValue.Create(value);

        if (targetType == typeof(bool))
            return JsonValue.Create(ParseBoolean(value));

        if (targetType.IsEnum)
        {
            var parsed = long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric)
                ? Enum.ToObject(targetType, numeric)
                : Enum.Parse(targetType, value, ignoreCase: true);
            return JsonValue.Create(Convert.ChangeType(parsed, Enum.GetUnderlyingType(targetType), CultureInfo.InvariantCulture));
        }

        if (targetType == typeof(Guid) || targetType == typeof(DateTime) || targetType == typeof(DateTimeOffset))
            return JsonValue.Create(value);

        if (targetType.IsPrimitive || targetType == typeof(decimal))
            return JsonValue.Create(Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture));

        return JsonValue.Create(value);
    }

    private static bool ParseBoolean(string value) =>
        value.ToLowerInvariant() switch
        {
            "" or "true" or "1" or "on" or "yes" => true,
            "false" or "0" or "off" or "no" => false,
            _ => throw new FormatException($"'{value}' is not a valid boolean query value.")
        };

    private static bool TryGetCollectionElementType(Type type, out Type elementType)
    {
        if (type == typeof(string))
        {
            elementType = typeof(string);
            return false;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        var enumerableType = type
            .GetInterfaces()
            .Prepend(type)
            .FirstOrDefault(static candidate =>
                candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (enumerableType is not null)
        {
            elementType = enumerableType.GetGenericArguments()[0];
            return true;
        }

        elementType = typeof(string);
        return false;
    }

    private sealed record QueryTarget(string JsonName, Type Type);
}
