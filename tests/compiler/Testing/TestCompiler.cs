using Microsoft.CodeAnalysis.CSharp;

namespace Workers.Compiler.Tests;

internal static class TestCompiler
{
    public static string Compile(string source) => Compile([source]);

    public static string Compile(params string[] sources) =>
        global::WorkerCompiler.Compile(
            sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            [typeof(global::Workers.Response).Assembly.Location]);

    public static string CompileAspNet(string source) =>
        global::WorkerCompiler.Compile(
            [CSharpSyntaxTree.ParseText(source)],
            Directory.EnumerateFiles(AspNetSharedFramework(), "*.dll")
                .Where(path => !Path.GetFileName(path).StartsWith("aspnetcorev2_", StringComparison.OrdinalIgnoreCase))
            .Append(typeof(global::Workers.Response).Assembly.Location));

    private static string AspNetSharedFramework()
    {
        var directory = new DirectoryInfo(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory());
        while (directory.Parent is not null
               && !Directory.Exists(Path.Combine(directory.FullName, "shared", "Microsoft.AspNetCore.App")))
            directory = directory.Parent;
        var dotnet = directory.FullName;
        return Directory.EnumerateDirectories(Path.Combine(dotnet, "shared", "Microsoft.AspNetCore.App"))
            .OrderByDescending(path => Version.Parse(Path.GetFileName(path)))
            .First();
    }
}
