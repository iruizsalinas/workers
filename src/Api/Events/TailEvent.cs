namespace Workers;

public sealed class TailEvent : IReadOnlyList<TailItem>
{
    public IReadOnlyList<TailItem> Events => WorkerApi.NotExecutable<IReadOnlyList<TailItem>>();
    public int Count => WorkerApi.NotExecutable<int>();

    public TailItem this[int index] => WorkerApi.NotExecutable<TailItem>();
    public IEnumerator<TailItem> GetEnumerator() => WorkerApi.NotExecutable<IEnumerator<TailItem>>();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class TailItem
{
    public string? ScriptName { get; init; }
    public string? Outcome { get; init; }
    public DateTimeOffset EventTimestamp { get; init; }
    public TailFetchEventInfo? Event { get; init; }
    public IReadOnlyList<TailLog> Logs { get; init; } = [];
    public IReadOnlyList<TailException> Exceptions { get; init; } = [];
}

public sealed class TailFetchEventInfo
{
    public TailRequest? Request { get; init; }
    public TailResponse? Response { get; init; }
}

public sealed class TailRequest
{
    public string? Url { get; init; }
    public string? Method { get; init; }
    public Headers Headers { get; init; } = new();
}

public sealed class TailResponse
{
    public int Status { get; init; }
}

public sealed class TailLog
{
    public string? Level { get; init; }
    public string? Message { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

public sealed class TailException
{
    public string? Name { get; init; }
    public string? Message { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
