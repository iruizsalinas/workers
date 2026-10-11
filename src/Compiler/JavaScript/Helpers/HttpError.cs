internal static partial class HelperSource
{
    private static string HttpError(Func<string, string> name) => $$"""
        class {{name("HttpError")}} extends Error {
          constructor(status, message) {
            if (!Number.isInteger(status) || status < 400 || status > 599)
              throw new RangeError("HttpError status must be between 400 and 599.");
            super(message);
            this.name = "HttpError";
            this.status = status;
          }
        }
        async function {{name("requestJson")}}(request, maxBytes) {
          // Read failures (including an already consumed body) are not malformed JSON.
          const text = maxBytes === undefined ? await request.text() : await {{name("requestText")}}(request, maxBytes);
          try {
            return JSON.parse(text);
          } catch (error) {
            if (error instanceof SyntaxError) throw new {{name("HttpError")}}(400, "Malformed JSON");
            throw error;
          }
        }

        """;
}
