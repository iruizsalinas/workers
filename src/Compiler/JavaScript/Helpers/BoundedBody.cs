internal static partial class HelperSource
{
    private static string BoundedBody(Func<string, string> name) => $$"""
        async function {{name("requestBytes")}}(request, maxBytes) {
          if (!Number.isInteger(maxBytes) || maxBytes <= 0 || maxBytes > 2147483647)
            throw new RangeError("maxBytes must be a positive 32-bit integer.");
          if (request.bodyUsed) throw new TypeError("The request body has already been consumed.");
          if (request.body === null) return new Uint8Array(0);
          const reader = request.body.getReader();
          try {
            let buffer = new Uint8Array(Math.min(maxBytes, 4096)), size = 0;
            while (true) {
              const { done, value } = await reader.read();
              if (done) return buffer.subarray(0, size);
              if (!(value instanceof Uint8Array)) throw new TypeError("Request body chunks must be bytes.");
              if (value.byteLength > maxBytes - size)
                throw new {{name("HttpError")}}(413, "Request body too large");
              const required = size + value.byteLength;
              if (required > buffer.byteLength) {
                // Grow with the input instead of allocating the full configured limit.
                // One buffer also avoids unbounded metadata from many tiny chunks.
                const grown = new Uint8Array(Math.min(maxBytes, Math.max(required, buffer.byteLength * 2)));
                grown.set(buffer.subarray(0, size));
                buffer = grown;
              }
              buffer.set(value, size);
              size = required;
            }
          } catch (error) {
            // Cancellation failures must not replace 413 or delay the response indefinitely.
            reader.cancel(error).catch(() => {});
            throw error;
          } finally {
            reader.releaseLock();
          }
        }
        async function {{name("requestText")}}(request, maxBytes) {
          return new TextDecoder().decode(await {{name("requestBytes")}}(request, maxBytes));
        }

        """;
}
