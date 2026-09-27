using TraceMap.Core;

if (args.Length == 1 && args[0] == "--self-test")
{
    var body = new IlBodyObservation("0x06000001", "identity", 1, "instructions", "opcodes", 0,
        "locals", 0, "regions", "8", false, "body", "digest", [], ["0:0:ldc.i4.s:i:3"]);
    var left = new IlBodyEvidenceExtractor.IlReaderResult("assembly", "module", "mvid", [body]);
    var right = new IlBodyEvidenceExtractor.IlReaderResult("assembly", "module", "mvid",
        [body with { Calls = [new IlCallObservation(0, "call", "methoddef", "0x06000002", "target")] }]);
    var result = Classify(left, right);
    var operand = Classify(left, new IlBodyEvidenceExtractor.IlReaderResult("assembly", "module", "mvid",
        [body with { InstructionsSha256 = "different", DiagnosticInstructions = ["0:0:ldc.i4.s:i:4"] }]));
    var passed = result.DisputedBodies == 1 && result.Calls == 1 && result.MissingBodies == 0
        && operand.OperandOnly == 1 && operand.FirstDifference == "operand" && operand.FirstOperandKind == "integer";
    Console.WriteLine(passed
        ? "ilReaderProbeSelfTest=pass" : "ilReaderProbeSelfTest=fail");
    return passed ? 0 : 1;
}
if (args.Length != 2 || !int.TryParse(args[1], out var maxText) || maxText < 71 || maxText > 65_536)
{
    Console.WriteLine("ilReaderProbeStatus=invalid-arguments");
    return 2;
}
try
{
    var file = new FileInfo(args[0]);
    if (!file.Exists || file.Length > 67_108_864)
    {
        Console.WriteLine("ilReaderProbeStatus=input-unavailable-or-over-limit");
        return 2;
    }
    var bytes = File.ReadAllBytes(file.FullName);
    var limits = new IlBodyLimits(MaxTextLength: maxText);
    var budget = new IlBodyEvidenceExtractor.IlWorkBudget(limits.MaxTotalWorkUnits);
    var srm = IlBodyEvidenceExtractor.ReadSystemReflectionMetadataBodies(bytes, limits, budget, CancellationToken.None, true);
    var cecil = IlBodyEvidenceExtractor.ReadCecilBodies(bytes, limits, budget, CancellationToken.None, true);
    var shape = Classify(cecil, srm);
    Console.WriteLine($"ilReaderProbeStatus={(shape.Assembly + shape.DisputedBodies == 0 ? "agreed" : "disputed")}");
    Console.WriteLine($"ilReaderProbeAssembly={shape.Assembly}");
    Console.WriteLine($"ilReaderProbeDisputedBodies={shape.DisputedBodies}");
    Console.WriteLine($"ilReaderProbeMissingBodies={shape.MissingBodies}");
    Console.WriteLine($"ilReaderProbeInstructions={shape.Instructions}");
    Console.WriteLine($"ilReaderProbeOpcodeStreams={shape.OpcodeStreams}");
    Console.WriteLine($"ilReaderProbeOperandOnly={shape.OperandOnly}");
    Console.WriteLine($"ilReaderProbeFirstDifference={shape.FirstDifference}");
    Console.WriteLine($"ilReaderProbeFirstOperandKind={shape.FirstOperandKind}");
    Console.WriteLine($"ilReaderProbeCalls={shape.Calls}");
    Console.WriteLine($"ilReaderProbeLocals={shape.Locals}");
    Console.WriteLine($"ilReaderProbeRegions={shape.Regions}");
    Console.WriteLine($"ilReaderProbeStackOrInit={shape.StackOrInit}");
    Console.WriteLine($"ilReaderProbeIdentityOnly={shape.IdentityOnly}");
    return 0;
}
catch (IlBodyEvidenceExtractor.IlEvidenceException error)
{
    var kind = error.GapKind is "IlTextLimitExceeded" or "IlTotalWorkLimitExceeded" or
        "IlBodyCountLimitExceeded" or "IlInstructionLimitExceeded" or "IlLocalLimitExceeded" or
        "IlExceptionRegionLimitExceeded" or "IlOperandEncodingUnsupported" or "MalformedIlBody"
        ? error.GapKind : "other-gap";
    Console.WriteLine($"ilReaderProbeStatus={kind}");
    return 0;
}
catch
{
    Console.WriteLine("ilReaderProbeStatus=unavailable");
    return 0;
}

