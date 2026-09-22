using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TraceMap.Tests;

/// <summary>
/// Read-only parser for the IL text that Microsoft ILDasm emits with
/// <c>/out=</c>. It exists only as the independent observation layer of the
/// public ILAsm/ILDAsm parity gate: TraceMap's identities come from its own
/// dual-reader contract, and this parser turns the disassembly of the very
/// same assemblies into comparable counts, offsets, and line directives.
/// It never feeds a scanner input and never writes binaries.
/// </summary>
internal static partial class IlDasmTextParser
{
    internal sealed record IlDasmLineDirective(
        int Offset,
        int StartLine,
        int EndLine,
        int StartColumn,
        int EndColumn,
        string Document);

    internal sealed record IlDasmMethodText(
        string TypeName,
        string MethodName,
        int MaxStack,
        int LocalCount,
        int ExceptionRegionCount,
        int TryBlockCount,
        IReadOnlyList<(int Offset, string Opcode, string Operand)> Instructions,
        IReadOnlyList<(int Offset, string Opcode)> CallSites,
        IReadOnlyList<IlDasmLineDirective> LineDirectives);

    internal sealed record IlDasmTextFile(
        IReadOnlyList<IlDasmMethodText> Methods,
        string NormalizedText)
    {
        public IlDasmMethodText Method(string typeName, string methodName) =>
            Methods.Single(method =>
                method.TypeName == typeName
                && method.MethodName == methodName);
    }

    // TraceMap's call observations cover exactly these opcodes (calli and
    // constrained. enter through their own operand paths), so the parity
    // comparison uses the same family for count and offset agreement.
    private static readonly HashSet<string> CallFamilyOpcodes =
        new(StringComparer.Ordinal) { "call", "callvirt", "newobj", "ldftn", "ldvirtftn", "calli", "constrained." };

    [GeneratedRegex(@"^\s*IL_([0-9a-f]{4}):\s+(\S+)(?:\s+(.+?))?\s*$")]
    private static partial Regex InstructionRegex();

    [GeneratedRegex(@"^\.line\s+(\d+),(\d+)\s*:\s*(\d+),(\d+)\s+'(.*)'\s*$")]
    private static partial Regex LineDirectiveRegex();

    internal static IlDasmTextFile Parse(string path) => ParseText(File.ReadAllText(path));

