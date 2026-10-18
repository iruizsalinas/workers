using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

// Writable references use accessors over the actual storage, rather than copying
// values back after a call. This preserves aliasing and writes before exceptions.
internal sealed partial class JavaScriptEmitter
{
    private string UserOutReference(IOperation target)
    {
        if (target is IFieldReferenceOperation { Field.ContainingType.IsTupleType: true } tupleField)
        {
            var storage = TupleFieldAccess("reference.value", tupleField.Field, target.Syntax);
            return $"((reference) => ({{ get value() {{ return {storage}; }}, set value(value) {{ {storage} = value; }} }}))({UserOutReference(tupleField.Instance!)})";
        }
        if (target is IParameterReferenceOperation primary && IsClassPrimaryParameter(primary.Parameter)) return AssignmentReference(target);
        if (target is IDeclarationExpressionOperation declaration)
        {
            if (declaration.Expression is ILocalReferenceOperation local) DeclareExpressionOutLocal(local);
            return UserOutReference(declaration.Expression);
        }
        if (target is IParameterReferenceOperation { Parameter.RefKind: RefKind.Out } forwarded)
            return UserIdentifier(forwarded.Parameter, forwarded.Parameter.Name);
        if (target is IDiscardOperation) return "({ value: undefined })";
        var key = $"out-reference:{target.Syntax.SyntaxTree.FilePath}:{target.Syntax.SpanStart}";
        var assigned = _names.Get(key + ":value", "assigned");
        string Reference(string storage) =>
            $"({{ get value() {{ return {storage}; }}, set value({assigned}) {{ {storage} = {assigned}; }} }})";
        if (target is ILocalReferenceOperation or IParameterReferenceOperation)
            return Reference(Expression(target));
        if (target is IFieldReferenceOperation field)
        {
            if (field.Field.IsStatic)
            {
                if (!IsUserStaticState(field.Field)) throw UnsupportedSymbol(field.Field, target.Syntax);
                var holder = _names.Get(key + ":holder", "holder");
                return $"(({holder}) => {Reference(holder + StaticMemberKey(field.Field))})({StaticHolder(field.Field.ContainingType)}())";
            }
            var receiver = _names.Get(key + ":receiver", "receiver");
            QueueUserType(field.Field.ContainingType, field.Syntax);
            return $"(({receiver}) => {{ if ({receiver} == null) throw {NullReferenceException()}; " +
                $"return {Reference(UserMemberAccess(receiver, field.Field))}; }})({Expression(field.Instance!)})";
        }
        if (target is IArrayElementReferenceOperation { Indices.Length: 1 } array)
        {
            var receiver = _names.Get(key + ":array", "array");
            var index = _names.Get(key + ":index", "index");
            var check = HelperInvocation(JavaScriptHelper.SequenceIndex, [receiver, index]);
            return $"(({receiver}, {index}) => {{ {check}; if (Array.isArray({index})) {index} = {index}[1] ? {receiver}.length - {index}[0] : {index}[0]; return {Reference($"{receiver}[{index}]")}; }})" +
                $"({Expression(array.ArrayReference)}, {Expression(array.Indices[0])})";
        }
        throw Unsupported("WRK108", target.Syntax);
    }
}
