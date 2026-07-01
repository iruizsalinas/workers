namespace Workers.Build;

internal static partial class RuntimeAdapterWriter
{
    private const string AdapterPart04 =
        """
                bodyBase64: bytes.length === 0 ? null : toBase64(bytes),
                contentType: await output.contentType()
              });
            }
            case 'media.contentType': {
              const output = mediaOutput(invocation, bindingName, payload);
              return JSON.stringify({ contentType: await output.contentType() });
            }
            case 'workflow.create': {
              const binding = requiredBinding(invocation, bindingName);
              const instance = await binding.create(payload.options ?? undefined);
              return JSON.stringify(workflowInstanceEnvelope(instance));
            }
            case 'workflow.createBatch': {
              const binding = requiredBinding(invocation, bindingName);
              const instances = await binding.createBatch(payload.batch ?? []);
              return JSON.stringify({ instances: instances.map(workflowInstanceEnvelope) });
            }
            case 'workflow.get': {
              const binding = requiredBinding(invocation, bindingName);
              const instance = await binding.get(payload.id);
              return JSON.stringify(workflowInstanceEnvelope(instance));
            }
            case 'workflow.instance.status': {
              const instance = await workflowInstance(invocation, bindingName, payload.id);
              return JSON.stringify(await instance.status());
            }
            case 'workflow.instance.pause': {
              const instance = await workflowInstance(invocation, bindingName, payload.id);
              await instance.pause();
              return '{}';
            }
            case 'workflow.instance.resume': {
              const instance = await workflowInstance(invocation, bindingName, payload.id);
              await instance.resume();
              return '{}';
            }
            case 'workflow.instance.terminate': {
              const instance = await workflowInstance(invocation, bindingName, payload.id);
              await instance.terminate();
              return '{}';
            }
            case 'workflow.instance.restart': {
              const instance = await workflowInstance(invocation, bindingName, payload.id);
              await instance.restart(payload.options ?? undefined);
              return '{}';
            }
            case 'workflow.instance.sendEvent': {
              const instance = await workflowInstance(invocation, bindingName, payload.id);
              await instance.sendEvent(payload.options);
              return '{}';
            }
            case 'vectorize.insert': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify(await binding.insert(vectorizeVectors(payload.vectors)));
            }
            case 'vectorize.upsert': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify(await binding.upsert(vectorizeVectors(payload.vectors)));
            }
            case 'vectorize.query': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify(await binding.query(payload.vector ?? [], vectorizeQueryOptions(payload.options)));
            }
            case 'vectorize.queryById': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify(await binding.queryById(payload.id, vectorizeQueryOptions(payload.options)));
            }
            case 'vectorize.getByIds': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify(await binding.getByIds(payload.ids ?? []));
            }
            case 'vectorize.deleteByIds': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify(await binding.deleteByIds(payload.ids ?? []));
            }
            case 'vectorize.describe': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify(await binding.describe());
            }
            case 'email.send': {
              const binding = requiredBinding(invocation, bindingName);
              const result = await binding.send(toSendEmailMessage(payload.message));
              return JSON.stringify({ messageId: result.messageId });
            }
            case 'email.sendRaw': {
              const binding = requiredBinding(invocation, bindingName);
              const result = await binding.send(new EmailMessage(payload.from, payload.to, payload.raw));
              return JSON.stringify({ messageId: result.messageId });
            }
            case 'email.rawBytes': {
              const message = emailMessage(invocation, payload.handle);
              const bytes = new Uint8Array(await new Response(message.raw).arrayBuffer());
              return JSON.stringify({ bodyBase64: bytes.length === 0 ? null : toBase64(bytes) });
            }
            case 'email.reject': {
              const message = emailMessage(invocation, payload.handle);
              message.setReject(payload.reason);
              return '{}';
            }
            case 'email.forward': {
              const message = emailMessage(invocation, payload.handle);
              const result = await message.forward(payload.recipient, fromHeadersEnvelope(payload.headers));
              return JSON.stringify({ messageId: result.messageId });
            }
            case 'email.replyRaw': {
              const message = emailMessage(invocation, payload.handle);
              const result = await message.reply(new EmailMessage(payload.from, payload.to, payload.raw));
              return JSON.stringify({ messageId: result.messageId });
            }
            case 'socket.connect': {
              const socket = connect(tcpSocketAddress(payload), tcpSocketOptions(payload.options));
              return JSON.stringify({ handle: retainTcpSocket(socket) });
            }
            case 'socket.opened': {
              const info = await tcpSocketEntry(payload.handle).socket.opened;
              return JSON.stringify({
                remoteAddress: info?.remoteAddress ?? null,
                localAddress: info?.localAddress ?? null
              });
            }
            case 'socket.closed':
              await tcpSocketEntry(payload.handle).socket.closed;
              tcpSockets.delete(payload.handle);
              return '{}';
            case 'socket.read': {
              const entry = tcpSocketEntry(payload.handle);
              const result = await tcpSocketReader(entry).read();
              if (result.done === true) {
                entry.reader.releaseLock();
                entry.reader = null;
                return JSON.stringify({ done: true, bodyBase64: null });
              }

              const bytes = result.value instanceof Uint8Array
                ? result.value
                : new Uint8Array(result.value);
              return JSON.stringify({ done: false, bodyBase64: toBase64(bytes) });
            }
            case 'socket.write':
              await tcpSocketWriter(tcpSocketEntry(payload.handle)).write(fromBase64(payload.bodyBase64));
              return '{}';
            case 'socket.closeWritable': {
              const entry = tcpSocketEntry(payload.handle);
              if (entry.writer != null) {
                await entry.writer.close();
                entry.writer.releaseLock();
                entry.writer = null;
              } else {
                const writer = entry.socket.writable.getWriter();
                await writer.close();
                writer.releaseLock();
              }
              return '{}';
            }
            case 'socket.close':
              await tcpSocketEntry(payload.handle).socket.close();
              tcpSockets.delete(payload.handle);
              return '{}';
            case 'socket.startTls': {
              const socket = tcpSocketEntry(payload.handle).socket.startTls();
              tcpSockets.delete(payload.handle);
              return JSON.stringify({ handle: retainTcpSocket(socket) });
            }
            case 'websocket.connect': {
              const response = await fetch(webSocketConnectRequest(payload));
              if (response.webSocket == null) {
                throw new Error('WebSocket server did not accept the upgrade request.');
              }

              return JSON.stringify({ handle: retainWebSocket(response.webSocket) });
            }
            case 'websocket.createPair': {
              const pair = new WebSocketPair();
              const [client, server] = Object.values(pair);
              const clientHandle = retainWebSocket(client);
              const serverHandle = retainWebSocket(server);
              return JSON.stringify({ client: clientHandle, server: serverHandle });
            }
            case 'websocket.accept':
              webSocket(payload.handle).accept();
              return '{}';
            case 'websocket.sendText':
              webSocket(payload.handle).send(payload.message);
              return '{}';
            case 'websocket.sendBytes':
              webSocket(payload.handle).send(fromBase64(payload.bodyBase64));
              return '{}';
            case 'websocket.receive': {
              const event = await nextWebSocketEvent(webSocketEntry(payload.handle));
              return JSON.stringify({ event });
            }
            case 'websocket.close': {
              const socket = webSocket(payload.handle);
              if (payload.code != null && payload.reason != null) {
                socket.close(payload.code, payload.reason);
              } else if (payload.code != null) {
                socket.close(payload.code);
              } else {
                socket.close();
              }
              releaseWebSocket(payload.handle);
              return '{}';
            }
            case 'rpc.stub.invoke': {
              const stub = rpcStub(payload.handle);
              const method = stub[payload.methodName];
              if (typeof method !== 'function') {
                throw new Error(`RPC stub method '${payload.methodName}' is not defined.`);
              }

              const value = await method(...fromRpcArguments(payload.arguments));
              return JSON.stringify({ value: value === undefined ? null : value });
            }
            case 'rpc.stub.invokeStub': {
              const stub = rpcStub(payload.handle);
              const method = stub[payload.methodName];
              if (typeof method !== 'function') {
                throw new Error(`RPC stub method '${payload.methodName}' is not defined.`);
              }

              const value = await method(...fromRpcArguments(payload.arguments));
              return JSON.stringify({ handle: retainRpcStub(value) });
            }
            case 'rpc.stub.call': {
              const stub = rpcStub(payload.handle);
              if (typeof stub !== 'function') {
                throw new Error(`RPC stub '${payload.handle}' is not callable.`);
              }

              const value = await stub(...fromRpcArguments(payload.arguments));
              return JSON.stringify({ value: value === undefined ? null : value });
            }
            case 'rpc.stub.callStub': {
              const stub = rpcStub(payload.handle);
              if (typeof stub !== 'function') {
                throw new Error(`RPC stub '${payload.handle}' is not callable.`);
              }

              const value = await stub(...fromRpcArguments(payload.arguments));
              return JSON.stringify({ handle: retainRpcStub(value) });
            }
            case 'rpc.stub.dup': {
              const stub = rpcStub(payload.handle);
              if (typeof stub.dup !== 'function') {
                throw new Error(`RPC stub '${payload.handle}' does not support dup().`);
              }

              return JSON.stringify({ handle: retainRpcStub(stub.dup()) });
            }
            case 'rpc.stub.dispose': {
              releaseRpcStub(payload.handle);
              return '{}';
            }
            default:
              throw new Error(`Unsupported binding operation '${operation}'.`);
          }
          // {{WORKER_BINDING_DISPATCH_SWITCH_END}}
        }

        // {{WORKER_PLATFORM_HELPERS_START}}
        function requiredBinding(invocation, bindingName) {
          const binding = invocation.env?.[bindingName];
          if (binding == null) {
            throw new Error(`Binding '${bindingName}' is not defined.`);
          }

          return binding;
        }

        async function workflowInstance(invocation, bindingName, id) {
          return await requiredBinding(invocation, bindingName).get(id);
        }

        function workflowInstanceEnvelope(instance) {
          return {
            id: instance.id
          };
        }

        function retainRpcStub(stub) {
          if (stub == null || !['object', 'function'].includes(typeof stub)) {
            throw new Error('RPC call did not return an object-capability stub.');
          }

          const handle = `rpc:${++nextRpcStubId}`;
          rpcStubs.set(handle, stub);
          return handle;
        }

        function rpcStub(handle) {
          const stub = rpcStubs.get(handle);
          if (stub == null) {
            throw new Error(`RPC stub '${handle}' is not defined.`);
          }

          return stub;
        }

        function releaseRpcStub(handle) {
          const stub = rpcStubs.get(handle);
          if (stub == null) {
            return;
          }

          rpcStubs.delete(handle);
          const dispose = stub[Symbol.dispose];
          if (typeof dispose === 'function') {
            dispose.call(stub);
          }
        }

        function fromRpcArguments(argumentsValue) {
          return (argumentsValue ?? []).map(argument => {
            if (argument?.rpcStubHandle != null) {
              return rpcStub(argument.rpcStubHandle);
            }

            return argument;
          });
        }

        function managedRpcReturnValue(envelope, host) {
          if (envelope?.rpcTargetHandle != null) {
            return createManagedRpcTarget(envelope.rpcTargetHandle, host);
          }

          return envelope?.value ?? null;
        }

        function createManagedRpcTarget(handle, host) {
          let disposed = false;
          const target = {
            dup() {
              if (disposed) {
                throw new Error(`Managed RPC target '${handle}' is disposed.`);
              }

              const result = host.managedRpcTargetDup(JSON.stringify({ handle }));
              const envelope = typeof result === 'string' ? JSON.parse(result) : result;
              return createManagedRpcTarget(envelope.handle, host);
            },
            [Symbol.dispose]() {
              if (disposed) {
                return;
              }

              disposed = true;
              return runManagedInvocation(
                managedRuntime,
                host.pumpContinuations,
                () => host.managedRpcTargetDisposeStart(JSON.stringify({ handle })),
                value => host.poll(value));
            }
          };

          return new Proxy(target, {
            get(targetObject, property) {
              if (property in targetObject) {
                return targetObject[property];
              }

              if (typeof property !== 'string') {
                return undefined;
              }

              return async (...args) => {
                if (disposed) {
                  throw new Error(`Managed RPC target '${handle}' is disposed.`);
                }

                const invocationId = retainInvocation(null, null);
                try {
                  const payloadJson = JSON.stringify({
                    invocationId,
                    handle,
                    methodName: property,
                    arguments: toManagedRpcArguments(args)
                  });
                  const result = await runManagedInvocation(
                    managedRuntime,
                    host.pumpContinuations,
                    () => host.managedRpcTargetInvokeStart(payloadJson),
                    value => host.poll(value));
                  const envelope = typeof result === 'string' ? JSON.parse(result) : result;
                  return managedRpcReturnValue(envelope, host);
                } finally {
                  releaseInvocation(invocationId);
                }
              };
            }
          });
        }

        function toManagedRpcArguments(argumentsValue) {
          return (argumentsValue ?? []).map(argument => {
            if (isRpcStubLike(argument)) {
              return { rpcStubHandle: retainRpcStub(argument) };
            }

            return argument;
          });
        }

        function isRpcStubLike(value) {
          if (value == null) {
            return false;
          }

          if (typeof value === 'function') {
            return true;
          }

          return typeof value === 'object'
            && (typeof value.dup === 'function' || typeof value[Symbol.dispose] === 'function');
        }

        function kvListOptions(payload) {
          const options = {};
          if (payload.limit != null) {
            options.limit = payload.limit;
          }
          if (payload.cursor != null) {
            options.cursor = payload.cursor;
          }
          if (payload.prefix != null) {
            options.prefix = payload.prefix;
          }

          return options;
        }

        function kvGetOptions(payload, type) {
          const options = { type };
          if (payload?.cacheTtl != null) {
            options.cacheTtl = payload.cacheTtl;
          }

          return options;
        }

        function kvPutOptions(payload) {
          if (payload == null) {
            return undefined;
          }

          const options = {};
          if (payload.expiration != null) {
            options.expiration = payload.expiration;
          }
          if (payload.expirationTtl != null) {
            options.expirationTtl = payload.expirationTtl;
          }
          if (payload.metadata != null) {
            options.metadata = payload.metadata;
          }

          return Object.keys(options).length === 0 ? undefined : options;
        }

        function kvMapValues(result, mapValue) {
        """;
}
