using System.Reflection;
using System.Reflection.Emit;
using Workers.Build;
using Xunit;

namespace Workers.Tests;

public sealed class EntrypointScannerTests
{
    [Fact]
    public void DiscoversFetchEntrypoint()
    {
        var manifest = EntrypointScanner.Scan(typeof(ValidWorker).Assembly);

        var entrypoint = Assert.Single(
            manifest.Entrypoints,
            static entrypoint => entrypoint.ContainingType == typeof(ValidWorker).FullName);
        Assert.Equal(EntrypointKind.Fetch, entrypoint.Kind);
        Assert.Equal(typeof(ValidWorker).FullName, entrypoint.ContainingType);
        Assert.Equal(nameof(ValidWorker.FetchAsync), entrypoint.MethodName);
    }

    [Fact]
    public void DiscoversEmailEntrypoint()
    {
        var manifest = EntrypointScanner.Scan(typeof(ValidWorker).Assembly);

        var entrypoint = Assert.Single(
            manifest.Entrypoints,
            static entrypoint => entrypoint.ContainingType == typeof(ValidEmailWorker).FullName);
        Assert.Equal(EntrypointKind.Email, entrypoint.Kind);
        Assert.Equal(typeof(ValidEmailWorker).FullName, entrypoint.ContainingType);
        Assert.Equal(nameof(ValidEmailWorker.EmailAsync), entrypoint.MethodName);
    }

    [Fact]
    public void DiscoversTailEntrypoint()
    {
        var manifest = EntrypointScanner.Scan(typeof(ValidWorker).Assembly);

        var entrypoint = Assert.Single(
            manifest.Entrypoints,
            static entrypoint => entrypoint.ContainingType == typeof(ValidTailWorker).FullName);
        Assert.Equal(EntrypointKind.Tail, entrypoint.Kind);
        Assert.Equal(typeof(ValidTailWorker).FullName, entrypoint.ContainingType);
        Assert.Equal(nameof(ValidTailWorker.TailAsync), entrypoint.MethodName);
    }

    [Fact]
    public void DiscoversDurableObjectEntrypoint()
    {
        var manifest = EntrypointScanner.Scan(typeof(ValidWorker).Assembly);

        var durableObject = Assert.Single(
            manifest.DurableObjects,
            static durableObject => durableObject.ContainingType == typeof(ValidDurableObject).FullName);
        Assert.Equal("CounterObject", durableObject.ExportName);
        Assert.Equal(typeof(ValidDurableObject).FullName, durableObject.ContainingType);
        Assert.Equal(nameof(ValidDurableObject.FetchAsync), durableObject.FetchMethodName);
        Assert.Equal(nameof(ValidDurableObject.AlarmAsync), durableObject.AlarmMethodName);
        Assert.Equal(nameof(ValidDurableObject.WebSocketMessageAsync), durableObject.WebSocketMessageMethodName);
        Assert.Equal(nameof(ValidDurableObject.WebSocketCloseAsync), durableObject.WebSocketCloseMethodName);
        Assert.Equal(nameof(ValidDurableObject.WebSocketErrorAsync), durableObject.WebSocketErrorMethodName);
        var rpcMethod = Assert.Single(durableObject.RpcMethods);
        Assert.Equal(nameof(ValidDurableObject.AddAsync), rpcMethod.Name);
        Assert.Equal(nameof(ValidDurableObject.AddAsync), rpcMethod.MethodName);
    }

