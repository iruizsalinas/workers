internal static partial class HelperSource
{
    private static string TryCall(Func<string, string> name) => $$"""
        function {{name("tryCall")}}(action, fallback) {
          try {
            return [true, action()];
          } catch {
            return [false, fallback];
          }
        }
        function {{name("dictionaryTake")}}(dictionary, key, fallback, remove) {
          if (key == null) throw new ({{name("exceptionType")}}("System.ArgumentNullException"))("Value cannot be null.");
          if (!Object.hasOwn(dictionary, key)) return [false, fallback];
          const value = dictionary[key];
          if (remove) delete dictionary[key];
          return [true, value];
        }

        """;

    // Response.redirect only accepts absolute URLs and returns immutable headers, while Location may be relative.
    private static string Redirect(Func<string, string> name) => $$"""
        function {{name("redirect")}}(location, status = 302) {
          if (![301, 302, 303, 307, 308].includes(status)) throw new RangeError(`Invalid redirect status: ${status}`);
          return new Response(null, { status, headers: { location } });
        }

        """;

    // ReadableStream.from is not available in Workers, so async iterables are pulled into a stream.
    private static string StreamFrom(Func<string, string> name) => $$"""
        function {{name("streamFrom")}}(chunks) {
          const iterator = chunks[Symbol.asyncIterator]();
          return new ReadableStream({
            async pull(controller) {
              const item = await iterator.next();
              if (item.done) controller.close(); else controller.enqueue(item.value);
            },
            cancel() { return iterator.return?.(); }
          });
        }

        """;
}
