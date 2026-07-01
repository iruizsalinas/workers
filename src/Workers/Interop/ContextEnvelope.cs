using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workers.Interop;

/// <summary>A JSON-friendly execution context shape for JavaScript/.NET interop.</summary>
internal sealed class ContextEnvelope
{
    /// <summary>Creates an execution context envelope.</summary>
    [JsonConstructor]
    public ContextEnvelope(JsonElement? props = null)
    {
        Props = props;
    }

    /// <summary>Props supplied to the Worker execution context, when present.</summary>
    public JsonElement? Props { get; }

    /// <summary>An empty execution context envelope.</summary>
    public static ContextEnvelope Empty { get; } = new();

    /// <summary>Converts the envelope into a Worker execution context.</summary>
    public Context ToExecutionContext() => new(Props);
}
