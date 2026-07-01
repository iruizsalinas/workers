namespace Workers.Build;

internal static partial class RuntimeAdapterWriter
{
    private const string AdapterPart03 =
        """
              return '{}';
            }
            case 'durable.container.monitor': {
              await requiredDurableObjectContainer(invocation).monitor();
              return '{}';
            }
            case 'durable.container.interceptOutboundHttp': {
              await requiredDurableObjectContainer(invocation).interceptOutboundHttp(
                payload.target,
                rpcStub(payload.workerHandle));
              return '{}';
            }
            case 'durable.container.interceptAllOutboundHttp': {
              await requiredDurableObjectContainer(invocation).interceptAllOutboundHttp(
                rpcStub(payload.workerHandle));
              return '{}';
            }
            case 'durable.container.interceptOutboundHttps': {
              await requiredDurableObjectContainer(invocation).interceptOutboundHttps(
                payload.target,
                rpcStub(payload.workerHandle));
              return '{}';
            }
            case 'durable.container.tcpPort.fetch': {
              const response = await requiredDurableObjectContainer(invocation)
                .getTcpPort(payload.port)
                .fetch(
                  fromRequestEnvelope(invocation, payload.fetch.request),
                  fetchOptions(invocation, payload.fetch.options));
              return JSON.stringify(fromResponseToNativeEnvelope(invocation, response));
            }
            case 'durable.container.tcpPort.connect': {
              const socket = requiredDurableObjectContainer(invocation)
                .getTcpPort(payload.port)
                .connect(containerTcpPortAddress(payload));
              return JSON.stringify({ handle: retainTcpSocket(socket) });
            }
            case 'durable.container.exec': {
              const process = await requiredDurableObjectContainer(invocation).exec(
                payload.command ?? [],
                containerExecOptions(payload.options));
              return JSON.stringify({ handle: retainContainerExecProcess(process), pid: process.pid ?? 0 });
            }
            case 'durable.container.exec.output': {
              const output = await containerExecProcess(payload.handle).output();
              return JSON.stringify({
                stdoutBase64: toBase64(new Uint8Array(output.stdout ?? new ArrayBuffer(0))),
                stderrBase64: toBase64(new Uint8Array(output.stderr ?? new ArrayBuffer(0))),
                exitCode: output.exitCode ?? 0
              });
            }
            case 'durable.container.exec.exitCode': {
              const exitCode = await containerExecProcess(payload.handle).exitCode;
              return JSON.stringify({ exitCode });
            }
            case 'durable.container.exec.kill': {
              const process = containerExecProcess(payload.handle);
              if (payload.signal == null) {
                process.kill();
              } else {
                process.kill(payload.signal);
              }

              return '{}';
            }
            case 'durable.container.exec.release': {
              containerExecProcesses.delete(payload.handle);
              return '{}';
            }
            case 'durable.storage.kv.get': {
              const value = requiredDurableObjectStorage(invocation).kv.get(payload.key);
              return JSON.stringify({ value: value === undefined ? null : value });
            }
            case 'durable.storage.kv.put': {
              requiredDurableObjectStorage(invocation).kv.put(payload.key, payload.value);
              return '{}';
            }
            case 'durable.storage.kv.delete': {
              const deleted = requiredDurableObjectStorage(invocation).kv.delete(payload.key);
              return JSON.stringify({ deleted });
            }
            case 'durable.storage.kv.list': {
              const values = requiredDurableObjectStorage(invocation).kv.list(
                durableStorageKvListOptions(payload.options));
              return JSON.stringify({ values: jsonRecordFromMap(values) });
            }
            case 'durable.storage.get': {
              const value = await requiredDurableObjectStorage(invocation).get(
                payload.key,
                durableStorageReadOptions(payload.options));
              return JSON.stringify({ value: value === undefined ? null : value });
            }
            case 'durable.storage.getMany': {
              const values = await requiredDurableObjectStorage(invocation).get(
                payload.keys ?? [],
                durableStorageReadOptions(payload.options));
              return JSON.stringify({ values: jsonRecordFromMap(values) });
            }
            case 'durable.storage.put': {
              await requiredDurableObjectStorage(invocation).put(
                payload.key,
                payload.value,
                durableStorageWriteOptions(payload.options));
              return '{}';
            }
            case 'durable.storage.putMany': {
              await requiredDurableObjectStorage(invocation).put(
                payload.values ?? {},
                durableStorageWriteOptions(payload.options));
              return '{}';
            }
            case 'durable.storage.delete': {
              const deleted = await requiredDurableObjectStorage(invocation).delete(
                payload.key,
                durableStorageWriteOptions(payload.options));
              return JSON.stringify({ deleted });
            }
            case 'durable.storage.deleteMany': {
              const deletedCount = await requiredDurableObjectStorage(invocation).delete(
                payload.keys ?? [],
                durableStorageWriteOptions(payload.options));
              return JSON.stringify({ deletedCount });
            }
            case 'durable.storage.deleteAll': {
              await requiredDurableObjectStorage(invocation).deleteAll(
                durableStorageWriteOptions(payload.options));
              return '{}';
            }
            case 'durable.storage.sync': {
              await requiredDurableObjectStorage(invocation).sync();
              return '{}';
            }
            case 'durable.storage.list': {
              const values = await requiredDurableObjectStorage(invocation).list(
                durableStorageListOptions(payload.options));
              return JSON.stringify({ values: jsonRecordFromMap(values) });
            }
            case 'durable.storage.getAlarm': {
              const alarm = await requiredDurableObjectStorage(invocation).getAlarm(
                durableStorageReadOptions(payload.options));
              return JSON.stringify({ scheduledTime: alarm == null ? null : new Date(alarm).getTime() });
            }
            case 'durable.storage.setAlarm': {
              await requiredDurableObjectStorage(invocation).setAlarm(
                payload.scheduledTime,
                durableStorageWriteOptions(payload.options));
              return '{}';
            }
            case 'durable.storage.deleteAlarm': {
              await requiredDurableObjectStorage(invocation).deleteAlarm(
                durableStorageWriteOptions(payload.options));
              return '{}';
            }
            case 'durable.storage.getCurrentBookmark': {
              const bookmark = await requiredDurableObjectStorage(invocation).getCurrentBookmark();
              return JSON.stringify({ bookmark });
            }
            case 'durable.storage.getBookmarkForTime': {
              const bookmark = await requiredDurableObjectStorage(invocation).getBookmarkForTime(
                payload.timestamp);
              return JSON.stringify({ bookmark });
            }
            case 'durable.storage.onNextSessionRestoreBookmark': {
              const bookmark = await requiredDurableObjectStorage(invocation).onNextSessionRestoreBookmark(
                payload.bookmark);
              return JSON.stringify({ bookmark });
            }
            case 'durable.storage.sql.all': {
              const cursor = durableSqlExec(invocation, payload);
              const rows = cursor.toArray();
              return JSON.stringify(durableSqlRowsEnvelope(cursor, rows));
            }
            case 'durable.storage.sql.one': {
              const cursor = durableSqlExec(invocation, payload);
              const value = cursor.one();
              return JSON.stringify({ value });
            }
            case 'durable.storage.sql.raw': {
              const cursor = durableSqlExec(invocation, payload);
              const rows = cursor.raw().toArray();
              return JSON.stringify(durableSqlRowsEnvelope(cursor, rows));
            }
            case 'durable.storage.sql.transactionSync.raw': {
              const storage = requiredDurableObjectStorage(invocation);
              const results = storage.transactionSync(() => (payload.statements ?? []).map(statement => {
                const cursor = durableSqlExecOnStorage(storage, statement);
                return durableSqlRowsEnvelope(cursor, cursor.raw().toArray());
              }));
              return JSON.stringify({ results });
            }
            case 'durable.storage.sql.cursor.open': {
              const cursor = durableSqlExec(invocation, payload);
              const handle = retainDurableSqlCursor(cursor);
              return JSON.stringify(durableSqlCursorEnvelope(handle, cursor));
            }
            case 'durable.storage.sql.cursor.next': {
              const cursor = durableSqlCursor(payload.handle);
              const result = cursor.next();
              return JSON.stringify(durableSqlCursorNextEnvelope(cursor, result));
            }
            case 'durable.storage.sql.cursor.rawNext': {
              const entry = durableSqlCursorEntry(payload.handle);
              entry.raw ??= entry.cursor.raw();
              const result = entry.raw.next();
              return JSON.stringify(durableSqlCursorNextEnvelope(entry.cursor, result));
            }
            case 'durable.storage.sql.cursor.dispose': {
              durableSqlCursors.delete(payload.handle);
              return '{}';
            }
            case 'durable.storage.sql.databaseSize': {
              return JSON.stringify({
                databaseSize: requiredDurableObjectStorage(invocation).sql.databaseSize
              });
            }
            case 'durable.storage.transaction.begin': {
              const handle = await beginDurableStorageTransaction(requiredDurableObjectStorage(invocation));
              return JSON.stringify({ handle });
            }
            case 'durable.storage.transaction.get': {
              const value = await durableTransaction(payload.handle).get(
                payload.key,
                durableStorageReadOptions(payload.options));
              return JSON.stringify({ value: value === undefined ? null : value });
            }
            case 'durable.storage.transaction.getMany': {
              const values = await durableTransaction(payload.handle).get(
                payload.keys ?? [],
                durableStorageReadOptions(payload.options));
              return JSON.stringify({ values: jsonRecordFromMap(values) });
            }
            case 'durable.storage.transaction.put': {
              await durableTransaction(payload.handle).put(
                payload.key,
                payload.value,
                durableStorageWriteOptions(payload.options));
              return '{}';
            }
            case 'durable.storage.transaction.putMany': {
              await durableTransaction(payload.handle).put(
                payload.values ?? {},
                durableStorageWriteOptions(payload.options));
              return '{}';
            }
            case 'durable.storage.transaction.delete': {
              const deleted = await durableTransaction(payload.handle).delete(
                payload.key,
                durableStorageWriteOptions(payload.options));
              return JSON.stringify({ deleted });
            }
            case 'durable.storage.transaction.deleteMany': {
              const deletedCount = await durableTransaction(payload.handle).delete(
                payload.keys ?? [],
                durableStorageWriteOptions(payload.options));
              return JSON.stringify({ deletedCount });
            }
            case 'durable.storage.transaction.list': {
              const values = await durableTransaction(payload.handle).list(
                durableStorageListOptions(payload.options));
              return JSON.stringify({ values: jsonRecordFromMap(values) });
            }
            case 'durable.storage.transaction.rollback': {
              const entry = durableTransactionEntry(payload.handle);
              entry.txn.rollback();
              entry.resolve();
              await entry.done;
              return '{}';
            }
            case 'durable.storage.transaction.commit': {
              const entry = durableTransactionEntry(payload.handle);
              entry.resolve();
              await entry.done;
              return '{}';
            }
            case 'fetch.global': {
              const response = await fetch(
                fromRequestEnvelope(invocation, payload.request),
                fetchOptions(invocation, payload.options));
              return JSON.stringify(fromResponseToNativeEnvelope(invocation, response));
            }
            case 'dynamicDispatcher.fetch': {
              const binding = requiredBinding(invocation, bindingName);
              const fetcher = binding.get(payload.name, undefined);
              const response = await fetcher.fetch(
                fromRequestEnvelope(invocation, payload.fetch.request),
                fetchOptions(invocation, payload.fetch.options));
              return JSON.stringify(fromResponseToNativeEnvelope(invocation, response));
            }
            case 'dynamicDispatcher.rpc': {
              const binding = requiredBinding(invocation, bindingName);
              const service = binding.get(payload.name, undefined);
              const method = service[payload.methodName];
              if (typeof method !== 'function') {
                throw new Error(`Dynamic Dispatch RPC method '${payload.methodName}' is not defined.`);
              }

              const value = await method.apply(service, fromRpcArguments(payload.arguments));
              return JSON.stringify({ value: value === undefined ? null : value });
            }
            case 'dynamicDispatcher.rpcStub': {
              const binding = requiredBinding(invocation, bindingName);
              const service = binding.get(payload.name, undefined);
              const method = service[payload.methodName];
              if (typeof method !== 'function') {
                throw new Error(`Dynamic Dispatch RPC method '${payload.methodName}' is not defined.`);
              }

              const value = await method.apply(service, fromRpcArguments(payload.arguments));
              return JSON.stringify({ handle: retainRpcStub(value) });
            }
            case 'abort.create': {
              const handle = `abort:${++nextAbortControllerId}`;
              invocation.abortControllers.set(handle, new AbortController());
              return JSON.stringify({ handle });
            }
            case 'abort.abort': {
              const controller = abortController(invocation, payload.handle);
              if (payload.reason == null) {
                controller.abort();
              } else {
                controller.abort(payload.reason);
              }

              return '{}';
            }
            case 'crypto.digest': {
              const digest = await globalThis.crypto.subtle.digest(payload.algorithm, fromBase64(payload.bodyBase64));
              return JSON.stringify({ bodyBase64: toBase64(new Uint8Array(digest)) });
            }
            case 'crypto.randomUUID':
              return JSON.stringify({ value: globalThis.crypto.randomUUID() });
            case 'crypto.getRandomValues': {
              const bytes = new Uint8Array(payload.count);
              globalThis.crypto.getRandomValues(bytes);
              return JSON.stringify({ bodyBase64: toBase64(bytes) });
            }
            case 'crypto.timingSafeEqual': {
              const left = fromBase64(payload.leftBase64);
              const right = fromBase64(payload.rightBase64);
              if (typeof globalThis.crypto.timingSafeEqual === 'function') {
                return JSON.stringify({
                  equal: left.byteLength === right.byteLength
                    && globalThis.crypto.timingSafeEqual(left, right)
                });
              }

              return JSON.stringify({ equal: timingSafeEqualBytes(left, right) });
            }
            case 'crypto.digestStream.create': {
              const stream = new globalThis.crypto.DigestStream(payload.algorithm);
              return JSON.stringify({ handle: retainDigestStream(stream) });
            }
            case 'crypto.digestStream.write':
              await digestStreamWriter(digestStreamEntry(payload.handle)).write(fromBase64(payload.bodyBase64));
              return '{}';
            case 'crypto.digestStream.close': {
              const entry = digestStreamEntry(payload.handle);
              if (entry.writer != null) {
                await entry.writer.close();
                entry.writer.releaseLock();
                entry.writer = null;
              } else {
                const writer = entry.stream.getWriter();
                await writer.close();
                writer.releaseLock();
              }
              return '{}';
            }
            case 'crypto.digestStream.digest': {
              const digest = await digestStreamEntry(payload.handle).stream.digest;
              digestStreams.delete(payload.handle);
              return JSON.stringify({ bodyBase64: toBase64(new Uint8Array(digest)) });
            }
            case 'runtime.delay': {
              await delay(payload.milliseconds);
              return '{}';
            }
            case 'ratelimit.limit': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify(await binding.limit({ key: payload.key }));
            }
            case 'analytics.writeDataPoint': {
              const binding = requiredBinding(invocation, bindingName);
              binding.writeDataPoint({
                indexes: payload.indexes ?? [],
                doubles: payload.doubles ?? [],
                blobs: (payload.blobs ?? []).map(toAnalyticsBlob)
              });
              return '{}';
            }
            case 'versionMetadata.get': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify({
                id: binding.id,
                tag: binding.tag,
                timestamp: binding.timestamp
              });
            }
            case 'secretStore.get': {
              const binding = requiredBinding(invocation, bindingName);
              const value = await binding.get();
              return JSON.stringify({ value: value ?? null });
            }
            case 'hyperdrive.connectionInfo': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify({
                connectionString: binding.connectionString,
                host: binding.host,
                port: binding.port,
                user: binding.user,
                password: binding.password,
                database: binding.database
              });
            }
            case 'hyperdrive.connect': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify({ handle: retainTcpSocket(binding.connect()) });
            }
            case 'ai.run': {
              const binding = requiredBinding(invocation, bindingName);
              const output = await binding.run(payload.model, payload.input);
              return JSON.stringify({ output: output ?? null });
            }
            case 'ai.runBytes': {
              const binding = requiredBinding(invocation, bindingName);
              const output = await binding.run(payload.model, payload.input);
              if (!(output instanceof ReadableStream)) {
                throw new Error('AI model did not return binary data. Use RunAsync for non-binary responses.');
              }

              const bytes = new Uint8Array(await new Response(output).arrayBuffer());
              return JSON.stringify({ bodyBase64: bytes.length === 0 ? null : toBase64(bytes) });
            }
            case 'images.info': {
              const binding = requiredBinding(invocation, bindingName);
              return JSON.stringify(await binding.info(imagesBody(payload)));
            }
            case 'images.pipeline': {
              const binding = requiredBinding(invocation, bindingName);
              let pipeline = binding.input(imagesBody(payload.image));

              for (const operation of payload.operations ?? []) {
                switch (operation.kind) {
                  case 'transform':
                    pipeline = pipeline.transform(operation.options ?? {});
                    break;
                  case 'draw':
                    pipeline = operation.options == null
                      ? pipeline.draw(imagesBody(operation.image))
                      : pipeline.draw(imagesBody(operation.image), operation.options);
                    break;
                  default:
                    throw new Error(`Unsupported Images operation '${operation.kind}'.`);
                }
              }

              const output = await pipeline.output(payload.output);
              const response = await output.response();
              return JSON.stringify(fromResponseToNativeEnvelope(invocation, response));
            }
            case 'media.response': {
              const output = mediaOutput(invocation, bindingName, payload);
              return JSON.stringify(fromResponseToNativeEnvelope(invocation, await output.response()));
            }
            case 'media.media': {
              const output = mediaOutput(invocation, bindingName, payload);
              const media = await output.media();
              const bytes = new Uint8Array(await new Response(media).arrayBuffer());
              return JSON.stringify({
        """;
}
