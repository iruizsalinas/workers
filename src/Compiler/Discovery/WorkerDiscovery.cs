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
        var distinctClasses = DistinctTypes(compilation, classes);
        var misplacedDefault = distinctClasses.FirstOrDefault(declaration =>
            HasAttribute(compilation, declaration, "Workers.DefaultExportAttribute")
            && !DerivesFrom(compilation, declaration, "Workers.WorkerEntrypoint"));
        if (misplacedDefault is not null)
            throw new NotSupportedException(
                $"WRK116: [DefaultExport] applies only to a WorkerEntrypoint class, not '{misplacedDefault.Identifier.ValueText}'.");

        var events = EventTypes
            .Select(type => FindEvent(compilation, methods, type))
            .OfType<WorkerEvent>()
            .ToArray();
        var durableObjects = distinctClasses.Where(declaration => DerivesFrom(compilation, declaration, "Workers.DurableObject")).ToArray();
        var workerEntrypoints = distinctClasses.Where(declaration => DerivesFrom(compilation, declaration, "Workers.WorkerEntrypoint")).ToArray();
        var htmlHandlers = distinctClasses.Where(declaration => IsHtmlHandler(compilation, declaration)).ToArray();

        var defaultExports = events.Length == 0 ? 0 : 1;
        defaultExports += workerEntrypoints.Count(declaration => HasAttribute(compilation, declaration, "Workers.DefaultExportAttribute"));
        if (defaultExports > 1)
            throw new NotSupportedException("WRK112: Multiple default Worker exports are not supported.");

        var duplicateExport = durableObjects.Concat(workerEntrypoints)
            .Where(declaration => !HasAttribute(compilation, declaration, "Workers.DefaultExportAttribute"))
            .GroupBy(declaration => declaration.Identifier.ValueText, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Skip(1).Any());
        if (duplicateExport is not null)
            throw new NotSupportedException($"WRK113: Multiple Worker classes export the name '{duplicateExport.Key}'.");

        if (events.Length == 0 && durableObjects.Length == 0 && workerEntrypoints.Length == 0)
            throw new InvalidOperationException("WRK001: No Worker event entrypoint was found.");

        return new WorkerProgram(events, durableObjects, workerEntrypoints, htmlHandlers);
    }

    private static ClassDeclarationSyntax[] DistinctTypes(
        CSharpCompilation compilation,
        IEnumerable<ClassDeclarationSyntax> declarations)
    {
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        return declarations.Where(declaration =>
        {
            var symbol = compilation.GetSemanticModel(declaration.SyntaxTree).GetDeclaredSymbol(declaration);
            return symbol is not null && seen.Add(symbol);
        }).ToArray();
    }

    private static WorkerEvent? FindEvent(
        CSharpCompilation compilation,
        IEnumerable<MethodDeclarationSyntax> methods,
        (string Attribute, string Event) type)
    {
        var matches = methods.Where(candidate => HasAttribute(
            compilation,
            candidate,
            $"Workers.{type.Attribute}Attribute")).Take(2).ToArray();
        if (matches.Length > 1)
            throw new NotSupportedException($"WRK111: Multiple '{type.Event}' event entrypoints are not supported.");
        if (matches.Length == 0) return null;
        if (!HasValidEventSignature(compilation, matches[0], type.Event))
            throw new NotSupportedException($"WRK114: The '{type.Event}' event entrypoint has an invalid signature.");
        return new WorkerEvent(type.Event, matches[0]);
    }

    private static bool HasAttribute(
        CSharpCompilation compilation,
        SyntaxNode declaration,
        string attribute) =>
        compilation.GetSemanticModel(declaration.SyntaxTree)
            .GetDeclaredSymbol(declaration)?
            .GetAttributes()
            .Any(candidate => candidate.AttributeClass?.ToDisplayString() == attribute) == true;

    private static bool IsHtmlHandler(CSharpCompilation compilation, ClassDeclarationSyntax declaration) =>
        DerivesFrom(compilation, declaration, "Workers.HtmlElementHandler")
        || DerivesFrom(compilation, declaration, "Workers.HtmlDocumentHandler");

    private static bool DerivesFrom(CSharpCompilation compilation, ClassDeclarationSyntax declaration, string baseType)
    {
        var type = compilation.GetSemanticModel(declaration.SyntaxTree).GetDeclaredSymbol(declaration)?.BaseType;
        for (; type is not null; type = type.BaseType)
            if (type.ToDisplayString() == baseType)
                return true;
        return false;
    }

    private static bool HasValidEventSignature(
        CSharpCompilation compilation,
        MethodDeclarationSyntax declaration,
        string eventName)
    {
        var method = compilation.GetSemanticModel(declaration.SyntaxTree).GetDeclaredSymbol(declaration)!;
        if (!method.IsStatic || method.IsGenericMethod || method.Parameters.Length > 3
            || method.Parameters.Any(parameter => parameter.RefKind != RefKind.None))
            return false;

        var expected = eventName switch
        {
            "fetch" => new[] { "Workers.Request", "Workers.Env", "Workers.ExecutionContext" },
            "scheduled" => new[] { "Workers.ScheduledController", "Workers.Env", "Workers.ExecutionContext" },
            "queue" => new[] { "Workers.MessageBatch<T>", "Workers.Env", "Workers.ExecutionContext" },
            "email" => new[] { "Workers.ForwardableEmailMessage", "Workers.Env", "Workers.ExecutionContext" },
            "tail" => new[] { "System.Collections.Generic.IReadOnlyList<Workers.TraceItem>", "Workers.Env", "Workers.ExecutionContext" },
            _ => throw new InvalidOperationException($"Unknown Worker event '{eventName}'.")
        };
        if (method.Parameters.Select(parameter => parameter.Type.OriginalDefinition.ToDisplayString() is var definition
                && definition.EndsWith("<T>", StringComparison.Ordinal) && definition.StartsWith("Workers.", StringComparison.Ordinal)
                    ? definition : parameter.Type.ToDisplayString())
            .Where((type, index) => type != expected[index]).Any())
            return false;

        var returnType = method.ReturnType.OriginalDefinition.ToDisplayString();
        return eventName == "fetch"
            ? method.ReturnType.ToDisplayString() == "Workers.Response"
              || returnType is "System.Threading.Tasks.Task<TResult>" or "System.Threading.Tasks.ValueTask<TResult>"
                 && ((INamedTypeSymbol)method.ReturnType).TypeArguments[0].ToDisplayString() == "Workers.Response"
            : method.ReturnsVoid || returnType is "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask";
    }
}
