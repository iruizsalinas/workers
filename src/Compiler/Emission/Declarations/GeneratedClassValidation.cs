using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private static bool IsGeneratedInstanceType(INamedTypeSymbol type) =>
        type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "Workers.DurableObjectAttribute")
        || type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "Workers.WorkerEntrypointAttribute")
        || type.BaseType?.ToDisplayString() is "Workers.HtmlElementHandler" or "Workers.HtmlDocumentHandler";

    private static bool IsIterator(MethodDeclarationSyntax method) =>
        method.DescendantNodes(node => node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax)
            .OfType<YieldStatementSyntax>()
            .Any();

    private string ParameterDeclaration(ParameterSyntax parameter) =>
        parameter.Default is null
            ? ParameterName(parameter)
            : $"{ParameterName(parameter)} = {Expression(parameter.Default.Value)}";

    private static void ValidateGeneratedClass(ClassDeclarationSyntax declaration, SemanticModel model, string typeName)
    {
        var type = model.GetDeclaredSymbol(declaration)!;
        var expectedBase = type.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == "Workers.DurableObjectAttribute")
                ? "object"
                : type.GetAttributes().Any(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == "Workers.WorkerEntrypointAttribute")
                    ? "Workers.WorkerEntrypoint"
                    : SupportedHtmlHandlerBase(type);
        var invalidType = type.IsAbstract
            || type.Arity != 0
            || type.ContainingType is not null
            || type.DeclaringSyntaxReferences.Length != 1
            || type.BaseType?.ToDisplayString() != expectedBase;
        if (invalidType)
            throw new NotSupportedException(
                $"WRK116: Generated class '{typeName}' must be a non-abstract, non-generic, non-nested, non-partial type with its supported base class.");

        var unsupported = declaration.Members.FirstOrDefault(member => member switch
        {
            FieldDeclarationSyntax field => field.Declaration.Variables.Any(variable =>
                model.GetDeclaredSymbol(variable) is not IFieldSymbol { IsStatic: false, IsConst: false }),
            ConstructorDeclarationSyntax constructor => !IsSupportedGeneratedConstructor(model.GetDeclaredSymbol(constructor)),
            MethodDeclarationSyntax method => !IsSupportedGeneratedMethod(model.GetDeclaredSymbol(method)),
            _ => true
        });
        if (unsupported is not null)
            throw new NotSupportedException($"WRK116: Generated class '{typeName}' contains an unsupported member: {unsupported}");
    }

    private static string? SupportedHtmlHandlerBase(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (current.ToDisplayString() is "Workers.HtmlElementHandler" or "Workers.HtmlDocumentHandler")
                return current.ToDisplayString();
        return null;
    }

    private static bool IsSupportedGeneratedMethod(IMethodSymbol? method) => method is
    {
        MethodKind: MethodKind.Ordinary,
        IsStatic: false,
        IsAbstract: false,
        IsExtern: false,
        ReturnsByRef: false,
        ReturnsByRefReadonly: false,
        PartialDefinitionPart: null,
        PartialImplementationPart: null
    } && method.Parameters.All(parameter => parameter.RefKind == RefKind.None && !parameter.IsParams);

    private static bool IsSupportedGeneratedConstructor(IMethodSymbol? constructor) => constructor is
    {
        MethodKind: MethodKind.Constructor,
        IsStatic: false,
        IsExtern: false
    } && constructor.Parameters.All(parameter => parameter.RefKind == RefKind.None && !parameter.IsParams);

    private static string GeneratedInstanceMethodName(IMethodSymbol method)
    {
        var name = method.Name switch
        {
            "FetchAsync" => "fetch",
            "AlarmAsync" => "alarm",
            "WebSocketMessageAsync" => "webSocketMessage",
            "WebSocketCloseAsync" => "webSocketClose",
            "WebSocketErrorAsync" => "webSocketError",
            var value => LowerNativeMethodName(value)
        };
        if (name == "constructor")
            throw new NotSupportedException(
                $"WRK118: Method '{method.ToDisplayString()}' compiles to the reserved JavaScript class method name 'constructor'.");
        return method.DeclaredAccessibility == Accessibility.Public ? name : "#" + name;
    }

    private static void ThrowIfDuplicateGeneratedMethods(
        ClassDeclarationSyntax declaration,
        SemanticModel model,
        string typeName)
    {
        var duplicate = declaration.Members.OfType<MethodDeclarationSyntax>()
            .Select(method => GeneratedInstanceMethodName(model.GetDeclaredSymbol(method)!))
            .GroupBy(name => name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Skip(1).Any());
        if (duplicate is not null)
            throw new NotSupportedException($"WRK115: Multiple methods on '{typeName}' compile to '{duplicate.Key.TrimStart('#')}'.");
    }

}
