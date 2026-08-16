# Workers

Write Cloudflare Workers in C# compiled to native JavaScript.

```csharp
using Workers;

public static class Worker
{
    [Fetch]
    public static Response Handle(
        Request request,
        Env env,
        Context context)
    {
        return Response.Text("Hello from C#!");
    }
}
```

## Getting started

Add the package to a .NET project:

```sh
dotnet add package Workers
```

Publish the Worker:

```sh
dotnet publish -c Release
```

The generated `dist/worker.js` is a native ES module ready for Wrangler. Configure bindings, routes, and other deployment settings in your Wrangler configuration.

## Workers API

The package provides a focused C# API for Workers requests, responses, events, bindings, Durable Objects, queues, KV, R2, D1, Cache, WebSockets, TCP sockets, email, and other platform features.

```csharp
[Fetch]
public static async Task<Response> Handle(
    Request request,
    Env env,
    Context context)
{
    var users = env.Kv("USERS");
    var user = await users.GetJsonAsync<User>("current");

    if (user is null)
        return Response.Text("User not found", status: 404);

    return Response.Json(user);
}
```

Familiar C# APIs such as `Task`, `Console`, `Guid`, and `DateTimeOffset` are supported where they map cleanly to the Workers runtime. Unsupported language or .NET features produce a compiler diagnostic instead of shipping a compatibility runtime.

## Version 0.3

Versions through `0.2.0` ran .NET on WebAssembly and supported managed assemblies and compatible NuGet packages. Every Worker also had to ship and initialize the .NET runtime, framework files, and a JavaScript interoperability adapter, resulting in large bundles, slow startup, and high CPU usage.

Starting with `0.3.0`, Workers compiles a focused C# profile directly to native JavaScript. Removing the runtime and interoperability layer reduces generated output by up to 99%. In a basic-response baseline, output falls from approximately 4.9 MB to about 200 bytes raw. This is a breaking change: arbitrary NuGet packages and the complete .NET BCL are no longer supported.

## Examples

See the [examples](./examples) directory for complete Workers covering HTTP, storage, queues, scheduled events, and other platform APIs.
