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
            ElementBindingExpressionSyntax element =>
                $"{receiver}?.[{string.Join(", ", element.ArgumentList.Arguments.Select(argument => Expression(argument.Expression)))}]",
            _ => throw Unsupported("WRK101", value.WhenNotNull)
        };
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
        return $"{receiver}[{string.Join(", ", value.ArgumentList.Arguments.Select(argument => Expression(argument.Expression)))}]";
    }
}
