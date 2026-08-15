internal static class HelperSource
{
    public static string Emit(JavaScriptHelper helper, Func<string, string> name) => helper switch
    {
        JavaScriptHelper.WithHeader => WithHeader(name),
        JavaScriptHelper.Delay => Delay(name),
        JavaScriptHelper.Stream => Streams(name),
        JavaScriptHelper.Socket => Sockets(name),
        JavaScriptHelper.Digest => Digest(name),
        JavaScriptHelper.WebSocketEvents => WebSocketEvents(name),
        JavaScriptHelper.IntegerDivide => IntegerDivide(name),
        _ => throw new ArgumentOutOfRangeException(nameof(helper))
    };

    private static string WithHeader(Func<string, string> name) => $$"""
        function {{name("withHeader")}}(response, name, value, operation = "set") {
          const copy = new Response(response.body, response);
          copy.headers[operation](name, value);
          return copy;
        }

        """;

    private static string Delay(Func<string, string> name) => $$"""
        function {{name("delay")}}(milliseconds) {
          return new Promise(resolve => setTimeout(resolve, milliseconds));
        }

        """;

    private static string Streams(Func<string, string> name) => $$"""
        const {{name("streamReaders")}} = new WeakMap();
        function {{name("streamReader")}}(stream) {
          let reader = {{name("streamReaders")}}.get(stream);
          if (!reader) {
            reader = stream.getReader();
            {{name("streamReaders")}}.set(stream, reader);
          }
          return reader;
        }
        async function {{name("streamRead")}}(stream) {
          const result = await {{name("streamReader")}}(stream).read();
          return { done: result.done, bytes: result.value ?? new Uint8Array() };
        }
        async function {{name("streamAll")}}(stream) {
          return new Uint8Array(await new Response(stream).arrayBuffer());
        }
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

    private static string Sockets(Func<string, string> name) => $$"""
        const {{name("socketReaders")}} = new WeakMap(), {{name("socketWriters")}} = new WeakMap();
        function {{name("socketReader")}}(socket) {
          let reader = {{name("socketReaders")}}.get(socket);
          if (!reader) {
            reader = socket.readable.getReader();
            {{name("socketReaders")}}.set(socket, reader);
          }
          return reader;
        }
        function {{name("socketWriter")}}(socket) {
          let writer = {{name("socketWriters")}}.get(socket);
          if (!writer) {
            writer = socket.writable.getWriter();
            {{name("socketWriters")}}.set(socket, writer);
          }
          return writer;
        }
        async function {{name("socketRead")}}(socket) {
          const result = await {{name("socketReader")}}(socket).read();
          return { done: result.done, bytes: result.value ?? new Uint8Array() };
        }

        """;

    private static string Digest(Func<string, string> name) => $$"""
        const {{name("digestWriters")}} = new WeakMap();
        function {{name("digestWriter")}}(stream) {
          let writer = {{name("digestWriters")}}.get(stream);
          if (!writer) {
            writer = stream.getWriter();
            {{name("digestWriters")}}.set(stream, writer);
          }
          return writer;
        }

        """;

    private static string WebSocketEvents(Func<string, string> name) => $$"""
        const {{name("webSocketQueues")}} = new WeakMap();
        function {{name("webSocketEvents")}}(socket) {
          let state = {{name("webSocketQueues")}}.get(socket);
          if (state) return state.api;
          const queue = [], waiters = [];
          const push = value => {
            const waiter = waiters.shift();
            waiter ? waiter(value) : queue.push(value);
          };
          socket.addEventListener("message", event => push({
            kind: 0,
            text: typeof event.data === "string" ? event.data : null,
            bytes: typeof event.data === "string" ? null : new Uint8Array(event.data)
          }));
          socket.addEventListener("close", event => push({
            kind: 1, code: event.code, reason: event.reason, wasClean: event.wasClean
          }));
          socket.addEventListener("error", () => push({ kind: 2 }));
          const api = {
            next: () => queue.length
              ? Promise.resolve(queue.shift())
              : new Promise(resolve => waiters.push(resolve))
          };
          state = { api };
          {{name("webSocketQueues")}}.set(socket, state);
          return api;
        }

        """;

    private static string IntegerDivide(Func<string, string> name) => $$"""
        function {{name("integerDivide")}}(left, right, unsigned) {
          if (right === 0) throw new RangeError("Integer division by zero.");
          if (!unsigned && left === -2147483648 && right === -1) {
            throw new RangeError("Integer division overflow.");
          }
          const value = Math.trunc(left / right);
          return unsigned ? value >>> 0 : value | 0;
        }

        """;
}
