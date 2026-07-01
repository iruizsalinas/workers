using System.Reflection;

namespace Workers.Build;

/// <summary>Discovers Worker event handlers from an assembly.</summary>
internal static class EntrypointScanner
{
    private static readonly HashSet<string> JavaScriptReservedClassNames = new(StringComparer.Ordinal)
    {
        "await",
        "break",
        "case",
        "catch",
        "class",
        "const",
        "continue",
        "debugger",
        "default",
        "delete",
        "do",
        "else",
        "enum",
        "export",
        "extends",
        "false",
        "finally",
        "for",
        "function",
        "if",
        "import",
        "in",
        "instanceof",
        "let",
        "new",
        "null",
        "return",
        "super",
        "switch",
        "this",
        "throw",
        "true",
        "try",
        "typeof",
        "var",
        "void",
        "while",
        "with",
        "yield",
        "implements",
        "interface",
        "package",
        "private",
        "protected",
        "public",
        "static"
    };

    /// <summary>Scans an assembly and returns a build manifest.</summary>
    public static BuildManifest Scan(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var types = assembly.GetTypes();
        var eventMethods = types
            .SelectMany(static type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            .Where(HasWorkerEventAttribute)
            .ToArray();

        foreach (var method in eventMethods)
            ValidateEventMethodShape(method);

        var entrypoints = eventMethods
            .Where(static method => method.IsStatic)
            .SelectMany(CreateEntrypoints)
            .ToArray();
        var durableObjects = types
            .Select(CreateDurableObject)
            .OfType<DurableObjectEntrypoint>()
            .ToArray();

        EnsureNoDuplicates(entrypoints);
        EnsureNoDuplicateDurableObjects(durableObjects);

        var assemblyName = assembly.GetName().Name ?? "worker";
        return new BuildManifest(
            EntryAssembly: assemblyName + ".dll",
            JavaScriptModule: "worker.js",
            WasmModule: assemblyName + ".wasm",
            Entrypoints: entrypoints)
        {
            DurableObjects = durableObjects
        };
    }

    private static bool HasWorkerEventAttribute(MethodInfo method) =>
        EventAttributeCount(method) > 0;

    private static void ValidateEventMethodShape(MethodInfo method)
    {
        var eventAttributeCount = EventAttributeCount(method);
        if (eventAttributeCount > 1)
            throw new EntrypointException($"Entrypoint '{Format(method)}' cannot have more than one Worker event attribute.");

        if (!method.IsStatic)
            throw new EntrypointException($"Entrypoint '{Format(method)}' must be static.");
    }

    private static int EventAttributeCount(MethodInfo method)
    {
        var count = 0;
        if (method.GetCustomAttribute<FetchEventAttribute>() is not null)
            count++;
        if (method.GetCustomAttribute<ScheduledEventAttribute>() is not null)
            count++;
        if (method.GetCustomAttribute<QueueEventAttribute>() is not null)
            count++;
        if (method.GetCustomAttribute<EmailEventAttribute>() is not null)
            count++;
        if (method.GetCustomAttribute<TailEventAttribute>() is not null)
            count++;

        return count;
    }

    private static IEnumerable<Entrypoint> CreateEntrypoints(MethodInfo method)
    {
        if (method.GetCustomAttribute<FetchEventAttribute>() is not null)
        {
            ValidateFetch(method);
            yield return Create(method, EntrypointKind.Fetch);
        }

        if (method.GetCustomAttribute<ScheduledEventAttribute>() is not null)
        {
            ValidateScheduled(method);
            yield return Create(method, EntrypointKind.Scheduled);
        }

        if (method.GetCustomAttribute<QueueEventAttribute>() is not null)
        {
            ValidateQueue(method);
            yield return Create(method, EntrypointKind.Queue);
        }

        if (method.GetCustomAttribute<EmailEventAttribute>() is not null)
        {
            ValidateEmail(method);
            yield return Create(method, EntrypointKind.Email);
        }

        if (method.GetCustomAttribute<TailEventAttribute>() is not null)
        {
            ValidateTail(method);
            yield return Create(method, EntrypointKind.Tail);
        }
    }

    private static Entrypoint Create(MethodInfo method, EntrypointKind kind)
    {
        var containingType = method.DeclaringType?.FullName
            ?? throw new EntrypointException($"Entrypoint '{method.Name}' is missing a declaring type.");

        return new Entrypoint(kind, containingType, method.Name);
    }

    private static DurableObjectEntrypoint? CreateDurableObject(Type type)
    {
        var attribute = type.GetCustomAttribute<DurableObjectAttribute>();
        if (attribute is null)
            return null;

        ValidateDurableObjectType(type, attribute);
        var fetch = DurableObjectMethod(type, "FetchAsync");
        var alarm = DurableObjectMethod(type, "AlarmAsync");
        var webSocketMessage = DurableObjectMethod(type, "WebSocketMessageAsync");
        var webSocketClose = DurableObjectMethod(type, "WebSocketCloseAsync");
        var webSocketError = DurableObjectMethod(type, "WebSocketErrorAsync");
        var rpcMethods = DurableObjectRpcMethods(type);

        if (fetch is null
            && alarm is null
            && webSocketMessage is null
            && webSocketClose is null
            && webSocketError is null
            && rpcMethods.Count == 0)
        {
            throw new EntrypointException(
                $"Durable Object '{type.FullName}' must define FetchAsync(Request), AlarmAsync(), AlarmAsync(AlarmInfo), a WebSocket hibernation handler, or a public RPC method.");
        }

        if (fetch is not null)
            ValidateDurableObjectFetch(fetch);

        if (alarm is not null)
            ValidateDurableObjectAlarm(alarm);

        if (webSocketMessage is not null)
            ValidateDurableObjectWebSocketMessage(webSocketMessage);

        if (webSocketClose is not null)
            ValidateDurableObjectWebSocketClose(webSocketClose);

        if (webSocketError is not null)
            ValidateDurableObjectWebSocketError(webSocketError);

        return new DurableObjectEntrypoint(
            attribute.ExportName ?? type.Name,
            type.FullName ?? throw new EntrypointException($"Durable Object '{type.Name}' is missing a full type name."),
            fetch?.Name,
            alarm?.Name,
            webSocketMessage?.Name,
            webSocketClose?.Name,
            webSocketError?.Name)
        {
            RpcMethods = rpcMethods
        };
    }

    private static void ValidateFetch(MethodInfo method)
    {
        EnsureNotGeneric(method);
        EnsureReturn(method, typeof(Response));
        EnsureParameters(method, typeof(Request), typeof(Env), typeof(Context));
    }

    private static void ValidateScheduled(MethodInfo method)
    {
        EnsureNotGeneric(method);
        EnsureVoidLikeReturn(method);
        EnsureParameters(method, typeof(ScheduledEvent), typeof(Env), typeof(Context));
    }

    private static void ValidateQueue(MethodInfo method)
    {
        EnsureNotGeneric(method);
        EnsureVoidLikeReturn(method);

        var parameters = method.GetParameters();
        if (parameters.Length != 3
            || !parameters[0].ParameterType.IsGenericType
            || parameters[0].ParameterType.GetGenericTypeDefinition() != typeof(QueueMessageBatch<>)
            || parameters[1].ParameterType != typeof(Env)
            || parameters[2].ParameterType != typeof(Context))
        {
            throw new EntrypointException(
                $"Queue entrypoint '{Format(method)}' must accept (QueueMessageBatch<T>, Env, Context).");
        }
    }

    private static void ValidateEmail(MethodInfo method)
    {
        EnsureNotGeneric(method);
        EnsureVoidLikeReturn(method);
        EnsureParameters(method, typeof(ForwardableEmailMessage), typeof(Env), typeof(Context));
    }

    private static void ValidateTail(MethodInfo method)
    {
        EnsureNotGeneric(method);
        EnsureVoidLikeReturn(method);
        EnsureParameters(method, typeof(TailEvent), typeof(Env), typeof(Context));
    }

    private static void ValidateDurableObjectType(Type type, DurableObjectAttribute attribute)
    {
        if (type.ContainsGenericParameters)
            throw new EntrypointException($"Durable Object '{type.FullName}' cannot be generic.");

        var exportName = attribute.ExportName ?? type.Name;
        if (!IsJavaScriptClassName(exportName))
            throw new EntrypointException($"Durable Object export name '{exportName}' is not a valid JavaScript class name.");

        var constructor = type.GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: [typeof(DurableObjectState), typeof(Env)],
            modifiers: null);

        if (constructor is null)
        {
            throw new EntrypointException(
                $"Durable Object '{type.FullName}' must define a constructor accepting (DurableObjectState, Env).");
        }
    }

