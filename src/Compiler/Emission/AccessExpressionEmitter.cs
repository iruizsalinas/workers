using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string ConditionalAccess(ConditionalAccessExpressionSyntax value)
    {
        var receiver = Expression(value.Expression);
        return value.WhenNotNull switch
        {
            MemberBindingExpressionSyntax member => ConditionalMember(value, member, receiver),
            ElementBindingExpressionSyntax element => ConditionalElement(value, element, receiver),
            _ => SpeculativeConditionalAccess(value, receiver)
        };
    }

    // General x?.rest: the rest is re-bound speculatively against a local of the receiver's
    // non-nullable type, emitted with the regular lowering, and guarded by a null check.
    private string SpeculativeConditionalAccess(ConditionalAccessExpressionSyntax value, string receiver)
    {
        const string local = "__workers_receiver";
        var receiverType = _model.GetTypeInfo(value.Expression).Type;
        var resultType = _model.GetTypeInfo(value).Type;
        if (receiverType is null) throw Unsupported("WRK101", value.WhenNotNull);
        var declared = UnwrapNullable(receiverType).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var access = local + value.WhenNotNull.ToFullString().Trim();
        var statement = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseStatement(
            $"{{ {declared} {local} = default!; {(resultType?.SpecialType == SpecialType.System_Void ? "" : "_ = ")}{access}; }}");
        // A chained x?.a?.b speculates again from inside a speculative model, which Roslyn only allows
        // from the original model at the original position.
        var (model, position) = _model.IsSpeculativeSemanticModel
            ? (_model.ParentModel!, _model.OriginalPositionForSpeculation)
            : (_model, value.SpanStart);
        if (!model.TryGetSpeculativeSemanticModel(position, statement, out var speculative))
            throw Unsupported("WRK101", value.WhenNotNull);
        var block = (BlockSyntax)statement;
        var expression = block.Statements[1] is ExpressionStatementSyntax
        {
            Expression: AssignmentExpressionSyntax { Right: var right }
        } ? right : ((ExpressionStatementSyntax)block.Statements[1]).Expression;
        var original = _model;
        string emitted;
        try
        {
            _model = speculative;
            emitted = Expression(expression);
        }
        finally
        {
            _model = original;
        }
        return $"(({local}) => {local} == null ? null : {emitted})({receiver})";
    }

    private string ConditionalElement(
        ConditionalAccessExpressionSyntax access,
        ElementBindingExpressionSyntax element,
        string receiver)
    {
        var arguments = element.ArgumentList.Arguments;
        if (IsJsonNodeType(_model.GetTypeInfo(access.Expression).Type))
            return SpeculativeConditionalAccess(access, receiver);
        if (IsRegexType(_model.GetTypeInfo(access.Expression).Type, "GroupCollection"))
        {
            var groups = _names.Get($"conditional-groups:{access.SyntaxTree.FilePath}:{access.SpanStart}", "groups");
            return $"(({groups}) => {groups} == null ? null : {RegexGroup(groups, Expression(arguments.Single().Expression))})({receiver})";
        }
        if (!IsSequenceType(_model.GetTypeInfo(access.Expression).Type))
        {
            if (!IsDictionary(_model.GetTypeInfo(access.Expression).Type))
                return $"({receiver}?.[{string.Join(", ", arguments.Select(argument => Expression(argument.Expression)))}] ?? null)";
            var dictionary = _names.Get($"conditional-dictionary:{access.SyntaxTree.FilePath}:{access.SpanStart}", "dictionary");
            return $"(({dictionary}) => {dictionary} == null ? null : {HelperInvocation(JavaScriptHelper.DictionaryIndex, [dictionary, Expression(arguments.Single().Expression)])})({receiver})";
        }
        var temporary = _names.Get($"conditional-index:{access.SyntaxTree.FilePath}:{access.SpanStart}", "sequence");
        return $"(({temporary}) => {temporary} == null ? null : {HelperInvocation(JavaScriptHelper.SequenceIndex, [temporary, Expression(arguments.Single().Expression)])})({receiver})";
    }

    private string ConditionalMember(
        ConditionalAccessExpressionSyntax access,
        MemberBindingExpressionSyntax member,
        string receiver)
    {
        var symbol = _model.GetSymbolInfo(member).Symbol;
        var receiverType = _model.GetTypeInfo(access.Expression).Type;
        if (IsDateOrTimeOnly(symbol?.ContainingType) || IsJsonNodeType(symbol?.ContainingType) || IsJsonNodeType(receiverType))
            return SpeculativeConditionalAccess(access, receiver);
        if (symbol?.Name == "Length"
            && (receiverType?.SpecialType == SpecialType.System_String || receiverType is IArrayTypeSymbol))
            return $"({receiver}?.length ?? null)";
        if (RegexCaptureField(symbol) is { } regexField)
            return $"({receiver}?.{regexField} ?? null)";
        if (symbol?.ContainingType is { } userType && IsUserInstanceType(userType) && RequiresUserClass(userType)
            && symbol is IFieldSymbol or IPropertySymbol)
        {
            QueueUserType(userType, member);
            var name = UserMemberName(symbol);
            return IsJavaScriptPropertyIdentifier(name)
                ? $"({receiver}?.{name} ?? null)"
                : $"({receiver}?.[{System.Text.Json.JsonSerializer.Serialize(name)}] ?? null)";
        }
        ThrowIfUnsupportedFrameworkMember(symbol, member);
        return $"({receiver}?.{LowerFirst(symbol?.Name ?? member.Name.Identifier.ValueText)} ?? null)";
    }

    private string ElementAccess(ElementAccessExpressionSyntax value)
    {
        if (IsJsonNodeType(_model.GetTypeInfo(value.Expression).Type))
            return JsonNodeElementAccess(value);
        var receiver = Expression(value.Expression);
        if (_model.GetTypeInfo(value.Expression).Type?.ToDisplayString() == "System.Text.Json.JsonElement")
        {
            var index = value.ArgumentList.Arguments.Single();
            if (_model.GetTypeInfo(index.Expression).Type?.SpecialType != SpecialType.System_Int32)
                throw Unsupported("WRK108", value);
            var position = Expression(index.Expression);
            return HelperInvocation(JavaScriptHelper.JsonElementGetIndex, [receiver, position]);
        }
        if (_model.GetTypeInfo(value.Expression).Type is INamedTypeSymbol lookup
            && lookup.OriginalDefinition.ToDisplayString() == "System.Linq.ILookup<TKey, TElement>")
            return $"{receiver}.get({string.Join(", ", value.ArgumentList.Arguments.Select(argument => Expression(argument.Expression)))})";
        if (BindingIntrinsicRegistry.IsQueueMessageBatch(_model.GetTypeInfo(value.Expression).Type))
            receiver += ".messages";
        if (IsRegexType(_model.GetTypeInfo(value.Expression).Type, "GroupCollection"))
            return RegexGroup(receiver, Expression(value.ArgumentList.Arguments.Single().Expression));
        if (_model.GetTypeInfo(value.Expression).Type?.ToDisplayString() == "System.Text.StringBuilder")
        {
            _helpers.Require(JavaScriptHelper.StringBuilder);
            return $"{_helpers.Name("stringBuilderCharAt")}({receiver}, {Expression(value.ArgumentList.Arguments.Single().Expression)})";
        }
        if (IsSequenceType(_model.GetTypeInfo(value.Expression).Type))
        {
            var index = value.ArgumentList.Arguments.Single();
            return HelperInvocation(JavaScriptHelper.SequenceIndex, [receiver, Expression(index.Expression)]);
        }
        if (IsDictionary(_model.GetTypeInfo(value.Expression).Type))
        {
            var key = value.ArgumentList.Arguments.Single();
            return HelperInvocation(JavaScriptHelper.DictionaryIndex, [receiver, Expression(key.Expression)]);
        }
        return $"{receiver}[{string.Join(", ", value.ArgumentList.Arguments.Select(argument => Expression(argument.Expression)))}]";
    }

    private static bool IsSequenceType(ITypeSymbol? type)
    {
        if (type?.SpecialType == SpecialType.System_String || type is IArrayTypeSymbol) return true;
        return type is INamedTypeSymbol named
            && (named.OriginalDefinition.ToDisplayString() is
                    "System.Collections.Generic.List<T>" or
                    "System.Collections.Generic.IList<T>" or
                    "System.Collections.Generic.IReadOnlyList<T>"
                || named.AllInterfaces.Any(item => item.OriginalDefinition.ToDisplayString() is
                    "System.Collections.Generic.IList<T>" or
                    "System.Collections.Generic.IReadOnlyList<T>"));
    }
}
