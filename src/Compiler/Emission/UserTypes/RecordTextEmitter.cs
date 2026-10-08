using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

// Records get a compiler-synthesized ToString() of the form "Name { A = 1, B = text }" that lists
// public instance properties and fields. It is emitted as one function per record type because
// plain records are lowered to object literals without methods.
internal sealed partial class JavaScriptEmitter
{
    private readonly Dictionary<INamedTypeSymbol, string> _recordTextFunctions = new(SymbolEqualityComparer.Default);
    private readonly Queue<(INamedTypeSymbol Type, SyntaxNode Source)> _pendingRecordTextFunctions = new();

    private static bool IsSynthesizedRecordToString(IMethodSymbol? method) =>
        method is { Name: "ToString", Parameters.Length: 0, IsImplicitlyDeclared: true, ContainingType.IsRecord: true }
        && IsUserInstanceType(method.ContainingType);

    private string RecordText(INamedTypeSymbol type, string value, SyntaxNode source) =>
        $"{RecordTextFunction(type, source)}({value})";

    private string RecordTextFunction(INamedTypeSymbol type, SyntaxNode source)
    {
        type = type.OriginalDefinition;
        if (_recordTextFunctions.TryGetValue(type, out var existing))
            return existing;
        var name = _names.Get($"record-text:{type.ToDisplayString()}", $"recordText${type.Name}");
        _recordTextFunctions.Add(type, name);
        _pendingRecordTextFunctions.Enqueue((type, source));
        return name;
    }

    private bool EmitPendingRecordTextFunctions()
    {
        var emitted = false;
        while (_pendingRecordTextFunctions.TryDequeue(out var pending))
        {
            emitted = true;
            var (type, source) = pending;
            var declaration = (TypeDeclarationSyntax)type.DeclaringSyntaxReferences[0].GetSyntax();
            _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
            var members = RecordPrintedMembers(type, declaration)
                .Select(member => $"{member.Name} = ${{{RecordMemberText(member.Type, UserMemberAccess("value", member.Symbol), source)}}}")
                .ToArray();
            var body = members.Length == 0 ? $"{type.Name} {{ }}" : $"{type.Name} {{ {string.Join(", ", members)} }}";
            _output.Append("function ").Append(_recordTextFunctions[type]).AppendLine("(value) {");
            _output.Append("  return `").Append(body).AppendLine("`;");
            _output.AppendLine("}").AppendLine();
        }
        return emitted;
    }

    private IEnumerable<(ISymbol Symbol, string Name, ITypeSymbol Type)> RecordPrintedMembers(
        INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        if (declaration is RecordDeclarationSyntax { ParameterList: { } parameters })
            foreach (var parameter in parameters.Parameters)
            {
                var property = type.GetMembers(parameter.Identifier.ValueText).OfType<IPropertySymbol>().Single();
                if (seen.Add(property)) yield return (property, property.Name, property.Type);
            }
        foreach (var member in declaration.Members)
        {
            var symbols = member switch
            {
                PropertyDeclarationSyntax property => [_model.GetDeclaredSymbol(property)!],
                FieldDeclarationSyntax field => field.Declaration.Variables.Select(variable => _model.GetDeclaredSymbol(variable)!).ToArray(),
                _ => Array.Empty<ISymbol>()
            };
            foreach (var symbol in symbols)
                if (symbol is { IsStatic: false, DeclaredAccessibility: Accessibility.Public } && seen.Add(symbol))
                    yield return (symbol, symbol.Name, symbol switch
                    {
                        IPropertySymbol property => property.Type,
                        IFieldSymbol field => field.Type,
                        _ => throw new InvalidOperationException()
                    });
        }
    }

    private string RecordMemberText(ITypeSymbol type, string value, SyntaxNode source)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            return $"(item => item == null ? \"\" : {RecordMemberText(nullable.TypeArguments[0], "item", source)})({value})";
        if (type.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
            return $"{_helpers.Require(JavaScriptHelper.NumberText)}({value}{(type.SpecialType == SpecialType.System_Single ? ", true" : "")})";
        if (type.SpecialType == SpecialType.System_Boolean)
            return $"({value} ? \"True\" : \"False\")";
        if (type.SpecialType is SpecialType.System_String)
            return $"({value} ?? \"\")";
        if (type.SpecialType is SpecialType.System_Char or >= SpecialType.System_SByte and <= SpecialType.System_UInt64
            || type.ToDisplayString() == "System.Guid")
            return value;
        if (type is INamedTypeSymbol { IsRecord: true } record && IsUserInstanceType(record))
            return $"(item => item == null ? \"\" : {RecordText(record, "item", source)})({value})";
        if (IsTextEnum(type))
            return EnumText(type, value);
        throw new NotSupportedException(
            $"WRK105: The synthesized ToString() of a record cannot format member type '{type.ToDisplayString()}'.");
    }
}
