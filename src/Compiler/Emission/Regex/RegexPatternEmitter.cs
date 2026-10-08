using System.Text;

// Writes the parsed pattern as a JavaScript pattern for the v flag.
internal static partial class RegexTranslator
{
    private sealed class Emitter(bool ignoreCase, bool explicitCase)
    {
        private int _groups;

        public string Emit(Node node) => node switch
        {
            Sequence sequence => EmitSequence(sequence),
            Alternation alternation => string.Join("|", alternation.Branches.Select(Emit)),
            Character character => Cased(Literal(character.Value), character.IgnoreCase),
            Atom atom => atom.IgnoreCase is { } cased ? Cased(atom.JavaScript, cased) : atom.JavaScript,
            Group group => EmitGroup(group),
            Repeat repeat => EmitRepeat(repeat),
            Reference reference => EmitReference(reference),
            _ => throw new InvalidOperationException()
        };

        // A surrogate pair written as two literal characters matches as one code point under the v flag.
        private string EmitSequence(Sequence sequence)
        {
            var output = new StringBuilder();
            for (var index = 0; index < sequence.Items.Count; index++)
            {
                if (sequence.Items[index] is Character { Value: var high } first && char.IsHighSurrogate(high)
                    && index + 1 < sequence.Items.Count
                    && sequence.Items[index + 1] is Character { Value: var low } second && char.IsLowSurrogate(low)
                    && first.IgnoreCase == second.IgnoreCase)
                {
                    output.Append(Cased(Escape(char.ConvertToUtf32(high, low)), first.IgnoreCase));
                    index++;
                    continue;
                }
                output.Append(Emit(sequence.Items[index]));
            }
            return output.ToString();
        }

        private static string Literal(char character)
        {
            if (char.IsSurrogate(character))
                throw Error("it contains a surrogate code unit that is not part of a literal pair");
            return "^$\\.*+?()[]{}|/".Contains(character) ? "\\" + character : Escape(character);
        }

        private string Cased(string javaScript, bool cased) => cased == ignoreCase && !explicitCase
            ? javaScript
            : $"(?{(cased ? "i" : "-i")}:{javaScript})";

        private string EmitGroup(Group group)
        {
            switch (group.Kind)
            {
                case GroupKind.Capture:
                    group.JavaScriptIndex = ++_groups;
                    return $"({Emit(group.Body)})";
                case GroupKind.Atomic:
                    // A lookahead does not backtrack into its match once it succeeds, which makes the
                    // captured text atomic when it is consumed by a backreference.
                    var index = ++_groups;
                    return $"(?:(?=({Emit(group.Body)}))\\{index})";
            }
            var prefix = group.Kind switch
            {
                GroupKind.NonCapture => "(?:",
                GroupKind.LookAhead => "(?=",
                GroupKind.NegativeLookAhead => "(?!",
                GroupKind.LookBehind => "(?<=",
                _ => "(?<!"
            };
            return $"{prefix}{Emit(group.Body)})";
        }

        // A quantifier always follows a single atom: a literal, a class, a group, or a construct wrapped in a group.
        private string EmitRepeat(Repeat repeat)
        {
            var quantifier = (repeat.Min, repeat.Max) switch
            {
                (0, -1) => "*",
                (1, -1) => "+",
                (0, 1) => "?",
                (var min, -1) => $"{{{min},}}",
                var (min, max) when min == max => $"{{{min}}}",
                var (min, max) => $"{{{min},{max}}}"
            };
            if (repeat is { Min: 0, Max: 1 } && IsNullable(repeat.Body))
                return repeat.Lazy ? $"(?:|{Emit(repeat.Body)})" : $"(?:{Emit(repeat.Body)}|)";
            return Emit(repeat.Body) + quantifier + (repeat.Lazy ? "?" : "");
        }

        private string EmitReference(Reference reference)
        {
            var index = reference.Target!.JavaScriptIndex;
            if (index == 0) throw Error("a backreference refers to a group that has not been emitted yet");
            return Cased($"(?:\\{index})", reference.IgnoreCase);
        }
    }
}
