namespace Workers;

public interface IAnalyticsEngineDataset : IBinding
{
    void WriteDataPoint(AnalyticsEngineDataPoint dataPoint);
}

public sealed record AnalyticsEngineDataPoint(IReadOnlyList<string> Indexes, IReadOnlyList<double> Doubles, IReadOnlyList<string> Blobs);
public sealed class AnalyticsEngineDataPointBuilder;
public readonly record struct AnalyticsEngineBlob(string? Text, string? BodyBase64);
