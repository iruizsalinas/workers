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

}
