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
        JavaScriptHelper.IntegerRemainder => IntegerRemainder(name),
        JavaScriptHelper.RandomNext => RandomNext(name),
        JavaScriptHelper.SetAdd => SetAdd(name),
        JavaScriptHelper.Base64 => Base64(name),
        JavaScriptHelper.RpcArguments => RpcArguments(name),
        JavaScriptHelper.IntParse => IntParse(name),
        JavaScriptHelper.HexDecode => HexDecode(name),
        JavaScriptHelper.EscapeDataString => EscapeDataString(name),
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

    private static string IntegerRemainder(Func<string, string> name) => $$"""
        function {{name("integerRemainder")}}(left, right, unsigned) {
          if (right === 0) throw new RangeError("Integer division by zero.");
          const value = left % right;
          return unsigned ? value >>> 0 : value | 0;
        }

        """;

    private static string RandomNext(Func<string, string> name) => $$"""
        function {{name("randomNext")}}(minimum, maximum) {
          if (minimum > maximum) throw new RangeError("Minimum cannot exceed maximum.");
          if (minimum === maximum) return minimum;
          return Math.floor(Math.random() * (maximum - minimum)) + minimum;
        }

        """;

    private static string SetAdd(Func<string, string> name) => $$"""
        function {{name("setAdd")}}(set, value) {
          if (set.has(value)) return false;
          set.add(value);
          return true;
        }

        """;

    private static string Base64(Func<string, string> name) => $$"""
        function {{name("base64Encode")}}(bytes) {
          let binary = "";
          for (const byte of bytes) binary += String.fromCharCode(byte);
          return btoa(binary);
        }
        function {{name("base64Decode")}}(value) {
          return Uint8Array.from(atob(value), character => character.charCodeAt(0));
        }

        """;

    private static string RpcArguments(Func<string, string> name) => $$"""
        function {{name("rpcArguments")}}(value) {
          return value ?? [];
        }

        """;

    private static string IntParse(Func<string, string> name) => $$"""
        function {{name("intParse")}}(input) {
          const value = input.trim();
          if (!/^[+-]?\d+$/.test(value)) throw new TypeError("Invalid Int32 value.");
          const number = Number(value);
          if (number < -2147483648 || number > 2147483647) throw new RangeError("Int32 overflow.");
          return number | 0;
        }

        """;

    private static string HexDecode(Func<string, string> name) => $$"""
        function {{name("hexDecode")}}(value) {
          if (value.length % 2 !== 0 || !/^[0-9a-f]*$/i.test(value)) throw new TypeError("Invalid hexadecimal value.");
          const bytes = new Uint8Array(value.length / 2);
          for (let index = 0; index < bytes.length; index++)
            bytes[index] = Number.parseInt(value.slice(index * 2, index * 2 + 2), 16);
          return bytes;
        }

        """;

    private static string EscapeDataString(Func<string, string> name) => $$"""
        function {{name("escapeDataString")}}(value) {
          return encodeURIComponent(value).replace(/[!'()*]/g, character =>
            `%${character.charCodeAt(0).toString(16).toUpperCase()}`);
        }

        """;
}
