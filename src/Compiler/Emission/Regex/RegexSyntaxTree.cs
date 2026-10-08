// The parsed form of a .NET pattern, and the inline options in effect while parsing it.
internal static partial class RegexTranslator
{
    private abstract class Node;

    private sealed class Sequence(List<Node> items) : Node
    {
        public List<Node> Items { get; } = items;
    }

    private sealed class Alternation(List<Node> branches) : Node
    {
        public List<Node> Branches { get; } = branches;
    }

    // One UTF-16 code unit matched literally.
    private sealed class Character(char value, bool ignoreCase) : Node
    {
        public char Value { get; } = value;
        public bool IgnoreCase { get; } = ignoreCase;
    }

    // An emitted construct such as a character class or an anchor. IgnoreCase is null when casing cannot
    // change what the construct matches.
    private sealed class Atom(string javaScript, bool nullable, bool? ignoreCase = null) : Node
    {
        public string JavaScript { get; } = javaScript;
        public bool Nullable { get; } = nullable;
        public bool? IgnoreCase { get; } = ignoreCase;
    }

    private enum GroupKind { Capture, NonCapture, LookAhead, NegativeLookAhead, LookBehind, NegativeLookBehind, Atomic }

    private sealed class Group(GroupKind kind, string? name = null) : Node
    {
        public Node Body { get; set; } = null!;
        public GroupKind Kind { get; } = kind;
        public string? Name { get; } = name;
        public int Number { get; set; }
        public int JavaScriptIndex { get; set; }
        public bool IsLookaround => Kind is GroupKind.LookAhead or GroupKind.NegativeLookAhead
            or GroupKind.LookBehind or GroupKind.NegativeLookBehind;
    }

    private sealed class Repeat(Node body, int min, int max, bool lazy) : Node
    {
        public Node Body { get; } = body;
        public int Min { get; } = min;
        public int Max { get; } = max;
        public bool Lazy { get; } = lazy;
    }

    private sealed class Reference(int number, string? name, bool ignoreCase) : Node
    {
        public int Number { get; } = number;
        public string? Name { get; } = name;
        public bool IgnoreCase { get; } = ignoreCase;
        public Group? Target { get; set; }
    }

    private record struct Scope(bool IgnoreCase, bool Multiline, bool ExplicitCapture, bool Singleline, bool Extended);
}
