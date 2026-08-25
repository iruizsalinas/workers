using Microsoft.CodeAnalysis.CSharp;

namespace Workers.Compiler.Tests;

internal static class TestCompiler
{
    public static string Compile(string source) => Compile([source]);

    public static string Compile(params string[] sources) =>
        global::WorkerCompiler.Compile(
            sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            [typeof(global::Workers.Response).Assembly.Location]);
}
