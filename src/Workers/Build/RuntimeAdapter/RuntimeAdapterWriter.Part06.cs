namespace Workers.Build;

internal static partial class RuntimeAdapterWriter
{
    private const string AdapterPart06 =
        """
          }

          return result;
        }

        function toEmailAddressOrString(value) {
          if (value.name == null || value.name.length === 0) {
            return value.email;
          }

          return {
            name: value.name,
            email: value.email
          };
        }

        function toEmailAttachment(attachment) {
          const value = {
            disposition: attachment.disposition === 1 ? 'inline' : 'attachment',
            filename: attachment.filename,
            type: attachment.contentType,
            content: attachment.bodyBase64 == null
              ? attachment.textContent
              : fromBase64(attachment.bodyBase64)
          };

          if (attachment.contentId != null) {
            value.contentId = attachment.contentId;
          }

          return value;
        }

        function retainDigestStream(stream) {
          const handle = `digest:${++nextDigestStreamId}`;
          digestStreams.set(handle, { stream, writer: null });
          return handle;
        }

        function digestStreamEntry(handle) {
          const entry = digestStreams.get(handle);
          if (entry == null) {
            throw new Error(`Digest stream handle '${handle}' is not defined.`);
          }

          return entry;
        }

        function digestStreamWriter(entry) {
          entry.writer ??= entry.stream.getWriter();
          return entry.writer;
        }

        function timingSafeEqualBytes(left, right) {
          const length = Math.max(left.byteLength, right.byteLength);
          let difference = left.byteLength ^ right.byteLength;

          for (let index = 0; index < length; index++) {
            difference |= (left[index] ?? 0) ^ (right[index] ?? 0);
          }

          return difference === 0;
        }

        function retainTcpSocket(socket) {
          const handle = `tcp:${++nextTcpSocketId}`;
          tcpSockets.set(handle, { socket, reader: null, writer: null });
          return handle;
        }

        function tcpSocketAddress(payload) {
          if (payload.addressText != null) {
            return payload.addressText;
          }

          return {
            hostname: payload.address.hostname,
            port: payload.address.port
          };
        }

        function tcpSocketOptions(payload) {
          if (payload == null) {
            return undefined;
          }

          const options = {};
          if (payload.secureTransport != null) {
            options.secureTransport = payload.secureTransport;
          }
          if (payload.allowHalfOpen != null) {
            options.allowHalfOpen = payload.allowHalfOpen;
          }

          return Object.keys(options).length === 0 ? undefined : options;
        }

        function tcpSocketEntry(handle) {
          const entry = tcpSockets.get(handle);
          if (entry == null) {
            throw new Error(`TCP socket handle '${handle}' is not defined.`);
          }

          return entry;
        }

        function tcpSocketReader(entry) {
          entry.reader ??= entry.socket.readable.getReader();
          return entry.reader;
        }

        function tcpSocketWriter(entry) {
          entry.writer ??= entry.socket.writable.getWriter();
          return entry.writer;
        }

        function retainWebSocket(socket, listen = true) {
          const existingHandle = webSocketHandles.get(socket);
          if (existingHandle != null) {
            return existingHandle;
          }

          const handle = `ws:${++nextWebSocketId}`;
          const entry = {
            socket,
            events: [],
            waiters: [],
            closed: false
          };

          if (listen) {
            socket.addEventListener('message', event => {
              Promise.resolve(webSocketMessageEvent(event.data))
                .then(message => enqueueWebSocketEvent(entry, message))
                .catch(error => rejectWebSocketEvent(entry, error));
            });
            socket.addEventListener('close', event => {
              entry.closed = true;
              enqueueWebSocketEvent(entry, {
                kind: 'close',
                code: event.code ?? null,
                reason: event.reason ?? null,
                wasClean: event.wasClean ?? null
              });
            });
            socket.addEventListener('error', event => {
              entry.closed = true;
              rejectWebSocketEvent(entry, event.error ?? new Error('WebSocket error event received.'));
            });
          }

          webSockets.set(handle, entry);
          webSocketHandles.set(socket, handle);
          return handle;
        }

        function releaseWebSocket(handle) {
          const entry = webSockets.get(handle);
          if (entry != null) {
            webSocketHandles.delete(entry.socket);
            webSockets.delete(handle);
          }
        }

        async function webSocketMessageEvent(data) {
          if (typeof data === 'string') {
            return {
              kind: 'message',
              text: data,
              bodyBase64: null
            };
          }

          if (data instanceof ArrayBuffer) {
            return {
              kind: 'message',
              text: null,
              bodyBase64: toBase64(new Uint8Array(data))
            };
          }

          if (ArrayBuffer.isView(data)) {
            return {
              kind: 'message',
              text: null,
              bodyBase64: toBase64(new Uint8Array(data.buffer, data.byteOffset, data.byteLength))
            };
          }

          if (typeof Blob !== 'undefined' && data instanceof Blob) {
            return {
              kind: 'message',
              text: null,
              bodyBase64: toBase64(new Uint8Array(await data.arrayBuffer()))
            };
          }

          throw new Error('Unsupported WebSocket message payload.');
        }

        async function toWebSocketMessageEnvelope(message) {
          const event = await webSocketMessageEvent(message);
          return {
            text: event.text,
            bodyBase64: event.bodyBase64
          };
        }

        function enqueueWebSocketEvent(entry, event) {
          const waiter = entry.waiters.shift();
          if (waiter != null) {
            waiter.resolve(event);
            return;
          }

          entry.events.push({ type: 'event', event });
        }

        function rejectWebSocketEvent(entry, error) {
          const waiter = entry.waiters.shift();
          if (waiter != null) {
            waiter.reject(error);
            return;
          }

          entry.events.push({ type: 'error', error });
        }

        function nextWebSocketEvent(entry) {
          const queued = entry.events.shift();
          if (queued != null) {
            if (queued.type === 'error') {
              throw queued.error;
            }

            return queued.event;
          }

          if (entry.closed) {
            return null;
          }

          return new Promise((resolve, reject) => {
            entry.waiters.push({ resolve, reject });
          });
        }

        function webSocketConnectRequest(payload) {
          const headers = new Headers();
          headers.set('Upgrade', 'websocket');
          if ((payload.protocols ?? []).length > 0) {
            headers.set('Sec-WebSocket-Protocol', payload.protocols.join(','));
          }

          return new Request(toWebSocketFetchUrl(payload.url), {
            method: 'GET',
            headers
          });
        }

        function toWebSocketFetchUrl(value) {
          const url = new URL(value);
          if (url.protocol === 'ws:') {
            url.protocol = 'http:';
          } else if (url.protocol === 'wss:') {
            url.protocol = 'https:';
          }

          return url.toString();
        }

        function webSocket(handle) {
          return webSocketEntry(handle).socket;
        }

        function webSocketEntry(handle) {
          const entry = webSockets.get(handle);
          if (entry == null) {
            throw new Error(`WebSocket handle '${handle}' is not defined.`);
          }

          return entry;
        }

        function queueSendOptions(payload) {
          const options = {};
          if (payload.contentType != null) {
            options.contentType = payload.contentType;
          } else if (payload.bodyBase64 != null) {
            options.contentType = 'bytes';
          }
          if (payload.delaySeconds != null) {
            options.delaySeconds = payload.delaySeconds;
          }

          return Object.keys(options).length === 0 ? undefined : options;
        }

        function queueMessageBody(payload) {
          if (payload.bodyBase64 != null) {
            return fromBase64(payload.bodyBase64);
          }

          return payload.body;
        }

        function toQueueMessageEnvelope(message) {
          const bodyBytes = queueMessageBodyBytes(message.body);
          return {
            id: message.id,
            timestamp: new Date(message.timestamp).toISOString(),
            attempts: message.attempts ?? 1,
            body: bodyBytes == null ? message.body : null,
            bodyBase64: bodyBytes == null ? null : toBase64(bodyBytes)
          };
        }

        function queueMessageBodyBytes(body) {
          if (body instanceof ArrayBuffer) {
            return new Uint8Array(body);
          }

          if (ArrayBuffer.isView(body)) {
            return new Uint8Array(body.buffer, body.byteOffset, body.byteLength);
          }

          return null;
        }

        function queueRetryOptions(payload) {
          if (payload == null) {
            return undefined;
          }

          const options = {};
          if (payload.delaySeconds != null) {
            options.delaySeconds = payload.delaySeconds;
          }

          return Object.keys(options).length === 0 ? undefined : options;
        }

        function prepareD1Statement(database, payload) {
          const values = (payload.values ?? []).map(fromD1Value);
          let statement = database.prepare(payload.query);
          if (values.length > 0) {
            statement = statement.bind(...values);
          }

          return statement;
        }

        function d1Session(invocation, binding, payload) {
          let session = invocation.d1Sessions.get(payload.handle);
          if (session != null) {
            return session;
          }

          session = payload.parameter == null
            ? binding.withSession()
            : binding.withSession(payload.parameter);
          invocation.d1Sessions.set(payload.handle, session);
          return session;
        }

        function d1RawOptions(options) {
          if (options?.columnNames == null) {
            return undefined;
          }

          return { columnNames: options.columnNames === true };
        }

        function fromD1Value(value) {
          switch (value.type) {
            case 'null':
              return null;
            case 'real':
            case 'integer':
            case 'text':
            case 'boolean':
              return value.value;
            case 'blob':
              return fromBase64(value.bodyBase64);
            default:
              throw new Error(`Unsupported D1 value type '${value.type}'.`);
          }
        }

        async function cacheForBinding(bindingName) {
          if (bindingName === '$default') {
            return caches.default;
          }

          return await caches.open(bindingName);
        }

        function cacheKey(invocation, key) {
          if (key.request != null) {
            return fromRequestEnvelope(invocation, key.request);
          }

          return key.url;
        }

        function cacheQueryOptions(payload) {
          const options = payload?.options ?? payload;
          return { ignoreMethod: options?.ignoreMethod === true };
        }

        function durableIdEnvelope(id) {
          return {
            id: id.toString(),
            name: id.name ?? null
          };
        }

        function durableStub(namespace, target, options) {
          const runtimeOptions = durableGetOptions(options);
          if (target.name != null) {
            return runtimeOptions == null
              ? namespace.getByName(target.name)
              : namespace.getByName(target.name, runtimeOptions);
          }

          const id = namespace.idFromString(target.id);
          return runtimeOptions == null
            ? namespace.get(id)
            : namespace.get(id, runtimeOptions);
        }

        function durableGetOptions(options) {
          if (options?.locationHint == null) {
            return undefined;
          }

          return { locationHint: options.locationHint };
        }

        function requiredDurableObjectContainer(invocation) {
          const state = requiredDurableObjectState(invocation);
          if (state.container == null) {
            throw new Error('Durable Object container is only available inside a Durable Object invocation with a container binding.');
          }

          return state.container;
        }

        function retainContainerExecProcess(process) {
          const handle = `container-exec:${++nextContainerExecProcessId}`;
          containerExecProcesses.set(handle, process);
          return handle;
        }

        function containerExecProcess(handle) {
          const process = containerExecProcesses.get(handle);
          if (process == null) {
            throw new Error(`Durable Object container process '${handle}' is not defined.`);
          }

          return process;
        }

        function containerTcpPortAddress(payload) {
          if (payload.addressText != null) {
            return payload.addressText;
          }

          const hostname = payload.address.hostname;
          const host = hostname.includes(':') && !hostname.startsWith('[')
            ? `[${hostname}]`
            : hostname;
          return `${host}:${payload.address.port}`;
        }

        function containerStartOptions(options) {
          if (options == null) {
            return undefined;
          }

          const result = {};
          if (options.env != null) {
            result.env = options.env;
          }

          if (options.entrypoint != null) {
            result.entrypoint = options.entrypoint;
          }

          if (options.enableInternet != null) {
            result.enableInternet = options.enableInternet;
          }

          return Object.keys(result).length === 0 ? undefined : result;
        }

        function containerExecOptions(options) {
          if (options == null) {
            return undefined;
        """;
}
