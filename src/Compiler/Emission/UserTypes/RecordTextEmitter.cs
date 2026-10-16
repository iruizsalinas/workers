using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

// Records get a compiler-synthesized ToString() of the form "Name { A = 1, B = text }" that lists
// public instance properties and fields. It is emitted as one function per record type because
// plain records are lowered to object literals without methods.
internal sealed partial class JavaScriptEmitter
{
    private readonly Dictionary<INamedTypeSymbol, string> _recordTextFunctions = new(SymbolEqualityComparer.Default);
    private readonly HashSet<INamedTypeSymbol> _requiredRecordTextFunctions = new(SymbolEqualityComparer.Default);
    private readonly HashSet<INamedTypeSymbol> _recordTextDispatchTypes = new(SymbolEqualityComparer.Default);
    private readonly HashSet<INamedTypeSymbol> _recordPrintDispatchTypes = new(SymbolEqualityComparer.Default);
    private readonly HashSet<INamedTypeSymbol> _requiredRecordPrintFunctions = new(SymbolEqualityComparer.Default);
    private readonly Queue<(INamedTypeSymbol Type, SyntaxNode Source)> _pendingRecordTextFunctions = new();
    private readonly Queue<(INamedTypeSymbol Type, SyntaxNode Source)> _pendingRecordPrintFunctions = new();

    private static bool IsSynthesizedRecordToString(IMethodSymbol? method) =>
        method is { Name: "ToString", Parameters.Length: 0, IsImplicitlyDeclared: true, ContainingType.IsRecord: true }
        && IsUserInstanceType(method.ContainingType);

    private static IMethodSymbol? RecordToStringMethod(INamedTypeSymbol type)
    {
        for (var current = type; current is { IsRecord: true }; current = current.BaseType)
            if (current.GetMembers("ToString").OfType<IMethodSymbol>().FirstOrDefault(method =>
                    !method.IsStatic && method.Parameters.Length == 0) is { } method)
                return method;
        return null;
    }

    private string RecordText(INamedTypeSymbol type, string value, SyntaxNode source)
    {
        type = (INamedTypeSymbol)ResolveType(type);
        if (RecordToStringMethod(type) is { IsImplicitlyDeclared: false } customText)
        {
            QueueUserType(type, source);
            return $"{value}.{UserInstanceMethodName(customText)}()";
        }
        if (!UsesUserHierarchy(type)) return $"{RecordTextFunction(type, source)}({value})";
        QueueUserType(type, source);
        _recordTextDispatchTypes.Add(type);
        return $"{value}.{RecordTextSlot}()";
    }

    private string RecordTextFunction(INamedTypeSymbol type, SyntaxNode source, bool require = true)
    {
        type = (INamedTypeSymbol)ResolveType(type);
        if (require && _requiredRecordTextFunctions.Add(type))
            _pendingRecordTextFunctions.Enqueue((type, source));
        if (_recordTextFunctions.TryGetValue(type, out var existing)) return existing;
        var name = _names.Get($"record-text:{type.ToDisplayString()}", $"recordText${type.Name}");
        _recordTextFunctions.Add(type, name);
        return name;
    }

    private string RecordPrintFunction(INamedTypeSymbol type, SyntaxNode source, bool require = true)
    {
        type = (INamedTypeSymbol)ResolveType(type);
        if (require && _requiredRecordPrintFunctions.Add(type))
            _pendingRecordPrintFunctions.Enqueue((type, source));
        return _names.Get($"record-print:{type.ToDisplayString()}", $"printMembers${type.Name}");
    }

    private void PrepareRecordTextDispatch()
    {
        foreach (var requested in _recordTextDispatchTypes.ToArray())
            foreach (var type in _userTypes.Keys.ToArray())
                if (type.IsRecord && !type.IsAbstract && _compilation.ClassifyConversion(type, requested).IsImplicit
                    && type.GetMembers("ToString").OfType<IMethodSymbol>().Any(IsSynthesizedRecordToString))
                    RecordTextFunction(type, type.DeclaringSyntaxReferences[0].GetSyntax());
        foreach (var requested in _requiredRecordTextFunctions.ToArray())
            if (UsesUserHierarchy(requested))
                RecordPrintFunction(requested, requested.DeclaringSyntaxReferences[0].GetSyntax());
        // A base.ToString() call keeps the base label but invokes PrintMembers virtually.
        foreach (var requested in _recordPrintDispatchTypes.ToArray())
            foreach (var type in _userTypes.Keys.ToArray())
                if (type.IsRecord && _compilation.ClassifyConversion(type, requested).IsImplicit)
                    RecordPrintFunction(type, type.DeclaringSyntaxReferences[0].GetSyntax());
    }

