# Workers

Write Cloudflare Workers in C# compiled to native JavaScript.

```csharp
using Workers;

public static class Worker
{
    [Fetch]
    public static Response Fetch(Request request, Env env, ExecutionContext ctx)
    {
        return new Response("Hello from C#!");
    }
}
```

## Getting started

Add the package to a .NET project and publish:

```sh
dotnet add package Workers
dotnet publish -c Release
```

The generated `dist/worker.js` is a native ES module ready for Wrangler. Configure bindings, routes, and deployment settings in your Wrangler configuration.

Workers uses a focused C# profile and native JavaScript/Web APIs. The full .NET BCL and arbitrary NuGet packages are not supported; unsupported APIs produce compiler diagnostics. Versions before `0.3.0` used .NET on WebAssembly.

## Examples

Start with [HelloWorld](./examples/HelloWorld), or browse the [examples and usage notes](./examples/README.md) for HTTP, routing, cookies, storage, queues, and scheduled events.

You can also explore a [live demo](https://cloudflare-workers-dotnet.mailpost.workers.dev/) built by [Michiel Post](https://github.com/michielpost).
