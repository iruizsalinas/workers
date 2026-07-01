# Workers

C# bindings and bundling utilities for Cloudflare Workers.

Write Worker-native C# code, publish it to WebAssembly, and bundle it into a Worker module.

```csharp
using Workers;

public static class Worker
{
    [FetchEvent]
    public static Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        return Task.FromResult(
            Response.Text("Hello from C# on Cloudflare Workers."));
    }
}
```

## Usage

```xml
<PackageReference Include="Workers" Version="0.1.0" />
```

Workers are plain static methods marked with event attributes:

```csharp
[FetchEvent]
public static Task<Response> FetchAsync(
    Request request,
    Env environment,
    Context context)
```

The SDK includes Worker-native APIs for requests, responses, routing, bindings, RPC stubs, Durable Objects, queues, R2, D1, KV, caches, sockets, email, and other Workers platform features.

## Publishing

Publish with the `browser-wasm` runtime:

```sh
dotnet publish -c Release -r browser-wasm
```

Workers defaults to invariant globalization for smaller bundles. If your Worker needs culture-specific formatting or comparisons, set `WorkersInvariantGlobalization` to `false`.

Publishing writes a `dist/` folder with the Worker module, runtime adapter, manifest, and `_framework` files for deployment with Wrangler.

Keep deployment settings like routes, bindings, migrations, vars, and observability in your Wrangler config.

For HTTP-only Workers that do not call platform bindings or helper APIs, set `WorkersIncludePlatformApis` to `false` to emit a smaller adapter.

## Examples

Small standalone examples live in `examples/`. Each one has its own project file and `wrangler.toml`, so you can copy a folder or deploy it directly:

```sh
cd examples/HelloWorld
wrangler deploy
```

| Example | Shows |
|---|---|
| `BasicResponse` | Plain text response |
| `JsonApi` | JSON response and request path |
| `Redirects` | Redirects from query parameters |
| `CorsHeaders` | CORS and response headers |
| `ProxyFetch` | Fetching another origin |
| `CacheApi` | Workers Cache API |
| `KvBinding` | KV namespace binding |
| `R2Binding` | R2 bucket binding |
| `QueueProducer` | Sending queue messages |
| `QueueConsumer` | Consuming queue messages |
| `ScheduledTask` | Cron/scheduled events |
| `HelloWorld` | Minimal router usage |
