namespace Workers;

public interface IAnalyticsEngineDataset : IBinding
{
    void WriteDataPoint(AnalyticsEngineDataPoint dataPoint);
}

public sealed record AnalyticsEngineDataPoint(IReadOnlyList<string> Indexes, IReadOnlyList<double> Doubles, IReadOnlyList<string> Blobs);