    [Fact]
    public void RejectsInvalidFetchSignature()
    {
        var ex = Assert.Throws<EntrypointException>(() =>
            InvokeValidateFetch(typeof(InvalidFetchWorker).GetMethod(nameof(InvalidFetchWorker.FetchAsync))!));

        Assert.Contains("must accept", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsInvalidEmailSignature()
    {
        var ex = Assert.Throws<EntrypointException>(() =>
            InvokeValidateEmail(typeof(InvalidEmailWorker).GetMethod(nameof(InvalidEmailWorker.EmailAsync))!));

        Assert.Contains("must accept", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsInvalidTailSignature()
    {
        var ex = Assert.Throws<EntrypointException>(() =>
            InvokeValidateTail(typeof(InvalidTailWorker).GetMethod(nameof(InvalidTailWorker.TailAsync))!));

        Assert.Contains("must accept", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnnotatedInstanceEntrypoint()
    {
        var assembly = CreateDynamicEntrypointAssembly(
            "Workers.Tests.DynamicInstanceEntrypoint",
            MethodAttributes.Public,
            typeof(FetchEventAttribute));

        var ex = Assert.Throws<EntrypointException>(() => EntrypointScanner.Scan(assembly));

        Assert.Contains("must be static", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsMultipleEventAttributesOnSameMethod()
    {
        var assembly = CreateDynamicEntrypointAssembly(
            "Workers.Tests.DynamicMultipleEventEntrypoint",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(FetchEventAttribute),
            typeof(ScheduledEventAttribute));

        var ex = Assert.Throws<EntrypointException>(() => EntrypointScanner.Scan(assembly));

        Assert.Contains("cannot have more than one Worker event attribute", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsReservedDurableObjectExportName()
    {
        var assembly = CreateDynamicDurableObjectAssembly(
            "Workers.Tests.DynamicReservedDurableObject",
            exportName: "class");

        var ex = Assert.Throws<EntrypointException>(() => EntrypointScanner.Scan(assembly));

        Assert.Contains("not a valid JavaScript class name", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsDurableObjectRpcMethodNamedConstructor()
    {
        var assembly = CreateDynamicDurableObjectAssembly(
            "Workers.Tests.DynamicConstructorRpcDurableObject",
            exportName: "ValidObject",
            rpcMethodName: "constructor");

        var ex = Assert.Throws<EntrypointException>(() => EntrypointScanner.Scan(assembly));

        Assert.Contains("cannot be named 'constructor'", ex.Message, StringComparison.Ordinal);
    }

    private static void InvokeValidateFetch(MethodInfo method)
    {
        var scanner = typeof(EntrypointScanner).GetMethod(
            "ValidateFetch",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        try
        {
            scanner.Invoke(null, [method]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static void InvokeValidateEmail(MethodInfo method)
    {
        var scanner = typeof(EntrypointScanner).GetMethod(
            "ValidateEmail",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        try
        {
            scanner.Invoke(null, [method]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static void InvokeValidateTail(MethodInfo method)
    {
        var scanner = typeof(EntrypointScanner).GetMethod(
            "ValidateTail",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        try
        {
            scanner.Invoke(null, [method]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static Assembly CreateDynamicEntrypointAssembly(
        string assemblyName,
        MethodAttributes methodAttributes,
        params Type[] eventAttributeTypes)
    {
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(assemblyName),
            AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName);
        var typeBuilder = moduleBuilder.DefineType(
            assemblyName + ".Worker",
            TypeAttributes.Public | TypeAttributes.Class);
        var methodBuilder = typeBuilder.DefineMethod(
            "FetchAsync",
            methodAttributes,
            typeof(Task<Response>),
            [typeof(Request), typeof(Env), typeof(Context)]);

        foreach (var attributeType in eventAttributeTypes)
        {
            var constructor = attributeType.GetConstructor(Type.EmptyTypes)
                ?? throw new InvalidOperationException($"Attribute '{attributeType.FullName}' does not have a parameterless constructor.");
            methodBuilder.SetCustomAttribute(new CustomAttributeBuilder(constructor, []));
        }

        var il = methodBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ret);

        typeBuilder.CreateType();
        return assemblyBuilder;
    }

    private static Assembly CreateDynamicDurableObjectAssembly(
        string assemblyName,
        string exportName,
        string? rpcMethodName = null)
    {
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(assemblyName),
            AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName);
        var typeBuilder = moduleBuilder.DefineType(
            assemblyName + ".Counter",
            TypeAttributes.Public | TypeAttributes.Class);

        var durableObjectAttributeConstructor = typeof(DurableObjectAttribute).GetConstructor([typeof(string)])
            ?? throw new InvalidOperationException("DurableObjectAttribute string constructor was not found.");
        typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(durableObjectAttributeConstructor, [exportName]));

        var constructor = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            [typeof(DurableObjectState), typeof(Env)]);
        var constructorIl = constructor.GetILGenerator();
        constructorIl.Emit(OpCodes.Ldarg_0);
        constructorIl.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
        constructorIl.Emit(OpCodes.Ret);

        if (rpcMethodName is not null)
        {
            var methodBuilder = typeBuilder.DefineMethod(
                rpcMethodName,
                MethodAttributes.Public,
                typeof(Task<int>),
                Type.EmptyTypes);
            var methodIl = methodBuilder.GetILGenerator();
            methodIl.Emit(OpCodes.Ldnull);
            methodIl.Emit(OpCodes.Ret);
        }

        typeBuilder.CreateType();
        return assemblyBuilder;
    }

    private sealed class ValidWorker
    {
        [FetchEvent]
        public static Task<Response> FetchAsync(
            Request request,
            Env environment,
            Context context)
        {
            _ = request;
            _ = environment;
            _ = context;
            return Task.FromResult(Response.Text("ok"));
        }
    }

    private sealed class InvalidFetchWorker
    {
        public static Task<Response> FetchAsync(Request request)
        {
            _ = request;
            return Task.FromResult(Response.Text("ok"));
        }
    }

    private sealed class ValidEmailWorker
    {
        [EmailEvent]
        public static Task EmailAsync(
            ForwardableEmailMessage message,
            Env environment,
            Context context)
        {
            _ = message;
            _ = environment;
            _ = context;
            return Task.CompletedTask;
        }
    }

    private sealed class InvalidEmailWorker
    {
        public static Task EmailAsync(ForwardableEmailMessage message)
        {
            _ = message;
            return Task.CompletedTask;
        }
    }

    private sealed class ValidTailWorker
    {
        [TailEvent]
        public static Task TailAsync(
            TailEvent tailEvent,
            Env environment,
            Context context)
        {
            _ = tailEvent;
            _ = environment;
            _ = context;
            return Task.CompletedTask;
        }
    }

    private sealed class InvalidTailWorker
    {
        public static Task TailAsync(TailEvent tailEvent)
        {
            _ = tailEvent;
            return Task.CompletedTask;
        }
    }

    [DurableObject("CounterObject")]
    private sealed class ValidDurableObject
    {
        public ValidDurableObject(DurableObjectState state, Env environment)
        {
            _ = state;
            _ = environment;
        }

        public Task<Response> FetchAsync(Request request)
        {
            _ = request;
            return Task.FromResult(Response.Text("ok"));
        }

        public Task AlarmAsync(AlarmInfo alarmInfo)
        {
            _ = alarmInfo;
            return Task.CompletedTask;
        }

        public Task WebSocketMessageAsync(WebSocket socket, WebSocketMessage message)
        {
            _ = socket;
            _ = message;
            return Task.CompletedTask;
        }

        public Task WebSocketCloseAsync(WebSocket socket, ushort code, string reason, bool wasClean)
        {
            _ = socket;
            _ = code;
            _ = reason;
            _ = wasClean;
            return Task.CompletedTask;
        }

        public Task WebSocketErrorAsync(WebSocket socket, WebSocketError error)
        {
            _ = socket;
            _ = error;
            return Task.CompletedTask;
        }

        public ValueTask<int> AddAsync(int left, int right) =>
            ValueTask.FromResult(left + right);
    }
}
