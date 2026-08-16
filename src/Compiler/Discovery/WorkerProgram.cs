using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed record WorkerProgram(
    IReadOnlyList<WorkerEvent> Events,
    IReadOnlyList<ClassDeclarationSyntax> DurableObjects,
    IReadOnlyList<ClassDeclarationSyntax> WorkerEntrypoints,
    IReadOnlyList<ClassDeclarationSyntax> HtmlHandlers);

internal sealed record WorkerEvent(string Name, MethodDeclarationSyntax Method);
