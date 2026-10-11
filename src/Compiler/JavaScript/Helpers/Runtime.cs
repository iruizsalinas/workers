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
          if (key == null) throw new TypeError("Value cannot be null.");
          if (!Object.hasOwn(dictionary, key)) return [false, fallback];
          const value = dictionary[key];
          if (remove) delete dictionary[key];
          return [true, value];
        }

        """;

    private static string WithHeader(Func<string, string> name) => $$"""
        function {{name("withHeader")}}(response, name, value, operation = "set") {
          const copy = new Response(response.body, response);
          copy.headers[operation](name, value);
          return copy;
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
