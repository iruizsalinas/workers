using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private string EmitSourceExtensionInvocation(
        IInvocationOperation operation,
        InvocationExpressionSyntax invocation,
        IMethodSymbol reduced)
    {
        var extension = reduced.GetConstructedReducedFrom()!;
        // IOperation exposes the static target and the receiver as argument zero.
        // The reduced symbol exposes only the arguments written after the receiver.
        var receiver = operation.Instance is not null
            ? Expression(operation.Instance)
            : Expression(operation.Arguments.First(argument => argument.Parameter?.Ordinal == 0).Value);
        return EmitNormalizedInvocation(invocation, reduced, receiver,
            (target, arguments) => EmitUserInvocation(extension, invocation, [target!, .. arguments]));
    }
}
