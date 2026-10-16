using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string RecordEqualitySlot => _names.Get("record-equality-slot", "recordEquals");
    private string RecordTextSlot => _names.Get("record-text-slot", "recordText");
    private string RecordPrintSlot => _names.Get("record-print-slot", "printRecordMembers");

    private void EmitRecordHierarchyMembers(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        if (!type.IsRecord || !UsesUserHierarchy(type)) return;
        _output.Append("  ").Append(RecordEqualitySlot).Append("(other) { return ")
            .Append(RecordEqualityFunction(type, declaration)).AppendLine("(this, other); }");
        var equalitySlots = new HashSet<string>(StringComparer.Ordinal);
        for (var parent = type.BaseType; parent is { IsRecord: true }; parent = parent.BaseType)
            if (UserRecordEqualsMethod(parent) is { IsVirtual: true } customEquality
                && equalitySlots.Add(UserInstanceMethodName(customEquality)))
                _output.Append("  ").Append(UserInstanceMethodName(customEquality))
                    .Append("(other) { return this.").Append(RecordEqualitySlot).AppendLine("(other); }");
        _output.Append("  ").Append(RecordTextSlot).Append("() { return ")
            .Append(RecordToStringMethod(type) is { IsImplicitlyDeclared: false } customText
                ? $"this.{UserInstanceMethodName(customText)}()"
                : $"{RecordTextFunction(type, declaration, require: false)}(this)").AppendLine("; }");
        _output.Append("  ").Append(RecordPrintSlot).Append("() { return ")
            .Append(RecordPrintFunction(type, declaration, require: false)).AppendLine("(this); }");
    }
}
