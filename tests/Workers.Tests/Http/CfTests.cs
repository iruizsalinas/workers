using System.Text.Json;
using Workers.Interop;
using Xunit;

namespace Workers.Tests;

public sealed class CfTests
{
    [Fact]
    public void UserConstructedRequestsDoNotHaveCloudflareMetadata()
    {
        var request = Request.Get("https://example.com/");

        Assert.Null(request.CfMetadata);
    }

    [Fact]
    public void RequestExposesTypedCloudflareMetadata()
    {
        var request = RequestWithCf(
            """
            {
              "botManagement": {
                "score": 89,
                "verifiedBot": true,
                "corporateProxy": false,
                "staticResource": true,
                "ja3Hash": "ja3",
                "ja4": "ja4",
                "jsDetection": { "passed": true },
                "detectionIds": [101, "202"]
              },
              "verifiedBotCategory": "Search Engine Crawler",
              "colo": "CDG",
              "asn": 13335,
              "asOrganization": "Cloudflare, Inc.",
              "country": "FR",
              "httpProtocol": "HTTP/3",
              "requestPriority": "weight=192;exclusive=1;group=3;group-weight=127",
              "tlsCipher": "AEAD-AES128-GCM-SHA256",
              "tlsClientAuth": {
                "certIssuerDN": "CN=issuer",
                "certSubjectDN": "CN=subject",
                "certVerified": "SUCCESS",
                "certFingerprintSHA256": "sha256"
              },
              "tlsVersion": "TLSv1.3",
              "city": "Paris",
              "continent": "EU",
              "latitude": "48.8566",
              "longitude": "2.3522",
              "postalCode": "75001",
              "metroCode": "0",
              "region": "Ile-de-France",
              "regionCode": "IDF",
              "timezone": "Europe/Paris",
              "isEUCountry": "1",
              "hostMetadata": { "tenant": "tenant-a" }
            }
            """);

        var cf = request.CfMetadata!;
        var priority = cf.RequestPriority!.Value;
        var coordinates = cf.Coordinates!.Value;
        var clientAuth = cf.TlsClientAuth!;
        var botManagement = cf.BotManagement!;

        Assert.Equal("CDG", cf.Colo);
        Assert.Equal((uint)13335, cf.Asn);
        Assert.Equal("Cloudflare, Inc.", cf.AsOrganization);
        Assert.Equal("FR", cf.Country);
        Assert.Equal("HTTP/3", cf.HttpProtocol);
        Assert.Equal(192, priority.Weight);
        Assert.True(priority.Exclusive);
        Assert.Equal(3, priority.Group);
        Assert.Equal(127, priority.GroupWeight);
        Assert.Equal("AEAD-AES128-GCM-SHA256", cf.TlsCipher);
        Assert.Equal("TLSv1.3", cf.TlsVersion);
        Assert.Equal("Paris", cf.City);
        Assert.Equal("EU", cf.Continent);
        Assert.Equal(48.8566f, coordinates.Latitude, precision: 4);
        Assert.Equal(2.3522f, coordinates.Longitude, precision: 4);
        Assert.Equal("75001", cf.PostalCode);
        Assert.Equal("0", cf.MetroCode);
        Assert.Equal("Ile-de-France", cf.Region);
        Assert.Equal("IDF", cf.RegionCode);
        Assert.Equal("Europe/Paris", cf.TimezoneName);
        Assert.True(cf.IsEuCountry);
        Assert.Equal("Search Engine Crawler", cf.VerifiedBotCategory);
        Assert.Equal((uint)89, botManagement.Score);
        Assert.True(botManagement.VerifiedBot);
        Assert.False(botManagement.CorporateProxy);
        Assert.True(botManagement.StaticResource);
        Assert.Equal("ja3", botManagement.Ja3Hash);
        Assert.Equal("ja4", botManagement.Ja4);
        Assert.True(botManagement.JsDetection!.Passed);
        Assert.Equal(new uint[] { 101, 202 }, botManagement.DetectionIds);
        Assert.Equal(JsonValueKind.Object, cf.BotManagementRaw!.Value.ValueKind);
        Assert.Equal(89, cf.BotManagementAs<BotManagement>()!.Score);
        Assert.Equal("tenant-a", cf.HostMetadata<HostMetadata>()!.Tenant);
        Assert.Equal("CN=issuer", clientAuth.CertIssuerDn);
        Assert.Equal("CN=subject", clientAuth.CertSubjectDn);
        Assert.Equal("SUCCESS", clientAuth.CertVerified);
        Assert.Equal("sha256", clientAuth.CertFingerprintSha256);
    }

    [Fact]
    public void RequestPriorityUsesCloudflareDefaultsForMissingFields()
    {
        var request = RequestWithCf("""{"requestPriority":"weight=12"}""");

        var priority = request.CfMetadata!.RequestPriority!.Value;

        Assert.Equal(12, priority.Weight);
        Assert.False(priority.Exclusive);
        Assert.Equal(0, priority.Group);
        Assert.Equal(0, priority.GroupWeight);
    }

    private static Request RequestWithCf(string cfJson)
    {
        var json = $$"""
        {
          "url": "https://example.com/",
          "method": "GET",
          "headers": [],
          "bodyBase64": null,
          "cf": {{cfJson}}
        }
        """;

        return JsonSerializer.Deserialize<RequestEnvelope>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!.ToRequest();
    }

    private sealed class BotManagement
    {
        public int Score { get; init; }
    }

    private sealed class HostMetadata
    {
        public string Tenant { get; init; } = "";
    }
}
