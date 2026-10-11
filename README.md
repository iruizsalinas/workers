# Workers

Write Cloudflare Workers in C# compiled to native JavaScript. Use C# to express logic and Workers APIs to interact with the runtime.

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

## Routing

`Router` maps methods and path patterns to handlers that take the native `Request` and return a native `Response`. Code before or after `HandleAsync` runs for every request, such as authentication or shared headers.

```csharp
private static readonly Router Routes = new Router()
    .Get("/users/{id:int}", GetUserAsync)
    .Delete("/users/{id:int}", DeleteUserAsync)
    .Get("/files/{*path}", (request, route) => Response.Text(route.Parameter("path")));

[Fetch]
public static Task<Response> Handle(Request request, Env env, Context context) =>
    Routes.HandleAsync(request, env, context);

private static async Task<Response> GetUserAsync(Request request, RouteContext route)
{
    var user = await route.Env.Kv("USERS").GetJsonAsync<User>(route.Parameter("id"));
    return user is null ? Response.Text("Not found", status: 404) : Response.Json(user);
}
```

Patterns are checked when the Worker is built. A parameter fills a whole segment: `{id:int}` matches signed 32-bit integers and `{id:uuid}` matches canonical hyphenated UUIDs. A final `{*path}` matches the rest of the path. The most specific route wins, `GET` routes also answer `HEAD`, a path that matches with another method gets `405` with an `Allow` header, and other requests go to `Fallback` or receive `404`.

## Cookies

`request.Cookies` reads the `Cookie` header, and `WithCookie` and `WithoutCookie` add `Set-Cookie` headers to a response.

```csharp
var session = request.Cookies.Get("session");

return Response.Json(user)
    .WithCookie("session", token, new CookieOptions { MaxAgeSeconds = 3600 })
    .WithoutCookie("legacy");
```

Cookies are `HttpOnly`, `Secure`, `SameSite=Lax` and `Path=/` unless options say otherwise. Values are percent-encoded on write and decoded on read, so a value can never add attributes. An invalid cookie throws instead of being silently dropped by the browser: a malformed name, path or domain, `SameSite=None` or `Partitioned` without `Secure`, a `__Secure-`, `__Host-` or `__Http-` prefix whose rules are not met, or more than 4096 bytes. To delete a cookie, pass `WithoutCookie` the same path, domain and `Secure` setting it was set with.

## Runtime APIs

JSON, dates, regular expressions, cancellation, streams, logging and number utilities live in `Workers` and follow JavaScript/Web behavior, as do the Cloudflare bindings. No additional package or `System.*` import is needed.

```csharp
var user = Json.Parse<User>(text);
var json = Json.Stringify(user);
var timestamp = new Date().ToIsoString();
var matched = new RegExp("^[a-z]+$", "i").Test("Workers");
var response = await Http.FetchAsync(url, new FetchOptions { Signal = AbortSignal.Timeout(3000) });
Log.Info(timestamp);
```

The compiler retains a small C# foundation for primitives, strings, collections, LINQ and async/await; string methods map to their JavaScript equivalents. It does not implement the .NET BCL. Unsupported APIs produce compiler diagnostics.

## Databases

D1 and Durable Object SQLite storage are built in, and typed reads map rows to classes and records. Hyperdrive bindings expose connection details and a TCP socket. External database clients such as PostgreSQL, MySQL and MongoDB are not provided yet.

```csharp
var users = await env.D1("DB")
    .Prepare("select id, name from users where team = ?")
    .Bind(teamId)
    .AllAsync<User>();
```

## Version 0.3

Versions through `0.2.0` ran .NET on WebAssembly and supported managed assemblies and compatible NuGet packages. Every Worker also had to ship and initialize the .NET runtime, framework files, and a JavaScript interoperability adapter, resulting in large bundles, slow startup, and high CPU usage.

Starting with `0.3.0`, Workers compiles a focused C# profile directly to native JavaScript. Removing the runtime and interoperability layer reduces generated output by up to 99%. In a basic-response baseline, output falls from approximately 4.9 MB to about 200 bytes raw. This is a breaking change: arbitrary NuGet packages and the complete .NET BCL are no longer supported.

## Examples

See the [examples](./examples) directory for complete Workers covering HTTP, storage, queues, scheduled events, and other platform APIs. You can also explore a [live demo](https://cloudflare-workers-dotnet.mailpost.workers.dev/) built by [Michiel Post](https://github.com/michielpost).
