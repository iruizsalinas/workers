using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workers.Interop;

/// <summary>A JSON-friendly environment shape for Worker variable bindings.</summary>
internal sealed class EnvEnvelope
{
    /// <summary>Creates an environment envelope.</summary>
    [JsonConstructor]
    public EnvEnvelope(IReadOnlyDictionary<string, JsonElement> bindings)
    {
        Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
    }

    /// <summary>JSON-compatible bindings keyed by binding name.</summary>
    public IReadOnlyDictionary<string, JsonElement> Bindings { get; }

    /// <summary>An empty environment envelope.</summary>
    public static EnvEnvelope Empty { get; } =
        new(new Dictionary<string, JsonElement>(StringComparer.Ordinal));

    /// <summary>Converts the envelope into a Worker environment.</summary>
    public Env ToEnvironment()
    {
        var environment = new Env(
            bindings: null,
            InvocationId,
            bindingDispatcher: null);

        foreach (var (name, value) in Bindings)
        {
            environment.Set(name, ToBindingValue(value));
        }

        return environment;
    }

    /// <summary>The live invocation id used by platform binding proxies.</summary>
    public string? InvocationId { get; init; }

    private static object? ToBindingValue(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.Null => null,
            JsonValueKind.Object => value.Clone(),
            JsonValueKind.Array => value.Clone(),
            _ => throw new WorkersException($"Unsupported environment binding JSON kind '{value.ValueKind}'.")
        };
}
