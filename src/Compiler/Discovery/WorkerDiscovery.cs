using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class WorkerDiscovery
{
    private static readonly (string Attribute, string Event)[] EventTypes =
    [
        ("Fetch", "fetch"),
        ("Scheduled", "scheduled"),
        ("Queue", "queue"),
        ("Email", "email"),
        ("Tail", "tail")
    ];

    public static WorkerProgram Discover(CSharpCompilation compilation)
    {
        var methods = compilation.SyntaxTrees
            .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .ToArray();
        var classes = compilation.SyntaxTrees
            .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            .ToArray();

        var events = EventTypes
            .Select(type => FindEvent(compilation, methods, type))
            .OfType<WorkerEvent>()
            .ToArray();
        var durableObjects = classes.Where(declaration => HasAttribute(
            compilation,
            declaration,
            "Workers.DurableObjectAttribute")).ToArray();
        var htmlHandlers = classes.Where(declaration => IsHtmlHandler(compilation, declaration)).ToArray();

        if (events.Length == 0 && durableObjects.Length == 0)
            throw new InvalidOperationException("WRK001: No Worker event entrypoint was found.");

        return new WorkerProgram(events, durableObjects, htmlHandlers);
    }

    private static WorkerEvent? FindEvent(
        CSharpCompilation compilation,
        IEnumerable<MethodDeclarationSyntax> methods,
        (string Attribute, string Event) type)
    {
        var method = methods.SingleOrDefault(candidate => HasAttribute(
            compilation,
            candidate,
            $"Workers.{type.Attribute}Attribute"));
        return method is null ? null : new WorkerEvent(type.Event, method);
    }

    private static bool HasAttribute(
        CSharpCompilation compilation,
        SyntaxNode declaration,
        string attribute) =>
        compilation.GetSemanticModel(declaration.SyntaxTree)
            .GetDeclaredSymbol(declaration)?
            .GetAttributes()
            .Any(candidate => candidate.AttributeClass?.ToDisplayString() == attribute) == true;

    private static bool IsHtmlHandler(CSharpCompilation compilation, ClassDeclarationSyntax declaration)
    {
        var type = compilation.GetSemanticModel(declaration.SyntaxTree).GetDeclaredSymbol(declaration)?.BaseType;
        return type?.ToDisplayString() is "Workers.HtmlElementHandler" or "Workers.HtmlDocumentHandler";
    }
}
