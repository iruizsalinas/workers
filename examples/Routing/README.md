# Routing

[Worker.cs](./Worker.cs) implements a KV notes API with `Router`. Follow the [running instructions](../README.md#running-an-example) and configure the `NOTES` KV namespace in [wrangler.toml](./wrangler.toml).

| Method and path | Behavior |
| --- | --- |
| `GET /` | Returns a greeting |
| `GET /notes/{id:uuid}` | Reads a note, or throws `HttpError(404)` |
| `PUT /notes/{id:uuid}` | Saves a JSON body such as `{"text":"Hello"}` |
| `DELETE /notes/{id:uuid}` | Deletes the note and returns 204 |
| `GET /files/{*path}` | Returns the captured path |

For example, use `/notes/123e4567-e89b-12d3-a456-426614174000` to save and then read a note. Keys normalize UUIDs to lowercase.

## Route behavior

Handlers receive the native `Request` and a `RouteContext`, which supplies parameters, bindings, and the execution context. `Use` middleware wraps handlers; this example adds `x-router` to the response after awaiting `next()`.

Patterns are checked at build time. Parameters occupy a whole segment: `{id:int}` matches signed 32-bit integers, and `{id:uuid}` matches canonical hyphenated UUIDs. A final `{*path}` captures the rest of the path. The most specific route wins.

`GET` routes also answer `HEAD` with no response body. A matching path with an unsupported method receives 405 with an `Allow` header. Unmatched paths use `Fallback`, or receive 404 when no fallback is configured.

Throw `HttpError` for an intended HTTP failure. See [RequestPolicy](../RequestPolicy/README.md) for custom JSON errors through `OnError`, and [CorsHeaders](../CorsHeaders/Worker.cs) for CORS middleware.