static Shape Classify(IlBodyEvidenceExtractor.IlReaderResult left, IlBodyEvidenceExtractor.IlReaderResult right)
{
    var shape = new Shape();
    if (left.AssemblyIdentity != right.AssemblyIdentity || left.ModuleName != right.ModuleName ||
        left.ModuleMvid != right.ModuleMvid)
        shape.Assembly++;
    var first = left.Bodies.ToDictionary(item => item.MetadataToken, StringComparer.Ordinal);
    var second = right.Bodies.ToDictionary(item => item.MetadataToken, StringComparer.Ordinal);
    foreach (var token in first.Keys.Concat(second.Keys).Distinct(StringComparer.Ordinal))
    {
        if (!first.TryGetValue(token, out var a) || !second.TryGetValue(token, out var b))
        {
            shape.DisputedBodies++;
            shape.MissingBodies++;
            continue;
        }
        var instructions = a.InstructionCount != b.InstructionCount || a.InstructionsSha256 != b.InstructionsSha256;
        var opcodeStreams = a.OpcodesSha256 != b.OpcodesSha256;
        var calls = a.Calls.Count != b.Calls.Count || !a.Calls.SequenceEqual(b.Calls);
        var locals = a.LocalCount != b.LocalCount || a.LocalsSha256 != b.LocalsSha256;
        var regions = a.ExceptionRegionCount != b.ExceptionRegionCount || a.ExceptionRegionsSha256 != b.ExceptionRegionsSha256;
        var stackOrInit = a.MaxStack != b.MaxStack || a.InitLocals != b.InitLocals;
        var identity = a.BodyIdentity != b.BodyIdentity;
        if (!instructions && !calls && !locals && !regions && !stackOrInit && !identity)
            continue;
        shape.DisputedBodies++;
        if (instructions) shape.Instructions++;
        if (instructions && opcodeStreams) shape.OpcodeStreams++;
        if (instructions && !opcodeStreams) shape.OperandOnly++;
        if (instructions && shape.FirstDifference == "none") ClassifyFirstInstructionDifference(a, b, shape);
        if (calls) shape.Calls++;
        if (locals) shape.Locals++;
        if (regions) shape.Regions++;
        if (stackOrInit) shape.StackOrInit++;
        if (identity && !instructions && !calls && !locals && !regions && !stackOrInit) shape.IdentityOnly++;
    }
    return shape;
}

static void ClassifyFirstInstructionDifference(IlBodyObservation a, IlBodyObservation b, Shape shape)
{
    if (a.InstructionCount != b.InstructionCount)
    {
        shape.FirstDifference = "instruction-count";
        return;
    }
    if (a.DiagnosticInstructions is null || b.DiagnosticInstructions is null)
    {
        shape.FirstDifference = "unavailable";
        return;
    }
    for (var index = 0; index < Math.Min(a.DiagnosticInstructions.Count, b.DiagnosticInstructions.Count); index++)
    {
        if (a.DiagnosticInstructions[index] == b.DiagnosticInstructions[index]) continue;
        var first = a.DiagnosticInstructions[index].Split(':', 4);
        var second = b.DiagnosticInstructions[index].Split(':', 4);
        if (first.Length != 4 || second.Length != 4)
        {
            shape.FirstDifference = "unavailable";
            return;
        }
        if (first[1] != second[1]) shape.FirstDifference = "offset";
        else if (first[2] != second[2]) shape.FirstDifference = "opcode";
        else
        {
            shape.FirstDifference = "operand";
            var kind = first[3].Split(':', 2)[0];
            shape.FirstOperandKind = kind switch
            {
                "br" => "branch", "sw" => "switch", "i" => "integer", "r" => "real",
                "str" => "string", "v" => "variable", "m" => "method", "t" => "type",
                "field" => "field", "sig" => "signature", "-" => "none", _ => "other"
            };
        }
        return;
    }
    shape.FirstDifference = "unavailable";
}

file sealed class Shape
{
    public int Assembly;
    public int DisputedBodies;
    public int MissingBodies;
    public int Instructions;
    public int OpcodeStreams;
    public int OperandOnly;
    public string FirstDifference = "none";
    public string FirstOperandKind = "none";
    public int Calls;
    public int Locals;
    public int Regions;
    public int StackOrInit;
    public int IdentityOnly;
}
