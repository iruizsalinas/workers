using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private string Invocation(IInvocationOperation operation)
    {
        var invocation = (InvocationExpressionSyntax)operation.Syntax;
        // Reduced extension symbols retain the platform adapters' receiver convention.
        var method = _model.GetSymbolInfo(invocation).Symbol as IMethodSymbol ?? operation.TargetMethod;
        if (method.DeclaringSyntaxReferences.Length == 0
            && TryEmitOutInvocation(operation, method, out var outInvocation))
            return outInvocation;
        if (method.Name is "Parse" or "TryParse" && method.ContainingType.SpecialType is
            SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Decimal or SpecialType.System_Single)
            throw UnsupportedSymbol(method, invocation);
        var arguments = invocation.ArgumentList.Arguments.Select(ArgumentValue).ToArray();
        var member = invocation.Expression as MemberAccessExpressionSyntax;
        var receiver = operation.Instance is null || operation.Instance is IInstanceReferenceOperation ? null : Operand(operation.Instance);
        if (method.MethodKind == MethodKind.DelegateInvoke)
            return invocation.ArgumentList.Arguments.Any(argument => argument.NameColon is not null)
                ? EmitNormalizedInvocation(invocation, method, Operand(operation.Instance!),
                    (target, values) => $"{target}({string.Join(", ", values)})")
                : $"{Operand(operation.Instance!)}({string.Join(", ", arguments)})";
        var isBindingIntrinsic = BindingIntrinsicRegistry.TryGet(method, out _);
        if (invocation.ArgumentList.Arguments.Any(argument => argument.NameColon is not null)
            && !isBindingIntrinsic)
        {
            return EmitNormalizedInvocation(invocation, method, receiver,
                (normalizedReceiver, normalizedArguments) =>
                    InvocationCore(invocation, method, normalizedArguments, member, normalizedReceiver));
        }
        return InvocationCore(invocation, method, arguments, member, receiver);
    }

    private string InvocationCore(
        InvocationExpressionSyntax invocation,
        IMethodSymbol? method,
        string[] arguments,
        MemberAccessExpressionSyntax? member,
        string? receiverOverride = null)
    {
        if (method is not null && TryEmitNativeInvocation(invocation, method, arguments, member, receiverOverride, out var native)) return native;
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
        if (TryEmitStaticInvocation(invocation, method, containingType, methodName, arguments, receiverOverride, out var result)) return result;
        if (member is not null || receiverOverride is not null)
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
            .Select(ArgumentValue).ToArray();
        var key = $"invocation:{invocation.SyntaxTree.FilePath}:{invocation.SpanStart}";
        var receiverTemporary = receiver is null ? null : _names.Get(key + ":receiver", "receiver");
        var temporaries = sourceArguments.Select((_, index) =>
            _names.Get($"{key}:argument:{index}", $"arg{index + 1}")).ToArray();
        var supplied = invocation.ArgumentList.Arguments.Select((argument, index) =>
            (Parameter: BoundParameter(argument), Value: temporaries[index])).ToArray();
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

    private IArgumentOperation BoundArgument(ArgumentSyntax argument)
    {
        if (_model.GetOperation(argument) is IArgumentOperation bound) return bound;
        // Expanded params elements belong to the implicit array/collection argument.
        ExpressionSyntax expression = argument.Expression;
        while (expression is PostfixUnaryExpressionSyntax { RawKind: (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.SuppressNullableWarningExpression }
            or ParenthesizedExpressionSyntax or CheckedExpressionSyntax)
            expression = expression switch
            {
                ParenthesizedExpressionSyntax parentheses => parentheses.Expression,
                CheckedExpressionSyntax checkedExpression => checkedExpression.Expression,
                _ => ((PostfixUnaryExpressionSyntax)expression).Operand
            };
        for (var operation = _model.GetOperation(expression); operation is not null; operation = operation.Parent)
            if (operation is IArgumentOperation parameter) return parameter;
        throw Unsupported("WRK105", argument);
    }

    private IParameterSymbol BoundParameter(ArgumentSyntax argument)
    {
        var parameter = BoundArgument(argument).Parameter ?? throw Unsupported("WRK105", argument);
        if (argument.Parent?.Parent is InvocationExpressionSyntax invocation
            && _model.GetSymbolInfo(invocation).Symbol is IMethodSymbol { ReducedFrom: not null } reduced)
            return reduced.Parameters[parameter.Ordinal - 1];
        return parameter;
    }

    private string ArgumentValue(ArgumentSyntax argument)
    {
        var bound = BoundArgument(argument);
        // Platform adapters deliberately receive expanded params as individual JS arguments.
        return bound.ArgumentKind is ArgumentKind.ParamArray or ArgumentKind.ParamCollection
            ? Expression(argument.Expression) : Expression(bound.Value);
    }

}