    internal static IlDasmTextFile ParseText(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var methods = new List<IlDasmMethodText>();
        var normalized = new StringBuilder();
        var typeStack = new List<string>();
        // A `.try` closing brace sits at the same absolute depth as the
        // enclosing method's own closing brace, so depth alone cannot tell
        // them apart. Every construct instead remembers the exact depth its
        // own closing brace will be seen at.
        var classClosingDepths = new Stack<int>();
        var braceDepth = 0;
        var methodClosingDepth = -1;
        IlDasmMethodTextBuilder? builder = null;
        StringBuilder? localsBuilder = null;
        var localsBalance = 0;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            var trimmed = line.Trim();
            // Comment-only lines (code size, MVID, token and byte comments)
            // are the disassembler's annotations, not disassembled content.
            if (!trimmed.StartsWith("//", StringComparison.Ordinal))
                normalized.Append(line).Append('\n');
            if (localsBuilder is not null)
            {
                localsBalance += Count(trimmed, '(') - Count(trimmed, ')');
                if (localsBalance > 0)
                    localsBuilder.Append(' ').Append(trimmed);
                else
                {
                    if (trimmed.Length > 0)
                        localsBuilder.Append(' ').Append(trimmed);
                    builder!.LocalCount = CountLocalEntries(InnerLocals(localsBuilder.ToString()));
                    localsBuilder = null;
                }
                continue;
            }

            if (trimmed.StartsWith(".class ", StringComparison.Ordinal))
            {
                typeStack.Add(ClassName(trimmed));
                classClosingDepths.Push(braceDepth + 1);
                continue;
            }
            if (trimmed.StartsWith(".method ", StringComparison.Ordinal))
            {
                builder = new IlDasmMethodTextBuilder(CurrentTypeName(typeStack), MethodName(trimmed));
                methodClosingDepth = braceDepth + 1;
                continue;
            }
            if (trimmed.Length > 0 && trimmed[0] == '{')
            {
                braceDepth++;
                continue;
            }
            if (trimmed.Length > 0 && trimmed[0] == '}')
            {
                if (builder is not null && braceDepth == methodClosingDepth)
                {
                    methods.Add(builder.Build());
                    builder = null;
                    methodClosingDepth = -1;
                }
                else if (typeStack.Count > 0 && classClosingDepths.Count > 0 && braceDepth == classClosingDepths.Peek())
                {
                    typeStack.RemoveAt(typeStack.Count - 1);
                    classClosingDepths.Pop();
                    // A builder still pending when its class closes belonged
                    // to a bodyless declaration (abstract or pinvokeimpl) that
                    // never opened a brace; it carries no IL to compare.
                    if (builder is not null)
                    {
                        builder = null;
                        methodClosingDepth = -1;
                    }
                }
                braceDepth--;
                continue;
            }
            if (builder is null)
                continue;

            if (trimmed.StartsWith(".maxstack ", StringComparison.Ordinal))
            {
                builder.MaxStack = int.Parse(trimmed[".maxstack ".Length..].Trim(), CultureInfo.InvariantCulture);
                continue;
            }
            if (trimmed.StartsWith(".locals ", StringComparison.Ordinal))
            {
                localsBuilder = new StringBuilder(trimmed);
                localsBalance = Count(trimmed, '(') - Count(trimmed, ')');
                if (localsBalance <= 0)
                {
                    builder.LocalCount = CountLocalEntries(InnerLocals(trimmed));
                    localsBuilder = null;
                }
                continue;
            }
            if (trimmed.StartsWith(".try", StringComparison.Ordinal))
            {
                builder.TryBlockCount++;
                continue;
            }
            if (IsHandlerKeywordLine(trimmed))
            {
                // Every printed handler keyword corresponds to one ECMA-335
                // exception-handling clause; ILDasm nests shared-try clauses
                // rather than duplicating instructions.
                builder.ExceptionRegionCount++;
                continue;
            }
            if (LineDirectiveRegex().Match(trimmed) is { Success: true } lineMatch)
            {
                builder.PendingLine = new IlDasmLineDirective(
                    0,
                    int.Parse(lineMatch.Groups[1].Value, CultureInfo.InvariantCulture),
                    int.Parse(lineMatch.Groups[2].Value, CultureInfo.InvariantCulture),
                    int.Parse(lineMatch.Groups[3].Value, CultureInfo.InvariantCulture),
                    int.Parse(lineMatch.Groups[4].Value, CultureInfo.InvariantCulture),
                    lineMatch.Groups[5].Value);
                continue;
            }
            if (InstructionRegex().Match(trimmed) is { Success: true } instruction)
            {
                var offset = int.Parse(instruction.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var opcode = instruction.Groups[2].Value;
                var operand = instruction.Groups[3].Success ? instruction.Groups[3].Value : string.Empty;
                if (builder.PendingLine is { } directive)
                {
                    builder.LineDirectives.Add(directive with { Offset = offset });
                    builder.PendingLine = null;
                }
                builder.Instructions.Add((offset, opcode, operand));
                if (CallFamilyOpcodes.Contains(opcode))
                    builder.CallSites.Add((offset, opcode));
                continue;
            }
        }

        if (builder is not null || localsBuilder is not null)
            throw new InvalidOperationException("Unterminated .method block in ILDasm text.");
        return new IlDasmTextFile(methods, Normalized(normalized.ToString()));
    }

