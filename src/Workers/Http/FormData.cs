using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Workers;

/// <summary>Parsed form data from a Worker request body.</summary>
public sealed class FormData
{
    private readonly ReadOnlyCollection<FormEntry> _entries;

    private FormData(List<FormEntry> entries)
    {
        _entries = entries.AsReadOnly();
    }

    /// <summary>All form entries in request order.</summary>
    public IReadOnlyList<FormEntry> Entries => _entries;

    /// <summary>Gets the first entry for a field name, or null when absent.</summary>
    public FormEntry? Get(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _entries.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.Ordinal));
    }

    /// <summary>Returns true when at least one entry with the given field name exists.</summary>
    public bool Contains(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _entries.Any(entry => string.Equals(entry.Name, name, StringComparison.Ordinal));
    }

    /// <summary>Tries to get the first entry for a field name.</summary>
    public bool TryGet(string name, [NotNullWhen(true)] out FormEntry? entry)
    {
        entry = Get(name);
        return entry is not null;
    }

    /// <summary>Gets all entries for a field name in request order.</summary>
    public IReadOnlyList<FormEntry> GetAll(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _entries
            .Where(entry => string.Equals(entry.Name, name, StringComparison.Ordinal))
            .ToArray();
    }

    /// <summary>Gets the first text field value for a field name, or null when absent or not a text field.</summary>
    public string? GetField(string name) => Get(name) is FormField field ? field.Value : null;

    /// <summary>Tries to get the first text field value for a field name.</summary>
    public bool TryGetField(string name, [NotNullWhen(true)] out string? value)
    {
        value = GetField(name);
        return value is not null;
    }

    /// <summary>Gets the first text field value for a field name, throwing when missing or not a text field.</summary>
    public string GetRequiredField(string name) =>
        GetField(name) ?? throw new WorkersException($"Form field '{name}' is not a text field or is not defined.");

    /// <summary>Gets the first file entry for a field name, or null when absent or not a file.</summary>
    public FormFile? GetFile(string name) => Get(name) as FormFile;

    /// <summary>Tries to get the first file entry for a field name.</summary>
    public bool TryGetFile(string name, [NotNullWhen(true)] out FormFile? file)
    {
        file = GetFile(name);
        return file is not null;
    }

    /// <summary>Gets the first file entry for a field name, throwing when missing or not a file.</summary>
    public FormFile GetRequiredFile(string name) =>
        GetFile(name) ?? throw new WorkersException($"Form field '{name}' is not a file or is not defined.");

    /// <summary>Deserializes text form fields into a typed object.</summary>
    public T As<T>(JsonSerializerOptions? options = null) =>
        QueryObject.Deserialize<T>(TextFields(), options, "Form data");

    internal static FormData Parse(Body body, string contentType)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var mediaType = FormContentType.Parse(contentType);
        return mediaType.Type switch
        {
            "application/x-www-form-urlencoded" => ParseUrlEncoded(body.AsText()),
            "multipart/form-data" => ParseMultipart(body.InternalBytes.Span, mediaType.RequireBoundary()),
            _ => throw new WorkersException($"Unsupported form content type '{mediaType.Type}'.")
        };
    }

    private static FormData ParseUrlEncoded(string value)
    {
        var entries = new List<FormEntry>();
        if (value.Length == 0)
            return new FormData(entries);

        foreach (var pair in value.Split('&'))
        {
            if (pair.Length == 0)
                continue;

            var separator = pair.IndexOf('=');
            var name = separator < 0 ? pair : pair[..separator];
            var fieldValue = separator < 0 ? "" : pair[(separator + 1)..];
            entries.Add(new FormField(DecodeUrlEncoded(name), DecodeUrlEncoded(fieldValue)));
        }

        return new FormData(entries);
    }

    private static string DecodeUrlEncoded(string value) =>
        WebUtility.UrlDecode(value.Replace('+', ' ')) ?? "";

    private QueryParameter[] TextFields() =>
        _entries
            .OfType<FormField>()
            .Select(static field => new QueryParameter(field.Name, field.Value))
            .ToArray();

    private static FormData ParseMultipart(ReadOnlySpan<byte> body, string boundary)
    {
        var delimiter = Encoding.ASCII.GetBytes("--" + boundary);
        var crlfDelimiter = Encoding.ASCII.GetBytes("\r\n--" + boundary);
        var lfDelimiter = Encoding.ASCII.GetBytes("\n--" + boundary);
        var entries = new List<FormEntry>();

        var current = body.IndexOf(delimiter);
        if (current < 0)
            throw new WorkersException("Multipart body does not contain the declared boundary.");

        while (true)
        {
            var afterDelimiter = current + delimiter.Length;
            if (body[afterDelimiter..].StartsWith("--"u8))
                return new FormData(entries);

            var lineBreakLength = LineBreakLength(body[afterDelimiter..]);
            if (lineBreakLength == 0)
                throw new WorkersException("Multipart boundary is not followed by a line break.");

            var partStart = afterDelimiter + lineBreakLength;
            var next = body[partStart..].IndexOf(crlfDelimiter);
            var prefixLength = 2;
            if (next < 0)
            {
                next = body[partStart..].IndexOf(lfDelimiter);
                prefixLength = 1;
            }

            if (next < 0)
                throw new WorkersException("Multipart body is missing a closing boundary.");

            var partEnd = partStart + next;
            entries.Add(ParsePart(body[partStart..partEnd]));
            current = partEnd + prefixLength;
        }
    }

    private static FormEntry ParsePart(ReadOnlySpan<byte> part)
    {
        var separator = part.IndexOf("\r\n\r\n"u8);
        var separatorLength = 4;
        if (separator < 0)
        {
            separator = part.IndexOf("\n\n"u8);
            separatorLength = 2;
        }

        if (separator < 0)
            throw new WorkersException("Multipart part is missing a header terminator.");

        var headers = ParsePartHeaders(Encoding.ASCII.GetString(part[..separator]));
        if (!headers.TryGetValue("content-disposition", out var disposition))
            throw new WorkersException("Multipart part is missing Content-Disposition.");

        var dispositionParameters = ParseHeaderParameters(disposition);
        if (!dispositionParameters.TryGetValue("name", out var name) || name.Length == 0)
            throw new WorkersException("Multipart Content-Disposition is missing a name parameter.");

        var content = part[(separator + separatorLength)..];
        if (dispositionParameters.TryGetValue("filename", out var fileName))
        {
            var contentType = headers.TryGetValue("content-type", out var parsedContentType)
                ? parsedContentType
                : "application/octet-stream";
            return new FormFile(name, fileName, Body.FromBytes(content, contentType));
        }

        return new FormField(name, Encoding.UTF8.GetString(content));
    }

    private static Dictionary<string, string> ParsePartHeaders(string value)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in value.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.Length == 0)
                continue;

            var separator = line.IndexOf(':');
            if (separator <= 0)
                throw new WorkersException("Multipart part contains an invalid header.");

            headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        return headers;
    }

    private static Dictionary<string, string> ParseHeaderParameters(string value)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = SplitHeaderSegments(value);
        for (var index = 1; index < parts.Length; index++)
        {
            var part = parts[index].Trim();
            var separator = part.IndexOf('=');
            if (separator <= 0)
                continue;

            var name = part[..separator].Trim();
            var rawValue = part[(separator + 1)..].Trim();
            parameters[name] = Unquote(rawValue);
        }

        return parameters;
    }

    private static string[] SplitHeaderSegments(string value)
    {
        var segments = new List<string>();
        var segmentStart = 0;
        var inQuotes = false;
        var escaped = false;

        for (var index = 0; index < value.Length; index++)
        {
            var c = value[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && inQuotes)
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ';' && !inQuotes)
            {
                segments.Add(value[segmentStart..index]);
                segmentStart = index + 1;
            }
        }

        segments.Add(value[segmentStart..]);
        return segments.ToArray();
    }

    private static int LineBreakLength(ReadOnlySpan<byte> value)
    {
        if (value.StartsWith("\r\n"u8))
            return 2;

        return value.StartsWith("\n"u8) ? 1 : 0;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            var result = new StringBuilder(value.Length - 2);
            var escaped = false;
            foreach (var c in value[1..^1])
            {
                if (escaped)
                {
                    result.Append(c);
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                result.Append(c);
            }

            if (escaped)
                result.Append('\\');

            return result.ToString();
        }

        return value;
    }

    private sealed record FormContentType(string Type, IReadOnlyDictionary<string, string> Parameters)
    {
        public static FormContentType Parse(string value)
        {
            var parts = SplitHeaderSegments(value);
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 1; index < parts.Length; index++)
            {
                var part = parts[index].Trim();
                var separator = part.IndexOf('=');
                if (separator <= 0)
                    continue;

                parameters[part[..separator].Trim()] = Unquote(part[(separator + 1)..].Trim());
            }

            return new FormContentType(parts[0].Trim().ToLowerInvariant(), parameters);
        }

        public string RequireBoundary()
        {
            if (Parameters.TryGetValue("boundary", out var boundary) && boundary.Length > 0)
                return boundary;

            throw new WorkersException("Multipart form data requires a boundary parameter.");
        }
    }
}

/// <summary>A parsed form-data entry.</summary>
public abstract class FormEntry
{
    private protected FormEntry(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    /// <summary>The form field name.</summary>
    public string Name { get; }
}

/// <summary>A parsed text form field.</summary>
public sealed class FormField : FormEntry
{
    internal FormField(string name, string value)
        : base(name)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>The UTF-8 decoded field value.</summary>
    public string Value { get; }
}

/// <summary>A parsed file upload form field.</summary>
public sealed class FormFile : FormEntry
{
    internal FormFile(string name, string fileName, Body body)
        : base(name)
    {
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        Body = body ?? throw new ArgumentNullException(nameof(body));
    }

    /// <summary>The uploaded file name from the form part.</summary>
    public string FileName { get; }

    /// <summary>The uploaded file body.</summary>
    public Body Body { get; }

    /// <summary>The uploaded file content type.</summary>
    public string? ContentType => Body.ContentType;

    /// <summary>The uploaded file bytes.</summary>
    public ReadOnlyMemory<byte> Bytes => Body.Bytes;

    /// <summary>Decodes the uploaded file as UTF-8 text.</summary>
    public string Text() => Body.AsText();
}
