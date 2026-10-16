using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

// Static fields and static auto-properties live in one lazily created holder object per type.
// The holder runs field initializers in declaration order followed by the static constructor the
// first time any static member of the type is touched, mirroring CLR type initialization and
// avoiding JavaScript temporal-dead-zone ordering problems between module-level declarations.
internal sealed partial class JavaScriptEmitter
{
    private readonly Dictionary<INamedTypeSymbol, string> _staticHolders = new(SymbolEqualityComparer.Default);
    private readonly Queue<INamedTypeSymbol> _pendingStaticHolders = new();
    private readonly Dictionary<IPropertySymbol, string> _staticGetters = new(SymbolEqualityComparer.Default);
    private readonly Queue<IPropertySymbol> _pendingStaticGetters = new();

    private static bool IsUserStaticState(ISymbol? symbol) => symbol switch
    {
        IFieldSymbol { IsStatic: true, HasConstantValue: false, IsImplicitlyDeclared: false } field =>
            IsStaticStateContainer(field.ContainingType)
            && field.DeclaringSyntaxReferences.Length == 1,
        IPropertySymbol { IsStatic: true, IsIndexer: false } property =>
            IsStaticStateContainer(property.ContainingType)
            && property.DeclaringSyntaxReferences is [{ } reference]
            && reference.GetSyntax() is PropertyDeclarationSyntax,
        _ => false
    };

