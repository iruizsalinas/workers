using System.Text.Json.Serialization;

namespace Workers.Interop;

/// <summary>A JSON-friendly inbound email message shape for JavaScript/.NET interop.</summary>
internal sealed class ForwardableEmailMessageEnvelope
{
    /// <summary>Creates an inbound email message envelope.</summary>
    [JsonConstructor]
    public ForwardableEmailMessageEnvelope(
        string invocationId,
        string handle,
        string from,
        string to,
        IReadOnlyList<Header> headers,
        long rawSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);

        InvocationId = invocationId;
        Handle = handle;
        From = from;
        To = to;
        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
        RawSize = rawSize;
    }

    /// <summary>The live invocation id used by platform binding proxies.</summary>
    public string InvocationId { get; }

    /// <summary>The JavaScript-side email message handle.</summary>
    public string Handle { get; }

    /// <summary>The envelope sender.</summary>
    public string From { get; }

    /// <summary>The envelope recipient.</summary>
    public string To { get; }

    /// <summary>The message headers.</summary>
    public IReadOnlyList<Header> Headers { get; }

    /// <summary>The raw message size reported by the Workers runtime.</summary>
    public long RawSize { get; }

    /// <summary>Converts the envelope into an inbound email message.</summary>
    public ForwardableEmailMessage ToMessage() =>
        new(
            InvocationId,
            Handle,
            From,
            To,
            global::Workers.Headers.From(Headers.Select(static header => new KeyValuePair<string, string>(header.Name, header.Value))),
            RawSize,
            BindingDispatcher.Current);
}
