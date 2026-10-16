using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private string Invocation(IInvocationOperation operation)
    {
        if (operation.Syntax is not InvocationExpressionSyntax)
        {
            if (IsEnumerableMethod(operation.TargetMethod))
                return LinqInvocation(operation.Syntax, operation.TargetMethod, operation.Arguments.Select(argument => ValueExpression(argument.Value)).ToArray(), null, null);
            if (operation.TargetMethod.DeclaringSyntaxReferences.Length != 0) return InitializerCall(operation);
            throw UnsupportedSymbol(operation.TargetMethod, operation.Syntax);
        }
        var invocation = (InvocationExpressionSyntax)operation.Syntax;
        // Reduced extension symbols retain the platform adapters' receiver convention.
        var method = _model.GetSymbolInfo(invocation).Symbol as IMethodSymbol ?? operation.TargetMethod;
        if (method is { ReducedFrom.DeclaringSyntaxReferences.Length: > 0 })
            return EmitSourceExtensionInvocation(operation, invocation, method);
        method = (IMethodSymbol)StaticInterfaceImplementation(method, operation.ConstrainedToType, invocation);
        method = ResolveMethod(method);
        if (method.MethodKind == MethodKind.LocalFunction)
            return EmitNormalizedInvocation(invocation, method, null, (_, values) =>
                $"{LocalFunctionTarget(method, invocation)}.call(this{(values.Length == 0 ? "" : ", " + string.Join(", ", values))})");
        if (method is { IsGenericMethod: true, IsStatic: false, DeclaringSyntaxReferences.Length: > 0 })
        {
            var receiverValue = operation.Instance is null ? "this" : Expression(operation.Instance);
            var targetFunction = invocation.Expression is MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax }
                ? QueueUserMethod(method, invocation) : GenericMethodTarget(method);
            return EmitNormalizedInvocation(invocation, method, receiverValue, (target, values) =>
                $"{targetFunction}.call({target}{(values.Length == 0 ? "" : ", " + string.Join(", ", values))})");
        }
        if (method.MethodKind != MethodKind.DelegateInvoke && method.DeclaringSyntaxReferences.Length == 0 && !IsSynthesizedRecordMember(method)
            && TryEmitOutInvocation(operation, method, out var outInvocation))
            return outInvocation;
        if (method.Name is "Parse" or "TryParse" && method.ContainingType.SpecialType is
            SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Decimal or SpecialType.System_Single)
            throw UnsupportedSymbol(method, invocation);
        var member = invocation.Expression as MemberAccessExpressionSyntax;
        var receiver = operation.Instance is null || operation.Instance is IInstanceReferenceOperation ? null : Operand(operation.Instance);
        if (method.MethodKind == MethodKind.DelegateInvoke)
            return EmitNormalizedInvocation(invocation, method, receiver ?? Operand(operation.Instance!),
                (target, values) => $"{NotNull(target!)}({string.Join(", ", values)})");
        var isBindingIntrinsic = BindingIntrinsicRegistry.TryGet(method, out _);
        if (!isBindingIntrinsic && !method.IsStatic && NeedsNullCheck(operation.Instance))
        {
            if (!method.Parameters.Any(parameter => parameter.IsParams)
                && !invocation.ArgumentList.Arguments.Any(argument => argument.NameColon is not null)
                && operation.Arguments.All(argument => IsTrivialArgumentValue(argument.Value)))
                return InvocationCore(invocation, method, invocation.ArgumentList.Arguments.Select(ArgumentValue).ToArray(), member, NotNull(receiver!));
            // A C# instance call evaluates every argument before the receiver null check.
            return EmitNormalizedInvocation(invocation, method, receiver,
                (target, values) => InvocationCore(invocation, method, values, member, NotNull(target!)));
        }
        if ((invocation.ArgumentList.Arguments.Any(argument => argument.NameColon is not null)
                || method.DeclaringSyntaxReferences.Length != 0 && method.Parameters.Any(parameter => parameter.IsParams)
                || receiver is not null && !method.IsStatic && method.DeclaringSyntaxReferences.Length != 0
                    && operation.Arguments.Any(argument => !IsTrivialArgumentValue(argument.Value)))
            && !isBindingIntrinsic)
        {
            return EmitNormalizedInvocation(invocation, method, receiver,
                (normalizedReceiver, normalizedArguments) =>
                    InvocationCore(invocation, method, normalizedArguments, member, normalizedReceiver));
        }
        return InvocationCore(invocation, method, invocation.ArgumentList.Arguments.Select(ArgumentValue).ToArray(), member, receiver);
    }

    private static bool IsTrivialArgumentValue(IOperation operation) => operation switch
    {
        _ when operation.ConstantValue.HasValue => true,
        ILocalReferenceOperation or IParameterReferenceOperation or IAnonymousFunctionOperation => true,
        IDelegateCreationOperation { Target: IAnonymousFunctionOperation } => true,
        IConversionOperation conversion when conversion.Conversion.IsIdentity || conversion.Conversion.IsReference =>
            IsTrivialArgumentValue(conversion.Operand),
        _ => false
    };

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
        if (method is { IsStatic: false } && IsSourceInterface(method.ContainingType))
            return method.DeclaredAccessibility == Accessibility.Private
                ? $"{QueueUserMethod(method, invocation)}.call(this{(arguments.Length == 0 ? "" : ", " + string.Join(", ", arguments))})"
                : $"this.{UserInstanceMethodName(method)}({string.Join(", ", arguments)})";
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
        var ordered = OrderCallArguments(method, invocation.ArgumentList.Arguments.ToArray(), temporaries, invocation);
        var lambdaParameters = receiverTemporary is null ? temporaries : [receiverTemporary, .. temporaries];
        var sourceValues = receiver is null ? sourceArguments : [receiver, .. sourceArguments];
        return $"(({string.Join(", ", lambdaParameters)}) => {emit(receiverTemporary, ordered)})"
            + $"({string.Join(", ", sourceValues)})";
    }

    // Roslyn has selected the overload, conversions and normal/expanded params form.
    // Only reorder captured values here, after evaluating all arguments in source order.
    private string[] OrderCallArguments(IMethodSymbol method, ArgumentSyntax[] arguments, string[] values, SyntaxNode source)
    {
        var supplied = arguments.Select((argument, index) =>
            (Parameter: BoundParameter(argument), Value: values[index], Kind: BoundArgument(argument).ArgumentKind)).ToArray();
        var sourceParams = (method.DeclaringSyntaxReferences.Length != 0 || method.MethodKind == MethodKind.DelegateInvoke)
            && method.Parameters.LastOrDefault()?.IsParams == true;
        var lastOrdinal = sourceParams ? method.Parameters.Length - 1
            : supplied.Length == 0 ? -1 : supplied.Max(argument => argument.Parameter.Ordinal);
        var ordered = new List<string>();
        foreach (var parameter in method.Parameters.Take(lastOrdinal + 1))
        {
            var parameterValues = supplied.Where(argument =>
                argument.Parameter.Ordinal == parameter.Ordinal).Select(argument => argument.Value).ToArray();
            if (sourceParams && parameter.IsParams)
            {
                if (parameter.Type is not IArrayTypeSymbol) throw UnsupportedSymbol(parameter, source);
                var explicitArray = supplied.Any(argument => argument.Parameter.Ordinal == parameter.Ordinal && argument.Kind == ArgumentKind.Explicit);
                ordered.Add(explicitArray ? parameterValues.Single() : $"[{string.Join(", ", parameterValues)}]");
                continue;
            }
            if (parameterValues.Length != 0)
            {
                ordered.AddRange(parameterValues);
                continue;
            }
            if (!parameter.HasExplicitDefaultValue)
                throw UnsupportedSymbol(method, source);
            ordered.Add(LiteralConstant(parameter.ExplicitDefaultValue, source));
        }

        return ordered.ToArray();
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
        if (bound.Parameter?.RefKind == RefKind.Out
            && (bound.Parameter.ContainingSymbol.DeclaringSyntaxReferences.Length != 0
                || bound.Parameter.ContainingSymbol is IMethodSymbol method && IsSynthesizedRecordMember(method)))
            return UserOutReference(bound.Value);
        // Platform adapters deliberately receive expanded params as individual JS arguments.
        return bound.ArgumentKind is ArgumentKind.ParamArray or ArgumentKind.ParamCollection
            ? CopyValue(_model.GetTypeInfo(argument.Expression).Type, Expression(argument.Expression)) : ValueExpression(bound.Value);
    }

}
