using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string ConditionalAccess(ConditionalAccessExpressionSyntax value)
    {
        var receiver = Expression(value.Expression);
        return value.WhenNotNull switch
        {
            MemberBindingExpressionSyntax member => $"{receiver}?.{LowerFirst(member.Name.Identifier.Text)}",
            ElementBindingExpressionSyntax element =>
                $"{receiver}?.[{string.Join(", ", element.ArgumentList.Arguments.Select(argument => Expression(argument.Expression)))}]",
            _ => throw Unsupported("WRK101", value.WhenNotNull)
        };
    }

    private string ElementAccess(ElementAccessExpressionSyntax value)
    {
        var receiver = Expression(value.Expression);
        if (BindingIntrinsicRegistry.IsQueueMessageBatch(_model.GetTypeInfo(value.Expression).Type))
            receiver += ".messages";
        return $"{receiver}[{string.Join(", ", value.ArgumentList.Arguments.Select(argument => Expression(argument.Expression)))}]";
    }
}
