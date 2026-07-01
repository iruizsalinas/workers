using System.Text.Json;

namespace Workers;

/// <summary>A blob value written to Analytics Engine.</summary>
public readonly record struct AnalyticsEngineBlob
{
    private AnalyticsEngineBlob(string? text, string? bodyBase64)
    {
        Text = text;
        BodyBase64 = bodyBase64;
    }

    /// <summary>The text value, when this blob is textual.</summary>
    public string? Text { get; }

    /// <summary>The base64-encoded binary value, when this blob is binary.</summary>
    public string? BodyBase64 { get; }

    /// <summary>Creates a textual blob value.</summary>
    public static AnalyticsEngineBlob FromText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new AnalyticsEngineBlob(value, bodyBase64: null);
    }

    /// <summary>Creates a binary blob value.</summary>
    public static AnalyticsEngineBlob FromBytes(ReadOnlySpan<byte> value) =>
        new(text: null, Convert.ToBase64String(value));
}

/// <summary>A single Analytics Engine data point.</summary>
public sealed record AnalyticsEngineDataPoint(
    IReadOnlyList<string> Indexes,
    IReadOnlyList<double> Doubles,
    IReadOnlyList<AnalyticsEngineBlob> Blobs)
{
    /// <summary>Creates an empty data point builder.</summary>
    public static AnalyticsEngineDataPointBuilder Create() => new();
}

/// <summary>Builds Analytics Engine data points.</summary>
public sealed class AnalyticsEngineDataPointBuilder
{
    private readonly List<string> _indexes = [];
    private readonly List<double> _doubles = [];
    private readonly List<AnalyticsEngineBlob> _blobs = [];

    /// <summary>Replaces the data point indexes.</summary>
    public AnalyticsEngineDataPointBuilder Indexes(params string[] indexes)
    {
        ArgumentNullException.ThrowIfNull(indexes);
        _indexes.Clear();
        _indexes.AddRange(indexes.Select(RequireValue));
        return this;
    }

    /// <summary>Adds a numeric value.</summary>
    public AnalyticsEngineDataPointBuilder AddDouble(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Analytics Engine doubles must be finite.");

        _doubles.Add(value);
        return this;
    }

    /// <summary>Replaces the numeric values.</summary>
    public AnalyticsEngineDataPointBuilder Doubles(params double[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _doubles.Clear();
        foreach (var value in values)
            AddDouble(value);

        return this;
    }

    /// <summary>Adds a textual blob value.</summary>
    public AnalyticsEngineDataPointBuilder AddBlob(string value)
    {
        _blobs.Add(AnalyticsEngineBlob.FromText(value));
        return this;
    }

    /// <summary>Adds a binary blob value.</summary>
    public AnalyticsEngineDataPointBuilder AddBlob(ReadOnlySpan<byte> value)
    {
        _blobs.Add(AnalyticsEngineBlob.FromBytes(value));
        return this;
    }

    /// <summary>Builds the data point.</summary>
    public AnalyticsEngineDataPoint Build() =>
        new(_indexes.ToArray(), _doubles.ToArray(), _blobs.ToArray());

    private static string RequireValue(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }
}

internal sealed class AnalyticsEngineDatasetBinding : IAnalyticsEngineDataset
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public AnalyticsEngineDatasetBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public Task WriteDataPointAsync(AnalyticsEngineDataPoint dataPoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataPoint);

        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            "analytics.writeDataPoint",
            JsonSerializer.Serialize(dataPoint, JsonOptions));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }
}
