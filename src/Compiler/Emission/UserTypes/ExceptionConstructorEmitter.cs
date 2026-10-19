using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

internal sealed partial class JavaScriptEmitter
{
    private string ExceptionTypeNameSlot => _names.Get("exception-type-name", "exceptionTypeName");

    // The base call is either a constructor initializer or, for a primary constructor, the base list entry.
    private void EmitExceptionInitialization(INamedTypeSymbol type, SyntaxNode? baseCall, ArgumentListSyntax? baseArguments)
    {
        var fallback = $"{JsonSerializer.Serialize("Exception of type '")} + this.{ExceptionTypeNameSlot} + {JsonSerializer.Serialize("' was thrown.")}";
        var arguments = baseArguments?.Arguments.ToArray() ?? [];
        var key = $"exception-base:{baseCall?.SyntaxTree.FilePath}:{baseCall?.SpanStart}";
        var temporaries = arguments.Select((_, index) => _names.Get($"{key}:{index}", $"arg{index + 1}")).ToArray();
        var message = fallback;
        string? inner = null;
        for (var index = 0; index < arguments.Length; index++)
        {
            var parameter = BoundParameter(arguments[index]);
            if (parameter.Name == "message") message = $"{temporaries[index]} ?? ({fallback})";
            else if (parameter.Name == "innerException") inner = temporaries[index];
            else throw UnsupportedSymbol(parameter, arguments[index]);
        }
        // Capture every base argument before initializing Error members, in source order.
        _output.Append("    ((").Append(string.Join(", ", temporaries)).Append(") => { this.message = ")
            .Append(message).Append(';');
        if (inner is not null) _output.Append(" this.cause = ").Append(inner).Append(';');
        _output.Append(" this.name = ").Append(JsonSerializer.Serialize(type.Name)).Append("; })(")
            .Append(string.Join(", ", arguments.Select(ArgumentValue))).AppendLine(");");
    }

}
