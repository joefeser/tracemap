using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TraceMap.Tests;

/// <summary>
/// Read-only parser for the IL text that Microsoft ILDasm emits with
/// <c>/out=</c>. It exists only as the independent observation layer of the
/// public ILAsm/ILDAsm parity gate: TraceMap's identities come from its own
/// dual-reader contract, and this parser turns the disassembly of the very
/// same assemblies into comparable counts, offsets, exception-region
/// extents, and line directives. It never feeds a scanner input and never
/// writes binaries.
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

    // Extents are exclusive ends on instruction boundaries, which is exactly
    // how ECMA-335 scopes exception clauses; the disassembler's own
    // instruction offsets define them without any byte arithmetic here.
    internal sealed record IlDasmExceptionRegion(
        string Kind,
        int TryStart,
        int TryEnd,
        int HandlerStart,
        int HandlerEnd,
        string? CatchType,
        int? FilterOffset);

    internal sealed record IlDasmMethodText(
        string TypeName,
        string MethodName,
        int MaxStack,
        int LocalCount,
        int CodeSize,
        IReadOnlyList<IlDasmExceptionRegion> ExceptionRegions,
        IReadOnlyList<(int Offset, string Opcode, string Operand)> Instructions,
        IReadOnlyList<(int Offset, string Opcode)> CallSites,
        IReadOnlyList<IlDasmLineDirective> LineDirectives)
    {
        public int ExceptionRegionCount => ExceptionRegions.Count;
        public string Header { get; init; } = string.Empty;
        public string LocalsSignature { get; init; } = string.Empty;

        // Order-sensitive inside the body, order-insensitive across members:
        // ILAsm legitimately reorders metadata row emission (observed for
        // assembly-level custom attributes), so whole-file text equality is
        // not the parity claim; identical canonical bodies are.
        public string Canonical() =>
            $"{TypeName}|{MethodName}|maxstack:{MaxStack.ToString(CultureInfo.InvariantCulture)}"
            + $"|locals:{LocalCount.ToString(CultureInfo.InvariantCulture)}"
            + $"|header:{Header}|locals-signature:{LocalsSignature}"
            + $"|codesize:{CodeSize.ToString(CultureInfo.InvariantCulture)}\n"
            + string.Join(string.Empty, Instructions.Select(instruction =>
                $"{instruction.Offset.ToString("x4", CultureInfo.InvariantCulture)}:{instruction.Opcode}:{instruction.Operand}\n"))
            + string.Join(string.Empty, ExceptionRegions.Select(region =>
                $"eh:{region.Kind}:try{region.TryStart.ToString("x4", CultureInfo.InvariantCulture)}-{region.TryEnd.ToString("x4", CultureInfo.InvariantCulture)}"
                + $":handler{region.HandlerStart.ToString("x4", CultureInfo.InvariantCulture)}-{region.HandlerEnd.ToString("x4", CultureInfo.InvariantCulture)}"
                + $":catch={region.CatchType ?? ""}:filter={region.FilterOffset?.ToString("x4", CultureInfo.InvariantCulture) ?? ""}\n"));
    }

    internal sealed record IlDasmTextFile(
        IReadOnlyList<IlDasmMethodText> Methods,
        string NormalizedText)
    {
        public IlDasmMethodText Method(string typeName, string methodName) =>
            Methods.Single(method =>
                method.TypeName == typeName
                && method.MethodName == methodName);

        public string CanonicalMethodsText() => string.Join(string.Empty,
            Methods.OrderBy(method => method.TypeName, StringComparer.Ordinal)
                .ThenBy(method => method.MethodName, StringComparer.Ordinal)
                .ThenBy(method => method.Header, StringComparer.Ordinal)
                .Select(method => method.Canonical()));
    }

    // TraceMap's call observations cover exactly these opcodes (calli and
    // constrained. enter through their own operand paths), so the parity
    // comparison uses the same family for count and offset agreement.
    private static readonly HashSet<string> CallFamilyOpcodes =
        new(StringComparer.Ordinal) { "call", "callvirt", "newobj", "ldftn", "ldvirtftn", "calli", "constrained." };

    [GeneratedRegex(@"^\s*IL_([0-9a-f]{4,8}):\s+(\S+)(?:\s+(.+?))?\s*$")]
    private static partial Regex InstructionRegex();

    [GeneratedRegex(@"^\.line\s+(\d+),(\d+)\s*:\s*(\d+),(\d+)\s+'(.*)'\s*$")]
    private static partial Regex LineDirectiveRegex();

    [GeneratedRegex(@"^//\s*Code size\s+(\d+)")]
    private static partial Regex CodeSizeRegex();

    // Switch jump tables print their targets on continuation lines that
    // consist solely of IL_XXXX tokens with commas and the closing paren.
    [GeneratedRegex(@"^\s*(?:IL_[0-9a-f]{4,8}\s*[,\)]?\s*)+$")]
    private static partial Regex SwitchContinuationRegex();

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
        // Headers may wrap inside parameters, custom modifiers, or generic
        // constraints. Only the opening body brace terminates the header.
        StringBuilder? methodHeaderBuffer = null;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            var trimmed = line.Trim();
            if (builder is not null && CodeSizeRegex().Match(trimmed) is { Success: true } sizeMatch)
                builder.CodeSize = int.Parse(sizeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            // Comment-only lines (code size, MVID, token and byte comments)
            // are the disassembler's annotations, not disassembled content.
            if (!trimmed.StartsWith("//", StringComparison.Ordinal))
                normalized.Append(line).Append('\n');
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                continue;
            if (localsBuilder is not null)
            {
                localsBalance += Count(trimmed, '(') - Count(trimmed, ')');
                if (localsBalance > 0)
                    localsBuilder.Append(' ').Append(trimmed);
                else
                {
                    if (trimmed.Length > 0)
                        localsBuilder.Append(' ').Append(trimmed);
                    builder!.SetLocals(localsBuilder.ToString());
                    localsBuilder = null;
                }
                continue;
            }

            if (methodHeaderBuffer is not null)
            {
                if (trimmed == "{")
                {
                    var header = CanonicalText(methodHeaderBuffer.ToString());
                    builder = new IlDasmMethodTextBuilder(CurrentTypeName(typeStack), MethodName(header), header);
                    methodClosingDepth = braceDepth + 1;
                    braceDepth++;
                    methodHeaderBuffer = null;
                }
                else
                {
                    methodHeaderBuffer.Append(' ').Append(trimmed);
                    if (methodHeaderBuffer.Length > 8192 || trimmed.StartsWith('}'))
                        throw new InvalidOperationException($"Malformed .method header in ILDasm text: {methodHeaderBuffer}");
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
                if (builder is not null)
                    throw new InvalidOperationException("Nested .method declaration in ILDasm text.");
                methodHeaderBuffer = new StringBuilder(trimmed);
                continue;
            }
            if (trimmed.Length > 0 && trimmed[0] == '{')
            {
                // ILDasm prints a filter's handler as a bare braced block
                // immediately after the filter block, not as a keyword.
                if (builder?.HasPendingFilterHandler == true)
                    builder.OpenFilterHandlerBlock(braceDepth + 1);
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
                else if (builder is not null && builder.HasOpenBlocks)
                {
                    builder.CloseBlock(braceDepth);
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
                    builder.SetLocals(trimmed);
                    localsBuilder = null;
                }
                continue;
            }
            if (trimmed.StartsWith(".try", StringComparison.Ordinal))
            {
                if (trimmed != ".try")
                    throw new InvalidOperationException($"Unsupported offset-form exception clause: {trimmed}");
                builder.OpenBlock(".try", braceDepth + 1);
                continue;
            }
            if (HandlerKind(trimmed) is { } handlerKind)
            {
                // The handler keyword follows its try's closing brace with no
                // instruction between them, so the pending try block is the
                // one this handler completes.
                builder.OpenHandlerBlock(handlerKind, trimmed, braceDepth + 1);
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
            if (trimmed.StartsWith(".line", StringComparison.Ordinal))
                throw new InvalidOperationException($"Unsupported ILDasm line directive: {trimmed}");
            if (InstructionRegex().Match(trimmed) is { Success: true } instruction)
            {
                var offset = int.Parse(instruction.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var opcode = instruction.Groups[2].Value;
                var operand = instruction.Groups[3].Success ? instruction.Groups[3].Value : string.Empty;
                builder.AddInstruction(offset, opcode, operand, pendingLine: builder.PendingLine);
                continue;
            }
            if (builder.SwitchTargetsPending && SwitchContinuationRegex().IsMatch(trimmed))
            {
                builder.AppendSwitchTargets(trimmed);
                continue;
            }
            // ILDasm wraps method/field/signature operands across lines too,
            // not just switch tables. Retain all continuation text; dropping
            // it can make different parameter or generic types compare equal.
            if (!trimmed.StartsWith('.') && builder.HasInstructions)
                builder.AppendOperand(trimmed);
        }

        if (builder is not null || localsBuilder is not null || methodHeaderBuffer is not null)
        {
            var state = builder is not null ? "body open" : "header incomplete";
            throw new InvalidOperationException($"Unterminated .method block in ILDasm text: {state} {methodHeaderBuffer}");
        }
        return new IlDasmTextFile(methods, Normalized(normalized.ToString()));
    }

    // Whitespace-insensitive canonical form kept for diagnostics; the parity
    // comparison uses CanonicalMethodsText, which tolerates metadata row
    // reordering that ILAsm legitimately performs.
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

    private static string? HandlerKind(string trimmed)
    {
        foreach (var keyword in new[] { "catch", "finally", "fault", "filter" })
            if (trimmed == keyword || trimmed.StartsWith(keyword + " ", StringComparison.Ordinal))
                return keyword;
        return null;
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
            throw new InvalidOperationException($"Unexpected trailing token as class name in ILDasm text: {classLine}");
        return name;
    }

    private static string MethodName(string methodLine)
    {
        // A return modreq/modopt, pinvokeimpl, or function-pointer signature
        // can precede the method's parameter list. Generic constraints can
        // contain parentheses too. Find top-level groups, respecting quotes.
        var depth = 0;
        var genericDepth = 0;
        var bracketDepth = 0;
        var quote = '\0';
        string? name = null;
        for (var i = 0; i < methodLine.Length; i++)
        {
            var ch = methodLine[i];
            if (quote != '\0')
            {
                if (ch == '\\') i++;
                else if (ch == quote) quote = '\0';
                continue;
            }
            if (ch is '\'' or '"') { quote = ch; continue; }
            if (ch == '<') genericDepth++;
            else if (ch == '>') genericDepth--;
            else if (ch == '[') bracketDepth++;
            else if (ch == ']') bracketDepth--;
            else if (ch == '(')
            {
                if (depth == 0 && genericDepth == 0 && bracketDepth == 0)
                {
                    var prefix = methodLine[..i].TrimEnd();
                    var match = Regex.Match(prefix, @"(?<name>'(?:\\.|[^'\\])*'|[^\s()<>'\[\]]+)(?:<.*>)?$");
                    if (match.Success && match.Groups["name"].Value is not ("modreq" or "modopt" or "pinvokeimpl" or "*"))
                        name = match.Groups["name"].Value.Trim('\'');
                }
                depth++;
            }
            else if (ch == ')') depth--;
        }
        if (name is null || depth != 0 || genericDepth != 0 || bracketDepth != 0 || quote != '\0')
            throw new InvalidOperationException($"Malformed .method header in ILDasm text: {methodLine}");
        return name;
    }

    // Collapse formatting whitespace only outside quoted identifiers/strings.
    // Literal whitespace and escaped quotes remain part of the observation.
    private static string CanonicalText(string text)
    {
        var result = new StringBuilder();
        var quote = '\0';
        var pendingSpace = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quote != '\0')
            {
                result.Append(ch);
                if (ch == '\\' && i + 1 < text.Length) result.Append(text[++i]);
                else if (ch == quote) quote = '\0';
                continue;
            }
            if (char.IsWhiteSpace(ch)) { pendingSpace = true; continue; }
            if (pendingSpace && result.Length > 0) result.Append(' ');
            pendingSpace = false;
            result.Append(ch);
            if (ch is '\'' or '"') quote = ch;
        }
        return result.ToString();
    }

    private static string CurrentTypeName(List<string> typeStack) => typeStack.Count == 0
        ? "<Module>"
        : string.Join(".", typeStack);

    private sealed class IlDasmMethodTextBuilder
    {
        private readonly List<(int Offset, string Opcode, string Operand)> instructions = [];
        private readonly List<(int Offset, string Opcode)> callSites = [];
        private readonly List<IlDasmLineDirective> lineDirectives = [];
        private readonly List<IlDasmExceptionRegion> regions = [];
        // Blocks whose closing brace was seen but whose exclusive end (the
        // next instruction offset, or the code size at the method end) is not
        // yet known.
        private readonly List<Block> closedBlocks = [];

        public IlDasmMethodTextBuilder(string typeName, string methodName, string header)
        {
            TypeName = typeName;
            MethodName = methodName;
            Header = header;
        }

        private sealed class Block(string kind, int closingDepth)
        {
            public string Kind { get; } = kind;
            public int ClosingDepth { get; } = closingDepth;
            public int Start { get; set; } = -1;
            public int End { get; set; } = -1;
            public string? CatchType { get; set; }
            public Block? FilterBlock { get; set; }
            // The try block this handler completes, captured when the handler
            // keyword appears directly after the try's closing brace.
            public Block? PairedTry { get; set; }
        }

        public string TypeName { get; }
        public string MethodName { get; }
        public string Header { get; }
        public string LocalsSignature { get; private set; } = string.Empty;
        public bool HasInstructions => instructions.Count > 0;
        public int MaxStack { get; set; }
        public int LocalCount { get; set; }
        public int CodeSize { get; set; }
        public bool HasOpenBlocks => OpenBlocks.Count > 0;
        public bool HasPendingFilterHandler => closedBlocks.LastOrDefault(block => block.End == -1)?.Kind == "filter";
        public IlDasmLineDirective? PendingLine { get; set; }
        public bool SwitchTargetsPending { get; private set; }
        private List<Block> OpenBlocks { get; } = [];

        public void OpenBlock(string kind, int closingDepth)
        {
            if (kind != ".try")
                throw new InvalidOperationException($"Unsupported ILDasm block keyword: {kind}");
            OpenBlocks.Add(new Block(".try", closingDepth));
        }

        public void OpenHandlerBlock(string kind, string declaration, int closingDepth)
        {
            var handler = new Block(kind, closingDepth);
            if (kind == "catch")
            {
                var catchType = declaration["catch".Length..].Trim();
                if (catchType.Length == 0)
                    throw new InvalidOperationException($"Catch handler in {TypeName}.{MethodName} has no type identity.");
                handler.CatchType = CanonicalText(catchType);
            }
            // A later catch/filter shares its preceding handler's try. No
            // instruction occurs between the sibling handler blocks.
            var lastClosed = closedBlocks.LastOrDefault(block => block.End == -1);
            var pairedTry = lastClosed?.Kind == ".try" ? lastClosed : lastClosed?.PairedTry;
            if (pairedTry is null)
                throw new InvalidOperationException($"Handler '{kind}' in {TypeName}.{MethodName} follows no open try region.");
            handler.PairedTry = pairedTry;
            OpenBlocks.Add(handler);
        }

        public void OpenFilterHandlerBlock(int closingDepth)
        {
            var filter = closedBlocks.LastOrDefault(block => block.End == -1 && block.Kind == "filter");
            if (filter?.PairedTry is null)
                throw new InvalidOperationException($"Filter handler in {TypeName}.{MethodName} follows no filter region.");
            OpenBlocks.Add(new Block("filter-handler", closingDepth) { PairedTry = filter.PairedTry, FilterBlock = filter });
        }

        public void CloseBlock(int closingDepth)
        {
            var block = OpenBlocks[^1];
            // /linenum can include PDB lexical-scope braces inside EH blocks.
            // Closing such a scope must not close the enclosing try/handler.
            if (block.ClosingDepth != closingDepth)
                return;
            OpenBlocks.RemoveAt(OpenBlocks.Count - 1);
            closedBlocks.Add(block);
        }

        public void AddInstruction(int offset, string opcode, string operand, IlDasmLineDirective? pendingLine)
        {
            if (pendingLine is not null)
            {
                lineDirectives.Add(pendingLine with { Offset = offset });
                PendingLine = null;
            }
            // The first instruction at or after a closed block's brace fixes
            // that block's exclusive end on an instruction boundary; later
            // instructions must not move it.
            foreach (var block in closedBlocks)
                block.End = block.End == -1 ? offset : block.End;
            FinalizeCompletedClauses();
            foreach (var open in OpenBlocks)
                open.Start = open.Start < 0 ? offset : open.Start;
            if (SwitchTargetsPending)
                throw new InvalidOperationException($"Unterminated switch in {TypeName}.{MethodName}.");
            instructions.Add((offset, opcode, CanonicalText(operand)));
            if (CallFamilyOpcodes.Contains(opcode))
                callSites.Add((offset, opcode));
            SwitchTargetsPending = opcode == "switch" && !operand.Contains(')');
        }

        public void SetLocals(string directive)
        {
            LocalCount = CountLocalEntries(InnerLocals(directive));
            LocalsSignature = CanonicalText(directive);
        }

        public void AppendOperand(string continuation)
        {
            if (SwitchTargetsPending || continuation.StartsWith("IL_", StringComparison.Ordinal))
                throw new InvalidOperationException($"Unsupported instruction continuation: {continuation}");
            var last = instructions[^1];
            instructions[^1] = (last.Offset, last.Opcode, CanonicalText(last.Operand + " " + continuation));
        }

        // A clause is complete once its handler's end is known; ECMA-335
        // scopes both the try and the handler on instruction boundaries.
        private void FinalizeCompletedClauses()
        {
            foreach (var handler in closedBlocks
                         .Where(block => block.Kind is not (".try" or "filter") && block.PairedTry is not null && block.End != -1)
                         .ToArray())
            {
                regions.Add(new IlDasmExceptionRegion(
                    handler.FilterBlock is null ? handler.Kind : "filter",
                    handler.PairedTry!.Start,
                    handler.PairedTry.End,
                    handler.Start,
                    handler.End,
                    handler.CatchType,
                    handler.FilterBlock?.Start));
                closedBlocks.Remove(handler.PairedTry);
                if (handler.FilterBlock is not null)
                    closedBlocks.Remove(handler.FilterBlock);
                closedBlocks.Remove(handler);
            }
        }

        public void AppendSwitchTargets(string continuation)
        {
            if (instructions.Count == 0)
                return;
            var last = instructions[^1];
            var targets = Regex.Matches(continuation, @"IL_([0-9a-f]{4,8})")
                .Select(match => match.Groups[1].Value)
                .ToArray();
            instructions[^1] = (last.Offset, last.Opcode, last.Operand + ":" + string.Join(",", targets));
            if (continuation.Contains(')'))
                SwitchTargetsPending = false;
        }

        public IlDasmMethodText Build()
        {
            if (OpenBlocks.Count > 0)
                throw new InvalidOperationException($"Unclosed exception block '{OpenBlocks[^1].Kind}' in {TypeName}.{MethodName}.");
            if (SwitchTargetsPending)
                throw new InvalidOperationException($"Unterminated switch in {TypeName}.{MethodName}.");
            foreach (var block in closedBlocks)
                block.End = block.End == -1 ? CodeSize : block.End;
            FinalizeCompletedClauses();
            if (closedBlocks.Count > 0)
                throw new InvalidOperationException($"Unpaired exception block '{closedBlocks[0].Kind}' in {TypeName}.{MethodName}.");
            return new IlDasmMethodText(
                TypeName,
                MethodName,
                MaxStack,
                LocalCount,
                CodeSize,
                regions.OrderBy(region => region.TryStart, Comparer<int>.Default)
                    .ThenBy(region => region.HandlerStart, Comparer<int>.Default)
                    .ToArray(),
                instructions.ToArray(),
                callSites.ToArray(),
                lineDirectives.ToArray()) { Header = Header, LocalsSignature = LocalsSignature };
        }
    }
}