    private static void ValidateDurableObjectFetch(MethodInfo method)
    {
        EnsureNotGeneric(method);
        EnsureReturn(method, typeof(Response));
        EnsureParameters(method, typeof(Request));
    }

    private static void ValidateDurableObjectAlarm(MethodInfo method)
    {
        EnsureNotGeneric(method);
        EnsureVoidLikeReturn(method);

        var parameters = method.GetParameters();
        if (parameters.Length == 0)
            return;

        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(AlarmInfo))
            return;

        throw new EntrypointException(
            $"Durable Object alarm handler '{Format(method)}' must accept no parameters or (AlarmInfo).");
    }

    private static void ValidateDurableObjectWebSocketMessage(MethodInfo method)
    {
        EnsureNotGeneric(method);
        EnsureVoidLikeReturn(method);
        EnsureParameters(method, typeof(WebSocket), typeof(WebSocketMessage));
    }

    private static void ValidateDurableObjectWebSocketClose(MethodInfo method)
    {
        EnsureNotGeneric(method);
        EnsureVoidLikeReturn(method);
        EnsureParameters(method, typeof(WebSocket), typeof(ushort), typeof(string), typeof(bool));
    }

    private static void ValidateDurableObjectWebSocketError(MethodInfo method)
    {
        EnsureNotGeneric(method);
        EnsureVoidLikeReturn(method);
        EnsureParameters(method, typeof(WebSocket), typeof(WebSocketError));
    }

    private static MethodInfo? DurableObjectMethod(Type type, string name)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(method => method.Name == name)
            .ToArray();

        return methods.Length switch
        {
            0 => null,
            1 => methods[0],
            _ => throw new EntrypointException($"Durable Object '{type.FullName}' has more than one '{name}' method.")
        };
    }

    private static IReadOnlyList<DurableObjectRpcMethod> DurableObjectRpcMethods(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static method => method.Name is not (
                "FetchAsync"
                or "AlarmAsync"
                or "WebSocketMessageAsync"
                or "WebSocketCloseAsync"
                or "WebSocketErrorAsync"))
            .Where(static method => !method.IsSpecialName)
            .ToArray();

        foreach (var method in methods)
            ValidateDurableObjectRpc(method);

        var duplicate = methods
            .GroupBy(static method => method.Name, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new EntrypointException(
                $"Durable Object '{type.FullName}' has more than one RPC method named '{duplicate.Key}'.");
        }

        return methods
            .OrderBy(static method => method.Name, StringComparer.Ordinal)
            .Select(static method => new DurableObjectRpcMethod(method.Name, method.Name))
            .ToArray();
    }

    private static void ValidateDurableObjectRpc(MethodInfo method)
    {
        EnsureNotGeneric(method);
        if (!IsJavaScriptIdentifier(method.Name))
            throw new EntrypointException($"Durable Object RPC method '{Format(method)}' is not a valid JavaScript method name.");

        if (string.Equals(method.Name, "constructor", StringComparison.Ordinal))
            throw new EntrypointException($"Durable Object RPC method '{Format(method)}' cannot be named 'constructor'.");
    }

    private static void EnsureNotGeneric(MethodInfo method)
    {
        if (method.IsGenericMethodDefinition || method.ContainsGenericParameters)
            throw new EntrypointException($"Entrypoint '{Format(method)}' cannot be generic.");
    }

    private static void EnsureReturn(MethodInfo method, Type resultType)
    {
        if (method.ReturnType == resultType
            || method.ReturnType == typeof(Task<>).MakeGenericType(resultType)
            || method.ReturnType == typeof(ValueTask<>).MakeGenericType(resultType))
        {
            return;
        }

        throw new EntrypointException(
            $"Entrypoint '{Format(method)}' must return {resultType.Name}, Task<{resultType.Name}>, or ValueTask<{resultType.Name}>.");
    }

    private static void EnsureVoidLikeReturn(MethodInfo method)
    {
        if (method.ReturnType == typeof(void)
            || method.ReturnType == typeof(Task)
            || method.ReturnType == typeof(ValueTask))
        {
            return;
        }

        throw new EntrypointException($"Entrypoint '{Format(method)}' must return void, Task, or ValueTask.");
    }

    private static void EnsureParameters(MethodInfo method, params Type[] expectedTypes)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == expectedTypes.Length
            && parameters.Select(static parameter => parameter.ParameterType).SequenceEqual(expectedTypes))
        {
            return;
        }

        var expected = string.Join(", ", expectedTypes.Select(static type => type.Name));
        throw new EntrypointException($"Entrypoint '{Format(method)}' must accept ({expected}).");
    }

    private static void EnsureNoDuplicates(IReadOnlyList<Entrypoint> entrypoints)
    {
        var duplicate = entrypoints
            .GroupBy(static entrypoint => entrypoint.Kind)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicate is not null)
            throw new EntrypointException($"Only one {duplicate.Key} entrypoint is supported per assembly.");
    }

    private static void EnsureNoDuplicateDurableObjects(IReadOnlyList<DurableObjectEntrypoint> durableObjects)
    {
        var duplicate = durableObjects
            .GroupBy(static durableObject => durableObject.ExportName, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicate is not null)
            throw new EntrypointException($"Only one Durable Object export named '{duplicate.Key}' is supported per assembly.");
    }

    private static bool IsJavaScriptIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!IsIdentifierStart(value[0]))
            return false;

        return value.Skip(1).All(IsIdentifierPart);
    }

    private static bool IsJavaScriptClassName(string value) =>
        IsJavaScriptIdentifier(value) && !JavaScriptReservedClassNames.Contains(value);

    private static bool IsIdentifierStart(char value) =>
        value is '_' or '$' || char.IsLetter(value);

    private static bool IsIdentifierPart(char value) =>
        IsIdentifierStart(value) || char.IsDigit(value);

    private static string Format(MethodInfo method) =>
        $"{method.DeclaringType?.FullName ?? "<unknown>"}.{method.Name}";
}
