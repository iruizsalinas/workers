namespace Workers;

/// <summary>JavaScript JSON conversion. Typed reads map properties to C# classes and records.</summary>
public static class Json
{
    public static JsonValue Parse(string text) => WorkerApi.NotExecutable<JsonValue>();
    public static T? Parse<T>(string text) => WorkerApi.NotExecutable<T?>();
    public static string Stringify<T>(T value) => WorkerApi.NotExecutable<string>();
}

/// <summary>A JavaScript value obtained from JSON. Missing properties are undefined.</summary>
public sealed class JsonValue
{
    private JsonValue() { }

    /// <summary>Treats a JSON-serializable value, such as an anonymous object, as a JSON value.</summary>
    public static JsonValue From<T>(T value) => WorkerApi.NotExecutable<JsonValue>();
    public JsonValue this[string name] => WorkerApi.NotExecutable<JsonValue>();
    public JsonValue this[int index] => WorkerApi.NotExecutable<JsonValue>();
    public bool IsNull => WorkerApi.NotExecutable<bool>();
    public bool IsUndefined => WorkerApi.NotExecutable<bool>();
    public int Length => WorkerApi.NotExecutable<int>();
    public string AsString() => WorkerApi.NotExecutable<string>();
    public double AsNumber() => WorkerApi.NotExecutable<double>();
    public bool AsBoolean() => WorkerApi.NotExecutable<bool>();
    public bool Has(string name) => WorkerApi.NotExecutable<bool>();
    public JsonValue[] Values() => WorkerApi.NotExecutable<JsonValue[]>();
    public T? As<T>() => WorkerApi.NotExecutable<T?>();
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonIgnoreAttribute : Attribute;
