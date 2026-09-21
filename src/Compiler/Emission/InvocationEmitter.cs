using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string Invocation(InvocationExpressionSyntax invocation)
    {
        var method = _model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (method?.Name == "TryParse" && method.ContainingType.SpecialType is
            SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64
            or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double
            or SpecialType.System_Boolean or SpecialType.System_Decimal)
            throw UnsupportedSymbol(method, invocation);
        if (method?.Name is "TryParse" or "TryParseExact"
            && method.ContainingType.ToDisplayString() == "System.Guid")
            throw UnsupportedSymbol(method, invocation);
        if (method?.Name == "TryGetProperty"
            && method.ContainingType.ToDisplayString() == "System.Text.Json.JsonElement")
            throw UnsupportedSymbol(method, invocation);
        if (method?.Name == "Parse" && method.ContainingType.SpecialType is
            SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Decimal)
            throw UnsupportedSymbol(method, invocation);
        var arguments = invocation.ArgumentList.Arguments.Select(argument => Expression(argument.Expression)).ToArray();
        var member = invocation.Expression as MemberAccessExpressionSyntax;
        var isBindingIntrinsic = method is not null && BindingIntrinsicRegistry.TryGet(method, out _);
        if (method is not null
            && (invocation.ArgumentList.Arguments.Any(argument => argument.NameColon is not null)
                || method.Parameters.Any(IsCancellationToken))
            && !isBindingIntrinsic)
        {
            var receiver = member is null || method.IsStatic ? null : Expression(member.Expression);
            return EmitNormalizedInvocation(invocation, method, receiver,
                (normalizedReceiver, normalizedArguments) =>
                    InvocationCore(invocation, method, normalizedArguments, member, normalizedReceiver));
        }
        return InvocationCore(invocation, method, arguments, member);
    }

    private string InvocationCore(
        InvocationExpressionSyntax invocation,
        IMethodSymbol? method,
        string[] arguments,
        MemberAccessExpressionSyntax? member,
        string? receiverOverride = null)
    {
        var containingType = method?.ContainingType.ToDisplayString();
        var methodName = method?.Name;
        if (IsEnumerableMethod(method))
            return LinqInvocation(invocation, method!, arguments, member, receiverOverride);
        if (methodName == "Contains"
            && method?.ContainingType.OriginalDefinition.ToDisplayString() == "System.Linq.ILookup<TKey, TElement>"
            && member is not null && arguments.Length == 1)
            return $"{receiverOverride ?? Expression(member.Expression)}.contains({arguments[0]})";
        if (containingType is "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask" && methodName == "FromResult")
            return $"Promise.resolve({arguments[0]})";
        if (containingType == "System.Threading.Tasks.Task" && methodName == "WhenAll")
            return TaskWhenAll(invocation, method!, arguments);
        if (containingType == "System.Threading.Tasks.Task" && methodName == "Delay")
        {
            return arguments.Length switch
            {
                1 => HelperInvocation(JavaScriptHelper.Delay, arguments),
                2 when IsCancellationToken(method!.Parameters[1]) =>
                    HelperInvocation(JavaScriptHelper.CancellationDelay, arguments),
                _ => throw UnsupportedSymbol(method, invocation)
            };
        }
        if (TryEmitStaticInvocation(invocation, method, containingType, methodName, arguments, receiverOverride, out var result)) return result;
        if (member is not null)
            return MemberInvocation(invocation, member, method, containingType, arguments, receiverOverride);
        if (method is { IsStatic: false } && IsGeneratedInstanceType(method.ContainingType))
            return $"this.{GeneratedInstanceMethodName(method)}({string.Join(", ", arguments)})";
        if (method is { IsStatic: false } && IsUserInstanceType(method.ContainingType))
        {
            QueueUserType(method.ContainingType, invocation);
            return $"this.{UserInstanceMethodName(method)}({string.Join(", ", arguments)})";
        }
        if (method is not null && method.DeclaringSyntaxReferences.Length != 0) return EmitUserInvocation(method, invocation, arguments);
        if (method is not null) throw UnsupportedSymbol(method, invocation);
        return $"{Expression(invocation.Expression)}({string.Join(", ", arguments)})";
    }

    private string EmitNormalizedInvocation(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        string? receiver,
        Func<string?, string[], string> emit)
    {
        var sourceArguments = invocation.ArgumentList.Arguments
            .Select(argument => Expression(argument.Expression)).ToArray();
        var key = $"invocation:{invocation.SyntaxTree.FilePath}:{invocation.SpanStart}";
        var receiverTemporary = receiver is null ? null : _names.Get(key + ":receiver", "receiver");
        var temporaries = sourceArguments.Select((_, index) =>
            _names.Get($"{key}:argument:{index}", $"arg{index + 1}")).ToArray();
        var supplied = invocation.ArgumentList.Arguments.Select((argument, index) =>
            (Parameter: InvocationParameter(method, argument, index), Value: temporaries[index])).ToArray();
        var lastOrdinal = supplied.Length == 0 ? -1 : supplied.Max(argument => argument.Parameter.Ordinal);
        var ordered = new List<string>();
        foreach (var parameter in method.Parameters.Take(lastOrdinal + 1))
        {
            var values = supplied.Where(argument =>
                SymbolEqualityComparer.Default.Equals(argument.Parameter, parameter)).Select(argument => argument.Value).ToArray();
            if (values.Length != 0)
            {
                ordered.AddRange(values);
                continue;
            }
            if (!parameter.HasExplicitDefaultValue)
                throw UnsupportedSymbol(method, invocation);
            ordered.Add(LiteralConstant(parameter.ExplicitDefaultValue, invocation));
        }

        var lambdaParameters = receiverTemporary is null ? temporaries : [receiverTemporary, .. temporaries];
        var sourceValues = receiver is null ? sourceArguments : [receiver, .. sourceArguments];
        return $"(({string.Join(", ", lambdaParameters)}) => {emit(receiverTemporary, ordered.ToArray())})"
            + $"({string.Join(", ", sourceValues)})";
    }

    private static IParameterSymbol InvocationParameter(IMethodSymbol method, ArgumentSyntax argument, int position) =>
        argument.NameColon is { } name
            ? method.Parameters.Single(parameter => parameter.Name == name.Name.Identifier.ValueText)
            : method.Parameters[Math.Min(position, method.Parameters.Length - 1)];

    private bool TryEmitStaticInvocation(
        InvocationExpressionSyntax invocation,
        IMethodSymbol? method,
        string? type,
        string? name,
        string[] arguments,
        string? receiverOverride,
        out string result)
    {
        result = (type, name) switch
        {
            ("System.TimeSpan", "FromDays") when arguments.Length == 1 => TimeSpanValue(arguments[0], 86400000),
            ("System.TimeSpan", "FromHours") when arguments.Length == 1 => TimeSpanValue(arguments[0], 3600000),
            ("System.TimeSpan", "FromMinutes") when arguments.Length == 1 => TimeSpanValue(arguments[0], 60000),
            ("System.TimeSpan", "FromSeconds") when arguments.Length == 1 => TimeSpanValue(arguments[0], 1000),
            ("System.TimeSpan", "FromMilliseconds") when arguments.Length == 1 => TimeSpanValue(arguments[0], 1),
            ("System.TimeSpan", "Compare") when arguments.Length == 2 => TimeSpanCompare(arguments[0], arguments[1]),
            ("System.TimeSpan", "Equals") when arguments.Length == 2 => $"{arguments[0]} === {arguments[1]}",
            ("System.DateTimeOffset", "FromUnixTimeMilliseconds") when arguments.Length == 1 =>
                $"{_helpers.Require(JavaScriptHelper.DateTimeFromUnixTime)}({arguments[0]}, false)",
            ("System.DateTimeOffset", "FromUnixTimeSeconds") when arguments.Length == 1 =>
                $"{_helpers.Require(JavaScriptHelper.DateTimeFromUnixTime)}({arguments[0]}, true)",
            ("System.DateTime", "IsLeapYear") when arguments.Length == 1 =>
                HelperInvocation(JavaScriptHelper.DateTimeIsLeapYear, arguments),
            ("System.DateTime", "DaysInMonth") when arguments.Length == 2 =>
                HelperInvocation(JavaScriptHelper.DateTimeDaysInMonth, arguments),
            ("System.DateTime", "Compare") when arguments.Length == 2 =>
                HelperInvocation(JavaScriptHelper.DateTimeCompare, arguments),
            ("System.DateTimeOffset", "Compare") when arguments.Length == 2 =>
                HelperInvocation(JavaScriptHelper.DateTimeCompare, arguments),
            ("System.Guid", "Parse") when HasParameters(method, SpecialType.System_String) =>
                HelperInvocation(JavaScriptHelper.GuidParse, arguments),
            ("System.Text.Json.JsonSerializer", "Serialize") when arguments.Length == 1 =>
                $"JSON.stringify({arguments[0]})",
            ("System.Text.Json.JsonSerializer", "SerializeToUtf8Bytes") when arguments.Length == 1 =>
                $"new TextEncoder().encode(JSON.stringify({arguments[0]}))",
            ("System.Text.Json.JsonSerializer", "Deserialize") when arguments.Length == 1 =>
                JsonDeserialize(invocation, method!, arguments[0]),
            ("System.Console", "WriteLine") when arguments.Length == 1 => $"console.log({arguments[0]})",
            ("System.Guid", "NewGuid") => "globalThis.crypto.randomUUID()",
            ("Workers.Performance", "Now") => "performance.now()",
            ("System.Uri", "UnescapeDataString") when HasParameters(method, SpecialType.System_String) => $"decodeURIComponent({arguments[0]})",
            ("System.Uri", "EscapeDataString") when HasParameters(method, SpecialType.System_String) => $"{_helpers.Require(JavaScriptHelper.EscapeDataString)}({arguments[0]})",
            ("System.Convert", "FromHexString") when HasParameters(method, SpecialType.System_String) => $"{_helpers.Require(JavaScriptHelper.HexDecode)}({arguments[0]})",
            ("System.Convert", "ToHexString") when arguments.Length == 1 && method?.Parameters[0].Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte } => $"Array.from({arguments[0]}, byte => byte.toString(16).padStart(2, \"0\")).join(\"\").toUpperCase()",
            ("System.Convert", "ToBase64String") when arguments.Length == 1 && method?.Parameters[0].Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte } => $"{_helpers.Require(JavaScriptHelper.Base64)}({arguments[0]})",
            ("System.Convert", "FromBase64String") when HasParameters(method, SpecialType.System_String) => Base64Decode(arguments[0]),
            ("System.Text.Encoding", "GetBytes") when HasParameters(method, SpecialType.System_String) && IsUtf8EncodingInvocation(invocation) => $"new TextEncoder().encode({arguments[0]})",
            ("int", "Parse") when HasParameters(method, SpecialType.System_String) => NumericParse(arguments[0], 0),
            ("uint", "Parse") when HasParameters(method, SpecialType.System_String) => NumericParse(arguments[0], 1),
            ("float", "Parse") when HasParameters(method, SpecialType.System_String) => NumericParse(arguments[0], 2),
            ("double", "Parse") when HasParameters(method, SpecialType.System_String) => NumericParse(arguments[0], 3),
            ("bool", "Parse") when HasParameters(method, SpecialType.System_String) => NumericParse(arguments[0], 4),
            ("System.Math" or "System.MathF", _) => MathInvocation(invocation, method!, name!, arguments),
            ("string", _) when method?.IsStatic == true => StringStaticInvocation(invocation, method, name!, arguments),
            ("System.Text.RegularExpressions.Regex", "IsMatch") when method?.IsStatic == true && arguments.Length == 2 =>
                RegexIsMatch(invocation, method, arguments),
            ("Workers.Timers", "SetTimeout") => $"setTimeout({arguments[0]}, {arguments[1]})",
            ("Workers.Timers", "ClearTimeout") => $"clearTimeout({arguments[0]})",
            ("Workers.Body", "Text" or "FromBytes") => arguments[0],
            ("Workers.Body", "Json") => $"JSON.stringify({arguments[0]})",
            ("Workers.Response", _) => ResponseInvocation(invocation, method!, name!, arguments, receiverOverride),
            _ => ""
        };
        return result.Length != 0;
    }

    private static bool HasParameters(IMethodSymbol? method, params SpecialType[] types) =>
        method is not null && method.Parameters.Select(parameter => parameter.Type.SpecialType).SequenceEqual(types);

    private string HelperInvocation(JavaScriptHelper helper, IReadOnlyList<string> arguments) =>
        $"{_helpers.Require(helper)}({string.Join(", ", arguments)})";

    private string TimeSpanValue(string value, int factor) =>
        $"{_helpers.Require(JavaScriptHelper.TimeSpan)}({(factor == 1 ? value : $"({value}) * {factor}")})";

    private string TimeSpanCompare(string left, string right)
    {
        _helpers.Require(JavaScriptHelper.TimeSpan);
        return $"{_helpers.Name("timeSpanCompare")}({left}, {right})";
    }

    private bool IsUtf8EncodingInvocation(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax { Expression: MemberAccessExpressionSyntax receiver }
        && _model.GetSymbolInfo(receiver).Symbol is IPropertySymbol
        {
            IsStatic: true,
            Name: "UTF8",
            ContainingType: { } containingType
        }
        && containingType.ToDisplayString() == "System.Text.Encoding";

    private string Base64Decode(string value)
    {
        _helpers.Require(JavaScriptHelper.Base64);
        return $"{_helpers.Name("base64Decode")}({value})";
    }

    private string NumericParse(string value, int kind) =>
        $"{_helpers.Require(JavaScriptHelper.NumericParse)}({value}, {kind})";

    private string JsonDeserialize(InvocationExpressionSyntax source, IMethodSymbol method, string value)
    {
        if (method.TypeArguments.FirstOrDefault() is INamedTypeSymbol resultType && IsUserInstanceType(resultType))
            RegisterJsonMaterializer(resultType, source);
        var input = method.Parameters[0].Type;
        var parsed = input.SpecialType == SpecialType.System_String
            ? $"JSON.parse({value})"
            : input.ToDisplayString() == "System.ReadOnlySpan<byte>" || input is IArrayTypeSymbol
            {
                ElementType.SpecialType: SpecialType.System_Byte
            }
                ? $"JSON.parse(new TextDecoder().decode({value}))"
                : throw UnsupportedSymbol(method, source);
        if (method.TypeArguments.FirstOrDefault() is INamedTypeSymbol target && IsUserInstanceType(target))
            return $"{QueueUserType(target, source)}.$fromJSON({parsed})";
        return parsed;
    }

    private string MathInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string name,
        string[] arguments)
    {
        var single = method.ContainingType.ToDisplayString() == "System.MathF";
        var type = method.ReturnType.SpecialType;
        if (type is not (SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Single or SpecialType.System_Double))
            throw UnsupportedSymbol(method, source);
        string result = name switch
        {
            "Abs" when arguments.Length == 1 && type == SpecialType.System_Int32 =>
                HelperInvocation(JavaScriptHelper.MathAbsInt, arguments),
            "Abs" when arguments.Length == 1 => $"Math.abs({arguments[0]})",
            "Clamp" when arguments.Length == 3 => HelperInvocation(JavaScriptHelper.MathClamp, arguments),
            "Round" when arguments.Length == 1 =>
                HelperInvocation(JavaScriptHelper.MathRound, [arguments[0], "0", single ? "6" : "15"]),
            "Round" when arguments.Length == 2
                              && method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 =>
                HelperInvocation(JavaScriptHelper.MathRound, [.. arguments, single ? "6" : "15"]),
            "Floor" when arguments.Length == 1 => $"Math.floor({arguments[0]})",
            "Ceiling" when arguments.Length == 1 => $"Math.ceil({arguments[0]})",
            "Truncate" when arguments.Length == 1 => $"Math.trunc({arguments[0]})",
            "Sign" when arguments.Length == 1 => HelperInvocation(JavaScriptHelper.MathSign, arguments),
            "Min" when arguments.Length == 2 => $"Math.min({arguments[0]}, {arguments[1]})",
            "Max" when arguments.Length == 2 => $"Math.max({arguments[0]}, {arguments[1]})",
            "Pow" when arguments.Length == 2 => $"Math.pow({arguments[0]}, {arguments[1]})",
            "Sqrt" when arguments.Length == 1 => $"Math.sqrt({arguments[0]})",
            "Log" when arguments.Length == 1 => $"Math.log({arguments[0]})",
            "Log" when arguments.Length == 2 => HelperInvocation(JavaScriptHelper.MathLog, arguments),
            "Log10" when arguments.Length == 1 => $"Math.log10({arguments[0]})",
            "Exp" when arguments.Length == 1 => $"Math.exp({arguments[0]})",
            "Sin" when arguments.Length == 1 => $"Math.sin({arguments[0]})",
            "Cos" when arguments.Length == 1 => $"Math.cos({arguments[0]})",
            "Tan" when arguments.Length == 1 => $"Math.tan({arguments[0]})",
            "Asin" when arguments.Length == 1 => $"Math.asin({arguments[0]})",
            "Acos" when arguments.Length == 1 => $"Math.acos({arguments[0]})",
            "Atan" when arguments.Length == 1 => $"Math.atan({arguments[0]})",
            "Atan2" when arguments.Length == 2 => $"Math.atan2({arguments[0]}, {arguments[1]})",
            _ => throw UnsupportedSymbol(method, source)
        };
        return single && type == SpecialType.System_Single ? $"Math.fround({result})" : result;
    }

    private string RegexIsMatch(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        IReadOnlyList<string> arguments)
    {
        if (invocation.ArgumentList.Arguments[1].Expression is not LiteralExpressionSyntax literal
            || !IsCompatibleRegexPattern(literal.Token.ValueText))
            throw UnsupportedSymbol(method, invocation);
        return $"new RegExp({arguments[1]}).test({arguments[0]})";
    }

    private static bool IsCompatibleRegexPattern(string pattern) =>
        pattern.All(character => character <= 0x7f)
        && !pattern.Contains('\\')
        && !pattern.Contains("(?", StringComparison.Ordinal)
        && !pattern.Contains("-[", StringComparison.Ordinal);

    private string TaskWhenAll(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        IReadOnlyList<string> arguments)
    {
        if (!method.Parameters[0].IsParams)
            return $"Promise.all({arguments[0]})";
        if (arguments.Count == 1 && IsTaskCollection(_model.GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression).Type))
            return $"Promise.all({arguments[0]})";
        return $"Promise.all([{string.Join(", ", arguments)}])";
    }

    private static bool IsTaskCollection(ITypeSymbol? type) => type is IArrayTypeSymbol
        || type is INamedTypeSymbol named && named.AllInterfaces.Any(item =>
            item.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");

    private string MemberInvocation(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax member,
        IMethodSymbol? method,
        string? type,
        string[] arguments,
        string? receiverOverride = null)
    {
        var name = method?.Name ?? member.Name.Identifier.ValueText;
        if (type == "System.IO.TextWriter" && name == "WriteLine"
            && _model.GetSymbolInfo(member.Expression).Symbol is IPropertySymbol { ContainingType: { } consoleType, Name: "Error" }
            && consoleType.ToDisplayString() == "System.Console")
            return arguments.Length == 1 ? $"console.error({arguments[0]})" : throw UnsupportedSymbol(method, invocation);
        var receiver = receiverOverride ?? Expression(member.Expression);
        if (TryEmitFrameworkInvocation(invocation, method, receiver, name, arguments, out var framework)) return framework;
        if (type == "Workers.Env" && EnvironmentBindings.Contains(name)) return $"{receiver}[{arguments[0]}]";
        if (type == "Workers.CacheStorage" && name == "OpenAsync") return $"caches.open({arguments[0]})";
        if (type == "Workers.Http" && name == "FetchAsync")
            return HttpFetch(invocation, method!, arguments);
        if (type == "Workers.WebSocketPair" && name == "Create") return "new WebSocketPair()";
        if (type == "Workers.TcpSocket" && name == "Connect") return SocketConnect(method, arguments);
        if (type == "Workers.Crypto") receiver = "globalThis.crypto";
        if (method is not null && BindingIntrinsicRegistry.TryGet(method, out var intrinsic)) return EmitBindingIntrinsic(receiver, invocation, method, intrinsic);
        if (method is { IsStatic: false } && IsGeneratedInstanceType(method.ContainingType))
            return $"{receiver}.{GeneratedInstanceMethodName(method)}({string.Join(", ", arguments)})";
        if (method is { IsStatic: false } && IsUserInstanceType(method.ContainingType))
        {
            QueueUserType(method.ContainingType, invocation);
            return $"{receiver}.{UserInstanceMethodName(method)}({string.Join(", ", arguments)})";
        }
        if (method is not null && method.DeclaringSyntaxReferences.Length != 0) return EmitUserInvocation(method, invocation, arguments);
        throw UnsupportedSymbol(method, invocation);
    }

    private string HttpFetch(InvocationExpressionSyntax source, IMethodSymbol method, string[] arguments)
    {
        var tokenIndex = method.Parameters.FirstOrDefault(IsCancellationToken)?.Ordinal ?? -1;
        if (tokenIndex < 0) return $"fetch({string.Join(", ", arguments)})";
        var token = arguments[tokenIndex];
        var native = arguments.Where((_, index) => index != tokenIndex).ToArray();
        return native.Length switch
        {
            1 => $"fetch({native[0]}, {{ signal: {token} ?? undefined }})",
            2 => $"fetch({native[0]}, {{ ...{native[1]}, ...({token} == null ? {{}} : {{ signal: {token} }}) }})",
            _ => throw UnsupportedSymbol(method, source)
        };
    }

    private static bool IsCancellationToken(IParameterSymbol parameter) =>
        parameter.Type.ToDisplayString() == "System.Threading.CancellationToken";

    private string ResponseInvocation(
        InvocationExpressionSyntax invocation,
        IMethodSymbol? method,
        string name,
        string[] arguments,
        string? receiverOverride) => name switch
    {
        "Text" => Response(arguments, "text"),
        "Html" => $"new Response({arguments[0]}{ResponseInit(arguments, 1, 2, "{ \"content-type\": \"text/html; charset=utf-8\" }")})",
        "Json" => $"Response.json({arguments[0]}{JsonResponseInit(arguments)})",
        "Empty" => $"new Response(null{ResponseInit(arguments, 0, 1)})",
        "Redirect" when arguments.Length > 2 => $"new Response(null{ResponseInit(arguments, 1, 2, $"{{ location: {arguments[0]} }}")})",
        "Redirect" => $"Response.redirect({arguments[0]}, {(arguments.Length > 1 ? arguments[1] : "302")})",
        "FromBody" => ResponseFromBody(invocation, arguments),
        "FromStream" when method?.Parameters.Length >= 2 && method.Parameters[1].Type.ToDisplayString() == "Workers.Headers" => $"new Response({arguments[0]}, {{ status: {(arguments.Length > 2 ? arguments[2] : "200")}, headers: {arguments[1]} }})",
        "FromStream" => $"new Response({arguments[0]}{ResponseInit(arguments, 1, 2)})",
        "WebSocket" => $"new Response(null, {{ status: 101, webSocket: {arguments[0]} }})",
        "WithHeader" => HeaderMutation(invocation, arguments, "set", receiverOverride),
        "AppendHeader" => HeaderMutation(invocation, arguments, "append", receiverOverride),
        "WithoutHeader" => HeaderMutation(invocation, arguments, "delete", receiverOverride),
        _ => ""
    };

    private string ResponseFromBody(InvocationExpressionSyntax invocation, string[] arguments)
    {
        var body = _names.Get($"response-body:{invocation.SyntaxTree.FilePath}:{invocation.SpanStart}", "body");
        return $"(({body}) => new Response({body}.body ?? {body}{ResponseInit(arguments, 1, 2)}))({arguments[0]})";
    }

    private string HeaderMutation(
        InvocationExpressionSyntax invocation,
        string[] arguments,
        string operation,
        string? receiverOverride)
    {
        var helper = _helpers.Require(JavaScriptHelper.WithHeader);
        var receiver = receiverOverride ?? Expression(((MemberAccessExpressionSyntax)invocation.Expression).Expression);
        return operation switch
        {
            "set" => $"{helper}({receiver}, {arguments[0]}, {arguments[1]})",
            "append" => $"{helper}({receiver}, {arguments[0]}, {arguments[1]}, \"append\")",
            _ => $"{helper}({receiver}, {arguments[0]}, undefined, \"delete\")"
        };
    }

    private string SocketConnect(IMethodSymbol? method, string[] arguments)
    {
        var connect = _imports.Require("cloudflare:sockets", "connect", "connectSocket");
        if (method?.Parameters.Length >= 2 && method.Parameters[0].Type.SpecialType == SpecialType.System_String
            && method.Parameters[1].Type.SpecialType == SpecialType.System_Int32)
            return $"{connect}({{ hostname: {arguments[0]}, port: {arguments[1]} }}{(arguments.Length > 2 ? $", {arguments[2]}" : "")})";
        return $"{connect}({string.Join(", ", arguments)})";
    }

    private static readonly HashSet<string> EnvironmentBindings =
    [
        "Get", "Variable", "Secret", "Raw", "Kv", "R2", "Service", "Assets", "Mtls", "Dispatcher", "Queue", "D1",
        "DurableObject", "RateLimiter", "Analytics", "Email", "Version", "Ai", "Workflow", "Images", "Media",
        "Vectorize", "SecretStore", "Hyperdrive"
    ];
}
