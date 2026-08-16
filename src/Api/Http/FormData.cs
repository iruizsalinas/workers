namespace Workers;

public sealed class FormData : IEnumerable<KeyValuePair<string, FormEntry>>
{
    public IEnumerator<KeyValuePair<string, FormEntry>> GetEnumerator() => WorkerApi.NotExecutable<IEnumerator<KeyValuePair<string, FormEntry>>>();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
public sealed class FormEntry
{
    public string? Text => WorkerApi.NotExecutable<string?>();
    public FormFile? File => WorkerApi.NotExecutable<FormFile?>();
}
public sealed class FormField
{
    public string Value => WorkerApi.NotExecutable<string>();
}

public sealed class FormFile
{
    public string FileName => WorkerApi.NotExecutable<string>();
    public string ContentType => WorkerApi.NotExecutable<string>();
    public Body Body => WorkerApi.NotExecutable<Body>();
    public long Size => WorkerApi.NotExecutable<long>();
    public long LastModified => WorkerApi.NotExecutable<long>();
    public Task<byte[]> SliceBytesAsync(int start, int end, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<byte[]>>();
}
