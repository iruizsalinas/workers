using System.Text.Json;

namespace Workers;

/// <summary>Fluent builder for Worker requests.</summary>
public sealed class RequestBuilder
{
    private Uri _url;
    private string _method = "GET";
    private Headers _headers = new();
    private Body _body = Body.Empty;
    private bool _contentTypeFromBody;

    internal RequestBuilder(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        _url = new Uri(url, UriKind.Absolute);
    }

    /// <summary>Sets the absolute request URL.</summary>
    public RequestBuilder WithUrl(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        _url = new Uri(url, UriKind.Absolute);
        return this;
    }

    /// <summary>Sets the absolute URL path while preserving the query string.</summary>
    public RequestBuilder WithPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!path.StartsWith('/'))
            throw new ArgumentException("URL paths must start with '/'.", nameof(path));

        _url = new UriBuilder(_url) { Path = path }.Uri;
        return this;
    }

    /// <summary>Sets the query string while preserving the path.</summary>
    public RequestBuilder WithQuery(string? query)
    {
        _url = new UriBuilder(_url) { Query = NormalizeQuery(query) }.Uri;
        return this;
    }

    /// <summary>Sets a query parameter, replacing existing values for the same name.</summary>
    public RequestBuilder WithQueryParameter(string name, string value)
    {
        _url = Request.SetQueryParameter(_url, name, value);
        return this;
    }

    /// <summary>Appends a query parameter value.</summary>
    public RequestBuilder AppendQueryParameter(string name, string value)
    {
        _url = Request.AppendQueryParameter(_url, name, value);
        return this;
    }

    /// <summary>Removes all query parameter values for a name.</summary>
    public RequestBuilder RemoveQueryParameter(string name)
    {
        _url = Request.RemoveQueryParameter(_url, name);
        return this;
    }

    /// <summary>Sets the path and query string while preserving the origin.</summary>
    public RequestBuilder WithPathAndQuery(string pathAndQuery)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathAndQuery);
        if (!pathAndQuery.StartsWith('/'))
            throw new ArgumentException("URL paths must start with '/'.", nameof(pathAndQuery));

        _url = new Uri(_url, pathAndQuery);
        return this;
    }

    /// <summary>Sets the HTTP method.</summary>
    public RequestBuilder WithMethod(string method)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        _method = method.ToUpperInvariant();
        return this;
    }

    /// <summary>Sets a request header, replacing any existing values.</summary>
    public RequestBuilder WithHeader(string name, string value)
    {
        _headers.Set(name, value);
        if (string.Equals(name, "content-type", StringComparison.OrdinalIgnoreCase))
            _contentTypeFromBody = false;

        return this;
    }

    /// <summary>Appends a request header value.</summary>
    public RequestBuilder AppendHeader(string name, string value)
    {
        _headers.Append(name, value);
        if (string.Equals(name, "content-type", StringComparison.OrdinalIgnoreCase))
            _contentTypeFromBody = false;

        return this;
    }

    /// <summary>Replaces the request headers.</summary>
    public RequestBuilder WithHeaders(Headers headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        _headers = Headers.From(headers);
        _contentTypeFromBody = false;
        ApplyBodyContentType();
        return this;
    }

    /// <summary>Sets the request body.</summary>
    public RequestBuilder WithBody(Body body)
    {
        ArgumentNullException.ThrowIfNull(body);
        _body = body;
        ApplyBodyContentType();
        return this;
    }

    /// <summary>Sets a UTF-8 text request body.</summary>
    public RequestBuilder WithText(string body, string contentType = "text/plain; charset=utf-8") =>
        WithBody(Body.Text(body, contentType));

    /// <summary>Sets a JSON request body.</summary>
    public RequestBuilder WithJson<T>(T body, JsonSerializerOptions? options = null) =>
        WithBody(Body.Json(body, options));

    /// <summary>Sets a binary request body.</summary>
    public RequestBuilder WithBytes(ReadOnlySpan<byte> body, string contentType = "application/octet-stream") =>
        WithBody(Body.FromBytes(body, contentType));

    /// <summary>Builds the request.</summary>
    public Request Build() => new(_url, _method, Headers.From(_headers), _body);

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

    private static string NormalizeQuery(string? query) =>
        string.IsNullOrEmpty(query) ? "" : query.TrimStart('?');
}
