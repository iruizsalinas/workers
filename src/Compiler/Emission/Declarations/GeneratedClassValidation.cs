using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private static bool IsGeneratedInstanceType(INamedTypeSymbol type) =>
        type.BaseType?.ToDisplayString() is "Workers.DurableObject" or "Workers.WorkerEntrypoint"
            or "Workers.HtmlElementHandler" or "Workers.HtmlDocumentHandler";

    // Names the runtime calls as handlers. A Durable Object or entrypoint receives them only by overriding
    // the base class handler, so its signature is checked; other methods must not compile to these names.
    // Methods the runtime calls on each base class; a same-named RPC method would be invoked as a handler.
    private static readonly HashSet<string> DurableObjectHandlerNames =
    [
        "fetch", "connect", "alarm", "webSocketMessage", "webSocketClose", "webSocketError"
    ];

    private static readonly HashSet<string> EntrypointHandlerNames =
    [
        "fetch", "connect", "scheduled", "queue", "email", "tail", "trace", "test"
    ];

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
        var expectedBase = type.BaseType?.ToDisplayString() is "Workers.DurableObject" or "Workers.WorkerEntrypoint"
            ? type.BaseType.ToDisplayString()
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
                model.GetDeclaredSymbol(variable) is not IFieldSymbol fieldSymbol
                || fieldSymbol is { IsConst: false, IsStatic: true }),
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
        var name = LowerNativeMethodName(method.Name);
        if (name == "constructor")
            throw new NotSupportedException(
                $"WRK118: Method '{method.ToDisplayString()}' compiles to the reserved JavaScript class method name 'constructor'.");
        var handlers = method.ContainingType.BaseType?.ToDisplayString() switch
        {
            "Workers.DurableObject" => DurableObjectHandlerNames,
            "Workers.WorkerEntrypoint" => EntrypointHandlerNames,
            _ => null
        };
        if (!method.IsOverride && handlers?.Contains(name) == true)
            throw new NotSupportedException(
                $"WRK118: Method '{method.ToDisplayString()}' compiles to the runtime handler name '{name}'; override the base class handler instead.");
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
