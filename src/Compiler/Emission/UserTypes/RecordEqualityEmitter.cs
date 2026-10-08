using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Records compare by value: the synthesized Equals compares every instance field with
// EqualityComparer<T>.Default. Generated per record type because plain records are object literals.
internal sealed partial class JavaScriptEmitter
{
    private readonly Dictionary<INamedTypeSymbol, string> _recordEqualityFunctions = new(SymbolEqualityComparer.Default);
    private readonly Queue<(INamedTypeSymbol Type, SyntaxNode Source)> _pendingRecordEqualityFunctions = new();

    private static bool IsSynthesizedRecordMember(IMethodSymbol? method) =>
        method is { IsImplicitlyDeclared: true, ContainingType.IsRecord: true }
        && IsUserInstanceType(method.ContainingType);

    private string RecordEquals(INamedTypeSymbol type, string left, string right, SyntaxNode source) =>
        $"{RecordEqualityFunction(type, source)}({left}, {right})";

    private string RecordEqualityFunction(INamedTypeSymbol type, SyntaxNode source)
    {
        type = type.OriginalDefinition;
        if (_recordEqualityFunctions.TryGetValue(type, out var existing))
            return existing;
        var name = _names.Get($"record-equals:{type.ToDisplayString()}", $"recordEquals${type.Name}");
        _recordEqualityFunctions.Add(type, name);
        _pendingRecordEqualityFunctions.Enqueue((type, source));
        return name;
    }

    private bool EmitPendingRecordEqualityFunctions()
    {
        var emitted = false;
        while (_pendingRecordEqualityFunctions.TryDequeue(out var pending))
        {
            emitted = true;
            var (type, source) = pending;
            var declaration = (TypeDeclarationSyntax)type.DeclaringSyntaxReferences[0].GetSyntax();
            _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
            var comparisons = RecordEqualityMembers(type)
                .Select(member => ValueEquality(member.Type,
                    UserMemberAccess("left", member.Symbol), UserMemberAccess("right", member.Symbol), source))
                .ToArray();
            _output.Append("function ").Append(_recordEqualityFunctions[type]).AppendLine("(left, right) {");
            _output.AppendLine("  if (left === right) return true;");
            _output.AppendLine("  if (left == null || right == null) return false;");
            _output.Append("  return ").Append(comparisons.Length == 0 ? "true" : string.Join("\n    && ", comparisons)).AppendLine(";");
            _output.AppendLine("}").AppendLine();
        }
        return emitted;
    }

    // Instance state: positional and declared auto-properties plus explicit fields.
    private static IEnumerable<(ISymbol Symbol, ITypeSymbol Type)> RecordEqualityMembers(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            if (member.IsStatic) continue;
            if (member is IFieldSymbol { AssociatedSymbol: IPropertySymbol property })
                yield return (property, property.Type);
            else if (member is IFieldSymbol { IsImplicitlyDeclared: false } field)
                yield return (field, field.Type);
        }
    }

    private string ValueEquality(ITypeSymbol type, string left, string right, SyntaxNode source)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            return $"((a, b) => a == null || b == null ? a == b : {ValueEquality(nullable.TypeArguments[0], "a", "b", source)})({left}, {right})";
        if (type.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
            return $"((a, b) => a === b || a !== a && b !== b)({left}, {right})";
        if (type.ToDisplayString() is "System.DateTimeOffset" or "System.DateTime")
            return $"(new Date({left}).getTime() === new Date({right}).getTime())";
        if (type is INamedTypeSymbol { IsRecord: true } record && IsUserInstanceType(record))
            return RecordEquals(record, left, right, source);
        return $"({left} === {right})";
    }
}
