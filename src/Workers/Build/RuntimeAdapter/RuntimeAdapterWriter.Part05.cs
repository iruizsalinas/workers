namespace Workers.Build;

internal static partial class RuntimeAdapterWriter
{
    private const string AdapterPart05 =
        """
          const values = {};
          const entries = result instanceof Map
            ? result.entries()
            : Object.entries(result ?? {});

          for (const [key, value] of entries) {
            values[key] = mapValue(value);
          }

          return values;
        }

        function kvMetadataEnvelope(entry) {
          return {
            value: entry?.value ?? null,
            metadata: entry?.metadata ?? null
          };
        }

        function kvListEnvelope(result) {
          return {
            keys: (result.keys ?? []).map(key => ({
              name: key.name,
              expiration: key.expiration ?? null,
              metadata: key.metadata ?? null
            })),
            listComplete: result.list_complete === true || result.listComplete === true,
            cursor: result.cursor ?? null
          };
        }

        function fetchOptions(invocation, options) {
          if (options == null) {
            return undefined;
          }

          const init = {};
          if (options.signalHandle != null) {
            init.signal = abortController(invocation, options.signalHandle).signal;
          }
          if (options.mode != null) {
            init.mode = options.mode;
          }
          if (options.credentials != null) {
            init.credentials = options.credentials;
          }
          if (options.referrer != null) {
            init.referrer = options.referrer;
          }
          if (options.referrerPolicy != null) {
            init.referrerPolicy = options.referrerPolicy;
          }
          if (options.redirect != null) {
            init.redirect = options.redirect;
          }
          if (options.cache != null) {
            init.cache = options.cache;
          }
          if (options.integrity != null) {
            init.integrity = options.integrity;
          }
          if (options.keepAlive != null) {
            init.keepalive = options.keepAlive;
          }
          if (options.cf != null) {
            init.cf = options.cf;
          }

          return Object.keys(init).length === 0 ? undefined : init;
        }

        function abortController(invocation, handle) {
          const controller = invocation.abortControllers.get(handle);
          if (controller == null) {
            throw new Error(`Abort controller handle '${handle}' is not defined.`);
          }

          return controller;
        }

        function delay(milliseconds) {
          return new Promise(resolve => setTimeout(resolve, milliseconds));
        }

        function toAnalyticsBlob(value) {
          if (value.bodyBase64 != null) {
            return fromBase64(value.bodyBase64);
          }

          return value.text;
        }

        function vectorizeVectors(vectors) {
          return (vectors ?? []).map(vector => {
            const value = {
              id: vector.id,
              values: vector.values ?? []
            };

            if (vector.namespace != null) {
              value.namespace = vector.namespace;
            }
            if (vector.metadata != null) {
              value.metadata = vector.metadata;
            }

            return value;
          });
        }

        function vectorizeQueryOptions(options) {
          if (options == null) {
            return undefined;
          }

          const value = {};
          if (options.topK != null) {
            value.topK = options.topK;
          }
          if (options.returnValues != null) {
            value.returnValues = options.returnValues === true;
          }
          if (options.returnMetadata != null) {
            value.returnMetadata = options.returnMetadata;
          }
          if (options.filter != null) {
            value.filter = options.filter;
          }
          if (options.namespace != null) {
            value.namespace = options.namespace;
          }

          return Object.keys(value).length === 0 ? undefined : value;
        }

        function r2ObjectEnvelope(object) {
          return {
            key: object.key,
            version: object.version,
            size: object.size,
            etag: object.etag,
            httpEtag: object.httpEtag,
            uploaded: object.uploaded == null ? null : new Date(object.uploaded).toISOString(),
            httpMetadata: r2HttpMetadataEnvelope(object.httpMetadata),
            customMetadata: object.customMetadata ?? {},
            checksums: r2ChecksumsEnvelope(object.checksums),
            range: r2RangeEnvelope(object.range)
          };
        }

        function r2ObjectsEnvelope(list) {
          return {
            objects: (list.objects ?? []).map(r2ObjectEnvelope),
            truncated: list.truncated === true,
            cursor: list.cursor ?? null,
            delimitedPrefixes: list.delimitedPrefixes ?? []
          };
        }

        function r2GetOptions(payload) {
          if (payload == null) {
            return undefined;
          }

          const options = {};
          const onlyIf = r2ConditionalOptions(payload.onlyIf);
          if (onlyIf != null) {
            options.onlyIf = onlyIf;
          }
          if (payload.range != null) {
            options.range = r2RangeOptions(payload.range);
          }

          return Object.keys(options).length === 0 ? undefined : options;
        }

        function r2ConditionalOptions(payload) {
          if (payload == null) {
            return null;
          }

          const options = {};
          if (payload.etagMatches != null) {
            options.etagMatches = payload.etagMatches;
          }
          if (payload.etagDoesNotMatch != null) {
            options.etagDoesNotMatch = payload.etagDoesNotMatch;
          }
          if (payload.uploadedBefore != null) {
            options.uploadedBefore = new Date(payload.uploadedBefore);
          }
          if (payload.uploadedAfter != null) {
            options.uploadedAfter = new Date(payload.uploadedAfter);
          }

          return Object.keys(options).length === 0 ? null : options;
        }

        function r2RangeOptions(payload) {
          const options = {};
          if (payload.offset != null) {
            options.offset = payload.offset;
          }
          if (payload.length != null) {
            options.length = payload.length;
          }
          if (payload.suffix != null) {
            options.suffix = payload.suffix;
          }

          return options;
        }

        function r2ListOptions(payload) {
          const options = {};
          if (payload.limit != null) {
            options.limit = payload.limit;
          }
          if (payload.prefix != null) {
            options.prefix = payload.prefix;
          }
          if (payload.startAfter != null) {
            options.startAfter = payload.startAfter;
          }
          if (payload.cursor != null) {
            options.cursor = payload.cursor;
          }
          if (payload.delimiter != null) {
            options.delimiter = payload.delimiter;
          }

          const include = [];
          if (payload.includeHttpMetadata === true) {
            include.push('httpMetadata');
          }
          if (payload.includeCustomMetadata === true) {
            include.push('customMetadata');
          }
          if (include.length > 0) {
            options.include = include;
          }

          return options;
        }

        function r2PutOptions(payload) {
          const options = {};
          const configured = payload.options ?? {};
          const httpMetadata = r2HttpMetadataOptions(configured.httpMetadata);
          if (payload.contentType != null && httpMetadata.contentType == null) {
            httpMetadata.contentType = payload.contentType;
          }
          if (Object.keys(httpMetadata).length > 0) {
            options.httpMetadata = httpMetadata;
          }
          if (configured.customMetadata != null) {
            options.customMetadata = configured.customMetadata;
          }
          const onlyIf = r2ConditionalOptions(configured.onlyIf);
          if (onlyIf != null) {
            options.onlyIf = onlyIf;
          }

          addR2Checksum(options, 'md5', configured.checksums?.md5);
          addR2Checksum(options, 'sha1', configured.checksums?.sha1);
          addR2Checksum(options, 'sha256', configured.checksums?.sha256);
          addR2Checksum(options, 'sha384', configured.checksums?.sha384);
          addR2Checksum(options, 'sha512', configured.checksums?.sha512);

          return Object.keys(options).length === 0 ? undefined : options;
        }

        function r2MultipartUploadOptions(payload) {
          if (payload == null) {
            return undefined;
          }

          const options = {};
          const httpMetadata = r2HttpMetadataOptions(payload.httpMetadata);
          if (Object.keys(httpMetadata).length > 0) {
            options.httpMetadata = httpMetadata;
          }
          if (payload.customMetadata != null) {
            options.customMetadata = payload.customMetadata;
          }

          return Object.keys(options).length === 0 ? undefined : options;
        }

        function r2MultipartUpload(binding, payload) {
          return binding.resumeMultipartUpload(payload.key, payload.uploadId);
        }

        function r2UploadedPart(part) {
          return {
            partNumber: part.partNumber,
            etag: part.etag
          };
        }

        function r2HttpMetadataOptions(metadata) {
          const options = {};
          if (metadata == null) {
            return options;
          }

          if (metadata.contentType != null) {
            options.contentType = metadata.contentType;
          }
          if (metadata.contentLanguage != null) {
            options.contentLanguage = metadata.contentLanguage;
          }
          if (metadata.contentDisposition != null) {
            options.contentDisposition = metadata.contentDisposition;
          }
          if (metadata.contentEncoding != null) {
            options.contentEncoding = metadata.contentEncoding;
          }
          if (metadata.cacheControl != null) {
            options.cacheControl = metadata.cacheControl;
          }
          if (metadata.cacheExpiry != null) {
            options.cacheExpiry = new Date(metadata.cacheExpiry);
          }

          return options;
        }

        function addR2Checksum(options, name, value) {
          if (value != null) {
            options[name] = fromBase64(value).buffer;
          }
        }

        function r2HttpMetadataEnvelope(metadata) {
          if (metadata == null) {
            return null;
          }

          return {
            contentType: metadata.contentType ?? null,
            contentLanguage: metadata.contentLanguage ?? null,
            contentDisposition: metadata.contentDisposition ?? null,
            contentEncoding: metadata.contentEncoding ?? null,
            cacheControl: metadata.cacheControl ?? null,
            cacheExpiry: metadata.cacheExpiry == null ? null : new Date(metadata.cacheExpiry).toISOString()
          };
        }

        function r2ChecksumsEnvelope(checksums) {
          if (checksums == null) {
            return null;
          }

          return {
            md5: checksumToBase64(checksums.md5),
            sha1: checksumToBase64(checksums.sha1),
            sha256: checksumToBase64(checksums.sha256),
            sha384: checksumToBase64(checksums.sha384),
            sha512: checksumToBase64(checksums.sha512)
          };
        }

        function checksumToBase64(value) {
          return value == null ? null : toBase64(new Uint8Array(value));
        }

        function r2RangeEnvelope(range) {
          if (range == null) {
            return null;
          }

          return {
            offset: range.offset ?? null,
            length: range.length ?? null,
            suffix: range.suffix ?? null
          };
        }

        function toSendEmailMessage(message) {
          const value = {
            from: toEmailAddressOrString(message.from),
            to: message.to.length === 1 ? message.to[0] : message.to,
            subject: message.subject
          };

          if (message.replyTo != null) {
            value.replyTo = toEmailAddressOrString(message.replyTo);
          }
          if ((message.cc ?? []).length > 0) {
            value.cc = message.cc.length === 1 ? message.cc[0] : message.cc;
          }
          if ((message.bcc ?? []).length > 0) {
            value.bcc = message.bcc.length === 1 ? message.bcc[0] : message.bcc;
          }
          if (message.headers != null && Object.keys(message.headers).length > 0) {
            value.headers = message.headers;
          }
          if (message.text != null) {
            value.text = message.text;
          }
          if (message.html != null) {
            value.html = message.html;
          }
          if ((message.attachments ?? []).length > 0) {
            value.attachments = message.attachments.map(toEmailAttachment);
          }

          return value;
        }

        function toEmailMessageEnvelope(invocationId, message) {
          return {
            invocationId,
            handle: retainEmailMessage(invocationId, message),
            from: message.from,
            to: message.to,
            headers: Array.from(message.headers ?? [], ([name, value]) => ({ name, value })),
            rawSize: message.rawSize ?? 0
          };
        }

        function retainEmailMessage(invocationId, message) {
          const invocation = invocations.get(invocationId);
          if (invocation == null) {
            throw new Error(`Worker invocation '${invocationId}' is no longer active.`);
          }

          const handle = `email:${++nextEmailMessageId}`;
          invocation.emailMessages.set(handle, message);
          return handle;
        }

        function emailMessage(invocation, handle) {
          const message = invocation.emailMessages.get(handle);
          if (message == null) {
            throw new Error(`Email message handle '${handle}' is not defined.`);
          }

          return message;
        }

        function fromHeadersEnvelope(headers) {
          if ((headers ?? []).length === 0) {
            return undefined;
          }

          const result = new Headers();
          for (const header of headers) {
            result.append(header.name, header.value);
        """;
}
