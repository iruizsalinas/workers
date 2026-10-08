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

    // ctx.storage.sql only exposes exec(query, ...bindings), which runs immediately. Prepared
    // statements defer execution until a result is requested so Bind can supply the parameters.
    private static string SqlStatement(Func<string, string> name) => $$"""
        function {{name("sqlStatement")}}(sql, query, bindings = []) {
          const run = () => sql.exec(query, ...bindings);
          const result = (cursor, rows) => ({ rows, columnNames: cursor.columnNames, rowsRead: cursor.rowsRead, rowsWritten: cursor.rowsWritten });
          return {
            bind: (...values) => {{name("sqlStatement")}}(sql, query, values),
            all: async () => { const cursor = run(); return result(cursor, cursor.toArray()); },
            one: async () => run().one(),
            raw: async () => { const cursor = run(); return result(cursor, Array.from(cursor.raw())); },
            cursor: async () => run(),
            rawSync: () => { const cursor = run(); return result(cursor, Array.from(cursor.raw())); }
          };
        }
        function {{name("sqlCursorNext")}}(cursor) {
          const next = cursor.next();
          return next.done ? null : next.value;
        }

        """;

    private static string WithHeader(Func<string, string> name) => $$"""
        function {{name("withHeader")}}(response, name, value, operation = "set") {
          const copy = new Response(response.body, response);
          copy.headers[operation](name, value);
          return copy;
        }

        """;

    private static string Delay(Func<string, string> name) => $$"""
        function {{name("delay")}}(milliseconds) {
          if (milliseconds < -1 || milliseconds > 4294967294)
            throw new RangeError("Delay is out of range.");
          if (milliseconds === -1) return new Promise(() => {});
          return scheduler.wait(milliseconds);
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
          const reader = {{name("streamReaders")}}.get(stream);
          if (!reader) return new Uint8Array(await new Response(stream).arrayBuffer());
          const chunks = [];
          let length = 0;
          while (true) {
            const result = await reader.read();
            if (result.done) break;
            chunks.push(result.value.slice());
            length += result.value.length;
          }
          const bytes = new Uint8Array(length);
          let offset = 0;
          for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.length; }
          return bytes;
        }
        function {{name("streamCancel")}}(stream) {
          const reader = {{name("streamReaders")}}.get(stream);
          return reader ? reader.cancel() : stream.cancel();
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

}
