using System.Globalization;
using System.Text.Json;

namespace Workers;

/// <summary>Cloudflare edge metadata attached to an inbound request.</summary>
public sealed class Cf
{
    private readonly JsonElement _root;

    internal Cf(JsonElement root)
    {
        if (root.ValueKind is not JsonValueKind.Object)
            throw new WorkersException("Cloudflare request metadata must be a JSON object.");

        _root = root.Clone();
    }

    /// <summary>The raw metadata object.</summary>
    public JsonElement Raw => _root;

    /// <summary>Information about Bot Management, when available.</summary>
    public BotManagement? BotManagement
    {
        get
        {
            var value = BotManagementRaw;
            return value is { ValueKind: JsonValueKind.Object } ? new BotManagement(value.Value) : null;
        }
    }

    /// <summary>The raw Bot Management metadata object, when available.</summary>
    public JsonElement? BotManagementRaw => GetProperty("botManagement");

    /// <summary>The verified bot category for the request, when available.</summary>
    public string? VerifiedBotCategory => GetString("verifiedBotCategory");

    /// <summary>The three-letter airport code for the Cloudflare colo that processed the request.</summary>
    public string? Colo => GetString("colo");

    /// <summary>The autonomous system number for the request IP, when available.</summary>
    public uint? Asn => GetUInt32("asn");

    /// <summary>The autonomous system organization name, when available.</summary>
    public string? AsOrganization => GetString("asOrganization");

    /// <summary>The two-letter country code for the request IP, when available.</summary>
    public string? Country => GetString("country");

    /// <summary>The HTTP protocol used by the request.</summary>
    public string? HttpProtocol => GetString("httpProtocol");

    /// <summary>The browser-requested HTTP prioritization information, when available.</summary>
    public RequestPriority? RequestPriority => global::Workers.RequestPriority.Parse(GetString("requestPriority"));

    /// <summary>The cipher used for the connection to Cloudflare.</summary>
    public string? TlsCipher => GetString("tlsCipher");

    /// <summary>TLS client certificate metadata, when available.</summary>
    public TlsClientAuth? TlsClientAuth
    {
        get
        {
            var value = GetProperty("tlsClientAuth");
            return value is { ValueKind: JsonValueKind.Object } ? new TlsClientAuth(value.Value) : null;
        }
    }

    /// <summary>The TLS version used for the connection to Cloudflare.</summary>
    public string? TlsVersion => GetString("tlsVersion");

    /// <summary>The request city, when available.</summary>
    public string? City => GetString("city");

    /// <summary>The request continent code, when available.</summary>
    public string? Continent => GetString("continent");

    /// <summary>The request latitude and longitude, when available.</summary>
    public Coordinates? Coordinates
    {
        get
        {
            var latitude = GetSingle("latitude");
            var longitude = GetSingle("longitude");
            return latitude is null || longitude is null
                ? null
                : new Coordinates(latitude.Value, longitude.Value);
        }
    }

    /// <summary>The request postal code, when available.</summary>
    public string? PostalCode => GetString("postalCode");

    /// <summary>The request metro code, when available.</summary>
    public string? MetroCode => GetString("metroCode");

    /// <summary>The request region name, when available.</summary>
    public string? Region => GetString("region");

    /// <summary>The request region code, when available.</summary>
    public string? RegionCode => GetString("regionCode");

    /// <summary>The request timezone name, when available.</summary>
    public string? TimezoneName => GetString("timezone");

    /// <summary>True when Cloudflare reports the request country as an EU country.</summary>
    public bool IsEuCountry => GetBoolean("isEUCountry") ?? false;

