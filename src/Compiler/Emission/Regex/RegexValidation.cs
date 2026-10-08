// Decides which constructs keep their .NET behavior when matched by JavaScript.
internal static partial class RegexTranslator
{
    // Rejects captures and backreferences whose JavaScript behavior differs from .NET.
    private static void Validate(Node node, List<Node> path)
    {
        path.Add(node);
        switch (node)
        {
            case Sequence sequence:
                foreach (var item in sequence.Items) Validate(item, path);
                break;
            case Alternation alternation:
                foreach (var branch in alternation.Branches) Validate(branch, path);
                break;
            case Group group:
                // Lookbehinds match right to left, which would run the atomic group emulation backwards.
                if (group.Kind == GroupKind.Atomic && path.Any(node => node is Group { Kind: GroupKind.LookBehind or GroupKind.NegativeLookBehind }))
                    throw Error("atomic groups inside lookbehinds are not supported");
                Validate(group.Body, path);
                break;
            case Repeat repeat:
                // JavaScript rejects an optional loop iteration that matches empty text and backtracks into it,
                // while .NET accepts the iteration and leaves the loop. An optional single iteration is emitted as
                // an alternation instead, which has no such check.
                if (repeat.Max != 1 && repeat.Min != repeat.Max && IsNullable(repeat.Body))
                    throw Error("a repeated construct can match empty text; make each repetition consume at least one character");
                // JavaScript clears a loop's captures at the start of each iteration, while .NET keeps the last
                // successful capture.
                if (repeat.Max != 1 && Captures(repeat.Body).Any(capture => !IsMandatory(repeat.Body, capture)))
                    throw Error("a capturing group inside a repetition does not participate in every iteration; make the group required or move it out of the loop");
                Validate(repeat.Body, path);
                break;
            case Reference reference:
                ValidateReference(reference, path);
                break;
        }
        path.RemoveAt(path.Count - 1);
    }

    // .NET fails a backreference to a group that has not captured, while JavaScript matches empty text,
    // so the group must always have captured before the reference is reached.
    private static void ValidateReference(Reference reference, List<Node> path)
    {
        var target = reference.Target ?? throw Error("a backreference refers to a group that does not exist");
        if (path.Any(node => node is Group { IsLookaround: true }))
            throw Error("backreferences inside lookarounds are not supported");
        if (path.Contains(target))
            throw Error("a backreference is inside the group it refers to");
        for (var index = path.Count - 1; index >= 0; index--)
        {
            if (path[index] is not Sequence sequence || !Contains(sequence, target)) continue;
            var referenceItem = sequence.Items.IndexOf(path[index + 1]);
            var targetItem = sequence.Items.FindIndex(item => Contains(item, target));
            if (targetItem < referenceItem && IsMandatory(sequence.Items[targetItem], target)) return;
            break;
        }
        throw Error("a backreference refers to a group that may not have captured yet");
    }

    private static bool Contains(Node node, Node target) => node == target || node switch
    {
        Sequence sequence => sequence.Items.Any(item => Contains(item, target)),
        Alternation alternation => alternation.Branches.Any(branch => Contains(branch, target)),
        Group group => Contains(group.Body, target),
        Repeat repeat => Contains(repeat.Body, target),
        _ => false
    };

    private static IEnumerable<Group> Captures(Node node) => node switch
    {
        Group { Kind: GroupKind.Capture } group => Captures(group.Body).Prepend(group),
        Group group => Captures(group.Body),
        Sequence sequence => sequence.Items.SelectMany(Captures),
        Alternation alternation => alternation.Branches.SelectMany(Captures),
        Repeat repeat => Captures(repeat.Body),
        _ => []
    };

    // Whether every match of the node includes a capture by the target group.
    private static bool IsMandatory(Node node, Group target) => node == target || node switch
    {
        Sequence sequence => sequence.Items.Any(item => IsMandatory(item, target)),
        Group { IsLookaround: false } group => IsMandatory(group.Body, target),
        Repeat { Min: > 0 } repeat => IsMandatory(repeat.Body, target),
        _ => false
    };

    private static bool IsNullable(Node node) => node switch
    {
        Sequence sequence => sequence.Items.All(IsNullable),
        Alternation alternation => alternation.Branches.Any(IsNullable),
        Group { IsLookaround: true } => true,
        Group group => IsNullable(group.Body),
        Repeat repeat => repeat.Min == 0 || IsNullable(repeat.Body),
        Atom atom => atom.Nullable,
        Reference => true,
        _ => false
    };

    // The case sensitivity of each construct whose matches depend on it.
    private static IEnumerable<bool> CaseSettings(Node node) => node switch
    {
        Sequence sequence => sequence.Items.SelectMany(CaseSettings),
        Alternation alternation => alternation.Branches.SelectMany(CaseSettings),
        Group group => CaseSettings(group.Body),
        Repeat repeat => CaseSettings(repeat.Body),
        Character character => [character.IgnoreCase],
        Atom { IgnoreCase: { } cased } => [cased],
        Reference reference => [reference.IgnoreCase],
        _ => []
    };
}
