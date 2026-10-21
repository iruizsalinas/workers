# Workers

Write Cloudflare Workers in C# compiled to native JavaScript.

```csharp
using Workers;

public static class Worker
{
    [Fetch]
    public static Response Fetch(Request request)
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

## How it works

C# compiles to JavaScript that runs directly on Workers, without shipping a .NET runtime. It uses native Web APIs and a focused C# profile rather than the full .NET BCL or arbitrary NuGet packages. Versions before 0.3.0 used .NET on WebAssembly.

Tested locally with Wrangler using a simple SHA-256 worker (1 MiB payload).

| | Old: WebAssembly (0.2.0) | New: JavaScript (0.3.0+) | Improvement |
|---|---:|---:|---:|
| Cold request | 356.8 ms | 12.6 ms | **28.3× faster** |
| Warm request | 143.14 ms | 5.72 ms | **25× faster** |
| Memory usage | 77.4 MB | 306 KB | **253× lower** |
| Bundle size (uncompressed) | 4.58 MB | 6.96 KB | **659× smaller** |

## Examples

Start with [HelloWorld](./examples/HelloWorld), or browse the [examples and usage notes](./examples/README.md) for HTTP, routing, cookies, storage, queues, and scheduled events.

You can also explore a [live demo](https://cloudflare-workers-dotnet.mailpost.workers.dev/) built by [Michiel Post](https://github.com/michielpost).