    /// <summary>Deserializes host metadata, when supplied by the runtime.</summary>
    public T? HostMetadata<T>(JsonSerializerOptions? options = null)
    {
        var value = GetProperty("hostMetadata");
        if (value is null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
            return default;

        return value.Value.Deserialize<T>(options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    /// <summary>Deserializes Bot Management metadata, when supplied by the runtime.</summary>
    public T? BotManagementAs<T>(JsonSerializerOptions? options = null)
    {
        var value = BotManagementRaw;
        if (value is null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
            return default;

        return value.Value.Deserialize<T>(options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private JsonElement? GetProperty(string name)
    {
        if (_root.TryGetProperty(name, out var value))
            return value;

        foreach (var property in _root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }

        return null;
    }

    private string? GetString(string name)
    {
        var value = GetProperty(name);
        return value?.ValueKind switch
        {
            JsonValueKind.String => value.Value.GetString(),
            JsonValueKind.Number => value.Value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private uint? GetUInt32(string name)
    {
        var value = GetProperty(name);
        if (value is null)
            return null;

        if (value.Value.ValueKind is JsonValueKind.Number && value.Value.TryGetUInt32(out var number))
            return number;

        return uint.TryParse(GetString(name), NumberStyles.None, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private float? GetSingle(string name)
    {
        var text = GetString(name);
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private bool? GetBoolean(string name)
    {
        var value = GetProperty(name);
        if (value is null)
            return null;

        return value.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.Value.TryGetInt32(out var number) => number != 0,
            JsonValueKind.String => value.Value.GetString() switch
            {
                "1" => true,
                "0" => false,
                { } text when bool.TryParse(text, out var parsed) => parsed,
                _ => null
            },
            _ => null
        };
    }
}

/// <summary>The request latitude and longitude reported by Cloudflare.</summary>
public readonly record struct Coordinates(float Latitude, float Longitude);

/// <summary>Browser-requested HTTP prioritization information.</summary>
public readonly record struct RequestPriority(int Weight, bool Exclusive, int Group, int GroupWeight)
{
    internal static RequestPriority? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var weight = 1;
        var exclusive = false;
        var group = 0;
        var groupWeight = 0;

        foreach (var segment in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                continue;

            switch (parts[0])
            {
                case "weight" when TryParseInt(parts[1], out var parsedWeight):
                    weight = parsedWeight;
                    break;
                case "exclusive":
                    exclusive = parts[1] == "1" || bool.TryParse(parts[1], out var parsedExclusive) && parsedExclusive;
                    break;
                case "group" when TryParseInt(parts[1], out var parsedGroup):
                    group = parsedGroup;
                    break;
                case "group-weight" when TryParseInt(parts[1], out var parsedGroupWeight):
                    groupWeight = parsedGroupWeight;
                    break;
            }
        }

        return new RequestPriority(weight, exclusive, group, groupWeight);
    }

    private static bool TryParseInt(string value, out int result) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result);
}

/// <summary>Cloudflare Bot Management metadata attached to a request.</summary>
public sealed class BotManagement
{
    private readonly JsonElement _root;

    internal BotManagement(JsonElement root)
    {
        _root = root.Clone();
    }

    /// <summary>The raw Bot Management metadata object.</summary>
    public JsonElement Raw => _root;

    /// <summary>The Cloudflare bot score for the request.</summary>
    public uint? Score => GetUInt32("score");

    /// <summary>True when the request is from a Cloudflare-verified bot.</summary>
    public bool? VerifiedBot => GetBoolean("verifiedBot");

    /// <summary>True when the request is from a known corporate proxy.</summary>
    public bool? CorporateProxy => GetBoolean("corporateProxy");

    /// <summary>True when the request is for a static resource.</summary>
    public bool? StaticResource => GetBoolean("staticResource");

    /// <summary>The JA3 TLS client fingerprint, when available.</summary>
    public string? Ja3Hash => GetString("ja3Hash");

    /// <summary>The JA4 TLS client fingerprint, when available.</summary>
    public string? Ja4 => GetString("ja4");

    /// <summary>JavaScript detection metadata, when available.</summary>
    public JsDetection? JsDetection
    {
        get
        {
            var value = GetProperty("jsDetection");
            return value is { ValueKind: JsonValueKind.Object } ? new JsDetection(value.Value) : null;
        }
    }

    /// <summary>The Bot Management detection IDs associated with the request.</summary>
    public IReadOnlyList<uint> DetectionIds
    {
        get
        {
            var value = GetProperty("detectionIds");
            if (value is not { ValueKind: JsonValueKind.Array })
                return Array.Empty<uint>();

            var ids = new List<uint>();
            foreach (var element in value.Value.EnumerateArray())
            {
                if (TryGetUInt32(element, out var id))
                    ids.Add(id);
            }

            return ids;
        }
    }

    private JsonElement? GetProperty(string name)
    {
        if (_root.TryGetProperty(name, out var value))
            return value;

        foreach (var property in _root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }

        return null;
    }

    private string? GetString(string name)
    {
        var value = GetProperty(name);
        return value?.ValueKind switch
        {
            JsonValueKind.String => value.Value.GetString(),
            JsonValueKind.Number => value.Value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private uint? GetUInt32(string name)
    {
        var value = GetProperty(name);
        return value is not null && TryGetUInt32(value.Value, out var number) ? number : null;
    }

    private bool? GetBoolean(string name)
    {
        var value = GetProperty(name);
        if (value is null)
            return null;

        return value.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.Value.TryGetInt32(out var number) => number != 0,
            JsonValueKind.String => value.Value.GetString() switch
            {
                "1" => true,
                "0" => false,
                { } text when bool.TryParse(text, out var parsed) => parsed,
                _ => null
            },
            _ => null
        };
    }

    private static bool TryGetUInt32(JsonElement value, out uint number)
    {
        if (value.ValueKind is JsonValueKind.Number && value.TryGetUInt32(out number))
            return true;

        if (value.ValueKind is JsonValueKind.String)
            return uint.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out number);

        number = 0;
        return false;
    }
}

/// <summary>Cloudflare JavaScript detection metadata attached to Bot Management data.</summary>
public sealed class JsDetection
{
    private readonly JsonElement _root;

    internal JsDetection(JsonElement root)
    {
        _root = root.Clone();
    }

    /// <summary>The raw JavaScript detection metadata object.</summary>
    public JsonElement Raw => _root;

    /// <summary>True when JavaScript detection passed.</summary>
    public bool? Passed => GetBoolean("passed");

    private bool? GetBoolean(string name)
    {
        if (!_root.TryGetProperty(name, out var value))
        {
            foreach (var property in _root.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    break;
                }
            }
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt32(out var number) => number != 0,
            JsonValueKind.String => value.GetString() switch
            {
                "1" => true,
                "0" => false,
                { } text when bool.TryParse(text, out var parsed) => parsed,
                _ => null
            },
            _ => null
        };
    }
}

/// <summary>TLS client certificate metadata attached to a request.</summary>
public sealed class TlsClientAuth
{
    private readonly JsonElement _root;

    internal TlsClientAuth(JsonElement root)
    {
        _root = root.Clone();
    }

    /// <summary>The raw TLS client auth metadata object.</summary>
    public JsonElement Raw => _root;

    /// <summary>The legacy issuer distinguished name for the client certificate.</summary>
    public string? CertIssuerDnLegacy => GetString("certIssuerDNLegacy");

    /// <summary>The issuer distinguished name for the client certificate.</summary>
    public string? CertIssuerDn => GetString("certIssuerDN");

    /// <summary>The RFC 2253 issuer distinguished name for the client certificate.</summary>
    public string? CertIssuerDnRfc2253 => GetString("certIssuerDNRFC2253");

    /// <summary>The legacy subject distinguished name for the client certificate.</summary>
    public string? CertSubjectDnLegacy => GetString("certSubjectDNLegacy");

    /// <summary>The client certificate verification status.</summary>
    public string? CertVerified => GetString("certVerified");

    /// <summary>The client certificate not-after timestamp.</summary>
    public string? CertNotAfter => GetString("certNotAfter");

    /// <summary>The subject distinguished name for the client certificate.</summary>
    public string? CertSubjectDn => GetString("certSubjectDN");

    /// <summary>The SHA-1 fingerprint for the client certificate.</summary>
    public string? CertFingerprintSha1 => GetString("certFingerprintSHA1");

    /// <summary>The SHA-256 fingerprint for the client certificate.</summary>
    public string? CertFingerprintSha256 => GetString("certFingerprintSHA256");

    /// <summary>The client certificate not-before timestamp.</summary>
    public string? CertNotBefore => GetString("certNotBefore");

    /// <summary>The client certificate serial number.</summary>
    public string? CertSerial => GetString("certSerial");

    /// <summary>Whether a client certificate was presented.</summary>
    public string? CertPresented => GetString("certPresented");

    /// <summary>The RFC 2253 subject distinguished name for the client certificate.</summary>
    public string? CertSubjectDnRfc2253 => GetString("certSubjectDNRFC2253");

    private string? GetString(string name)
    {
        if (_root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String)
            return value.GetString();

        foreach (var property in _root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind is JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }
}
