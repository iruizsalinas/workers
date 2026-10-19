using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private bool _usesHierarchyConstruction;
    private string HierarchySkipToken => _names.Get("hierarchy-constructor-skip", "skipConstruction");

    private static bool UsesUserHierarchy(INamedTypeSymbol type) => !type.IsSealed || IsUserException(type)
        || type.IsRecord && RecordEqualityMembers(type).Any(member => member.Type is INamedTypeSymbol { IsTupleType: true })
        || type.BaseType is { } parent && IsUserInstanceType(parent);

    private string ConstructorBodyName(IMethodSymbol constructor) => _names.Get(
        $"constructor-body:{constructor.OriginalDefinition.ToDisplayString()}", $"ctor${constructor.ContainingType.Name}${ConstructorSelector(constructor)}");

    private void EmitConstructorDispatch(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        _usesHierarchyConstruction = true;
        _output.AppendLine("  constructor($constructor, ...$arguments) {");
        if (type.BaseType is { } parent && IsUserInstanceType(parent))
            _output.Append("    super(").Append(HierarchySkipToken).AppendLine(");");
        else if (IsUserException(type))
            _output.AppendLine("    super();");
        _output.Append("    if ($constructor === ").Append(HierarchySkipToken).AppendLine(") return;");
        EmitExplicitStaticInitialization(type, 2);
        EmitHierarchyDefaults(type);
        _output.AppendLine("    switch ($constructor) {");
        foreach (var constructor in OrdinaryConstructors(type))
            _output.Append("      case ").Append(ConstructorSelector(constructor)).Append(": ")
                .Append(_userTypes[type]).Append(".prototype.").Append(ConstructorBodyName(constructor))
                .AppendLine(".call(this, ...$arguments); break;");
        _output.AppendLine("      default: throw new TypeError(\"Unknown C# constructor.\");");
        _output.AppendLine("    }");
        _output.AppendLine("  }");

        foreach (var constructor in OrdinaryConstructors(type))
        {
            var syntax = constructor.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as ConstructorDeclarationSyntax;
            var positional = syntax is null && declaration is RecordDeclarationSyntax ? PrimaryParameters(declaration) : default;
            var parameters = syntax?.ParameterList.Parameters ?? PrimaryParameters(declaration);
            _output.Append("  ").Append(ConstructorBodyName(constructor)).Append('(')
                .Append(string.Join(", ", parameters.Select(ParameterDeclaration))).AppendLine(") {");
            EmitWithExpressionScope(2, () =>
            {
                EmitExplicitStaticInitialization(type, 2);
                if (syntax is null) EmitPrimaryParameterInitialization(declaration);
                if (syntax?.Initializer is not { } initializer || !initializer.IsKind(SyntaxKind.ThisConstructorInitializer))
                    EmitUserInstanceInitializers(type, declaration, positional);
                if (syntax?.Initializer is { } call)
                {
                    var target = ResolveMethod((IMethodSymbol)_model.GetSymbolInfo(call).Symbol!);
                    if (IsUserInstanceType(target.ContainingType))
                        EmitConstructorBodyCall(target, call, call.ArgumentList.Arguments.ToArray());
                }
                else if (type.BaseType is { } baseType && IsUserInstanceType(baseType))
                {
                    var primaryBase = declaration.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>().SingleOrDefault();
                    var target = primaryBase is null
                        ? baseType.InstanceConstructors.Single(candidate => candidate.Parameters.Length == 0)
                        : ResolveMethod((IMethodSymbol)_model.GetSymbolInfo(primaryBase).Symbol!);
                    EmitConstructorBodyCall(target, primaryBase ?? (SyntaxNode)declaration,
                        primaryBase?.ArgumentList.Arguments.ToArray() ?? []);
                }
                if (IsUserException(type) && syntax?.Initializer?.IsKind(SyntaxKind.ThisConstructorInitializer) != true)
                {
                    var primaryBase = syntax is null
                        ? declaration.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>().SingleOrDefault()
                        : null;
                    EmitExceptionInitialization(type, (SyntaxNode?)syntax?.Initializer ?? primaryBase,
                        syntax?.Initializer?.ArgumentList ?? primaryBase?.ArgumentList);
                }
                EmitUserConstructorStatements(syntax);
            });
            _output.AppendLine("  }");
        }
    }

    private void EmitConstructorBodyCall(IMethodSymbol target, SyntaxNode source, ArgumentSyntax[] arguments)
    {
        var owner = QueueUserType(target.ContainingType, source);
        _output.Append("    ").Append(PositionalObjectCreation(source, target, arguments,
            values => $"{owner}.prototype.{ConstructorBodyName(target)}.call(this{(values.Count == 0 ? "" : ", " + string.Join(", ", values))})")).AppendLine(";");
    }

    private void EmitHierarchyDefaults(INamedTypeSymbol type, string receiver = "this")
    {
        if (type.BaseType is { } parent && IsUserInstanceType(parent)) EmitHierarchyDefaults(parent, receiver);
        EmitPrimaryParameterInitialization((TypeDeclarationSyntax)type.DeclaringSyntaxReferences[0].GetSyntax(), defaults: true, receiver);
        foreach (var field in type.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic))
        {
            if (field.IsImplicitlyDeclared && field.AssociatedSymbol is null) continue;
            _output.Append("    ").Append(BackingStorage(receiver, field)).Append(" = ")
                .Append(DefaultFieldValue(field.Type, type.DeclaringSyntaxReferences[0].GetSyntax())).AppendLine(";");
        }
    }
}
