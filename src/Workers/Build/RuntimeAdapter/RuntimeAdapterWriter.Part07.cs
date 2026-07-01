namespace Workers.Build;

internal static partial class RuntimeAdapterWriter
{
    private const string AdapterPart07 =
        """
          }

          const result = {};
          if (options.stdin != null) {
            result.stdin = options.stdin;
          }

          if (options.stdout != null) {
            result.stdout = options.stdout;
          }

          if (options.stderr != null) {
            result.stderr = options.stderr;
          }

          if (options.cwd != null) {
            result.cwd = options.cwd;
          }

          if (options.env != null) {
            result.env = options.env;
          }

          if (options.user != null) {
            result.user = options.user;
          }

          return Object.keys(result).length === 0 ? undefined : result;
        }

        function requiredDurableObjectStorage(invocation) {
          const state = requiredDurableObjectState(invocation);
          if (state.storage == null) {
            throw new Error('Durable Object storage is only available inside a Durable Object invocation.');
          }

          return state.storage;
        }

        function requiredDurableObjectState(invocation) {
          if (invocation.durableObjectState == null) {
            throw new Error('Durable Object state is only available inside a Durable Object invocation.');
          }

          return invocation.durableObjectState;
        }

        function durableObjectId(state) {
          const id = state?.id;
          if (id == null) {
            throw new Error('Durable Object state is missing its id.');
          }

          return id.toString();
        }

        function durableSqlExec(invocation, payload) {
          return durableSqlExecOnStorage(requiredDurableObjectStorage(invocation), payload);
        }

        function durableSqlExecOnStorage(storage, payload) {
          return storage.sql.exec(
            payload.query,
            ...(payload.values ?? []).map(fromD1Value));
        }

        function durableSqlRowsEnvelope(cursor, rows) {
          return {
            rows,
            columnNames: cursor.columnNames ?? [],
            rowsRead: cursor.rowsRead ?? 0,
            rowsWritten: cursor.rowsWritten ?? 0
          };
        }

        function retainDurableSqlCursor(cursor) {
          const handle = `sql-cursor:${++nextDurableSqlCursorId}`;
          durableSqlCursors.set(handle, { cursor, raw: null });
          return handle;
        }

        function durableSqlCursor(handle) {
          return durableSqlCursorEntry(handle).cursor;
        }

        function durableSqlCursorEntry(handle) {
          const entry = durableSqlCursors.get(handle);
          if (entry == null) {
            throw new Error(`Durable Object SQL cursor '${handle}' is not defined.`);
          }

          return entry;
        }

        function durableSqlCursorEnvelope(handle, cursor) {
          return {
            handle,
            columnNames: cursor.columnNames ?? [],
            rowsRead: cursor.rowsRead ?? 0,
            rowsWritten: cursor.rowsWritten ?? 0
          };
        }

        function durableSqlCursorNextEnvelope(cursor, result) {
          return {
            done: result.done === true,
            value: result.done === true ? null : result.value,
            columnNames: cursor.columnNames ?? [],
            rowsRead: cursor.rowsRead ?? 0,
            rowsWritten: cursor.rowsWritten ?? 0
          };
        }

        async function beginDurableStorageTransaction(storage) {
          let enteredResolve;
          let enteredReject;
          const entered = new Promise((resolve, reject) => {
            enteredResolve = resolve;
            enteredReject = reject;
          });

          const done = storage.transaction(async txn => {
            const handle = `txn:${++nextDurableTransactionId}`;
            let resolve;
            let reject;
            const finished = new Promise((finishResolve, finishReject) => {
              resolve = finishResolve;
              reject = finishReject;
            });
            const entry = { txn, resolve, reject, done: null };
            durableTransactions.set(handle, entry);
            enteredResolve(handle);

            try {
              await finished;
            } finally {
              durableTransactions.delete(handle);
            }
          });

          done.catch(error => enteredReject(error));
          const handle = await entered;
          durableTransactionEntry(handle).done = done;
          return handle;
        }

        function durableTransaction(handle) {
          return durableTransactionEntry(handle).txn;
        }

        function durableTransactionEntry(handle) {
          const entry = durableTransactions.get(handle);
          if (entry == null) {
            throw new Error(`Durable Object transaction '${handle}' is not defined.`);
          }

          return entry;
        }

        function durableStorageReadOptions(options) {
          if (options == null) {
            return undefined;
          }

          const result = {};
          if (options.allowConcurrency != null) {
            result.allowConcurrency = options.allowConcurrency;
          }
          if (options.noCache != null) {
            result.noCache = options.noCache;
          }

          return Object.keys(result).length === 0 ? undefined : result;
        }

        function durableStorageWriteOptions(options) {
          if (options == null) {
            return undefined;
          }

          const result = durableStorageReadOptions(options) ?? {};
          if (options.allowUnconfirmed != null) {
            result.allowUnconfirmed = options.allowUnconfirmed;
          }

          return Object.keys(result).length === 0 ? undefined : result;
        }

        function durableStorageListOptions(options) {
          if (options == null) {
            return undefined;
          }

          const result = durableStorageReadOptions(options) ?? {};
          if (options.start != null) {
            result.start = options.start;
          }
          if (options.startAfter != null) {
            result.startAfter = options.startAfter;
          }
          if (options.end != null) {
            result.end = options.end;
          }
          if (options.prefix != null) {
            result.prefix = options.prefix;
          }
          if (options.reverse != null) {
            result.reverse = options.reverse;
          }
          if (options.limit != null) {
            result.limit = options.limit;
          }

          return Object.keys(result).length === 0 ? undefined : result;
        }

        function durableStorageKvListOptions(options) {
          if (options == null) {
            return undefined;
          }

          const result = {};
          if (options.start != null) {
            result.start = options.start;
          }
          if (options.startAfter != null) {
            result.startAfter = options.startAfter;
          }
          if (options.end != null) {
            result.end = options.end;
          }
          if (options.prefix != null) {
            result.prefix = options.prefix;
          }
          if (options.reverse != null) {
            result.reverse = options.reverse;
          }
          if (options.limit != null) {
            result.limit = options.limit;
          }

          return Object.keys(result).length === 0 ? undefined : result;
        }

        function jsonRecordFromMap(values) {
          if (values == null) {
            return {};
          }

          if (values instanceof Map) {
            return Object.fromEntries(values);
          }

          return values;
        }

        // {{WORKER_PLATFORM_HELPERS_END}}

        // {{WORKER_PLATFORM_CONVERSION_HELPERS_START}}
        function headersEnvelope(headers) {
          const values = [];

          for (const [name, value] of headers ?? []) {
            if (name.toLowerCase() !== 'set-cookie') {
              values.push({ name, value });
            }
          }

          for (const value of setCookieHeaders(headers)) {
            values.push({ name: 'set-cookie', value });
          }

          return values;
        }

        function setCookieHeaders(headers) {
          if (headers == null) {
            return [];
          }

          if (typeof headers.getSetCookie === 'function') {
            return headers.getSetCookie();
          }

          if (typeof headers.getAll === 'function') {
            try {
              return headers.getAll('Set-Cookie');
            } catch {
              return [];
            }
          }

          return [];
        }

        function fromRequestEnvelope(invocation, envelope) {
          const headers = new Headers();
          for (const header of envelope.headers ?? []) {
            headers.append(header.name, header.value);
          }

          if (envelope.nativeRequestHandle != null) {
            const request = nativeRequest(invocation, envelope.nativeRequestHandle);
            const init = {
              method: envelope.method,
              headers
            };

            if (request.body != null && envelope.method !== 'GET' && envelope.method !== 'HEAD') {
              init.body = request.body;
            }

            return new Request(envelope.url, init);
          }

          const body = envelope.bodyBase64 == null
            ? undefined
            : fromBase64(envelope.bodyBase64);

          return new Request(envelope.url, {
            method: envelope.method,
            headers,
            body
          });
        }

        function imagesBody(payload) {
          if (payload?.bodyBase64 == null) {
            throw new Error('Images body payload is required.');
          }

          const headers = payload.contentType == null
            ? undefined
            : { 'content-type': payload.contentType };

          return new Response(fromBase64(payload.bodyBase64), { headers }).body;
        }

        function mediaBody(payload) {
          if (payload?.bodyBase64 == null) {
            throw new Error('Media body payload is required.');
          }

          const headers = payload.contentType == null
            ? undefined
            : { 'content-type': payload.contentType };

          return new Response(fromBase64(payload.bodyBase64), { headers }).body;
        }

        function mediaOutput(invocation, bindingName, payload) {
          const binding = requiredBinding(invocation, bindingName);
          let pipeline = binding.input(mediaBody(payload.media));

          if (payload.hasTransform === true) {
            pipeline = payload.transformOptions == null
              ? pipeline.transform()
              : pipeline.transform(payload.transformOptions);
          }

          return pipeline.output(payload.output);
        }

        async function fromResponseToEnvelope(response) {
          const bytes = new Uint8Array(await response.arrayBuffer());

          return {
            status: response.status,
            statusText: response.statusText,
            headers: headersEnvelope(response.headers),
            bodyBase64: bytes.length === 0 ? null : toBase64(bytes),
            cf: response.cf ?? null
          };
        }

        function fromResponseToNativeEnvelope(invocation, response) {
          return {
            status: response.status,
            statusText: response.statusText,
            headers: headersEnvelope(response.headers),
            bodyBase64: null,
            nativeResponseHandle: retainNativeResponse(invocation, response),
            cf: response.cf ?? null
          };
        }

        function createHtmlRewriter(invocation, payload) {
          const rewriter = new HTMLRewriter();
          for (const item of payload.selectors ?? []) {
            rewriter.on(item.selector, createHtmlElementHandler(invocation, payload.registryId, item.handlerId));
          }

          if (payload.documentHandlerId != null) {
            rewriter.onDocument(createHtmlDocumentHandler(invocation, payload.registryId, payload.documentHandlerId));
          }

          return rewriter;
        }

        function createHtmlElementHandler(invocation, registryId, handlerId) {
          return {
            element: element => invokeHtmlRewriterCallback(registryId, handlerId, 'element', htmlElementSnapshot(element))
              .then(actions => applyHtmlElementActions(invocation, element, registryId, actions)),
            text: text => invokeHtmlRewriterCallback(registryId, handlerId, 'text', htmlTextSnapshot(text))
              .then(actions => applyHtmlTextActions(invocation, text, actions)),
            comments: comment => invokeHtmlRewriterCallback(registryId, handlerId, 'comments', htmlCommentSnapshot(comment))
              .then(actions => applyHtmlCommentActions(invocation, comment, actions))
          };
        }

        function createHtmlDocumentHandler(invocation, registryId, handlerId) {
          return {
            doctype: doctype => invokeHtmlRewriterCallback(registryId, handlerId, 'doctype', htmlDoctypeSnapshot(doctype)),
            text: text => invokeHtmlRewriterCallback(registryId, handlerId, 'text', htmlTextSnapshot(text))
              .then(actions => applyHtmlTextActions(invocation, text, actions)),
            comments: comment => invokeHtmlRewriterCallback(registryId, handlerId, 'comments', htmlCommentSnapshot(comment))
              .then(actions => applyHtmlCommentActions(invocation, comment, actions)),
            end: end => invokeHtmlRewriterCallback(registryId, handlerId, 'end', {})
              .then(actions => applyHtmlDocumentEndActions(invocation, end, actions))
          };
        }

        async function invokeHtmlRewriterCallback(registryId, handlerId, kind, snapshot) {
          const payloadJson = JSON.stringify({ registryId, handlerId, kind, snapshot });
          const result = await runManagedInvocation(
            managedRuntime,
            managedHost.pumpContinuations,
            () => managedHost.htmlRewriterCallbackStart(payloadJson),
            value => managedHost.poll(value));
          return typeof result === 'string' ? JSON.parse(result) : (result ?? []);
        }

        function htmlElementSnapshot(element) {
          return {
            tagName: element.tagName,
            namespaceUri: element.namespaceURI ?? null,
            removed: element.removed === true,
            attributes: Array.from(element.attributes, ([name, value]) => ({ name, value }))
          };
        }

        function htmlTextSnapshot(text) {
          return {
            text: text.text,
            lastInTextNode: text.lastInTextNode === true,
            removed: text.removed === true
          };
        }

        function htmlCommentSnapshot(comment) {
          return {
            text: comment.text,
            removed: comment.removed === true
          };
        }

        function htmlDoctypeSnapshot(doctype) {
          return {
            name: doctype.name ?? null,
            publicId: doctype.publicId ?? null,
            systemId: doctype.systemId ?? null
          };
        }

        function htmlEndTagSnapshot(endTag) {
          return {
            name: endTag.name
          };
        }

        function applyHtmlElementActions(invocation, element, registryId, actions) {
          for (const action of actions ?? []) {
            switch (action.type) {
              case 'setTagName':
                element.tagName = action.value;
                break;
              case 'setAttribute':
                element.setAttribute(action.name, action.value);
                break;
              case 'removeAttribute':
                element.removeAttribute(action.name);
                break;
              case 'before':
                element.before(htmlActionContent(invocation, action), htmlContentOptions(action));
                break;
              case 'after':
                element.after(htmlActionContent(invocation, action), htmlContentOptions(action));
                break;
              case 'prepend':
                element.prepend(htmlActionContent(invocation, action), htmlContentOptions(action));
                break;
              case 'append':
                element.append(htmlActionContent(invocation, action), htmlContentOptions(action));
                break;
              case 'replace':
                element.replace(htmlActionContent(invocation, action), htmlContentOptions(action));
                break;
              case 'setInnerContent':
                element.setInnerContent(htmlActionContent(invocation, action), htmlContentOptions(action));
                break;
              case 'remove':
                element.remove();
                break;
              case 'removeAndKeepContent':
                element.removeAndKeepContent();
                break;
              case 'onEndTag':
                element.onEndTag(endTag => invokeHtmlRewriterCallback(registryId, action.handlerId, 'endTag', htmlEndTagSnapshot(endTag))
                  .then(endActions => applyHtmlEndTagActions(invocation, endTag, endActions)));
                break;
              default:
                throw new Error(`Unsupported HTMLRewriter element action '${action.type}'.`);
            }
          }
        }

        function applyHtmlEndTagActions(invocation, endTag, actions) {
          for (const action of actions ?? []) {
            switch (action.type) {
              case 'setName':
                endTag.name = action.value;
                break;
              case 'before':
                endTag.before(htmlActionContent(invocation, action), htmlContentOptions(action));
                break;
              case 'after':
                endTag.after(htmlActionContent(invocation, action), htmlContentOptions(action));
                break;
              case 'remove':
                endTag.remove();
                break;
              default:
                throw new Error(`Unsupported HTMLRewriter end tag action '${action.type}'.`);
            }
          }
        }

        function applyHtmlTextActions(invocation, text, actions) {
          for (const action of actions ?? []) {
            switch (action.type) {
              case 'before':
                text.before(htmlActionContent(invocation, action), htmlContentOptions(action));
                break;
              case 'after':
                text.after(htmlActionContent(invocation, action), htmlContentOptions(action));
                break;
              case 'replace':
                text.replace(htmlActionContent(invocation, action), htmlContentOptions(action));
                break;
              case 'remove':
                text.remove();
                break;
              default:
                throw new Error(`Unsupported HTMLRewriter text action '${action.type}'.`);
            }
          }
        }

        function applyHtmlCommentActions(invocation, comment, actions) {
          for (const action of actions ?? []) {
            switch (action.type) {
              case 'setText':
                comment.text = action.value;
                break;
              case 'before':
                comment.before(htmlActionContent(invocation, action), htmlContentOptions(action));
                break;
              case 'after':
                comment.after(htmlActionContent(invocation, action), htmlContentOptions(action));
                break;
              case 'replace':
                comment.replace(htmlActionContent(invocation, action), htmlContentOptions(action));
                break;
              case 'remove':
                comment.remove();
                break;
              default:
                throw new Error(`Unsupported HTMLRewriter comment action '${action.type}'.`);
            }
          }
        }

        function applyHtmlDocumentEndActions(invocation, end, actions) {
          for (const action of actions ?? []) {
            switch (action.type) {
              case 'append':
                end.append(htmlActionContent(invocation, action), htmlContentOptions(action));
                break;
              default:
                throw new Error(`Unsupported HTMLRewriter document end action '${action.type}'.`);
            }
          }
        }

        function htmlContentOptions(action) {
          if (action.hasContentOptions !== true) {
            return undefined;
          }

          return { html: action.html === true };
        }

        function htmlActionContent(invocation, action) {
          if (action.response != null) {
            return toResponseEnvelope(invocation, action.response);
          }

          if (action.streamSource != null && action.streamHandle != null) {
            if (action.streamSource === 'managed') {
              return createManagedReadableStream(invocation, action.streamHandle);
            }

            return nativeBodyStream(invocation, action.streamSource, action.streamHandle);
          }

          return action.content ?? '';
        }

        function wrapHtmlRewriterResponse(response, registryId, invocationId) {
          let released = false;
          const release = () => {
            if (released) {
              return;
            }

            released = true;
            managedHost.htmlRewriterRelease(registryId);
            releaseInvocation(invocationId);
          };

          if (response.body == null) {
            release();
            return response;
          }

          const reader = response.body.getReader();

          const body = new ReadableStream({
            async pull(controller) {
              try {
                const result = await reader.read();
                if (result.done === true) {
                  release();
                  controller.close();
                  return;
                }

                controller.enqueue(result.value);
              } catch (error) {
                release();
                controller.error(error);
              }
            },
            async cancel(reason) {
              try {
                await reader.cancel(reason);
              } finally {
                release();
              }
            }
          });

          return new Response(body, response);
        }

        function createManagedReadableStream(invocation, handle) {
          let released = false;
          const release = () => {
            if (released) {
              return;
            }

            released = true;
            releaseInvocation(invocation.id);
          };

          retainInvocationRef(invocation.id);

          return new ReadableStream({
            async pull(controller) {
              try {
                const result = await runManagedInvocation(
                  managedRuntime,
                  managedHost.pumpContinuations,
                  () => managedHost.managedReadableStreamPullStart(handle),
                  value => managedHost.poll(value));

                const read = typeof result === 'string' ? JSON.parse(result) : result;
                if (read?.done === true) {
                  release();
                  controller.close();
                  return;
                }

                controller.enqueue(read?.bodyBase64 == null ? new Uint8Array() : fromBase64(read.bodyBase64));
              } catch (error) {
                release();
                controller.error(error);
              }
            },
            async cancel() {
              try {
                await runManagedInvocation(
                  managedRuntime,
                  managedHost.pumpContinuations,
                  () => managedHost.managedReadableStreamCancelStart(handle),
                  value => managedHost.poll(value));
              } finally {
                release();
              }
            }
          });
        }
        // {{WORKER_PLATFORM_CONVERSION_HELPERS_END}}

        async function resolveManagedHost(runtime, config) {
          const exportSets = [
            await runtime.getAssemblyExports('Workers'),
            await runtime.getAssemblyExports(config.mainAssemblyName)
          ];

          const candidates = [
            ['Workers', 'Interop', 'Host'],
            ['Host']
          ];

          for (const exports of exportSets) {
            for (const candidate of candidates) {
              const resolved = resolvePath(exports, candidate);
              if (resolved != null) {
                return normalizeManagedHost(resolved);
              }
            }
          }

          throw new Error('Unable to find Workers.Interop.Host in .NET exports.');
        }

        function normalizeManagedHost(host) {
          return {
            pumpContinuations: host.PumpContinuations,
            poll: host.Poll,
            fetchStart: host.FetchStart,
            fetchPoll: host.FetchPoll,
            scheduledStart: host.ScheduledStart,
            queueStart: host.QueueStart,
            emailStart: host.EmailStart,
            tailStart: host.TailStart,
            durableObjectFetchStart: host.DurableObjectFetchStart,
            durableObjectAlarmStart: host.DurableObjectAlarmStart,
            durableObjectRpcStart: host.DurableObjectRpcStart,
            managedRpcTargetInvokeStart: host.ManagedRpcTargetInvokeStart,
            managedRpcTargetDup: host.ManagedRpcTargetDup,
            managedRpcTargetDisposeStart: host.ManagedRpcTargetDisposeStart,
            durableObjectWebSocketMessageStart: host.DurableObjectWebSocketMessageStart,
            durableObjectWebSocketCloseStart: host.DurableObjectWebSocketCloseStart,
            durableObjectWebSocketErrorStart: host.DurableObjectWebSocketErrorStart,
            waitUntilStart: host.WaitUntilStart,
            durableObjectStateCallbackStart: host.DurableObjectStateCallbackStart,
            htmlRewriterCallbackStart: host.HtmlRewriterCallbackStart,
            htmlRewriterRelease: host.HtmlRewriterRelease,
            managedReadableStreamPullStart: host.ManagedReadableStreamPullStart,
            managedReadableStreamCancelStart: host.ManagedReadableStreamCancelStart
          };
        }

        function resolvePath(value, path) {
          let current = value;

          for (const segment of path) {
            if (current == null || !(segment in current)) {
              return null;
            }

            current = current[segment];
          }

          return current;
        }

        function toBase64(bytes) {
          let binary = '';
          for (const byte of bytes) {
            binary += String.fromCharCode(byte);
          }

          return btoa(binary);
        }

        function fromBase64(value) {
          const binary = atob(value);
          const bytes = new Uint8Array(binary.length);

          for (let index = 0; index < binary.length; index++) {
            bytes[index] = binary.charCodeAt(index);
          }

          return bytes;
        }
        """;
}