    private static bool IsStaticStateContainer(INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Class
        && type.DeclaringSyntaxReferences is [{ } reference]
        && reference.GetSyntax() is TypeDeclarationSyntax
        && !IsGeneratedInstanceType(type);

    private string StaticMemberAccess(ISymbol member, SyntaxNode source)
    {
        if (member is IPropertySymbol property
            && property.DeclaringSyntaxReferences[0].GetSyntax() is PropertyDeclarationSyntax declaration
            && !IsAutoProperty(declaration))
        {
            if (IsAssignmentTarget(source))
                throw new NotSupportedException(
                    $"WRK110: Static property '{property.ToDisplayString()}' has a custom accessor and cannot be assigned.");
            return $"{StaticGetter(property)}()";
        }
        return $"{StaticHolder(member.ContainingType)}(){StaticMemberKey(member)}";
    }

    private static bool IsAssignmentTarget(SyntaxNode node)
    {
        var target = node.Parent is MemberAccessExpressionSyntax access && access.Name == node ? access : node;
        return target.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == target,
            PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax => true,
            ArgumentSyntax argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None),
            _ => false
        };
    }

    private static string StaticMemberKey(ISymbol member) =>
        member.Name != "__proto__" && member.Name.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '$')
            && !char.IsAsciiDigit(member.Name[0])
            ? $".{member.Name}"
            : $"[{JsonSerializer.Serialize(member.Name)}]";

    private string StaticHolder(INamedTypeSymbol type)
    {
        type = (INamedTypeSymbol)ResolveType(type);
        if (_staticHolders.TryGetValue(type, out var existing))
            return existing;
        var name = _names.Get($"static-holder:{type.ToDisplayString()}", $"statics${type.Name}");
        _staticHolders.Add(type, name);
        _pendingStaticHolders.Enqueue(type);
        return name;
    }

    private string StaticGetter(IPropertySymbol property)
    {
        var owner = (INamedTypeSymbol)ResolveType(property.ContainingType);
        property = owner.GetMembers(property.Name).OfType<IPropertySymbol>().Single();
        if (_staticGetters.TryGetValue(property, out var existing))
            return existing;
        var name = _names.Get($"static-getter:{property.ToDisplayString()}", $"cs${property.ContainingType.Name}$get_{property.Name}");
        _staticGetters.Add(property, name);
        _pendingStaticGetters.Enqueue(property);
        return name;
    }

    private bool EmitPendingStaticState()
    {
        var emitted = false;
        while (_pendingStaticHolders.Count != 0 || _pendingStaticGetters.Count != 0)
        {
            emitted = true;
            if (_pendingStaticHolders.TryDequeue(out var type))
                EmitStaticHolder(type);
            else if (_pendingStaticGetters.TryDequeue(out var property))
                EmitStaticGetter(property);
        }
        return emitted;
    }

    private void EmitStaticHolder(INamedTypeSymbol type)
    {
        using var specialization = SpecializationScope(type);
        var declaration = (TypeDeclarationSyntax)type.DeclaringSyntaxReferences[0].GetSyntax();
        _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
        var function = _staticHolders[type];
        var storage = _names.Get($"static-storage:{type.ToDisplayString()}", $"static${type.Name}");
        var failure = _names.Get($"static-failure:{type.ToDisplayString()}", $"staticFailure${type.Name}");
        var members = new List<(ISymbol Symbol, ITypeSymbol Type, ExpressionSyntax? Initializer, SyntaxNode Source)>();
        foreach (var member in declaration.Members)
        {
            if (member is FieldDeclarationSyntax field && field.Modifiers.Any(SyntaxKind.StaticKeyword)
                && !field.Modifiers.Any(SyntaxKind.ConstKeyword))
                foreach (var variable in field.Declaration.Variables)
                {
                    var symbol = (IFieldSymbol)_model.GetDeclaredSymbol(variable)!;
                    members.Add((symbol, symbol.Type, variable.Initializer?.Value, variable));
                }
            else if (member is PropertyDeclarationSyntax property && property.Modifiers.Any(SyntaxKind.StaticKeyword)
                     && IsAutoProperty(property))
            {
                var symbol = (IPropertySymbol)_model.GetDeclaredSymbol(property)!;
                members.Add((symbol, symbol.Type, property.Initializer?.Value, property));
            }
        }
        _output.Append("var ").Append(storage).AppendLine(";");
        _output.Append("var ").Append(failure).AppendLine(";");
        _output.Append("function ").Append(function).AppendLine("() {");
        _output.Append("  if (").Append(failure).Append(" !== undefined) throw ").Append(failure).AppendLine(";");
        _output.Append("  if (").Append(storage).Append(" !== undefined) return ").Append(storage).AppendLine(";");
        _output.Append("  ").Append(storage).AppendLine(" = Object.create(null);");
        // Publish every default before evaluating any initializer, including cyclic reads.
        foreach (var member in members)
            _output.Append("  ").Append(storage).Append(StaticMemberKey(member.Symbol)).Append(" = ")
                .Append(DefaultFieldValue(member.Type, member.Source)).AppendLine(";");
        _output.AppendLine("  try {");
        foreach (var member in members.Where(member => member.Initializer is not null))
            _output.Append("    ").Append(storage).Append(StaticMemberKey(member.Symbol)).Append(" = ")
                .Append(Expression(member.Initializer!)).AppendLine(";");
        var staticConstructor = declaration.Members.OfType<ConstructorDeclarationSyntax>()
            .SingleOrDefault(constructor => constructor.Modifiers.Any(SyntaxKind.StaticKeyword));
        if (staticConstructor?.ExpressionBody is not null)
            _output.Append("    ").Append(Expression(staticConstructor.ExpressionBody.Expression)).AppendLine(";");
        else if (staticConstructor is not null)
        {
            _output.AppendLine("    (() => {");
            EmitStatements(staticConstructor.Body?.Statements ?? [], 3);
            _output.AppendLine("    })();");
        }
        _output.AppendLine("  } catch (cause) {");
        _output.Append("    ").Append(failure).Append(" = new Error(")
            .Append(JsonSerializer.Serialize($"The type initializer for '{type.ToDisplayString()}' threw an exception."))
            .AppendLine(", { cause });");
        _output.Append("    throw ").Append(failure).AppendLine(";");
        _output.AppendLine("  }");
        _output.Append("  return ").Append(storage).AppendLine(";");
        _output.AppendLine("}").AppendLine();
    }

    private void EmitStaticGetter(IPropertySymbol property)
    {
        using var specialization = SpecializationScope(property.ContainingType);
        var declaration = (PropertyDeclarationSyntax)property.DeclaringSyntaxReferences[0].GetSyntax();
        _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
        if (declaration.AccessorList?.Accessors.Any(accessor => !accessor.IsKind(SyntaxKind.GetAccessorDeclaration)) == true)
            throw new NotSupportedException(
                $"WRK110: Static property '{property.ToDisplayString()}' must be an auto-property or a getter-only property.");
        _output.Append("function ").Append(_staticGetters[property]).AppendLine("() {");
        EmitExplicitStaticInitialization(property.ContainingType, 1);
        var getter = declaration.AccessorList?.Accessors.Single();
        var expression = declaration.ExpressionBody?.Expression ?? getter?.ExpressionBody?.Expression;
        EmitWithExpressionScope(1, () =>
        {
            if (expression is not null)
                _output.Append("  return ").Append(Expression(expression)).AppendLine(";");
            else
                EmitStatements(getter?.Body?.Statements ?? [], 1);
        });
        _output.AppendLine("}").AppendLine();
    }

    private bool HasExplicitStaticConstructor(INamedTypeSymbol type) =>
        IsStaticStateContainer(type) && type.StaticConstructors.Any(constructor => !constructor.IsImplicitlyDeclared);

    private void EmitExplicitStaticInitialization(INamedTypeSymbol type, int depth)
    {
        if (HasExplicitStaticConstructor(type))
            _output.Append(new string(' ', depth * 2)).Append(StaticHolder(type)).AppendLine("();");
    }
}
