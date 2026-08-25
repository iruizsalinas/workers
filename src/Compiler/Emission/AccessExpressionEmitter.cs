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
        ThrowIfUnsupportedFrameworkMember(symbol, member);
        return $"{receiver}?.{LowerFirst(member.Name.Identifier.Text)}";
    }

    private string ElementAccess(ElementAccessExpressionSyntax value)
    {
        var receiver = Expression(value.Expression);
        if (BindingIntrinsicRegistry.IsQueueMessageBatch(_model.GetTypeInfo(value.Expression).Type))
            receiver += ".messages";
        return $"{receiver}[{string.Join(", ", value.ArgumentList.Arguments.Select(argument => Expression(argument.Expression)))}]";
    }
}