    private bool EmitPendingRecordTextFunctions()
    {
        var emitted = false;
        while (_pendingRecordTextFunctions.TryDequeue(out var pending))
        {
            emitted = true;
            var (type, source) = pending;
            using var specialization = SpecializationScope(type);
            var declaration = (TypeDeclarationSyntax)type.DeclaringSyntaxReferences[0].GetSyntax();
            _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
            _output.Append("function ").Append(_recordTextFunctions[type]).AppendLine("(value) {");
            _output.AppendLine($"  if (value == null) throw {NullReferenceException()};");
            if (UsesUserHierarchy(type))
            {
                _output.Append("  const members = value.").Append(RecordPrintSlot).AppendLine("();");
                _output.Append("  return ").Append(JsonSerializer.Serialize(type.Name + " {"))
                    .AppendLine(" + (members.length === 0 ? \" \" : \" \" + members + \" \") + \"}\";");
            }
            else
            {
                var members = RecordPrintedText(type, declaration, source);
                var body = members.Length == 0 ? $"{type.Name} {{ }}" : $"{type.Name} {{ {string.Join(", ", members)} }}";
                _output.Append("  return `").Append(body).AppendLine("`;");
            }
            _output.AppendLine("}").AppendLine();
        }
        while (_pendingRecordPrintFunctions.TryDequeue(out var pending))
        {
            emitted = true;
            var (type, source) = pending;
            using var specialization = SpecializationScope(type);
            var declaration = (TypeDeclarationSyntax)type.DeclaringSyntaxReferences[0].GetSyntax();
            _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
            var members = RecordPrintedText(type, declaration, source);
            _output.Append("function ").Append(RecordPrintFunction(type, source)).AppendLine("(value) {");
            _output.Append("  return `").Append(string.Join(", ", members)).AppendLine("`;");
            _output.AppendLine("}").AppendLine();
        }
        return emitted;
    }

    private string[] RecordPrintedText(INamedTypeSymbol type, TypeDeclarationSyntax declaration, SyntaxNode source) =>
        RecordPrintedMembers(type, declaration)
            .Select(member => $"{member.Name} = ${{{RecordMemberText(member.Type, UserMemberAccess("value", member.Symbol), source)}}}")
            .ToArray();

    private IEnumerable<(ISymbol Symbol, string Name, ITypeSymbol Type)> RecordPrintedMembers(
        INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        if (type.BaseType is { IsRecord: true } parent
            && parent.DeclaringSyntaxReferences[0].GetSyntax() is TypeDeclarationSyntax parentDeclaration)
            foreach (var member in RecordPrintedMembers(parent, parentDeclaration))
                if (seen.Add(member.Symbol.OriginalDefinition)) yield return member;
        if (declaration is RecordDeclarationSyntax { ParameterList: { } parameters })
            foreach (var parameter in parameters.Parameters)
            {
                var property = type.GetMembers(parameter.Identifier.ValueText).OfType<IPropertySymbol>().SingleOrDefault();
                if (property is null || property.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is PropertyDeclarationSyntax)) continue;
                if (seen.Add(property.OriginalDefinition)) yield return (property, property.Name, property.Type);
            }
        foreach (var member in declaration.Members)
        {
            var symbols = member switch
            {
                PropertyDeclarationSyntax property => [_compilation.GetSemanticModel(property.SyntaxTree).GetDeclaredSymbol(property)!],
                FieldDeclarationSyntax field => field.Declaration.Variables.Select(variable => _compilation.GetSemanticModel(variable.SyntaxTree).GetDeclaredSymbol(variable)!).ToArray(),
                _ => Array.Empty<ISymbol>()
            };
            foreach (var symbol in symbols)
                if (symbol is { IsStatic: false, DeclaredAccessibility: Accessibility.Public } && seen.Add(symbol.OriginalDefinition))
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
        type = ResolveType(type);
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            return $"(item => item == null ? \"\" : {RecordMemberText(nullable.TypeArguments[0], "item", source)})({value})";
        if (type.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
            return $"String({value})";
        if (type.SpecialType == SpecialType.System_Boolean)
            return $"({value} ? \"True\" : \"False\")";
        if (type.SpecialType is SpecialType.System_String)
            return $"({value} ?? \"\")";
        if (type.SpecialType is SpecialType.System_Char or >= SpecialType.System_SByte and <= SpecialType.System_UInt64)
            return value;
        if (type is INamedTypeSymbol { IsRecord: true } record && IsUserInstanceType(record))
            return $"(item => item == null ? \"\" : ({RecordText(record, "item", source)} ?? \"\"))({value})";
        if (type is INamedTypeSymbol { IsTupleType: true } tuple) return TupleText(tuple, value, source);
        if (type is INamedTypeSymbol pair && IsKeyValuePair(pair)) return KeyValuePairText(pair, value, source);
        if (IsTextEnum(type))
            return EnumText(type, value);
        if (type.SpecialType == SpecialType.System_Object)
            return ObjectText(value);
        if (type is INamedTypeSymbol { IsAnonymousType: true } anonymous) return AnonymousText(anonymous, value, source);
        if (type is INamedTypeSymbol user && IsUserInstanceType(user))
            return $"(item => item == null ? \"\" : ({UserObjectText(user, "item", source)} ?? \"\"))({value})";
        if (type is IArrayTypeSymbol || type is INamedTypeSymbol named && named.ContainingNamespace.ToDisplayString().StartsWith("System.Collections", StringComparison.Ordinal))
            return $"({value} == null ? \"\" : {JsonSerializer.Serialize(RuntimeTypeName(type))})";
        throw new NotSupportedException(
            $"WRK105: The synthesized ToString() of a record cannot format member type '{type.ToDisplayString()}'.");
    }
}
