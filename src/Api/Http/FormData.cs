namespace Workers;

/// <summary>Form fields, as parsed from <c>multipart/form-data</c> or <c>application/x-www-form-urlencoded</c>, or built for a request body.</summary>
public sealed class FormData : IEnumerable<KeyValuePair<string, FormDataEntryValue>>
{
    public FormData() => WorkerApi.NotExecutable();

    public void Append(string name, string value) => WorkerApi.NotExecutable();
    public void Append(string name, Blob value, string? filename = null) => WorkerApi.NotExecutable();
    public void Set(string name, string value) => WorkerApi.NotExecutable();
    public void Set(string name, Blob value, string? filename = null) => WorkerApi.NotExecutable();
    public void Delete(string name) => WorkerApi.NotExecutable();
    /// <summary>The first value for this name, or null.</summary>
    public FormDataEntryValue? Get(string name) => WorkerApi.NotExecutable<FormDataEntryValue?>();
    public IReadOnlyList<FormDataEntryValue> GetAll(string name) => WorkerApi.NotExecutable<IReadOnlyList<FormDataEntryValue>>();
    public bool Has(string name) => WorkerApi.NotExecutable<bool>();

    public IEnumerator<KeyValuePair<string, FormDataEntryValue>> GetEnumerator() =>
        WorkerApi.NotExecutable<IEnumerator<KeyValuePair<string, FormDataEntryValue>>>();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>A form value: text, or an uploaded file.</summary>
public sealed class FormDataEntryValue
{
    private FormDataEntryValue() { }

    /// <summary>The value when it is text, otherwise null.</summary>
    public string? Text => WorkerApi.NotExecutable<string?>();
    /// <summary>The value when it is a file, otherwise null.</summary>
    public File? File => WorkerApi.NotExecutable<File?>();
}

/// <summary>Immutable binary data with a media type.</summary>
public class Blob
{
    private protected Blob() { }
    public Blob(string text, string? type = null) => WorkerApi.NotExecutable();
    public Blob(ReadOnlyMemory<byte> bytes, string? type = null) => WorkerApi.NotExecutable();

    public long Size => WorkerApi.NotExecutable<long>();
    /// <summary>The media type, or empty.</summary>
    public string Type => WorkerApi.NotExecutable<string>();

    public Blob Slice(long start = 0, long? end = null, string? contentType = null) => WorkerApi.NotExecutable<Blob>();
    public Task<string> TextAsync() => WorkerApi.NotExecutable<Task<string>>();
    public Task<byte[]> BytesAsync() => WorkerApi.NotExecutable<Task<byte[]>>();
    public ReadableStream Stream() => WorkerApi.NotExecutable<ReadableStream>();
}

/// <summary>A named <see cref="Blob"/>, such as an uploaded file.</summary>
public sealed class File : Blob
{
    public File(string text, string name, string? type = null) => WorkerApi.NotExecutable();
    public File(ReadOnlyMemory<byte> bytes, string name, string? type = null) => WorkerApi.NotExecutable();

    public string Name => WorkerApi.NotExecutable<string>();
    /// <summary>Milliseconds since the Unix epoch.</summary>
    public double LastModified => WorkerApi.NotExecutable<double>();
}
