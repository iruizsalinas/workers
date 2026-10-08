using System.Globalization;
using System.Text.RegularExpressions;

// Parses .NET pattern syntax: alternation, groups, inline options, quantifiers and escapes.
internal static partial class RegexTranslator
{
    private sealed partial class Parser(string pattern, RegexOptions options)
    {
        private int _position;

        public List<Group> Captures { get; } = [];
        public List<Reference> References { get; } = [];

        public Node ParseRoot()
        {
            var scope = new Scope(
                options.HasFlag(RegexOptions.IgnoreCase), options.HasFlag(RegexOptions.Multiline),
                options.HasFlag(RegexOptions.ExplicitCapture), options.HasFlag(RegexOptions.Singleline),
                options.HasFlag(RegexOptions.IgnorePatternWhitespace));
            var root = ParseAlternation(scope);
            if (_position != pattern.Length) throw Unexpected();
            return root;
        }

        // Inline options such as (?i) apply to the rest of the enclosing group, across alternation branches.
        private Node ParseAlternation(Scope scope)
        {
            var branches = new List<Node>();
            var items = new List<Node>();
            while (true)
            {
                SkipBlank(scope);
                if (_position == pattern.Length || pattern[_position] == ')') break;
                if (pattern[_position] == '|')
                {
                    _position++;
                    branches.Add(new Sequence(items));
                    items = [];
                    continue;
                }
                if (TryParseInlineOptions(ref scope)) continue;
                if (ParseAtom(scope) is not { } atom) continue;
                SkipBlank(scope);
                items.Add(ParseQuantifier(atom));
            }
            branches.Add(new Sequence(items));
            return branches.Count == 1 ? branches[0] : new Alternation(branches);
        }

        private void SkipBlank(Scope scope)
        {
            while (_position < pattern.Length)
            {
                if (scope.Extended && pattern[_position] is ' ' or '\t' or '\n' or '\f' or '\r')
                    _position++;
                else if (scope.Extended && pattern[_position] == '#')
                {
                    while (_position < pattern.Length && pattern[_position] != '\n') _position++;
                }
                else if (string.CompareOrdinal(pattern, _position, "(?#", 0, 3) == 0)
                {
                    var end = pattern.IndexOf(')', _position);
                    _position = end < 0 ? throw Unexpected() : end + 1;
                }
                else break;
            }
        }

        private bool TryParseInlineOptions(ref Scope scope)
        {
            if (!pattern.AsSpan(_position).StartsWith("(?")) return false;
            var end = _position + 2;
            while (end < pattern.Length && pattern[end] is 'i' or 'm' or 'n' or 's' or 'x' or '-') end++;
            if (end == _position + 2 || end == pattern.Length || pattern[end] != ')') return false;
            scope = ApplyOptions(scope, pattern[(_position + 2)..end]);
            _position = end + 1;
            return true;
        }

        private static Scope ApplyOptions(Scope scope, string flags)
        {
            var enable = true;
            foreach (var flag in flags)
            {
                switch (flag)
                {
                    case '-': enable = false; break;
                    case 'i': scope.IgnoreCase = enable; break;
                    case 'm': scope.Multiline = enable; break;
                    case 'n': scope.ExplicitCapture = enable; break;
                    case 's': scope.Singleline = enable; break;
                    case 'x': scope.Extended = enable; break;
                }
            }
            return scope;
        }

        private Node? ParseAtom(Scope scope)
        {
            var character = pattern[_position++];
            return character switch
            {
                '(' => ParseGroup(scope),
                '[' => new Atom(ParseClass(), nullable: false, scope.IgnoreCase),
                '.' => new Atom(scope.Singleline ? @"[\s\S]" : @"[^\n]", nullable: false),
                '^' => new Atom(scope.Multiline ? @"(?<![^\n])" : "^", nullable: true),
                '$' => new Atom(scope.Multiline ? @"(?![^\n])" : @"(?=\n?$)", nullable: true),
                '\\' => ParseEscape(scope),
                '*' or '+' or '?' => throw Unexpected(),
                _ => new Character(character, scope.IgnoreCase)
            };
        }

        private Node ParseQuantifier(Node atom)
        {
            if (_position == pattern.Length) return atom;
            int min, max;
            switch (pattern[_position])
            {
                case '*': min = 0; max = -1; _position++; break;
                case '+': min = 1; max = -1; _position++; break;
                case '?': min = 0; max = 1; _position++; break;
                case '{' when TryParseBounds(out min, out max): break;
                default: return atom;
            }
            if (atom is Atom { Nullable: true } or Group { IsLookaround: true })
                throw Error("a quantifier is applied to an anchor or lookaround");
            var lazy = _position < pattern.Length && pattern[_position] == '?';
            if (lazy) _position++;
            return new Repeat(atom, min, max, lazy);
        }

        // {n}, {n,} and {n,m}; any other brace is a literal character in .NET.
        private bool TryParseBounds(out int min, out int max)
        {
            min = max = 0;
            var position = _position + 1;
            if (!TryReadNumber(ref position, out min)) return false;
            max = min;
            if (position < pattern.Length && pattern[position] == ',')
            {
                position++;
                max = -1;
                if (position < pattern.Length && char.IsAsciiDigit(pattern[position]) && !TryReadNumber(ref position, out max))
                    return false;
            }
            if (position >= pattern.Length || pattern[position] != '}') return false;
            _position = position + 1;
            return true;
        }

