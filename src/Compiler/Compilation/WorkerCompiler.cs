using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class WorkerCompiler
{
    public static int Run(string[] args) => CompilerCommand.Run(args);

    internal static string Compile(IEnumerable<SyntaxTree> trees, IEnumerable<string>? additionalReferences = null) =>
        Compile(CreateCompilation(trees, additionalReferences));

    internal static string Compile(CSharpCompilation compilation)
    {
        CompilationGuard.ThrowIfInvalid(compilation);
        return JavaScriptEmitter.Emit(compilation);
    }

    internal static CSharpCompilation CreateCompilation(IEnumerable<SyntaxTree> trees, IEnumerable<string>? additionalReferences = null)
    {
        var inputTrees = trees.ToArray();
        var parseOptions = inputTrees.OfType<CSharpSyntaxTree>().FirstOrDefault()?.Options
            ?? new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTrees = inputTrees.Prepend(CSharpSyntaxTree.ParseText(GlobalUsings, parseOptions, "Workers.Compiler.GlobalUsings.g.cs"));
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Concat(additionalReferences ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
        return CSharpCompilation.Create(
            "Workers.UserCode",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private const string GlobalUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.Linq;
        global using System.Threading.Tasks;
        """;
}
