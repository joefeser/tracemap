using System.Globalization;

namespace TraceMap.Reporting;

public static partial class CombinedDependencyPathReporter
{
    private const string ConstructedGenericRuleId = "combined.paths.constructed-generic-member.v1";
    internal sealed record ConstructedMember(string Head, string[] Arguments, string Signature, string DefinitionSignature);

    // This parser never normalizes assembly versions or length-framed names.
    // It supports closed declaring types and non-generic methods only. Mapping
    // an edge to a definition body does not specialize that body's downstream IL.
    internal static ConstructedMember? ParseConstructedMember(string identity)
    {
        try
        {
            var parser = new ConstructedMemberParser(identity);
            var owner = parser.Owner();
            parser.Expect("<");
            var arguments = new List<string>();
            do { arguments.Add(parser.Type([], 0)); } while (parser.Take(","));
            parser.Expect(">");
            if (arguments.Count > 32) return null;
            var head = owner + parser.Member();
            var definitionSignature = parser.Remaining;
            var signature = parser.Signature(arguments.ToArray());
            return new(head, arguments.ToArray(), signature, definitionSignature);
        }
        catch (FormatException) { return null; }
    }

    private static string? GenericMemberHead(string reference)
    {
        try
        {
            var parser = new ConstructedMemberParser(reference);
            return parser.Owner() + parser.Member();
        }
        catch (FormatException) { return null; }
    }

    internal static bool ConstructedMemberMatches(string definitionReference, int definitionArity, ConstructedMember call)
    {
        // Arity came from the length-framed declaration identity parser.
        if (definitionArity != call.Arguments.Length) return false;
        try
        {
            var parser = new ConstructedMemberParser(definitionReference);
            var owner = parser.Owner();
            var head = owner + parser.Member();
            if (head != call.Head) return false;
            // The supported native MemberRefs retain definition-relative
            // !n signature slots. Substitution must not erase which slot was
            // referenced (including when two arguments happen to be identical).
            if (parser.Remaining != call.DefinitionSignature) return false;
            return parser.Signature(call.Arguments) == call.Signature;
        }
        catch (FormatException) { return false; }
    }

    private sealed class ConstructedMemberParser
    {
        private readonly string input;
        private int p;
        private int types;
        public string Remaining => input[p..];
        public ConstructedMemberParser(string value)
        {
            if (value.Length > 65_536) throw new FormatException();
            input = value;
        }
        public string Owner()
        {
            Expect("memberref|type:");
            var start = p;
            NamedType(requireScope: true);
            return input[start..p];
        }
        public string Member()
        {
            var start = p;
            Expect("|member:"); Component();
            return input[start..p];
        }
        public string Signature(string[] arguments)
        {
            var start = p;
            Expect("|arity:0|call:default|hasThis:"); Boolean();
            Expect("|explicitThis:false|(");
            var header = input[start..p];
            var parameters = new List<string>();
            if (!Peek(")"))
                do { parameters.Add(Type(arguments, 0)); } while (Take(","));
            Expect(")->");
            var result = header + string.Join(",", parameters) + ")->" + Type(arguments, 0);
            if (p != input.Length) throw new FormatException();
            return result;
        }
        public string Type(string[] arguments, int depth)
        {
            if (depth > 32 || ++types > 256) throw new FormatException();
            string value;
            if (Take("!"))
            {
                var start = p;
                while (p < input.Length && char.IsAsciiDigit(input[p])) p++;
                if (!int.TryParse(input.AsSpan(start, p - start), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var index) || index >= arguments.Length) throw new FormatException();
                value = arguments[index];
            }
            else
            {
                var start = p;
                NamedType(false);
                value = input[start..p];
                if (Take("<"))
                {
                    var items = new List<string>();
                    do { items.Add(Type(arguments, depth + 1)); } while (Take(","));
                    Expect(">");
                    value += "<" + string.Join(",", items) + ">";
                }
            }
            while (true)
            {
                if (Take("[]")) value += "[]";
                else if (Take("&")) { value += "&"; break; }
                else break;
            }
            return value;
        }
        private void NamedType(bool requireScope)
        {
            if (Take("scope("))
            {
                Expect("assembly:name:"); Component(); Expect("|version:"); Component();
                Expect("|culture:"); Component(); Expect("|publicKeyToken:"); Component(); Expect(")");
            }
            else if (requireScope) throw new FormatException();
            Expect("type(namespace:"); Component(); Expect("|names:");
            var count = 0;
            while (p < input.Length && char.IsAsciiDigit(input[p])) { Component(); count++; }
            if (count == 0) throw new FormatException();
            Expect(")");
        }
        private void Component()
        {
            if (!ConsumeComponent(input, ref p)) throw new FormatException();
        }
        private void Boolean() { if (!Take("true") && !Take("false")) throw new FormatException(); }
        private bool Peek(string value) => input.AsSpan(p).StartsWith(value, StringComparison.Ordinal);
        public bool Take(string value) { if (!Peek(value)) return false; p += value.Length; return true; }
        public void Expect(string value) { if (!Take(value)) throw new FormatException(); }
    }
}
