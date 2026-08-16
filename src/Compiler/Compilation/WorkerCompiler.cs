using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class WorkerCompiler
{
    public static int Run(string[] args) => CompilerCommand.Run(args);

    internal static string Compile(
        IEnumerable<SyntaxTree> trees,
        IEnumerable<string>? additionalReferences = null,
        bool useTrustedPlatformAssemblies = true)
    {
        var inputTrees = trees.ToArray();
        var parseOptions = inputTrees.OfType<CSharpSyntaxTree>().FirstOrDefault()?.Options
            ?? new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTrees = inputTrees.Prepend(CSharpSyntaxTree.ParseText(GlobalUsings, parseOptions, "Workers.Compiler.GlobalUsings.g.cs"));
        var platformReferences = useTrustedPlatformAssemblies
            ? ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            : [];
        var references = platformReferences.Concat(additionalReferences ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
        var hasTopLevelStatements = inputTrees.Any(tree =>
            tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.GlobalStatementSyntax>().Any());
        var compilation = CSharpCompilation.Create(
            "Workers.UserCode",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(hasTopLevelStatements
                ? OutputKind.ConsoleApplication
                : OutputKind.DynamicallyLinkedLibrary));

        CompilationGuard.ThrowIfInvalid(compilation);
        if (AspNetApplicationDiscovery.TryDiscover(compilation, out var application))
            return AspNetJavaScriptEmitter.Emit(application);
        return JavaScriptEmitter.Emit(compilation);
    }

    private const string GlobalUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Net.Http;
        global using System.Threading;
        global using System.Threading.Tasks;
        """;
}
