namespace Workers.Build;

/// <summary>Generates the JavaScript adapter that hosts the .NET WebAssembly runtime.</summary>
internal static partial class RuntimeAdapterWriter
{
    /// <summary>Creates the runtime adapter module.</summary>
    public static string WriteAdapter() => WriteAdapter(RuntimeAdapterOptions.All);

    /// <summary>Creates the runtime adapter module.</summary>
    public static string WriteAdapter(RuntimeAdapterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return string.Join("\n",
        [
            AdapterPart01,
            AdapterPart02,
            AdapterPart03,
            AdapterPart04,
            AdapterPart05,
            AdapterPart06,
            AdapterPart07,
        ])
            .Replace("{{WORKER_BOOT_RESOURCE_IMPORTS}}", "", StringComparison.Ordinal)
            .Replace("{{WORKER_BOOT_RESOURCE_MAP}}", "const bootResourceModules = null;", StringComparison.Ordinal)
            .Replace("{{WORKER_BOOT_RESOURCE_FALLBACK}}", DynamicResourceLoader, StringComparison.Ordinal)
            .ApplyBlock("WORKER_FETCH_HANDLER", options.IncludeFetch)
            .ApplyBlock("WORKER_SCHEDULED_HANDLER", options.IncludeScheduled)
            .ApplyBlock("WORKER_QUEUE_HANDLER", options.IncludeQueue)
            .ApplyBlock("WORKER_EMAIL_HANDLER", options.IncludeEmail)
            .ApplyBlock("WORKER_TAIL_HANDLER", options.IncludeTail)
            .ApplyBlock("WORKER_DURABLE_OBJECT_HANDLERS", options.IncludeDurableObjects)
            .ApplyBlock("WORKER_QUEUE_DISPOSITIONS", options.IncludeQueue)
            .ApplyBlock("WORKER_TAIL_HELPERS", options.IncludeTail)
            .ApplyBlock("WORKER_PLATFORM_STATE", options.IncludePlatformApis)
            .ApplyBlock("WORKER_PLATFORM_STATE_ASSIGNMENT", options.IncludePlatformApis)
            .ApplyBlock("WORKER_PLATFORM_RESPONSE_WEBSOCKET", options.IncludePlatformApis)
            .ApplyBlock("WORKER_PLATFORM_IMPORTS", options.IncludePlatformApis)
            .ApplyBlock("WORKER_PLATFORM_HELPERS", options.IncludePlatformApis)
            .ApplyBlock("WORKER_PLATFORM_CONVERSION_HELPERS", options.IncludePlatformApis)
            .ApplyReplacementBlock(
                "WORKER_BINDING_DISPATCH_SWITCH",
                options.IncludePlatformApis ? null : CoreBindingDispatchSwitch);
    }

    private const string DynamicResourceLoader =
        """
        if (type === 'dotnetwasm') {
          return import(`./_framework/${name}`).then(module => ({
            ok: true,
            compiledModule: module.default ?? module
          }));
        }

        return import(`./_framework/${name}`).then(module => new Response(module.default ?? module));
        """;

    private const string CoreBindingDispatchSwitch =
        """
          switch (operation) {
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
            default:
              throw new Error(`Workers platform APIs are not enabled for binding operation '${operation}'.`);
          }
        """;

    private static string ApplyBlock(this string source, string name, bool include)
    {
        var startMarker = $"// {{{{{name}_START}}}}";
        var endMarker = $"// {{{{{name}_END}}}}";
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, StringComparison.Ordinal);

        if (start < 0 || end < 0 || end < start)
            throw new InvalidOperationException($"Runtime adapter block '{name}' was not found.");

        end += endMarker.Length;
        return include
            ? source.Remove(end - endMarker.Length, endMarker.Length).Remove(start, startMarker.Length)
            : source.Remove(start, end - start);
    }

    private static string ApplyReplacementBlock(this string source, string name, string? replacement)
    {
        var startMarker = $"// {{{{{name}_START}}}}";
        var endMarker = $"// {{{{{name}_END}}}}";
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, StringComparison.Ordinal);

        if (start < 0 || end < 0 || end < start)
            throw new InvalidOperationException($"Runtime adapter block '{name}' was not found.");

        end += endMarker.Length;
        return replacement is null
            ? source.Remove(end - endMarker.Length, endMarker.Length).Remove(start, startMarker.Length)
            : source.Remove(start, end - start).Insert(start, replacement);
    }
}
