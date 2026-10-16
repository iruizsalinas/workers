using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private bool IsCheckedContext(SyntaxNode source)
    {
        foreach (var ancestor in source.Ancestors())
        {
            if (ancestor is CheckedExpressionSyntax expression) return expression.Keyword.IsKind(SyntaxKind.CheckedKeyword);
            if (ancestor is CheckedStatementSyntax statement) return statement.Keyword.IsKind(SyntaxKind.CheckedKeyword);
        }
        return _compilation.Options.CheckOverflow;
    }
    private string BitwiseAssignment(ICompoundAssignmentOperation operation)
    {
        if (operation.OperatorMethod is not null || operation.InConversion.IsUserDefined || operation.OutConversion.IsUserDefined
            || !IsSimpleMutationTarget(operation.Target)) throw Unsupported("WRK108", operation.Syntax);
        var type = ResolveType(operation.Target.Type!);
        var underlying = UnwrapNullable(type);
        var kind = NumericKind(underlying);
        var boolean = underlying.SpecialType == SpecialType.System_Boolean;
        if (!boolean && kind is not (SpecialType.System_Char or SpecialType.System_Byte or SpecialType.System_SByte
            or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32))
            throw Unsupported("WRK108", operation.Syntax);
        var key = $"bitwise-assignment:{operation.Syntax.SyntaxTree.FilePath}:{operation.Syntax.SpanStart}";
        var left = _names.Get(key + ":left", "left");
        var right = _names.Get(key + ":right", "right");
        var token = operation.OperatorKind switch
        {
            BinaryOperatorKind.And => "&", BinaryOperatorKind.Or => "|", BinaryOperatorKind.ExclusiveOr => "^",
            BinaryOperatorKind.LeftShift => "<<", BinaryOperatorKind.RightShift when kind == SpecialType.System_UInt32 => ">>>",
            BinaryOperatorKind.RightShift => ">>", BinaryOperatorKind.UnsignedRightShift => ">>>",
            _ => throw Unsupported("WRK108", operation.Syntax)
        };
        var lhs = kind == SpecialType.System_Char ? left + ".charCodeAt(0)" : left;
        var raw = $"({lhs} {token} {right})";
        var converted = boolean ? $"Boolean({raw})"
            : ConvertValue(_compilation.GetSpecialType(SpecialType.System_Int32), underlying, raw, operation.Syntax);
        // Roslyn's IsChecked describes the operator, which never overflows for
        // shifts. Its implicit narrowing conversion still uses the checked context.
        if (IsCheckedContext(operation.Syntax) && kind is SpecialType.System_Char or SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16)
        {
            var (minimum, maximum) = kind switch
            {
                SpecialType.System_SByte => (-128, 127), SpecialType.System_Byte => (0, 255),
                SpecialType.System_Int16 => (-32768, 32767), _ => (0, 65535)
            };
            var result = _names.Get(key + ":result", "result");
            converted = $"(({result}) => {{ if ({result} < {minimum} || {result} > {maximum}) throw {NewBuiltInException("System.OverflowException", "Arithmetic operation resulted in an overflow.")}; return {ConvertValue(_compilation.GetSpecialType(SpecialType.System_Int32), underlying, result, operation.Syntax)}; }})({raw})";
        }
        if (operation.IsLifted)
        {
            if (boolean && operation.OperatorKind is BinaryOperatorKind.And or BinaryOperatorKind.Or)
            {
                var decisive = operation.OperatorKind == BinaryOperatorKind.And ? "false" : "true";
                converted = $"{left} === {decisive} || {right} === {decisive} ? {decisive} : {left} == null || {right} == null ? null : {converted}";
            }
            else converted = $"{left} == null || {right} == null ? null : {converted}";
        }
        return Mutate(operation.Target, target => $"({target} = (({left}, {right}) => {converted})({target}, {ValueExpression(operation.Value)}))", ContainsAwait(operation.Value));
    }
}
