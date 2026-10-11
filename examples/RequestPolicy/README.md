# Request policy

[Worker.cs](./Worker.cs) validates a JSON reading before forwarding it upstream. Follow the [running instructions](../README.md#running-an-example) and set `ORIGIN` in [wrangler.toml](./wrangler.toml) to your upstream service.

Send `POST /v1/readings` with `Content-Type: application/json` and a body such as:

```json
{"deviceId":"sensor-1","value":21.5,"tags":["office"]}
```

## Validation and forwarding

The Worker requires JSON content type (415 otherwise) and reads at most 4096 body bytes with `request.JsonAsync<ReadingInput>(maxBytes: MaxBodyBytes)`. The limit applies to actual bytes read; overflow throws `HttpError(413)`, and malformed JSON throws `HttpError(400)`.

Application validation requires a device ID of 3–32 lowercase letters, digits, or hyphens; a value from -100 to 1000; and at most eight unique, nonempty tags of at most 24 characters. Invalid readings receive 400.

The upstream request removes authorization, cookie, and content-length headers, adds a generated request ID, and sends the validated JSON. `AbortSignal.Timeout(3000)` limits the fetch, and `RedirectMode.Manual` leaves redirects to the caller. Fetch failures are logged and return 502.

`Router.OnError` formats HTTP errors as JSON containing `error` and `requestId`. Successful upstream responses are copied into a new `Response` to add `x-policy` and `x-upstream-host` headers.
