using Microsoft.CodeAnalysis;
using System.Text.Json;

internal sealed partial class JavaScriptEmitter
{
    private bool _usesPlainRecordIdentity;
    private string PlainRecordIdentitySlot
    {
        get { _usesPlainRecordIdentity = true; return _names.Get("plain-record-identity", "recordType"); }
    }

    private string PlainRecordIdentity(INamedTypeSymbol type) => JsonSerializer.Serialize(RuntimeTypeName(ResolveType(type)));
}
