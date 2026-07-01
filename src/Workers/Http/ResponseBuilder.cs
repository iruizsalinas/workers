using System.Text.Json;

namespace Workers;

/// <summary>Fluent builder for Worker responses.</summary>
public sealed class ResponseBuilder
{
    private int _status;
    private string? _statusText;
    private Headers _headers = new();
    private Body _body = Body.Empty;
    private bool _contentTypeFromBody;
    private JsonElement? _cf;
    private ResponseEncodeBody _encodeBody;

    internal ResponseBuilder(int status)
    {
        _status = status;
    }

    /// <summary>Sets the response status code.</summary>
    public ResponseBuilder WithStatus(int status)
    {
        _status = status;
        return this;
    }

    /// <summary>Sets the response status code and status text.</summary>
    public ResponseBuilder WithStatus(int status, string? statusText)
    {
        _status = status;
        _statusText = statusText;
        return this;
    }

    /// <summary>Sets the response status text.</summary>
    public ResponseBuilder WithStatusText(string? statusText)
    {
        _statusText = statusText;
        return this;
    }

    /// <summary>Sets a response header, replacing any existing values.</summary>
    public ResponseBuilder WithHeader(string name, string value)
    {
        _headers.Set(name, value);
        if (string.Equals(name, "content-type", StringComparison.OrdinalIgnoreCase))
            _contentTypeFromBody = false;

        return this;
    }

    /// <summary>Appends a response header value.</summary>
    public ResponseBuilder AppendHeader(string name, string value)
    {
        _headers.Append(name, value);
        if (string.Equals(name, "content-type", StringComparison.OrdinalIgnoreCase))
            _contentTypeFromBody = false;

        return this;
    }

    /// <summary>Sets the response body.</summary>
    public ResponseBuilder WithBody(Body body)
    {
        ArgumentNullException.ThrowIfNull(body);
        _body = body;
        ApplyBodyContentType();
        return this;
    }

    /// <summary>Sets a UTF-8 text response body.</summary>
    public ResponseBuilder WithText(string body, string contentType = "text/plain; charset=utf-8") =>
        WithBody(Body.Text(body, contentType));

    /// <summary>Sets an HTML response body.</summary>
    public ResponseBuilder WithHtml(string body) =>
        WithBody(Body.Text(body, "text/html; charset=utf-8"));

    /// <summary>Sets a JSON response body.</summary>
    public ResponseBuilder WithJson<T>(T body, JsonSerializerOptions? options = null) =>
        WithBody(Body.Json(body, options));

    /// <summary>Sets a binary response body.</summary>
    public ResponseBuilder WithBytes(ReadOnlySpan<byte> body, string contentType = "application/octet-stream") =>
        WithBody(Body.FromBytes(body, contentType));

    /// <summary>Attaches Cloudflare response metadata or options.</summary>
    public ResponseBuilder WithCf<T>(T cf, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(cf);
        _cf = JsonSerializer.SerializeToElement(cf, options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return this;
    }

    /// <summary>Sets how the Workers runtime encodes the response body before sending it.</summary>
    public ResponseBuilder WithEncodeBody(ResponseEncodeBody encodeBody)
    {
        _encodeBody = encodeBody;
        return this;
    }

    /// <summary>Applies CORS headers to the response being built.</summary>
    public ResponseBuilder WithCors(Cors cors)
    {
        ArgumentNullException.ThrowIfNull(cors);
        cors.ApplyTo(_headers);
        return this;
    }

    /// <summary>Builds the response.</summary>
    public Response Build() =>
        new(_status, Headers.From(_headers), _body, webSocket: null, _cf, _encodeBody, _statusText);

    private void ApplyBodyContentType()
    {
        if (_body.ContentType is null)
        {
            if (_contentTypeFromBody)
            {
                _headers.Delete("content-type");
                _contentTypeFromBody = false;
            }

            return;
        }

        if (!_headers.Contains("content-type") || _contentTypeFromBody)
        {
            _headers.Set("content-type", _body.ContentType);
            _contentTypeFromBody = true;
        }
    }
}
