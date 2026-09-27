using TraceMap.Core;

if (args.Length == 1 && args[0] == "--self-test")
{
    var body = new IlBodyObservation("0x06000001", "identity", 1, "instructions", "opcodes", 0,
        "locals", 0, "regions", "8", false, "body", "digest", []);
    var left = new IlBodyEvidenceExtractor.IlReaderResult("assembly", "module", "mvid", [body]);
    var right = new IlBodyEvidenceExtractor.IlReaderResult("assembly", "module", "mvid",
        [body with { Calls = [new IlCallObservation(0, "call", "methoddef", "0x06000002", "target")] }]);
    var result = Classify(left, right);
    Console.WriteLine(result.DisputedBodies == 1 && result.Calls == 1 && result.MissingBodies == 0
        ? "ilReaderProbeSelfTest=pass" : "ilReaderProbeSelfTest=fail");
    return result.DisputedBodies == 1 && result.Calls == 1 && result.MissingBodies == 0 ? 0 : 1;
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
    var srm = IlBodyEvidenceExtractor.ReadSystemReflectionMetadataBodies(bytes, limits, budget, CancellationToken.None);
    var cecil = IlBodyEvidenceExtractor.ReadCecilBodies(bytes, limits, budget, CancellationToken.None);
    var shape = Classify(cecil, srm);
    Console.WriteLine($"ilReaderProbeStatus={(shape.Assembly + shape.DisputedBodies == 0 ? "agreed" : "disputed")}");
    Console.WriteLine($"ilReaderProbeAssembly={shape.Assembly}");
    Console.WriteLine($"ilReaderProbeDisputedBodies={shape.DisputedBodies}");
    Console.WriteLine($"ilReaderProbeMissingBodies={shape.MissingBodies}");
    Console.WriteLine($"ilReaderProbeInstructions={shape.Instructions}");
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
        var calls = a.Calls.Count != b.Calls.Count || !a.Calls.SequenceEqual(b.Calls);
        var locals = a.LocalCount != b.LocalCount || a.LocalsSha256 != b.LocalsSha256;
        var regions = a.ExceptionRegionCount != b.ExceptionRegionCount || a.ExceptionRegionsSha256 != b.ExceptionRegionsSha256;
        var stackOrInit = a.MaxStack != b.MaxStack || a.InitLocals != b.InitLocals;
        var identity = a.BodyIdentity != b.BodyIdentity;
        if (!instructions && !calls && !locals && !regions && !stackOrInit && !identity)
            continue;
        shape.DisputedBodies++;
        if (instructions) shape.Instructions++;
        if (calls) shape.Calls++;
        if (locals) shape.Locals++;
        if (regions) shape.Regions++;
        if (stackOrInit) shape.StackOrInit++;
        if (identity && !instructions && !calls && !locals && !regions && !stackOrInit) shape.IdentityOnly++;
    }
    return shape;
}

file sealed class Shape
{
    public int Assembly;
    public int DisputedBodies;
    public int MissingBodies;
    public int Instructions;
    public int Calls;
    public int Locals;
    public int Regions;
    public int StackOrInit;
    public int IdentityOnly;
}
