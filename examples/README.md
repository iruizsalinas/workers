# Examples

Each example contains a complete C# Worker and a `wrangler.toml` configuration. Start with [HelloWorld](./HelloWorld/Worker.cs).

## Running an example

Use the .NET 10 SDK and Wrangler. From the repository root:

```sh
cd examples/HelloWorld
dotnet publish -c Release
npx wrangler dev
```

Wrangler also runs the publish command through each example's build configuration. To deploy, run `npx wrangler deploy` from the same directory. For examples with bindings, create the required resources and replace placeholder IDs, names, and URLs in `wrangler.toml` first. The examples in this repository build against the local Workers projects.

## Choose an example

| Example | Demonstrates |
| --- | --- |
| [HelloWorld](./HelloWorld/Worker.cs) | Text responses and a 404 fallback |
| [JsonApi](./JsonApi/Worker.cs) | JSON responses and request paths |
| [Routing](./Routing/README.md) | Route parameters, middleware, HTTP errors, and a KV notes API |
| [CorsHeaders](./CorsHeaders/Worker.cs) | CORS middleware and response headers |
| [Cookies](./Cookies/README.md) | Reading, setting, and deleting cookies |
| [Redirects](./Redirects/Worker.cs) | Redirect responses |
| [ProxyFetch](./ProxyFetch/Worker.cs) | Outbound fetch and copying a response to change its headers |
| [RequestPolicy](./RequestPolicy/README.md) | Bounded JSON reads, validation, and upstream timeouts |
| [CacheApi](./CacheApi/Worker.cs) | Cache lookup and background writes with `Context.WaitUntil` |
| [KvBinding](./KvBinding/Worker.cs) | Reading text from KV |
| [R2Binding](./R2Binding/Worker.cs) | Streaming objects with HTTP metadata |
| [QueueProducer](./QueueProducer/Worker.cs) | Sending JSON messages to a queue |
| [QueueConsumer](./QueueConsumer/Worker.cs) | Processing and acknowledging queue messages |
| [ScheduledTask](./ScheduledTask/Worker.cs) | Cron events and logging |
| [TelemetryPipeline](./TelemetryPipeline/Worker.cs) | D1 queries, queues, scheduled work, and Durable Objects |

## Runtime APIs

Use `Workers` APIs such as `Json.Parse<T>`, `Json.Stringify`, `Date`, `RegExp`, `AbortSignal`, and `Log`. They follow JavaScript/Web behavior. [RequestPolicy](./RequestPolicy/Worker.cs) combines JSON, regular expressions, cancellation, and logging; [CacheApi](./CacheApi/Worker.cs) uses dates.

The compiler supports a small C# foundation for primitives, strings, collections, LINQ, and async/await. It does not implement the .NET BCL; unsupported APIs produce compiler diagnostics.

For database access, [TelemetryPipeline](./TelemetryPipeline/Worker.cs) shows parameterized D1 queries and typed row reads. D1 and Durable Object SQLite storage are built in; Hyperdrive exposes connection details and a TCP socket. External PostgreSQL, MySQL, and MongoDB clients are not provided.
