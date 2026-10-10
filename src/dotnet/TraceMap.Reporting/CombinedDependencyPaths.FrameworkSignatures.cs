using System.Globalization;
using System.Text;

namespace TraceMap.Reporting;

public static partial class CombinedDependencyPathReporter
{
    private const string FrameworkVersionCandidateRuleId = "combined.paths.framework-signature-candidate.v1";
    private const string FrameworkVersionCandidateEdge = "compiled-il-framework-version-candidate";

    // This is a candidate key, never a replacement for the retained identity.
    // Only embedded desktop-framework scopes are compared tolerantly. The
    // declaring assembly (including its version) and all other identity fields
    // remain exact. Parse length-framed components so names cannot inject syntax.
    internal static string? FrameworkSignatureCandidateKey(string identity)
    {
        if (identity.Length > 65_536) return null;
        var parser = new FrameworkSignatureParser(identity);
        return parser.Parse();
    }

    private sealed class FrameworkSignatureParser(string input)
    {
        private int position;
        private readonly StringBuilder output = new();
        private int types;

        internal string? Parse()
        {
            if (!Literal("memberref|type:") || !Type(false, 0) || !Literal("|member:")
                || !Component(out _) || !Literal("|arity:") || !Number()
                || !Literal("|call:") || !Choice("default", "vararg")
                || !Literal("|hasThis:") || !Choice("true", "false")
                || !Literal("|explicitThis:") || !Choice("true", "false") || !Literal("|(")) return null;
            if (!Peek(")"))
            {
                do { if (!Type(true, 0)) return null; } while (Literal(","));
            }
            if (!Literal(")->") || !Type(true, 0) || position != input.Length) return null;
            return output.ToString();
        }

        private bool Type(bool normalize, int depth)
        {
            if (depth > 32 || ++types > 256) return false;
            if (Literal("modreq(") || Literal("modopt("))
            {
                if (!Type(normalize, depth + 1) || !Literal(") ") || !Type(normalize, depth + 1)) return false;
                return true;
            }
            if (Literal("!!") || Literal("!")) { if (!Number()) return false; }
            else
            {
                if (Literal("scope("))
                {
                    if (!Assembly(normalize) || !Literal(")")) return false;
                }
                if (!Literal("type(namespace:") || !Component(out _) || !Literal("|names:")) return false;
                var count = 0;
                while (position < input.Length && char.IsAsciiDigit(input[position]))
                { if (!Component(out _)) return false; count++; }
                if (count == 0 || !Literal(")")) return false;
            }
            if (Literal("<"))
            {
                do { if (!Type(normalize, depth + 1)) return false; } while (Literal(","));
                if (!Literal(">")) return false;
            }
            while (Literal("[]") || Literal("&") || Literal("*")) { }
            // Shaped arrays and function pointers are deliberately not generalized.
            return true;
        }

        private bool Assembly(bool normalize)
        {
            if (!Literal("assembly:name:") || !Component(out var name) || !Literal("|version:")) return false;
            var versionStart = output.Length;
            if (!Component(out var version)) return false;
            var versionEnd = output.Length;
            if (!Literal("|culture:") || !Component(out var culture)
                || !Literal("|publicKeyToken:") || !Component(out var token)) return false;
            if (normalize && name is "mscorlib" or "System" or "System.Data"
                && culture == "neutral" && token == "b77a5c561934e089"
                && version is "2.0.0.0" or "4.0.0.0")
            {
                output.Remove(versionStart, versionEnd - versionStart);
                output.Insert(versionStart, "7:4.0.0.0");
            }
            return true;
        }

        private bool Component(out string value)
        {
            value = string.Empty;
            var start = position;
            var end = start;
            while (end < input.Length && char.IsAsciiDigit(input[end])) end++;
            if (end == start || end >= input.Length || input[end] != ':'
                || !int.TryParse(input.AsSpan(start, end - start), NumberStyles.None, CultureInfo.InvariantCulture, out var length)
                || length < 0 || length > input.Length - end - 1) return false;
            value = input.Substring(end + 1, length);
            position = end + 1 + length;
            output.Append(input.AsSpan(start, position - start));
            return true;
        }
        private bool Number()
        {
            var start = position;
            while (position < input.Length && char.IsAsciiDigit(input[position])) position++;
            if (position == start || position - start > 6) return false;
            output.Append(input.AsSpan(start, position - start));
            return true;
        }
        private bool Choice(string first, string second) => Literal(first) || Literal(second);
        private bool Peek(string text) => input.AsSpan(position).StartsWith(text, StringComparison.Ordinal);
        private bool Literal(string text)
        {
            if (!Peek(text)) return false;
            position += text.Length;
            output.Append(text);
            return true;
        }
    }
}
