using Microsoft.CodeAnalysis;
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
            _ => throw Unsupported("WRK101", value.WhenNotNull)
        };
    }

    private string ConditionalElement(
        ConditionalAccessExpressionSyntax access,
        ElementBindingExpressionSyntax element,
        string receiver)
    {
        var arguments = element.ArgumentList.Arguments;
        if (!IsSequenceType(_model.GetTypeInfo(access.Expression).Type))
        {
            if (!IsDictionary(_model.GetTypeInfo(access.Expression).Type))
                return $"{receiver}?.[{string.Join(", ", arguments.Select(argument => Expression(argument.Expression)))}]";
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
        if (member.Name.Identifier.Text == "Length"
            && (receiverType?.SpecialType == SpecialType.System_String || receiverType is IArrayTypeSymbol))
            return $"{receiver}?.length";
        if (symbol?.ContainingType is { } userType && IsUserInstanceType(userType) && RequiresUserClass(userType)
            && symbol is IFieldSymbol or IPropertySymbol)
        {
            QueueUserType(userType, member);
            var name = UserMemberName(symbol);
            return IsJavaScriptPropertyIdentifier(name)
                ? $"{receiver}?.{name}"
                : $"{receiver}?.[{System.Text.Json.JsonSerializer.Serialize(name)}]";
        }
        ThrowIfUnsupportedFrameworkMember(symbol, member);
        return $"{receiver}?.{LowerFirst(member.Name.Identifier.Text)}";
    }

    private string ElementAccess(ElementAccessExpressionSyntax value)
    {
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
