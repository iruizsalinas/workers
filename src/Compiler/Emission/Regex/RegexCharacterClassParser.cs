using System.Globalization;
using System.Text;

// Parses character classes and the escapes they share with the rest of a pattern.
internal static partial class RegexTranslator
{
    private sealed partial class Parser
    {
        // A shorthand or Unicode category escape, as the contents of a v-flag character class.
        private string ParseClassEscape(char character)
        {
            switch (character)
            {
                case 'd': return @"\p{Nd}";
                case 'D': return @"\P{Nd}";
                case 'w': return WordClass;
                case 'W': return $"[^{WordClass}]";
                case 's': return SpaceClass;
                case 'S': return $"[^{SpaceClass}]";
            }
            if (_position == pattern.Length || pattern[_position] != '{') throw Unexpected();
            var end = pattern.IndexOf('}', _position);
            if (end < 0) throw Unexpected();
            var name = pattern[(_position + 1)..end];
            _position = end + 1;
            if (!Categories.Contains(name))
                throw Error($"the Unicode category or block '{name}' is not supported");
            return $@"\{character}{{{name}}}";
        }

        private char ParseCharacterEscape(bool inClass)
        {
            var character = pattern[_position++];
            switch (character)
            {
                case 'x': return (char)ReadHex(2);
                case 'u': return (char)ReadHex(4);
                case 'a': return '\a';
                case 'b' when inClass: return '\b';
                case 'e': return '\u001B';
                case 'f': return '\f';
                case 'n': return '\n';
                case 'r': return '\r';
                case 't': return '\t';
                case 'v': return '\v';
                case 'c':
                    var control = _position < pattern.Length ? pattern[_position++] : throw Unexpected();
                    if (control is >= 'a' and <= 'z') control = (char)(control - ('a' - 'A'));
                    return control is >= '@' and <= '_' ? (char)(control - '@') : throw Unexpected();
                case >= '0' and <= '7' when character == '0' || inClass:
                    var octal = character - '0';
                    for (var count = 1; count < 3 && _position < pattern.Length && pattern[_position] is >= '0' and <= '7'; count++)
                        octal = octal * 8 + (pattern[_position++] - '0');
                    return (char)(octal & 0xFF);
                default:
                    if (char.IsLetterOrDigit(character) || character == '_') throw Unexpected();
                    return character;
            }
        }

        private int ReadHex(int digits)
        {
            if (_position + digits > pattern.Length
                || !int.TryParse(pattern.AsSpan(_position, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
                throw Unexpected();
            _position += digits;
            return value;
        }

        // Mirrors .NET's character class scanner, including ranges, a leading literal ']', literal '-' placement
        // and a trailing subtraction such as [a-z-[aeiou]].
        private string ParseClass()
        {
            var negate = _position < pattern.Length && pattern[_position] == '^';
            if (negate) _position++;
            var items = new StringBuilder();
            string? subtraction = null;
            var inRange = false;
            var previous = '\0';
            for (var first = true; ; first = false)
            {
                if (_position == pattern.Length) throw Unexpected();
                var character = pattern[_position++];
                var translated = false;
                if (character == ']' && !first) break;
                if (character == '\\' && _position < pattern.Length)
                {
                    var escape = pattern[_position];
                    if (escape is 'd' or 'D' or 'w' or 'W' or 's' or 'S' or 'p' or 'P')
                    {
                        if (inRange) throw Unexpected();
                        _position++;
                        items.Append(ParseClassEscape(escape));
                        continue;
                    }
                    if (escape == '-')
                    {
                        _position++;
                        items.Append(ClassCharacter('-'));
                        continue;
                    }
                    character = ParseCharacterEscape(inClass: true);
                    translated = true;
                }
                else if (character == '[' && _position < pattern.Length && pattern[_position] == ':' && !inRange)
                    throw Error("POSIX-style character class names are not supported");

                if (inRange)
                {
                    inRange = false;
                    if (character == '[' && !translated && !first)
                    {
                        items.Append(ClassCharacter(previous));
                        subtraction = ParseClass();
                        if (_position >= pattern.Length || pattern[_position++] != ']') throw Unexpected();
                        break;
                    }
                    if (previous > character) throw Unexpected();
                    items.Append(ClassCharacter(previous)).Append('-').Append(ClassCharacter(character));
                }
                else if (_position + 1 < pattern.Length && pattern[_position] == '-' && pattern[_position + 1] != ']')
                {
                    previous = character;
                    inRange = true;
                    _position++;
                }
                else if (_position < pattern.Length && character == '-' && !translated && pattern[_position] == '[' && !first)
                {
                    _position++;
                    subtraction = ParseClass();
                    if (_position >= pattern.Length || pattern[_position++] != ']') throw Unexpected();
                    break;
                }
                else
                    items.Append(ClassCharacter(character));
            }
            var set = $"[{(negate ? "^" : "")}{items}]";
            return subtraction is null ? set : $"[{set}--{subtraction}]";
        }
    }

    private static string ClassCharacter(char character)
    {
        if (char.IsSurrogate(character))
            throw Error("character classes cannot contain surrogate code units");
        return "()[]{}/-\\|&!#%,:;<=>@`~^$.*+?".Contains(character) ? "\\" + character : Escape(character);
    }

    private static string Escape(int codePoint) => codePoint is >= 0x20 and < 0x7F
        ? ((char)codePoint).ToString()
        : $"\\u{{{codePoint:X}}}";
}
