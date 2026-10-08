internal static partial class HelperSource
{
    // A compiled Regex keeps its translated expression with the .NET group numbering: groups maps each .NET
    // group number to its JavaScript group index, and names holds the group names by number. Match and Group
    // values are plain objects whose fields mirror Value, Index, Length, Success and Name; a match is also its
    // own group 0. Scanning follows .NET: after an empty match the next attempt starts one character later.
    private static string Regex(Func<string, string> name) => $$"""
        function {{name("regexCreate")}}(source, pattern, flags, groups, names) {
          return { source, expression: new RegExp(pattern, flags), groups, names };
        }
        function {{name("regexEmptyGroup")}}() {
          return { success: false, value: "", index: 0, length: 0, name: "" };
        }
        // Failed matches are all Match.Empty, so they compare equal like the .NET singleton.
        let {{name("regexEmpty")}} = null;
        function {{name("regexEmptyMatch")}}() {
          if ({{name("regexEmpty")}} !== null) return {{name("regexEmpty")}};
          const match = { success: false, value: "", index: 0, length: 0, name: "0", regex: null, input: "" };
          match.groups = [match];
          return {{name("regexEmpty")}} = match;
        }
        function {{name("regexInput")}}(input, start = 0) {
          if (input == null) throw new TypeError("Value cannot be null. (Parameter 'input')");
          if (!Number.isInteger(start) || start < 0 || start > input.length)
            throw new RangeError("Specified argument was out of the range of valid values. (Parameter 'startat')");
          return input;
        }
        function {{name("regexExec")}}(regex, input, start) {
          const expression = regex.expression;
          expression.lastIndex = start;
          const result = expression.exec(input);
          if (result === null) return {{name("regexEmptyMatch")}}();
          const groups = regex.groups.map((index, number) => {
            const value = result[index], name = regex.names[number];
            if (value === undefined) return { success: false, value: "", index: 0, length: 0, name };
            const position = number === 0 ? result.index : result.indices[index][0];
            return { success: true, value, index: position, length: value.length, name };
          });
          const match = groups[0];
          groups.regex = regex;
          match.groups = groups;
          match.regex = regex;
          match.input = input;
          return match;
        }
        function {{name("regexMatch")}}(regex, input, start = 0) {
          return {{name("regexExec")}}(regex, {{name("regexInput")}}(input, start), start);
        }
        function {{name("regexIsMatch")}}(regex, input, start = 0) {
          const expression = regex.expression;
          expression.lastIndex = start;
          return expression.test({{name("regexInput")}}(input, start));
        }
        function {{name("regexNextMatch")}}(match) {
          if (match.regex === null) return match;
          const end = match.index + match.length;
          if (match.length !== 0) return {{name("regexExec")}}(match.regex, match.input, end);
          return end >= match.input.length
            ? {{name("regexEmptyMatch")}}()
            : {{name("regexExec")}}(match.regex, match.input, end + 1);
        }
        function {{name("regexMatches")}}(regex, input, start = 0) {
          const matches = [];
          for (let match = {{name("regexMatch")}}(regex, input, start); match.success; match = {{name("regexNextMatch")}}(match))
            matches.push(match);
          return matches;
        }
        function {{name("regexCount")}}(regex, input) {
          let count = 0;
          for (let match = {{name("regexMatch")}}(regex, input); match.success; match = {{name("regexNextMatch")}}(match))
            count++;
          return count;
        }
        function {{name("regexGroup")}}(groups, key) {
          if (key == null) throw new TypeError("Value cannot be null. (Parameter 'groupname')");
          const number = typeof key === "number" ? key : groups.regex ? groups.regex.names.indexOf(key) : -1;
          return number >= 0 && number < groups.length ? groups[number] : {{name("regexEmptyGroup")}}();
        }
        function {{name("regexSplit")}}(regex, input) {
          const parts = [];
          let last = 0;
          for (let match = {{name("regexMatch")}}(regex, input); match.success; match = {{name("regexNextMatch")}}(match)) {
            parts.push(input.slice(last, match.index));
            last = match.index + match.length;
            for (let number = 1; number < match.groups.length; number++)
              if (match.groups[number].success) parts.push(match.groups[number].value);
          }
          parts.push(input.slice(last));
          return parts;
        }
        function {{name("regexReplace")}}(regex, input, replacement, count = -1) {
          {{name("regexInput")}}(input);
          if (replacement == null)
            throw new TypeError(`Value cannot be null. (Parameter '${typeof replacement === "function" ? "evaluator" : "replacement"}')`);
          if (!Number.isInteger(count) || count < -1)
            throw new RangeError("Count cannot be less than -1. (Parameter 'count')");
          const substitute = typeof replacement === "function"
            ? match => replacement(match) ?? ""
            : {{name("regexSubstitution")}}(regex, replacement);
          let output = "", last = 0;
          for (let match = {{name("regexExec")}}(regex, input, 0); match.success && count !== 0; match = {{name("regexNextMatch")}}(match), count--) {
            output += input.slice(last, match.index) + substitute(match);
            last = match.index + match.length;
          }
          return output + input.slice(last);
        }
        // Parses a .NET replacement pattern. Group references are $number, ${number} and ${name}; $$, $&, $`,
        // $', $+ and $_ are the special forms; any other $ is literal text.
        function {{name("regexSubstitution")}}(regex, text) {
          const parts = [], word = /[\p{L}\p{Mn}\p{Nd}\p{Pc}‌‍]/u;
          let literal = "";
          for (let index = 0; index < text.length; index++) {
            if (text[index] !== "$" || index + 1 === text.length) {
              literal += text[index];
              continue;
            }
            let position = index + 1, character = text[position], angled = false, reference = null;
            if (character === "{" && position + 1 < text.length) {
              angled = true;
              character = text[++position];
            }
            if (character >= "0" && character <= "9") {
              let number = 0;
              while (position < text.length && text[position] >= "0" && text[position] <= "9")
                number = Math.min(number * 10 + (text.charCodeAt(position++) - 48), 2147483647);
              if ((!angled || text[position++] === "}") && number < regex.groups.length) reference = number;
            } else if (angled && word.test(character)) {
              const start = position;
              while (position < text.length && word.test(text[position])) position++;
              const number = regex.names.indexOf(text.slice(start, position));
              if (text[position++] === "}" && number > 0) reference = number;
            } else if (!angled) {
              reference = { "$": "$", "&": 0, "`": -1, "'": -2, "+": -3, "_": -4 }[character] ?? null;
              position++;
            }
            if (reference === null) {
              literal += "$";
              continue;
            }
            if (reference === "$") {
              literal += "$";
            } else {
              if (literal.length !== 0) parts.push(literal);
              literal = "";
              parts.push(reference);
            }
            index = position - 1;
          }
          if (literal.length !== 0) parts.push(literal);
          return match => {
            let output = "";
            for (const part of parts) {
              if (typeof part === "string") output += part;
              else if (part >= 0) output += match.groups[part].value;
              else if (part === -1) output += match.input.slice(0, match.index);
              else if (part === -2) output += match.input.slice(match.index + match.length);
              else if (part === -3) output += match.groups[match.groups.length - 1].value;
              else output += match.input;
            }
            return output;
          };
        }

        """;
}
