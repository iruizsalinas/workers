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
        JavaScriptHelper.JsonElementToString => JsonElementToString(name),
        JavaScriptHelper.DateTimeOffset => DateTimeOffset(name),
        JavaScriptHelper.DateTimeAddMonths => DateTimeAddMonths(name),
        JavaScriptHelper.DateTimeFromUnixTime => DateTimeFromUnixTime(name),
        JavaScriptHelper.DateTimeCompare => DateTimeCompare(name),
        JavaScriptHelper.DateTimeDayOfYear => DateTimeDayOfYear(name),
        JavaScriptHelper.DateTimeIsLeapYear => DateTimeIsLeapYear(name),
        JavaScriptHelper.DateTimeDaysInMonth => DateTimeDaysInMonth(name),
        JavaScriptHelper.DateTimeAddMilliseconds => DateTimeAddMilliseconds(name),
        JavaScriptHelper.LinqWhere => LinqWhere(name),
        JavaScriptHelper.LinqSelect => LinqSelect(name),
        JavaScriptHelper.LinqSkip => LinqSkip(name),
        JavaScriptHelper.LinqTake => LinqTake(name),
        JavaScriptHelper.LinqConcat => LinqConcat(name),
        JavaScriptHelper.LinqAny => LinqAny(name),
        JavaScriptHelper.LinqAll => LinqAll(name),
        JavaScriptHelper.LinqCount => LinqCount(name),
        JavaScriptHelper.LinqContains => LinqContains(name),
        JavaScriptHelper.LinqFirst => LinqFirst(name),
        JavaScriptHelper.LinqLast => LinqLast(name),
        JavaScriptHelper.LinqSingle => LinqSingle(name),
        JavaScriptHelper.LinqToArray => LinqToArray(name),
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

    private static string JsonElementToString(Func<string, string> name) => $$"""
        function {{name("jsonElementToString")}}(value) {
          if (value == null) return "";
          if (typeof value === "string") return value;
          if (typeof value === "boolean") return value ? "True" : "False";
          return JSON.stringify(value);
        }

        """;

    private static string DateTimeOffset(Func<string, string> name) => $$"""
        function {{name("dateTimeOffset")}}(year, month, day, hour, minute, second, millisecond = 0) {
          const value = new Date(0);
          value.setUTCFullYear(year, month - 1, day);
          value.setUTCHours(hour, minute, second, millisecond);
          if (year < 1 || year > 9999
              || value.getUTCFullYear() !== year || value.getUTCMonth() !== month - 1
              || value.getUTCDate() !== day || value.getUTCHours() !== hour
              || value.getUTCMinutes() !== minute || value.getUTCSeconds() !== second
              || value.getUTCMilliseconds() !== millisecond) {
            throw new RangeError("Invalid DateTimeOffset components.");
          }
          return value;
        }

        """;

    private static string DateTimeAddMonths(Func<string, string> name) => $$"""
        function {{name("dateTimeAddMonths")}}(input, months) {
          if (!Number.isInteger(months) || months < -120000 || months > 120000)
            throw new RangeError("Months must be between -120000 and 120000.");
          const value = new Date(input);
          const absoluteMonth = value.getUTCFullYear() * 12 + value.getUTCMonth() + months;
          const year = Math.floor(absoluteMonth / 12), month = absoluteMonth - year * 12;
          if (year < 1 || year > 9999) throw new RangeError("DateTime value is out of range.");
          const monthEnd = new Date(0);
          monthEnd.setUTCFullYear(year, month + 1, 0);
          const day = Math.min(value.getUTCDate(), monthEnd.getUTCDate());
          value.setUTCDate(1);
          value.setUTCFullYear(year, month, day);
          return value;
        }

        """;

    private static string DateTimeFromUnixTime(Func<string, string> name) => $$"""
        function {{name("dateTimeFromUnixTime")}}(value, seconds) {
          const minimum = seconds ? -62135596800 : -62135596800000;
          const maximum = seconds ? 253402300799 : 253402300799999;
          if (!Number.isInteger(value) || value < minimum || value > maximum)
            throw new RangeError("Unix time is out of range.");
          return new Date(seconds ? value * 1000 : value);
        }

        """;

    private static string DateTimeCompare(Func<string, string> name) => $$"""
        function {{name("dateTimeCompare")}}(left, right) {
          const difference = new Date(left).getTime() - new Date(right).getTime();
          return difference < 0 ? -1 : difference > 0 ? 1 : 0;
        }

        """;

    private static string DateTimeDayOfYear(Func<string, string> name) => $$"""
        function {{name("dateTimeDayOfYear")}}(input) {
          const value = new Date(input), start = new Date(0);
          start.setUTCFullYear(value.getUTCFullYear(), 0, 1);
          start.setUTCHours(0, 0, 0, 0);
          return Math.floor((value.getTime() - start.getTime()) / 86400000) + 1;
        }

        """;

    private static string DateTimeIsLeapYear(Func<string, string> name) => $$"""
        function {{name("dateTimeIsLeapYear")}}(year) {
          if (!Number.isInteger(year) || year < 1 || year > 9999)
            throw new RangeError("Year must be between 1 and 9999.");
          return year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
        }

        """;

    private static string DateTimeDaysInMonth(Func<string, string> name) => $$"""
        function {{name("dateTimeDaysInMonth")}}(year, month) {
          if (!Number.isInteger(month) || month < 1 || month > 12)
            throw new RangeError("Month must be between 1 and 12.");
          return [31, {{name("dateTimeIsLeapYear")}}(year) ? 29 : 28, 31, 30, 31, 30,
            31, 31, 30, 31, 30, 31][month - 1];
        }

        """;

    private static string DateTimeAddMilliseconds(Func<string, string> name) => $$"""
        function {{name("dateTimeAddMilliseconds")}}(input, delta) {
          const milliseconds = new Date(input).getTime() + delta;
          if (!Number.isFinite(delta) || !Number.isFinite(milliseconds))
            throw new RangeError("DateTime value is out of range.");
          const value = new Date(milliseconds), year = value.getUTCFullYear();
          if (year < 1 || year > 9999)
            throw new RangeError("DateTime value is out of range.");
          return value;
        }

        """;

    private static string LinqWhere(Func<string, string> name) => $$"""
        function {{name("linqWhere")}}(source, predicate) {
          if (source == null || predicate == null) throw new TypeError("LINQ argument cannot be null.");
          return { *[Symbol.iterator]() {
            let index = 0;
            for (const value of source) if (predicate(value, index++)) yield value;
          }
          };
        }

        """;

    private static string LinqSelect(Func<string, string> name) => $$"""
        function {{name("linqSelect")}}(source, selector) {
          if (source == null || selector == null) throw new TypeError("LINQ argument cannot be null.");
          return { *[Symbol.iterator]() {
            let index = 0;
            for (const value of source) yield selector(value, index++);
          }
          };
        }

        """;

    private static string LinqSkip(Func<string, string> name) => $$"""
        function {{name("linqSkip")}}(source, count) {
          if (source == null) throw new TypeError("LINQ source cannot be null.");
          return { *[Symbol.iterator]() {
            let remaining = Math.max(0, count);
            for (const value of source) if (remaining > 0) remaining--; else yield value;
          }
          };
        }

        """;

    private static string LinqTake(Func<string, string> name) => $$"""
        function {{name("linqTake")}}(source, count) {
          if (source == null) throw new TypeError("LINQ source cannot be null.");
          return { *[Symbol.iterator]() {
            let remaining = count;
            if (remaining <= 0) return;
            for (const value of source) {
              yield value;
              if (--remaining === 0) return;
            }
          }
          };
        }

        """;

    private static string LinqConcat(Func<string, string> name) => $$"""
        function {{name("linqConcat")}}(first, second) {
          if (first == null || second == null) throw new TypeError("LINQ source cannot be null.");
          return { *[Symbol.iterator]() { yield* first; yield* second; }
          };
        }

        """;

    private static string LinqAny(Func<string, string> name) => $$"""
        function {{name("linqAny")}}(source, predicate, hasPredicate) {
          if (source == null || (hasPredicate && predicate == null))
            throw new TypeError("LINQ argument cannot be null.");
          let index = 0;
          for (const value of source) if (predicate === null || predicate(value, index++)) return true;
          return false;
        }

        """;

    private static string LinqAll(Func<string, string> name) => $$"""
        function {{name("linqAll")}}(source, predicate) {
          if (source == null || predicate == null) throw new TypeError("LINQ argument cannot be null.");
          let index = 0;
          for (const value of source) if (!predicate(value, index++)) return false;
          return true;
        }

        """;

    private static string LinqCount(Func<string, string> name) => $$"""
        function {{name("linqCount")}}(source, predicate, hasPredicate) {
          if (source == null || (hasPredicate && predicate == null))
            throw new TypeError("LINQ argument cannot be null.");
          let count = 0, index = 0;
          for (const value of source) if (predicate === null || predicate(value, index++)) {
            if (count === 2147483647) throw new RangeError("Enumerable count overflow.");
            count++;
          }
          return count;
        }

        """;

    private static string LinqContains(Func<string, string> name) => $$"""
        function {{name("linqContains")}}(source, target) {
          if (source == null) throw new TypeError("LINQ source cannot be null.");
          for (const value of source)
            if (value === target || (value !== value && target !== target)) return true;
          return false;
        }

        """;

    private static string LinqFirst(Func<string, string> name) => $$"""
        function {{name("linqFirst")}}(source, predicate, hasPredicate, defaultValue, orDefault) {
          if (source == null || (hasPredicate && predicate == null))
            throw new TypeError("LINQ argument cannot be null.");
          let index = 0;
          for (const value of source)
            if (predicate === null || predicate(value, index++)) return value;
          if (orDefault) return defaultValue;
          throw new RangeError("Sequence contains no matching element.");
        }

        """;

    private static string LinqLast(Func<string, string> name) => $$"""
        function {{name("linqLast")}}(source, predicate, hasPredicate, defaultValue, orDefault) {
          if (source == null || (hasPredicate && predicate == null))
            throw new TypeError("LINQ argument cannot be null.");
          let found = false, result = defaultValue, index = 0;
          for (const value of source) if (predicate === null || predicate(value, index++)) {
            found = true; result = value;
          }
          if (found || orDefault) return result;
          throw new RangeError("Sequence contains no matching element.");
        }

        """;

    private static string LinqSingle(Func<string, string> name) => $$"""
        function {{name("linqSingle")}}(source, predicate, hasPredicate, defaultValue, orDefault) {
          if (source == null || (hasPredicate && predicate == null))
            throw new TypeError("LINQ argument cannot be null.");
          let found = false, result = defaultValue, index = 0;
          for (const value of source) if (predicate === null || predicate(value, index++)) {
            if (found) throw new RangeError("Sequence contains more than one matching element.");
            found = true; result = value;
          }
          if (found || orDefault) return result;
          throw new RangeError("Sequence contains no matching element.");
        }

        """;

    private static string LinqToArray(Func<string, string> name) => $$"""
        function {{name("linqToArray")}}(source) {
          if (source == null) throw new TypeError("LINQ source cannot be null.");
          return Array.from(source);
        }

        """;
}
