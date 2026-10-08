using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class WorkerCompiler
{
    public static int Run(string[] args) => CompilerCommand.Run(args);

    internal static string Compile(IEnumerable<SyntaxTree> trees, IEnumerable<string>? additionalReferences = null) =>
        CompileWorker(trees, additionalReferences).Source;

    internal static EmittedWorker CompileWorker(IEnumerable<SyntaxTree> trees, IEnumerable<string>? additionalReferences = null)
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
        var compilation = CSharpCompilation.Create(
            "Workers.UserCode",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation = GeneratedRegexImplementations.Add(compilation);

        CompilationGuard.ThrowIfInvalid(compilation);
        return JavaScriptEmitter.EmitWorker(compilation);
    }

    private const string GlobalUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Threading;
        global using System.Threading.Tasks;
        """;
}
