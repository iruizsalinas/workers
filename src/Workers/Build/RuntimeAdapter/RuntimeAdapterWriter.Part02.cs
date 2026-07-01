namespace Workers.Build;

internal static partial class RuntimeAdapterWriter
{
    private const string AdapterPart02 =
        """
          }

          return new Date(value).toISOString();
        }
        // {{WORKER_TAIL_HELPERS_END}}

        function retainInvocation(env, ctx, durableObjectState = null) {
          const invocationId = String(++nextInvocationId);
          invocations.set(invocationId, {
            id: invocationId,
            env,
            ctx,
            durableObjectState,
            refCount: 1,
            emailMessages: new Map(),
            abortControllers: new Map(),
            nativeRequests: new Map(),
            nativeResponses: new Map(),
            nativeReaders: new Map(),
            d1Sessions: new Map()
          });
          return invocationId;
        }

        function releaseInvocation(invocationId) {
          const invocation = invocations.get(invocationId);
          if (invocation == null) {
            return;
          }

          invocation.refCount--;
          if (invocation.refCount <= 0) {
            invocations.delete(invocationId);
          }
        }

        function retainInvocationRef(invocationId) {
          const invocation = requiredInvocation(invocationId);
          invocation.refCount++;
        }

        function requiredInvocation(invocationId) {
          const invocation = invocations.get(invocationId);
          if (invocation == null) {
            throw new Error(`Worker invocation '${invocationId}' is no longer active.`);
          }

          return invocation;
        }

        function retainNativeRequest(invocationId, request) {
          const invocation = invocations.get(invocationId);
          if (invocation == null) {
            throw new Error(`Worker invocation '${invocationId}' is no longer active.`);
          }

          const handle = `request:${++nextNativeRequestId}`;
          invocation.nativeRequests.set(handle, request);
          return handle;
        }

        function nativeRequest(invocation, handle) {
          const request = invocation.nativeRequests.get(handle);
          if (request != null) {
            return request;
          }

          throw new Error(`Native request '${handle}' is not defined.`);
        }

        function retainNativeResponse(invocation, response) {
          const handle = `response:${++nextNativeResponseId}`;
          invocation.nativeResponses.set(handle, response);
          return handle;
        }

        function nativeResponse(invocation, handle) {
          const response = invocation.nativeResponses.get(handle);
          if (response != null) {
            return response;
          }

          throw new Error(`Native response '${handle}' is not defined.`);
        }

        function releaseNativeResponse(invocation, handle) {
          invocation.nativeResponses.delete(handle);
        }

        function nativeBodyStream(invocation, source, handle) {
          switch (source) {
            case 'request':
              return nativeRequest(invocation, handle).body;
            case 'response':
              return nativeResponse(invocation, handle).body;
            default:
              throw new Error(`Unsupported native stream source '${source}'.`);
          }
        }

        function nativeReaderKey(source, handle) {
          return `${source}:${handle}`;
        }

        function nativeReader(invocation, source, handle) {
          const key = nativeReaderKey(source, handle);
          let reader = invocation.nativeReaders.get(key);
          if (reader != null) {
            return reader;
          }

          const stream = nativeBodyStream(invocation, source, handle);
          if (stream == null) {
            throw new Error(`Native ${source} '${handle}' does not have a readable body stream.`);
          }

          reader = stream.getReader();
          invocation.nativeReaders.set(key, reader);
          return reader;
        }

        function releaseNativeReader(invocation, source, handle) {
          invocation.nativeReaders.delete(nativeReaderKey(source, handle));
        }

        async function dispatchBinding(invocationId, bindingName, operation, payloadJson) {
          const invocation = invocations.get(invocationId);
          if (invocation == null) {
            throw new Error(`Worker invocation '${invocationId}' is no longer active.`);
          }

          const payload = payloadJson == null || payloadJson.length === 0
            ? {}
            : JSON.parse(payloadJson);

          // {{WORKER_BINDING_DISPATCH_SWITCH_START}}
          switch (operation) {
            case 'binding.getProperty': {
              const binding = requiredBinding(invocation, bindingName);
              const value = binding[payload.propertyName];
              return JSON.stringify({ value: value === undefined ? null : value });
            }
            case 'binding.invoke': {
              const binding = requiredBinding(invocation, bindingName);
              const method = binding[payload.methodName];
              if (typeof method !== 'function') {
                throw new Error(`Binding method '${payload.methodName}' is not defined.`);
              }

              const value = await method.apply(binding, payload.arguments ?? []);
              return JSON.stringify({ value: value === undefined ? null : value });
            }
            case 'runtime.console': {
              switch (payload.level) {
                case 'debug':
                  console.debug(payload.message);
                  break;
                case 'error':
                  console.error(payload.message);
                  break;
                case 'warn':
                  console.warn(payload.message);
                  break;
                case 'log':
                  console.log(payload.message);
                  break;
                default:
                  throw new Error(`Unsupported console level '${payload.level}'.`);
              }

              return '{}';
            }
            case 'native.request.text': {
              return JSON.stringify({ value: await nativeRequest(invocation, payload.handle).text() });
            }
            case 'native.request.bytes': {
              const bytes = new Uint8Array(await nativeRequest(invocation, payload.handle).arrayBuffer());
              return JSON.stringify({ bodyBase64: bytes.length === 0 ? null : toBase64(bytes) });
            }
            case 'native.response.text': {
              const response = nativeResponse(invocation, payload.handle);
              return JSON.stringify({ value: await new Response(response.body).text() });
            }
            case 'native.response.bytes': {
              const response = nativeResponse(invocation, payload.handle);
              const bytes = new Uint8Array(await new Response(response.body).arrayBuffer());
              return JSON.stringify({ bodyBase64: bytes.length === 0 ? null : toBase64(bytes) });
            }
            case 'stream.read': {
              const reader = nativeReader(invocation, payload.source, payload.handle);
              const result = await reader.read();
              if (result.done === true) {
                releaseNativeReader(invocation, payload.source, payload.handle);
                return JSON.stringify({ done: true, bodyBase64: null });
              }

              const bytes = new Uint8Array(result.value);
              return JSON.stringify({ done: false, bodyBase64: bytes.length === 0 ? null : toBase64(bytes) });
            }
            case 'stream.cancel': {
              const reader = nativeReader(invocation, payload.source, payload.handle);
              try {
                await reader.cancel();
              } finally {
                releaseNativeReader(invocation, payload.source, payload.handle);
              }

              return '{}';
            }
            case 'htmlRewriter.transform': {
              const input = toResponseEnvelope(invocation, payload.response);
              const rewriter = createHtmlRewriter(invocation, payload);
              retainInvocationRef(invocationId);
              try {
                const transformed = rewriter.transform(input);
                const response = wrapHtmlRewriterResponse(transformed, payload.registryId, invocationId);
                return JSON.stringify(fromResponseToNativeEnvelope(invocation, response));
              } catch (error) {
                managedHost.htmlRewriterRelease(payload.registryId);
                releaseInvocation(invocationId);
                throw error;
              }
            }
            case 'kv.getText': {
              const binding = requiredBinding(invocation, bindingName);
              const value = await binding.get(payload.key, kvGetOptions(payload.options, 'text'));
              return JSON.stringify({ value });
            }
            case 'kv.getTextWithMetadata': {
              const binding = requiredBinding(invocation, bindingName);
              const result = await binding.getWithMetadata(payload.key, kvGetOptions(payload.options, 'text'));
              const value = result?.value ?? null;
              const metadata = result?.metadata ?? null;
              return JSON.stringify({ value, metadata });
            }
            case 'kv.getTextBulk': {
              const binding = requiredBinding(invocation, bindingName);
              const result = await binding.get(payload.keys, kvGetOptions(payload.options, 'text'));
              return JSON.stringify({ values: kvMapValues(result, value => value ?? null) });
            }
            case 'kv.getTextBulkWithMetadata': {
              const binding = requiredBinding(invocation, bindingName);
              const result = await binding.getWithMetadata(payload.keys, kvGetOptions(payload.options, 'text'));
              return JSON.stringify({ values: kvMapValues(result, kvMetadataEnvelope) });
            }
            case 'kv.getBytes': {
              const binding = requiredBinding(invocation, bindingName);
              const value = await binding.get(payload.key, kvGetOptions(payload.options, 'arrayBuffer'));
              return JSON.stringify({ bodyBase64: value == null ? null : toBase64(new Uint8Array(value)) });
            }
            case 'kv.getBytesWithMetadata': {
              const binding = requiredBinding(invocation, bindingName);
              const result = await binding.getWithMetadata(payload.key, kvGetOptions(payload.options, 'arrayBuffer'));
              const value = result?.value ?? null;
              const metadata = result?.metadata ?? null;
              return JSON.stringify({
                bodyBase64: value == null ? null : toBase64(new Uint8Array(value)),
                metadata
              });
            }
            case 'kv.getJson': {
              const binding = requiredBinding(invocation, bindingName);
              const value = await binding.get(payload.key, kvGetOptions(payload.options, 'json'));
              return JSON.stringify({ value: value ?? null });
            }
            case 'kv.getJsonWithMetadata': {
              const binding = requiredBinding(invocation, bindingName);
              const result = await binding.getWithMetadata(payload.key, kvGetOptions(payload.options, 'json'));
              const value = result?.value ?? null;
              const metadata = result?.metadata ?? null;
              return JSON.stringify({ value, metadata });
            }
            case 'kv.getJsonBulk': {
              const binding = requiredBinding(invocation, bindingName);
              const result = await binding.get(payload.keys, kvGetOptions(payload.options, 'json'));
              return JSON.stringify({ values: kvMapValues(result, value => value ?? null) });
            }
            case 'kv.getJsonBulkWithMetadata': {
              const binding = requiredBinding(invocation, bindingName);
              const result = await binding.getWithMetadata(payload.keys, kvGetOptions(payload.options, 'json'));
              return JSON.stringify({ values: kvMapValues(result, kvMetadataEnvelope) });
            }
            case 'kv.putText': {
              const binding = requiredBinding(invocation, bindingName);
              await binding.put(payload.key, payload.value, kvPutOptions(payload.options));
              return '{}';
            }
            case 'kv.putBytes': {
              const binding = requiredBinding(invocation, bindingName);
              await binding.put(payload.key, fromBase64(payload.bodyBase64), kvPutOptions(payload.options));
              return '{}';
            }
            case 'kv.putJson': {
              const binding = requiredBinding(invocation, bindingName);
              await binding.put(payload.key, payload.valueJson, kvPutOptions(payload.options));
              return '{}';
            }
            case 'kv.delete': {
              const binding = requiredBinding(invocation, bindingName);
              await binding.delete(payload.key);
              return '{}';
            }
            case 'kv.list': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify(kvListEnvelope(await binding.list(kvListOptions(payload))));
            }
            case 'r2.get': {
              const binding = requiredBinding(invocation, bindingName);
              const object = await binding.get(payload.key, r2GetOptions(payload.options));
              if (object == null) {
                return 'null';
              }

              if (object.arrayBuffer == null) {
                return JSON.stringify({
                  bodyBase64: null,
                  contentType: object.httpMetadata?.contentType ?? null
                });
              }

              const bytes = new Uint8Array(await object.arrayBuffer());
              return JSON.stringify({
                bodyBase64: toBase64(bytes),
                contentType: object.httpMetadata?.contentType ?? null
              });
            }
            case 'r2.head': {
              const binding = requiredBinding(invocation, bindingName);
              const object = await binding.head(payload.key);
              return object == null
                ? 'null'
                : JSON.stringify(r2ObjectEnvelope(object));
            }
            case 'r2.list': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify(r2ObjectsEnvelope(await binding.list(r2ListOptions(payload))));
            }
            case 'r2.put': {
              const binding = requiredBinding(invocation, bindingName);
              const body = fromBase64(payload.bodyBase64);
              const object = await binding.put(payload.key, body, r2PutOptions(payload));
              return object == null
                ? 'null'
                : JSON.stringify(r2ObjectEnvelope(object));
            }
            case 'r2.multipart.create': {
              const binding = requiredBinding(invocation, bindingName);
              const upload = await binding.createMultipartUpload(
                payload.key,
                r2MultipartUploadOptions(payload.options));
              return JSON.stringify({
                key: upload.key,
                uploadId: upload.uploadId
              });
            }
            case 'r2.multipart.uploadPart': {
              const upload = r2MultipartUpload(requiredBinding(invocation, bindingName), payload);
              const part = await upload.uploadPart(payload.partNumber, fromBase64(payload.bodyBase64));
              return JSON.stringify({
                partNumber: part.partNumber,
                etag: part.etag
              });
            }
            case 'r2.multipart.complete': {
              const upload = r2MultipartUpload(requiredBinding(invocation, bindingName), payload);
              const object = await upload.complete((payload.parts ?? []).map(r2UploadedPart));
              return JSON.stringify(r2ObjectEnvelope(object));
            }
            case 'r2.multipart.abort': {
              const upload = r2MultipartUpload(requiredBinding(invocation, bindingName), payload);
              await upload.abort();
              return '{}';
            }
            case 'r2.delete': {
              const binding = requiredBinding(invocation, bindingName);
              await binding.delete(payload.key);
              return '{}';
            }
            case 'r2.deleteMany': {
              const binding = requiredBinding(invocation, bindingName);
              await binding.delete(payload.keys ?? []);
              return '{}';
            }
            case 'service.fetch': {
              const binding = requiredBinding(invocation, bindingName);
              const response = await binding.fetch(
                fromRequestEnvelope(invocation, payload.request),
                fetchOptions(invocation, payload.options));
              return JSON.stringify(fromResponseToNativeEnvelope(invocation, response));
            }
            case 'service.rpc': {
              const binding = requiredBinding(invocation, bindingName);
              const method = binding[payload.methodName];
              if (typeof method !== 'function') {
                throw new Error(`Service binding RPC method '${payload.methodName}' is not defined.`);
              }

              const value = await method.apply(binding, fromRpcArguments(payload.arguments));
              return JSON.stringify({ value: value === undefined ? null : value });
            }
            case 'service.rpcStub': {
              const binding = requiredBinding(invocation, bindingName);
              const method = binding[payload.methodName];
              if (typeof method !== 'function') {
                throw new Error(`Service binding RPC method '${payload.methodName}' is not defined.`);
              }

              const value = await method.apply(binding, fromRpcArguments(payload.arguments));
              return JSON.stringify({ handle: retainRpcStub(value) });
            }
            case 'queue.send': {
              const binding = requiredBinding(invocation, bindingName);
              await binding.send(queueMessageBody(payload), queueSendOptions(payload));
              return '{}';
            }
            case 'queue.sendBatch': {
              const binding = requiredBinding(invocation, bindingName);
              const messages = (payload.messages ?? []).map(message => ({
                body: queueMessageBody(message),
                contentType: message.contentType,
                delaySeconds: message.delaySeconds ?? undefined
              }));
              const options = payload.delaySeconds == null
                ? undefined
                : { delaySeconds: payload.delaySeconds };
              await binding.sendBatch(messages, options);
              return '{}';
            }
            case 'queue.metrics': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify(await binding.metrics());
            }
            case 'd1.exec': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify(await binding.exec(payload.query));
            }
            case 'd1.dump': {
              const binding = requiredBinding(invocation, bindingName);
              const bytes = new Uint8Array(await binding.dump());
              return JSON.stringify({ bodyBase64: toBase64(bytes) });
            }
            case 'd1.run': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify(await prepareD1Statement(binding, payload).run());
            }
            case 'd1.all': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify(await prepareD1Statement(binding, payload).all());
            }
            case 'd1.raw': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify(await prepareD1Statement(binding, payload).raw(d1RawOptions(payload.options)));
            }
            case 'd1.first': {
              const binding = requiredBinding(invocation, bindingName);
              const value = await prepareD1Statement(binding, payload)
                .first(payload.columnName ?? undefined);
              return JSON.stringify({ value: value ?? null });
            }
            case 'd1.batch': {
              const binding = requiredBinding(invocation, bindingName);
              const statements = (payload.statements ?? []).map(statement => prepareD1Statement(binding, statement));
              return JSON.stringify(await binding.batch(statements));
            }
            case 'd1.session.run': {
              const session = d1Session(invocation, requiredBinding(invocation, bindingName), payload);
              return JSON.stringify(await prepareD1Statement(session, payload.payload).run());
            }
            case 'd1.session.all': {
              const session = d1Session(invocation, requiredBinding(invocation, bindingName), payload);
              return JSON.stringify(await prepareD1Statement(session, payload.payload).all());
            }
            case 'd1.session.raw': {
              const session = d1Session(invocation, requiredBinding(invocation, bindingName), payload);
              return JSON.stringify(await prepareD1Statement(session, payload.payload).raw(d1RawOptions(payload.payload.options)));
            }
            case 'd1.session.first': {
              const session = d1Session(invocation, requiredBinding(invocation, bindingName), payload);
              const value = await prepareD1Statement(session, payload.payload)
                .first(payload.payload.columnName ?? undefined);
              return JSON.stringify({ value: value ?? null });
            }
            case 'd1.session.batch': {
              const session = d1Session(invocation, requiredBinding(invocation, bindingName), payload);
              const statements = (payload.payload.statements ?? []).map(statement => prepareD1Statement(session, statement));
              return JSON.stringify(await session.batch(statements));
            }
            case 'd1.session.getBookmark': {
              const session = d1Session(invocation, requiredBinding(invocation, bindingName), payload);
              return JSON.stringify({ bookmark: session.getBookmark() ?? null });
            }
            case 'cache.put': {
              const cache = await cacheForBinding(bindingName);
              await cache.put(cacheKey(invocation, payload.key), toResponseEnvelope(invocation, payload.response));
              return '{}';
            }
            case 'cache.match': {
              const cache = await cacheForBinding(bindingName);
              const response = await cache.match(cacheKey(invocation, payload.key), cacheQueryOptions(payload));
              return response == null
                ? 'null'
                : JSON.stringify(fromResponseToNativeEnvelope(invocation, response));
            }
            case 'cache.delete': {
              const cache = await cacheForBinding(bindingName);
              const deleted = await cache.delete(cacheKey(invocation, payload.key), cacheQueryOptions(payload));
              return JSON.stringify({ deleted });
            }
            case 'durable.idFromName': {
              const binding = requiredBinding(invocation, bindingName);
              const id = binding.idFromName(payload.name);
              return JSON.stringify(durableIdEnvelope(id));
            }
            case 'durable.idFromString': {
              const binding = requiredBinding(invocation, bindingName);
              const id = binding.idFromString(payload.id);
              return JSON.stringify(durableIdEnvelope(id));
            }
            case 'durable.newUniqueId': {
              const binding = requiredBinding(invocation, bindingName);
              const id = payload.options?.jurisdiction == null
                ? binding.newUniqueId()
                : binding.newUniqueId({ jurisdiction: payload.options.jurisdiction });
              return JSON.stringify(durableIdEnvelope(id));
            }
            case 'durable.fetch': {
              const binding = requiredBinding(invocation, bindingName);
              const stub = durableStub(binding, payload.target, payload.options);
              const response = await stub.fetch(fromRequestEnvelope(invocation, payload.request));
              return JSON.stringify(fromResponseToNativeEnvelope(invocation, response));
            }
            case 'durable.rpc': {
              const binding = requiredBinding(invocation, bindingName);
              const stub = durableStub(binding, payload.target, payload.options);
              const method = stub[payload.methodName];
              if (typeof method !== 'function') {
                throw new Error(`Durable Object RPC method '${payload.methodName}' is not defined.`);
              }

              const value = await method.apply(stub, fromRpcArguments(payload.arguments));
              return JSON.stringify({ value: value === undefined ? null : value });
            }
            case 'durable.rpcStub': {
              const binding = requiredBinding(invocation, bindingName);
              const stub = durableStub(binding, payload.target, payload.options);
              const method = stub[payload.methodName];
              if (typeof method !== 'function') {
                throw new Error(`Durable Object RPC method '${payload.methodName}' is not defined.`);
              }

              const value = await method.apply(stub, fromRpcArguments(payload.arguments));
              return JSON.stringify({ handle: retainRpcStub(value) });
            }
            case 'durable.state.blockConcurrencyWhile': {
              await requiredDurableObjectState(invocation).blockConcurrencyWhile(
                () => runManagedInvocation(
                  managedRuntime,
                  managedHost.pumpContinuations,
                  () => managedHost.durableObjectStateCallbackStart(payload.handle),
                  handle => managedHost.poll(handle)));
              return '{}';
            }
            case 'durable.state.abort': {
              const state = requiredDurableObjectState(invocation);
              if (payload.reason == null) {
                state.abort();
              } else {
                state.abort(payload.reason);
              }

              return '{}';
            }
            case 'durable.state.acceptWebSocket': {
              requiredDurableObjectState(invocation).acceptWebSocket(
                webSocket(payload.handle),
                payload.tags ?? []);
              return '{}';
            }
            case 'durable.state.getWebSockets': {
              const sockets = payload.tag == null
                ? requiredDurableObjectState(invocation).getWebSockets()
                : requiredDurableObjectState(invocation).getWebSockets(payload.tag);
              return JSON.stringify({ handles: sockets.map(retainWebSocket) });
            }
            case 'durable.state.getTags': {
              const tags = requiredDurableObjectState(invocation).getTags(webSocket(payload.handle));
              return JSON.stringify({ tags });
            }
            case 'durable.state.setWebSocketAutoResponse': {
              const pair = payload.pair == null
                ? undefined
                : new WebSocketRequestResponsePair(payload.pair.request, payload.pair.response);
              requiredDurableObjectState(invocation).setWebSocketAutoResponse(pair);
              return '{}';
            }
            case 'durable.state.getWebSocketAutoResponse': {
              const pair = requiredDurableObjectState(invocation).getWebSocketAutoResponse();
              return JSON.stringify({
                pair: pair == null ? null : {
                  request: pair.getRequest(),
                  response: pair.getResponse()
                }
              });
            }
            case 'durable.state.getWebSocketAutoResponseTimestamp': {
              const timestamp = requiredDurableObjectState(invocation)
                .getWebSocketAutoResponseTimestamp(webSocket(payload.handle));
              return JSON.stringify({ timestamp: timestamp == null ? null : new Date(timestamp).getTime() });
            }
            case 'durable.state.setHibernatableWebSocketEventTimeout': {
              if (payload.timeoutMilliseconds == null) {
                requiredDurableObjectState(invocation).setHibernatableWebSocketEventTimeout();
              } else {
                requiredDurableObjectState(invocation).setHibernatableWebSocketEventTimeout(
                  payload.timeoutMilliseconds);
              }

              return '{}';
            }
            case 'durable.state.getHibernatableWebSocketEventTimeout': {
              const timeoutMilliseconds = requiredDurableObjectState(invocation)
                .getHibernatableWebSocketEventTimeout();
              return JSON.stringify({ timeoutMilliseconds: timeoutMilliseconds ?? null });
            }
            case 'durable.container.running': {
              return JSON.stringify({ running: requiredDurableObjectContainer(invocation).running === true });
            }
            case 'durable.container.start': {
              requiredDurableObjectContainer(invocation).start(containerStartOptions(payload.options));
              return '{}';
            }
            case 'durable.container.destroy': {
              await requiredDurableObjectContainer(invocation).destroy(payload.error ?? undefined);
              return '{}';
            }
            case 'durable.container.signal': {
              requiredDurableObjectContainer(invocation).signal(payload.signal);
        """;
}
