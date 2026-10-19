using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string RecordCloneSlot => _names.Get("record-clone-slot", "cloneRecord");
    private string RecordCopyBody(INamedTypeSymbol type) => _names.Get(
        $"record-copy-body:{type.OriginalDefinition.ToDisplayString()}", $"copy${type.Name}");

    private void EmitHierarchyRecordCopy(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        var constructor = declaration.Members.OfType<ConstructorDeclarationSyntax>()
            .SingleOrDefault(candidate => IsUserRecordCopyConstructor((IMethodSymbol)_model.GetDeclaredSymbol(candidate)!));
        var source = constructor is null ? "$source" : ParameterName(constructor.ParameterList.Parameters.Single());
        _output.Append("  ").Append(RecordCloneSlot).AppendLine("() {");
        _output.AppendLine("    const result = Object.create(Object.getPrototypeOf(this));");
        EmitHierarchyDefaults(type, "result");
        _output.Append("    result.").Append(RecordCopyBody(type)).AppendLine("(this);");
        _output.AppendLine("    return result;");
        _output.AppendLine("  }");
        _output.Append("  ").Append(RecordCopyBody(type)).Append('(').Append(source).AppendLine(") {");
        if (type.BaseType is { IsRecord: true } parent)
            _output.Append("    ").Append(QueueUserType(parent, declaration)).Append(".prototype.")
                .Append(RecordCopyBody(parent)).Append(".call(this, ")
                .Append(constructor?.Initializer is { } initializer
                    ? ArgumentValue(initializer.ArgumentList.Arguments.Single()) : source).AppendLine(");");
        if (constructor is not null)
            EmitWithExpressionScope(2, () => EmitUserConstructorStatements(constructor));
        else
            foreach (var field in type.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic))
            {
                _output.Append("    ").Append(BackingStorage("this", field)).Append(" = ")
                    .Append(CopyValue(field.Type, BackingStorage(source, field))).AppendLine(";");
            }
        _output.AppendLine("  }");
    }
}
