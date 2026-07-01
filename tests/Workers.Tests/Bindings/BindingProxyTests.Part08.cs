using System.Security.Cryptography;
using System.Text.Json;
using Workers.Interop;
using Xunit;

namespace Workers.Tests;

public sealed partial class BindingProxyTests
{
    [Fact]
    public async Task GlobalFetchDispatchesCloudflareCacheOptions()
    {
        var response = ResponseEnvelope.FromResponse(Response.Text("cf"));
        var dispatcher = new CapturingDispatcher(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-fetch-cf");

        var result = await environment.FetchAsync(
            "https://origin.example/cached",
            new FetchOptions
            {
                Cf = new FetchCfOptions
                {
                    Apps = false,
                    CacheEverything = true,
                    CacheKey = "custom-cache-key",
                    CacheTtl = 120,
                    CacheTtlByStatus = new Dictionary<string, int>
                    {
                        ["200-299"] = 3600,
                        ["404"] = 60,
                        ["500-599"] = -1
                    },
                    Minify = new FetchMinifyOptions
                    {
                        Js = true,
                        Html = false,
                        Css = true
                    },
                    Mirage = false,
                    Polish = FetchPolish.Lossless,
                    ResolveOverride = "origin.example",
                    ScrapeShield = false
                }
            });

        Assert.Equal("cf", result.Body.AsText());
        using var payload = JsonDocument.Parse(dispatcher.Invocations.Single().PayloadJson);
        var cf = payload.RootElement.GetProperty("options").GetProperty("cf");
        Assert.False(cf.GetProperty("apps").GetBoolean());
        Assert.True(cf.GetProperty("cacheEverything").GetBoolean());
        Assert.Equal("custom-cache-key", cf.GetProperty("cacheKey").GetString());
        Assert.Equal(120, cf.GetProperty("cacheTtl").GetInt32());
        Assert.Equal(3600, cf.GetProperty("cacheTtlByStatus").GetProperty("200-299").GetInt32());
        Assert.Equal(60, cf.GetProperty("cacheTtlByStatus").GetProperty("404").GetInt32());
        Assert.Equal(-1, cf.GetProperty("cacheTtlByStatus").GetProperty("500-599").GetInt32());
        Assert.True(cf.GetProperty("minify").GetProperty("js").GetBoolean());
        Assert.False(cf.GetProperty("minify").GetProperty("html").GetBoolean());
        Assert.True(cf.GetProperty("minify").GetProperty("css").GetBoolean());
        Assert.False(cf.GetProperty("mirage").GetBoolean());
        Assert.Equal("lossless", cf.GetProperty("polish").GetString());
        Assert.Equal("origin.example", cf.GetProperty("resolveOverride").GetString());
        Assert.False(cf.GetProperty("scrapeShield").GetBoolean());
    }

    [Fact]
    public async Task GlobalFetchDispatchesCloudflareImageOptions()
    {
        var response = ResponseEnvelope.FromResponse(Response.Text("image"));
        var dispatcher = new CapturingDispatcher(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-fetch-image");

        var result = await environment.FetchAsync(
            "https://origin.example/image.jpg",
            new FetchOptions
            {
                Cf = new FetchCfOptions
                {
                    Image = new FetchImageOptions
                    {
                        Anim = false,
                        Background = "#ffffff",
                        Blur = 12,
                        Border = new FetchImageBorder
                        {
                            Color = "rgb(0,0,0)",
                            Top = 1,
                            Right = 2,
                            Bottom = 3,
                            Left = 4
                        },
                        Brightness = 1.2,
                        Compression = FetchImageCompression.Fast,
                        Contrast = 0.8,
                        Dpr = 2,
                        Draw = new FetchImageDraw
                        {
                            Url = "https://origin.example/watermark.png",
                            Opacity = 0.5,
                            Repeat = FetchImageDrawRepeat.FromAxis("x"),
                            Top = 10,
                            Left = 20
                        },
                        Fit = FetchImageFit.ScaleDown,
                        Flip = FetchImageFlip.Both,
                        Format = FetchImageFormat.Avif,
                        Gamma = 0.9,
                        Gravity = FetchImageGravity.FromCoordinates(0.5, 0.2),
                        Height = 600,
                        Metadata = FetchImageMetadata.Copyright,
                        OriginAuth = FetchImageOriginAuth.SharePublicly,
                        OnError = FetchImageOnError.Redirect,
                        Quality = FetchImageQuality.FromValue(85),
                        Rotate = 90,
                        Saturation = 1.1,
                        Sharpen = 2.5,
                        Trim = new FetchImageTrim { Top = 5, Width = 200, Height = 100 },
                        Width = 800
                    }
                }
            });

        Assert.Equal("image", result.Body.AsText());
        using var payload = JsonDocument.Parse(dispatcher.Invocations.Single().PayloadJson);
        var cf = payload.RootElement.GetProperty("options").GetProperty("cf");
        Assert.False(cf.TryGetProperty("cacheKey", out var omittedCacheKey));

        var image = cf.GetProperty("image");
        Assert.False(image.GetProperty("anim").GetBoolean());
        Assert.Equal("#ffffff", image.GetProperty("background").GetString());
        Assert.Equal(12, image.GetProperty("blur").GetInt32());
        Assert.Equal("rgb(0,0,0)", image.GetProperty("border").GetProperty("color").GetString());
        Assert.Equal(1, image.GetProperty("border").GetProperty("top").GetInt32());
        Assert.False(image.GetProperty("border").TryGetProperty("width", out var omittedBorderWidth));
        Assert.Equal(1.2, image.GetProperty("brightness").GetDouble());
        Assert.Equal("fast", image.GetProperty("compression").GetString());
        Assert.Equal(0.8, image.GetProperty("contrast").GetDouble());
        Assert.Equal(2, image.GetProperty("dpr").GetDouble());
        Assert.Equal("https://origin.example/watermark.png", image.GetProperty("draw").GetProperty("url").GetString());
        Assert.Equal(0.5, image.GetProperty("draw").GetProperty("opacity").GetDouble());
        Assert.Equal("x", image.GetProperty("draw").GetProperty("repeat").GetString());
        Assert.Equal(10, image.GetProperty("draw").GetProperty("top").GetInt32());
        Assert.Equal("scale-down", image.GetProperty("fit").GetString());
        Assert.Equal("hv", image.GetProperty("flip").GetString());
        Assert.Equal("avif", image.GetProperty("format").GetString());
        Assert.Equal(0.9, image.GetProperty("gamma").GetDouble());
        Assert.Equal(0.5, image.GetProperty("gravity").GetProperty("x").GetDouble());
        Assert.Equal(0.2, image.GetProperty("gravity").GetProperty("y").GetDouble());
        Assert.Equal(600, image.GetProperty("height").GetInt32());
        Assert.Equal("copyright", image.GetProperty("metadata").GetString());
        Assert.Equal("share-publicly", image.GetProperty("origin-auth").GetString());
        Assert.Equal("redirect", image.GetProperty("onerror").GetString());
        Assert.Equal(85, image.GetProperty("quality").GetInt32());
        Assert.Equal(90, image.GetProperty("rotate").GetInt32());
        Assert.Equal(1.1, image.GetProperty("saturation").GetDouble());
        Assert.Equal(2.5, image.GetProperty("sharpen").GetDouble());
        Assert.Equal(5, image.GetProperty("trim").GetProperty("top").GetInt32());
        Assert.Equal(200, image.GetProperty("trim").GetProperty("width").GetInt32());
        Assert.False(image.GetProperty("trim").TryGetProperty("bottom", out var omittedTrimBottom));
        Assert.Equal(800, image.GetProperty("width").GetInt32());
    }

    [Fact]
    public async Task FetchRejectsInvalidCloudflareCacheOptions()
    {
        using var _ = BindingDispatcher.Use(new CapturingDispatcher("{}"));
        var environment = EnvironmentWithInvocation("invocation-fetch-cf-invalid");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            environment.FetchAsync(
                "https://origin.example/cached",
                new FetchOptions
                {
                    Cf = new FetchCfOptions { CacheTtl = -1 }
                }));
    }

    [Fact]
    public async Task CryptoDispatchesDigest()
    {
        var expected = SHA256.HashData("Hello, World!"u8);
        var dispatcher = new CapturingDispatcher($$"""{"bodyBase64":"{{Convert.ToBase64String(expected)}}"}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-crypto");

        var digest = await environment.Crypto().DigestTextAsync(DigestAlgorithm.Sha256, "Hello, World!");

        Assert.Equal(expected, digest);
        Assert.Equal("crypto.digest", dispatcher.Invocations.Single().Operation);
        Assert.Equal("$crypto", dispatcher.Invocations.Single().BindingName);

        using var payload = JsonDocument.Parse(dispatcher.Invocations.Single().PayloadJson);
        Assert.Equal("SHA-256", payload.RootElement.GetProperty("algorithm").GetString());
        Assert.Equal(Convert.ToBase64String("Hello, World!"u8), payload.RootElement.GetProperty("bodyBase64").GetString());
    }

    [Fact]
    public async Task CryptoDispatchesRandomUuid()
    {
        var dispatcher = new CapturingDispatcher("""{"value":"3d2a9dfa-3a14-491f-8fa9-4935a8ad63f2"}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-crypto-random-uuid");

        var value = await environment.Crypto().RandomUuidAsync();

        Assert.Equal("3d2a9dfa-3a14-491f-8fa9-4935a8ad63f2", value);
        Assert.Equal("crypto.randomUUID", dispatcher.Invocations.Single().Operation);
        Assert.Equal("$crypto", dispatcher.Invocations.Single().BindingName);
        Assert.Equal("{}", dispatcher.Invocations.Single().PayloadJson);
    }

    [Fact]
    public async Task CryptoDispatchesRandomBytes()
    {
        var expected = new byte[] { 1, 2, 3, 4, 5 };
        var dispatcher = new CapturingDispatcher($$"""{"bodyBase64":"{{Convert.ToBase64String(expected)}}"}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-crypto-random-bytes");

        var value = await environment.Crypto().GetRandomBytesAsync(expected.Length);

        Assert.Equal(expected, value);
        Assert.Equal("crypto.getRandomValues", dispatcher.Invocations.Single().Operation);
        Assert.Equal("$crypto", dispatcher.Invocations.Single().BindingName);

        using var payload = JsonDocument.Parse(dispatcher.Invocations.Single().PayloadJson);
        Assert.Equal(expected.Length, payload.RootElement.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task CryptoRejectsInvalidRandomByteCounts()
    {
        using var _ = BindingDispatcher.Use(new CapturingDispatcher("{}"));
        var environment = EnvironmentWithInvocation("invocation-crypto-random-bytes-invalid");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => environment.Crypto().GetRandomBytesAsync(-1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => environment.Crypto().GetRandomBytesAsync(65537));
    }

    [Fact]
    public async Task CryptoDispatchesTimingSafeEqual()
    {
        var dispatcher = new CapturingDispatcher("""{"equal":true}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-crypto-timing-safe-equal");

        var equal = await environment.Crypto().TimingSafeEqualAsync("left"u8.ToArray(), "right"u8.ToArray());

        Assert.True(equal);
        Assert.Equal("crypto.timingSafeEqual", dispatcher.Invocations.Single().Operation);
        Assert.Equal("$crypto", dispatcher.Invocations.Single().BindingName);

        using var payload = JsonDocument.Parse(dispatcher.Invocations.Single().PayloadJson);
        Assert.Equal(Convert.ToBase64String("left"u8), payload.RootElement.GetProperty("leftBase64").GetString());
        Assert.Equal(Convert.ToBase64String("right"u8), payload.RootElement.GetProperty("rightBase64").GetString());
    }

    [Fact]
    public async Task CryptoDispatchesDigestStream()
    {
        var expected = SHA256.HashData("streamed"u8);
        var dispatcher = new CapturingDispatcher(
            """{"handle":"digest:1"}""",
            "{}",
            "{}",
            $$"""{"bodyBase64":"{{Convert.ToBase64String(expected)}}"}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-crypto-stream");

        var stream = await environment.Crypto().CreateDigestStreamAsync(DigestAlgorithm.Sha256);
        await stream.WriteTextAsync("streamed");
        await stream.CloseAsync();
        var digest = await stream.DigestAsync();

        Assert.Equal(expected, digest);
        Assert.Equal(
            [
                "crypto.digestStream.create",
                "crypto.digestStream.write",
                "crypto.digestStream.close",
                "crypto.digestStream.digest"
            ],
            dispatcher.Invocations.Select(static invocation => invocation.Operation));
        Assert.All(dispatcher.Invocations, static invocation => Assert.Equal("$crypto", invocation.BindingName));

        using var createPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("SHA-256", createPayload.RootElement.GetProperty("algorithm").GetString());

        using var writePayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("digest:1", writePayload.RootElement.GetProperty("handle").GetString());
        Assert.Equal(Convert.ToBase64String("streamed"u8), writePayload.RootElement.GetProperty("bodyBase64").GetString());

        using var digestPayload = JsonDocument.Parse(dispatcher.Invocations[3].PayloadJson);
        Assert.Equal("digest:1", digestPayload.RootElement.GetProperty("handle").GetString());
    }

    [Fact]
    public async Task DelayDispatchesRuntimeTimer()
    {
        var dispatcher = new CapturingDispatcher("{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-delay");

        await environment.DelayAsync(TimeSpan.FromMilliseconds(25));

        Assert.Equal("runtime.delay", dispatcher.Invocations.Single().Operation);
        Assert.Equal("$runtime", dispatcher.Invocations.Single().BindingName);

        using var payload = JsonDocument.Parse(dispatcher.Invocations.Single().PayloadJson);
        Assert.Equal(25, payload.RootElement.GetProperty("milliseconds").GetInt32());
    }

    [Fact]
    public async Task DelayRejectsInvalidDurations()
    {
        using var _ = BindingDispatcher.Use(new CapturingDispatcher("{}"));
        var environment = EnvironmentWithInvocation("invocation-delay-invalid");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            environment.DelayAsync(TimeSpan.FromMilliseconds(-1)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            environment.DelayAsync(TimeSpan.FromMilliseconds((double)int.MaxValue + 1)));
    }

    [Fact]
    public async Task RateLimiterDispatchesLimitKey()
    {
        var dispatcher = new CapturingDispatcher("""{"success":true}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-rate-limit");

        var outcome = await environment.RateLimiter("LOGIN_LIMIT").LimitAsync("user:1");

        Assert.True(outcome.Success);
        Assert.Equal("ratelimit.limit", dispatcher.Invocations.Single().Operation);
        Assert.Equal("LOGIN_LIMIT", dispatcher.Invocations.Single().BindingName);

        using var payload = JsonDocument.Parse(dispatcher.Invocations.Single().PayloadJson);
        Assert.Equal("user:1", payload.RootElement.GetProperty("key").GetString());
    }

    [Fact]
    public async Task AnalyticsEngineDispatchesDataPoint()
    {
        var dispatcher = new CapturingDispatcher("{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-analytics");
        var point = AnalyticsEngineDataPoint.Create()
            .Indexes("colo:SJC")
            .AddDouble(200)
            .AddDouble(12.5)
            .AddBlob("GET")
            .AddBlob(new byte[] { 1, 2, 3 })
            .Build();

        await environment.AnalyticsEngine("HTTP_ANALYTICS").WriteDataPointAsync(point);

        Assert.Equal("analytics.writeDataPoint", dispatcher.Invocations.Single().Operation);
        Assert.Equal("HTTP_ANALYTICS", dispatcher.Invocations.Single().BindingName);

        using var payload = JsonDocument.Parse(dispatcher.Invocations.Single().PayloadJson);
        Assert.Equal("colo:SJC", payload.RootElement.GetProperty("indexes")[0].GetString());
        Assert.Equal(200, payload.RootElement.GetProperty("doubles")[0].GetDouble());
        Assert.Equal("GET", payload.RootElement.GetProperty("blobs")[0].GetProperty("text").GetString());
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), payload.RootElement.GetProperty("blobs")[1].GetProperty("bodyBase64").GetString());
    }

    [Fact]
    public async Task VersionMetadataDispatchesGet()
    {
        var dispatcher = new CapturingDispatcher(
            """{"id":"version-1","tag":"prod","timestamp":"2026-06-29T17:30:00Z"}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-version");

        var version = await environment.VersionMetadata("CF_VERSION_METADATA").GetAsync();

        Assert.Equal("version-1", version.Id);
        Assert.Equal("prod", version.Tag);
        Assert.Equal("2026-06-29T17:30:00Z", version.Timestamp);
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("versionMetadata.get", invocation.Operation);
        Assert.Equal("CF_VERSION_METADATA", invocation.BindingName);
    }

    [Fact]
    public async Task SecretStoreDispatchesGet()
    {
        var dispatcher = new CapturingDispatcher(
            """{"value":"secret-value"}""",
            """{"value":null}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-secret-store");

        var value = await environment.SecretStore("API_KEY").GetAsync();
        var missing = await environment.SecretStore("MISSING_SECRET").GetAsync();

        Assert.Equal("secret-value", value);
        Assert.Null(missing);
        Assert.Equal(["secretStore.get", "secretStore.get"], dispatcher.Invocations.Select(static invocation => invocation.Operation));
        Assert.Equal(["API_KEY", "MISSING_SECRET"], dispatcher.Invocations.Select(static invocation => invocation.BindingName));
        Assert.All(dispatcher.Invocations, static invocation => Assert.Equal("{}", invocation.PayloadJson));
    }

    [Fact]
    public async Task HyperdriveDispatchesConnectionInfo()
    {
        var dispatcher = new CapturingDispatcher(
            """
            {
              "connectionString": "postgres://user:pass@db.example:5432/app",
              "host": "db.example",
              "port": 5432,
              "user": "user",
              "password": "pass",
              "database": "app"
            }
            """);
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-hyperdrive");

        var info = await environment.Hyperdrive("DB").GetConnectionInfoAsync();

        Assert.Equal("postgres://user:pass@db.example:5432/app", info.ConnectionString);
        Assert.Equal("db.example", info.Host);
        Assert.Equal((ushort)5432, info.Port);
        Assert.Equal("user", info.User);
        Assert.Equal("pass", info.Password);
        Assert.Equal("app", info.Database);
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("hyperdrive.connectionInfo", invocation.Operation);
        Assert.Equal("DB", invocation.BindingName);
        Assert.Equal("{}", invocation.PayloadJson);
    }
}