        private bool TryReadNumber(ref int position, out int value)
        {
            value = 0;
            var start = position;
            while (position < pattern.Length && char.IsAsciiDigit(pattern[position]))
            {
                if (value > (int.MaxValue - 9) / 10) throw Error("a quantifier bound is too large");
                value = value * 10 + (pattern[position++] - '0');
            }
            return position != start;
        }

        private Node? ParseGroup(Scope scope)
        {
            var kind = GroupKind.Capture;
            string? name = null;
            if (_position < pattern.Length && pattern[_position] == '?')
            {
                _position++;
                var character = _position < pattern.Length ? pattern[_position++] : throw Unexpected();
                switch (character)
                {
                    case ':': kind = GroupKind.NonCapture; break;
                    case '=': kind = GroupKind.LookAhead; break;
                    case '!': kind = GroupKind.NegativeLookAhead; break;
                    case '>': kind = GroupKind.Atomic; break;
                    case '<' when _position < pattern.Length && pattern[_position] == '=':
                        _position++;
                        kind = GroupKind.LookBehind;
                        break;
                    case '<' when _position < pattern.Length && pattern[_position] == '!':
                        _position++;
                        kind = GroupKind.NegativeLookBehind;
                        break;
                    case '<' or '\'':
                        name = ParseGroupName(character == '<' ? '>' : '\'');
                        break;
                    case '(':
                        throw Error("conditional groups are not supported");
                    case 'i' or 'm' or 'n' or 's' or 'x' or '-':
                        var start = _position - 1;
                        while (_position < pattern.Length && pattern[_position] is 'i' or 'm' or 'n' or 's' or 'x' or '-')
                            _position++;
                        if (_position == pattern.Length || pattern[_position] != ':') throw Unexpected();
                        scope = ApplyOptions(scope, pattern[start.._position]);
                        _position++;
                        kind = GroupKind.NonCapture;
                        break;
                    default:
                        throw Unexpected();
                }
            }
            else if (scope.ExplicitCapture)
                kind = GroupKind.NonCapture;

            // Captures are recorded when they open, so nested captures follow their parent as in .NET numbering.
            var group = new Group(kind, name);
            if (kind == GroupKind.Capture) Captures.Add(group);
            group.Body = ParseAlternation(scope);
            if (_position == pattern.Length || pattern[_position] != ')') throw Unexpected();
            _position++;
            return group;
        }

        private string ParseGroupName(char terminator)
        {
            var start = _position;
            while (_position < pattern.Length && pattern[_position] != terminator) _position++;
            if (_position == pattern.Length) throw Unexpected();
            var name = pattern[start.._position++];
            if (name.Contains('-')) throw Error("balancing groups are not supported");
            if (name.Length == 0 || char.IsAsciiDigit(name[0])) throw Error("numbered group names are not supported");
            return name;
        }

        private Node ParseEscape(Scope scope)
        {
            if (_position == pattern.Length) throw Unexpected();
            var character = pattern[_position++];
            switch (character)
            {
                case 'b': return new Atom($"(?:(?<={BoundaryWordClass})(?!{BoundaryWordClass})|(?<!{BoundaryWordClass})(?={BoundaryWordClass}))", nullable: true);
                case 'B': return new Atom($"(?:(?<={BoundaryWordClass})(?={BoundaryWordClass})|(?<!{BoundaryWordClass})(?!{BoundaryWordClass}))", nullable: true);
                case 'A': return new Atom("^", nullable: true);
                case 'z': return new Atom("$", nullable: true);
                case 'Z': return new Atom(@"(?=\n?$)", nullable: true);
                case 'G': throw Error(@"\G is not supported");
                case 'd' or 'D' or 'w' or 'W' or 's' or 'S' or 'p' or 'P':
                    var set = ParseClassEscape(character);
                    // Case-insensitive matching can widen a Unicode category, while the shorthand classes are
                    // closed under case already.
                    return new Atom(set.StartsWith('[') ? set : $"[{set}]", nullable: false,
                        character is 'p' or 'P' ? scope.IgnoreCase : null);
                case 'k':
                    if (_position == pattern.Length || pattern[_position] is not ('<' or '\''))
                        throw Error(@"\k must be followed by a group name");
                    var terminator = pattern[_position++] == '<' ? '>' : '\'';
                    var end = pattern.IndexOf(terminator, _position);
                    if (end < 0) throw Unexpected();
                    var name = pattern[_position..end];
                    _position = end + 1;
                    return AddReference(int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                        ? new Reference(number, null, scope.IgnoreCase)
                        : new Reference(-1, name, scope.IgnoreCase));
                case >= '1' and <= '9':
                    var value = character - '0';
                    while (_position < pattern.Length && char.IsAsciiDigit(pattern[_position]))
                    {
                        if (value > (int.MaxValue - 9) / 10) throw Error("a backreference number is too large");
                        value = value * 10 + (pattern[_position++] - '0');
                    }
                    return AddReference(new Reference(value, null, scope.IgnoreCase));
                default:
                    _position--;
                    return new Character(ParseCharacterEscape(inClass: false), scope.IgnoreCase);
            }
        }

        private Reference AddReference(Reference reference)
        {
            References.Add(reference);
            return reference;
        }

        private NotSupportedException Unexpected() =>
            Error($"the construct at offset {_position} could not be translated");
    }
}