    // Whitespace-insensitive canonical form for whole-file comparison between
    // two disassemblies: blank lines collapse and every remaining line keeps
    // its content with trailing blanks trimmed.
    internal static string Normalized(string text)
    {
        var builder = new StringBuilder();
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0)
                continue;
            builder.Append(line).Append('\n');
        }
        return builder.ToString();
    }

    private static bool IsHandlerKeywordLine(string trimmed)
    {
        foreach (var keyword in new[] { "catch", "finally", "fault", "filter" })
            if (trimmed == keyword || trimmed.StartsWith(keyword + " ", StringComparison.Ordinal))
                return true;
        return false;
    }

    private static string InnerLocals(string directive)
    {
        var opening = directive.IndexOf('(');
        var closing = directive.LastIndexOf(')');
        if (opening < 0)
            throw new InvalidOperationException("Malformed .locals directive in ILDasm text.");
        return closing < opening ? directive[(opening + 1)..] : directive[(opening + 1)..closing];
    }

    // Locals are separated by commas that sit outside every nested generic or
    // array bracket, so a depth-aware split is exact for well-formed ILDasm.
    private static int CountLocalEntries(string inner)
    {
        var trimmed = inner.Trim();
        if (trimmed.Length == 0)
            return 0;
        var entries = 0;
        var depth = 0;
        var sawContent = false;
        foreach (var character in trimmed)
        {
            if (character is '<' or '[' or '(')
                depth++;
            else if (character is '>' or ']' or ')')
                depth--;
            else if (character == ',' && depth == 0)
            {
                entries++;
                sawContent = false;
                continue;
            }
            else if (!char.IsWhiteSpace(character))
                sawContent = true;
        }
        return sawContent || entries > 0 ? entries + 1 : 0;
    }

    private static int Count(string text, char value)
    {
        var total = 0;
        foreach (var character in text)
            if (character == value)
                total++;
        return total;
    }

    private static string ClassName(string classLine)
    {
        // Top-level classes print their namespace-qualified name; nested
        // classes print only their own name after the nesting flags.
        var tokens = classLine.Split(' ');
        var name = tokens[^1];
        if (name == "extends" || name.StartsWith("implements", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected trailing token as class name in ILDasm text.");
        return name;
    }

    private static string MethodName(string methodLine)
    {
        var parenthesis = methodLine.IndexOf('(');
        if (parenthesis < 0)
            throw new InvalidOperationException("Malformed .method header in ILDasm text.");
        var before = methodLine[..parenthesis].TrimEnd();
        var lastSpace = before.LastIndexOf(' ');
        var token = lastSpace < 0 ? before : before[(lastSpace + 1)..];
        // Generic method declarations print Ignore<T>; the bare name is the
        // token before the arity suffix.
        var arity = token.IndexOf('<');
        return arity < 0 ? token : token[..arity];
    }

    private static string CurrentTypeName(List<string> typeStack) => typeStack.Count == 0
        ? "<Module>"
        : string.Join(".", typeStack);

    private sealed class IlDasmMethodTextBuilder
    {
        public IlDasmMethodTextBuilder(string typeName, string methodName)
        {
            TypeName = typeName;
            MethodName = methodName;
        }

        public string TypeName { get; }
        public string MethodName { get; }
        public int MaxStack { get; set; }
        public int LocalCount { get; set; }
        public int TryBlockCount { get; set; }
        public int ExceptionRegionCount { get; set; }
        public List<(int Offset, string Opcode, string Operand)> Instructions { get; } = [];
        public List<(int Offset, string Opcode)> CallSites { get; } = [];
        public List<IlDasmLineDirective> LineDirectives { get; } = [];
        public IlDasmLineDirective? PendingLine { get; set; }

        public IlDasmMethodText Build() => new(
            TypeName,
            MethodName,
            MaxStack,
            LocalCount,
            ExceptionRegionCount,
            TryBlockCount,
            Instructions.ToArray(),
            CallSites.ToArray(),
            LineDirectives.ToArray());
    }
}
