using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

// Source generators do not run when a Worker is compiled, so [GeneratedRegex] partial methods and properties
// would have no implementation. This adds one that constructs the Regex from the attribute's pattern and options;
// the emitter compiles each use of the member to the translated pattern directly. The implementation is inserted
// into the same type declaration, before its closing brace and without line breaks, so the type keeps a single
// declaration and diagnostics keep their line numbers.
internal static class GeneratedRegexImplementations
{
    private const string AttributeName = "System.Text.RegularExpressions.GeneratedRegexAttribute";

    public static CSharpCompilation Add(CSharpCompilation compilation)
    {
        foreach (var tree in compilation.SyntaxTrees.ToArray())
        {
            var model = compilation.GetSemanticModel(tree);
            var changes = new List<TextChange>();
            foreach (var type in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var implementations = type.Members
                    .Where(member => member is MethodDeclarationSyntax or PropertyDeclarationSyntax
                                     && member.Modifiers.Any(SyntaxKind.PartialKeyword)
                                     && member.AttributeLists.Count != 0)
                    .Select(member => Implementation(member, model.GetDeclaredSymbol(member)))
                    .OfType<string>()
                    .ToArray();
                if (implementations.Length != 0 && !type.CloseBraceToken.IsMissing)
                    changes.Add(new TextChange(new TextSpan(type.CloseBraceToken.SpanStart, 0), string.Concat(implementations)));
            }
            if (changes.Count != 0)
                compilation = compilation.ReplaceSyntaxTree(tree, tree.WithChangedText(tree.GetText().WithChanges(changes)));
        }
        return compilation;
    }

    private static string? Implementation(MemberDeclarationSyntax declaration, ISymbol? symbol)
    {
        var unimplemented = symbol switch
        {
            IMethodSymbol { IsPartialDefinition: true, PartialImplementationPart: null, Parameters.Length: 0 } => true,
            IPropertySymbol { IsPartialDefinition: true, PartialImplementationPart: null } => true,
            _ => false
        };
        var attribute = symbol?.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == AttributeName);
        if (!unimplemented || attribute is null) return null;
        var pattern = attribute.ConstructorArguments.FirstOrDefault().Value as string ?? "";
        var options = attribute.ConstructorArguments.Skip(1).FirstOrDefault().Value as int? ?? 0;
        var body = $"new global::System.Text.RegularExpressions.Regex({SymbolDisplay.FormatLiteral(pattern, quote: true)}, "
            + $"(global::System.Text.RegularExpressions.RegexOptions){options})";
        var modifiers = string.Join(" ", declaration.Modifiers.Select(modifier => modifier.ValueText));
        var name = declaration is MethodDeclarationSyntax method ? $"{method.Identifier.ValueText}()" : ((PropertyDeclarationSyntax)declaration).Identifier.ValueText;
        return $" {modifiers} global::System.Text.RegularExpressions.Regex {name} => {body}; ";
    }
}
