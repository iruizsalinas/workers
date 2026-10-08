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
        type.TypeKind == TypeKind.Class && type.Arity == 0 && type.ContainingType is null
        && type.DeclaringSyntaxReferences is [{ } reference]
        && reference.GetSyntax() is ClassDeclarationSyntax
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
        type = type.OriginalDefinition;
        if (_staticHolders.TryGetValue(type, out var existing))
            return existing;
        var name = _names.Get($"static-holder:{type.ToDisplayString()}", $"statics${type.Name}");
        _staticHolders.Add(type, name);
        _pendingStaticHolders.Enqueue(type);
        return name;
    }

    private string StaticGetter(IPropertySymbol property)
    {
        property = property.OriginalDefinition;
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
        var declaration = (ClassDeclarationSyntax)type.DeclaringSyntaxReferences[0].GetSyntax();
        _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
        var function = _staticHolders[type];
        var storage = _names.Get($"static-storage:{type.ToDisplayString()}", $"static${type.Name}");
        _output.Append("var ").Append(storage).AppendLine(";");
        _output.Append("function ").Append(function).AppendLine("() {");
        _output.Append("  if (").Append(storage).Append(" !== undefined) return ").Append(storage).AppendLine(";");
        _output.Append("  ").Append(storage).AppendLine(" = Object.create(null);");
        foreach (var member in declaration.Members)
        {
            if (member is FieldDeclarationSyntax field && field.Modifiers.Any(SyntaxKind.StaticKeyword)
                && !field.Modifiers.Any(SyntaxKind.ConstKeyword))
                foreach (var variable in field.Declaration.Variables)
                {
                    var symbol = (IFieldSymbol)_model.GetDeclaredSymbol(variable)!;
                    _output.Append("  ").Append(storage).Append(StaticMemberKey(symbol)).Append(" = ")
                        .Append(variable.Initializer is null
                            ? DefaultFieldValue(symbol.Type, variable)
                            : Expression(variable.Initializer.Value)).AppendLine(";");
                }
            else if (member is PropertyDeclarationSyntax property && property.Modifiers.Any(SyntaxKind.StaticKeyword)
                     && IsAutoProperty(property))
            {
                var symbol = (IPropertySymbol)_model.GetDeclaredSymbol(property)!;
                _output.Append("  ").Append(storage).Append(StaticMemberKey(symbol)).Append(" = ")
                    .Append(property.Initializer is null
                        ? DefaultFieldValue(symbol.Type, property)
                        : Expression(property.Initializer.Value)).AppendLine(";");
            }
        }
        var staticConstructor = declaration.Members.OfType<ConstructorDeclarationSyntax>()
            .SingleOrDefault(constructor => constructor.Modifiers.Any(SyntaxKind.StaticKeyword));
        if (staticConstructor?.ExpressionBody is not null)
            _output.Append("  ").Append(Expression(staticConstructor.ExpressionBody.Expression)).AppendLine(";");
        else
            foreach (var statement in staticConstructor?.Body?.Statements ?? [])
                EmitStatement(statement, 1);
        _output.Append("  return ").Append(storage).AppendLine(";");
        _output.AppendLine("}").AppendLine();
    }

    private void EmitStaticGetter(IPropertySymbol property)
    {
        var declaration = (PropertyDeclarationSyntax)property.DeclaringSyntaxReferences[0].GetSyntax();
        _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
        if (declaration.AccessorList?.Accessors.Any(accessor => !accessor.IsKind(SyntaxKind.GetAccessorDeclaration)) == true)
            throw new NotSupportedException(
                $"WRK110: Static property '{property.ToDisplayString()}' must be an auto-property or a getter-only property.");
        _output.Append("function ").Append(_staticGetters[property]).AppendLine("() {");
        var getter = declaration.AccessorList?.Accessors.Single();
        var expression = declaration.ExpressionBody?.Expression ?? getter?.ExpressionBody?.Expression;
        if (expression is not null)
            _output.Append("  return ").Append(Expression(expression)).AppendLine(";");
        else
            foreach (var statement in getter?.Body?.Statements ?? [])
                EmitStatement(statement, 1);
        _output.AppendLine("}").AppendLine();
    }
}
