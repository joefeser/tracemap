using System.Text.Json;
using TraceMap.Core;
using TraceMap.Combine;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

[Collection("Git metadata sensitive")]
public sealed class LazyConstructorLoggingTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Property_profile_dynamic_lookup_is_distinct_from_literal_audit(bool compiledOnly)
    {
        var repo = FindRepo();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var bin = Path.Combine(repo, "samples", "fixture-build", "lazy-constructor", "bin", configuration, "net48");
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(repo, "samples", "messy-dotnet-workspace", "vb-lazy-constructor"),
            Path.Combine(temp.Path, "scan"), CompiledInputPaths: [Path.Combine(bin, "PublicLazy.Website.dll"),
                Path.Combine(bin, "PublicLazy.Framework.dll")], IlBodyEvidence: true));
        AssertScanIdentity(scan);
        var entry = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.Properties.GetValueOrDefault("metadataName") == "Profile_Click");
        var index = Path.Combine(temp.Path, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        var composition = await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["synthetic-profile"]));
        var source = Assert.Single(composition.Sources);
        var report = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(
            new CombinedDependencyPathOptions(combined, temp.Path, ToSurface: "database-api", MaxDepth: 20, MaxPaths: 256)
            { CompiledOnly = compiledOnly, ExactFromSymbol = true, MaxTraversalWork = 100_000 },
            [new(source.SourceIndexId, source.ScanId, source.CommitSha, entry.TargetSymbol!)], combinedIndex: true);
        bool Method(CombinedPathNode node, string name) => node.SymbolId?.Contains($"|method:{name.Length}:{name}|", StringComparison.Ordinal) == true;
        Assert.Equal(3, report.Paths.Count);
        var lookup = Assert.Single(report.Paths, path => path.Nodes.Any(node => Method(node, "GetEmail")));
        Assert.Equal("SqlCommand.ExecuteScalar", lookup.Nodes.Last().SurfaceName);
        var route = lookup.Nodes.ToList();
        var getter = route.FindIndex(node => Method(node, "get_EmployeeInfo"));
        var constructor = route.FindIndex(node => node.SymbolId?.Contains("names:15:ProfileEmployee|", StringComparison.Ordinal) == true
            && node.SymbolId.Contains("|constructor:5:.ctor|", StringComparison.Ordinal));
        Assert.True(getter >= 0 && constructor > getter);
        Assert.True(route.FindIndex(node => Method(node, "GetProfile")) > constructor);
        Assert.True(route.FindIndex(node => Method(node, "GetEmail")) > route.FindIndex(node => Method(node, "GetProfile")));
        var binding = lookup.Nodes.Last().CommandBinding!;
        Assert.Equal("1", binding.CommandTypeFromPath!.Origin.Identity);
        var text = binding.CommandTextFromPath!;
        Assert.Equal("unresolved-operand", text.State);
        Assert.Equal("call-result", text.Origin.Kind);
        var producerBody = Assert.Single(scan.Facts, fact => $"{source.SourceIndexId}:{fact.FactId}" == text.OriginBodyFactId);
        var producer = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved
            && fact.Properties.GetValueOrDefault("ilBodyFactId") == producerBody.FactId
            && fact.Properties.GetValueOrDefault("ilOffset") == text.Origin.Identity);
        Assert.Contains("Concat", producer.Properties["targetIdentity"], StringComparison.Ordinal);
        Assert.Contains("IlCommandOperandValueUnresolved", text.Gaps);
        Assert.Contains("IlCommandReturnTargetMissingOrAmbiguous", text.Gaps);
        Assert.Contains("IlCommandReturnTargetEdgeMissing", text.Gaps);
        Assert.Empty(text.ReturnSteps ?? []);
        var audit = Assert.Single(report.Paths, path => path.Nodes.Any(node => Method(node, "WriteAudit")));
        Assert.Equal("4", audit.Nodes.Last().CommandBinding!.CommandTypeFromPath!.Origin.Identity);
        Assert.Equal("method-local-constant", audit.Nodes.Last().CommandBinding!.CommandTextFromPath!.State);
        Assert.DoesNotContain(lookup.Nodes, node => Method(node, "WriteAudit"));
        Assert.Single(report.Paths, path => path.Nodes.Last().SurfaceName == "DbDataAdapter.Fill");
        Assert.DoesNotContain(report.Gaps, gap => gap.GapKind == "TruncatedByLimit");
        Assert.DoesNotContain("SELECT Email", JsonSerializer.Serialize(report), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Deep_projectless_property_constructor_logging_retains_routes_and_return_value_gap(bool compiledOnly)
    {
        var repo = FindRepo();
        var source = Path.Combine(repo, "samples", "messy-dotnet-workspace", "vb-lazy-constructor");
        Assert.Empty(Directory.GetFiles(source, "*.*proj", SearchOption.AllDirectories));
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var bin = Path.Combine(repo, "samples", "fixture-build", "lazy-constructor", "bin", configuration, "net48");
        var assemblies = new[] { "PublicLazy.Website.dll", "PublicLazy.Framework.dll" }.Select(name => Path.Combine(bin, name)).ToArray();
        using (var provider = Mono.Cecil.AssemblyDefinition.ReadAssembly(assemblies[1]))
        {
            var logger = Assert.Single(provider.MainModule.Types, type => type.FullName == "PublicLazy.Framework.PublicLog");
            var literal = Assert.Single(logger.Methods, method => method.Name == "LiteralText");
            var direct = Assert.Single(logger.Methods, method => method.Name == "InsertLiteral");
            var returnedString = Assert.Single(literal.Body.Instructions,
                instruction => instruction.OpCode.Code == Mono.Cecil.Cil.Code.Ldstr).Operand;
            Assert.Equal(Assert.Single(direct.Body.Instructions,
                instruction => instruction.OpCode.Code == Mono.Cecil.Cil.Code.Ldstr).Operand, returnedString);
            Assert.Contains(literal.Body.Instructions, instruction => instruction.OpCode.Code == Mono.Cecil.Cil.Code.Ret);
            Assert.All(literal.Body.Instructions, instruction => Assert.Contains(instruction.OpCode.Code,
                new[] { Mono.Cecil.Cil.Code.Nop, Mono.Cecil.Cil.Code.Ldstr, Mono.Cecil.Cil.Code.Stloc_0,
                    Mono.Cecil.Cil.Code.Ldloc_0, Mono.Cecil.Cil.Code.Br_S, Mono.Cecil.Cil.Code.Br, Mono.Cecil.Cil.Code.Ret }));
        }
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(source, Path.Combine(temp.Path, "scan"),
            CompiledInputPaths: assemblies, IlBodyEvidence: true));
        AssertScanIdentity(scan);
        var entry = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.TargetSymbol!.Contains("LazyOverview|", StringComparison.Ordinal)
            && fact.TargetSymbol.Contains("|method:10:Load_Click|", StringComparison.Ordinal));
        var index = Path.Combine(temp.Path, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        var composition = await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["public-lazy-corpus"]));
        var origin = Assert.Single(composition.Sources);
        var selector = new CombinedPathSymbolRoot(origin.SourceIndexId, origin.ScanId, origin.CommitSha, entry.TargetSymbol!);
        async Task<CombinedDependencyPathReport> Query(string? surface) =>
            await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(
                new CombinedDependencyPathOptions(combined, temp.Path, ToSurface: "database-api", SurfaceName: surface,
                    MaxDepth: 20, MaxPaths: 256)
                { CompiledOnly = compiledOnly, ExactFromSymbol = true, MaxTraversalWork = 100_000 },
                [selector], combinedIndex: true);

        var broad = await Query(null);
        var scalar = broad.Paths.Where(path => path.Nodes.Last().SurfaceName == "SqlCommand.ExecuteScalar").ToArray();
        Assert.Equal(6, broad.Paths.Count);
        Assert.Equal(5, scalar.Length);
        bool Type(CombinedPathNode node, string name) => node.SymbolId?.Contains($"names:{name.Length}:{name}|", StringComparison.Ordinal) == true;
        bool Method(CombinedPathNode node, string name) => node.SymbolId?.Contains($"|method:{name.Length}:{name}|", StringComparison.Ordinal) == true;
        bool Constructor(CombinedPathNode node, string name) => Type(node, name)
            && node.SymbolId!.Contains("|constructor:5:.ctor|", StringComparison.Ordinal);
        var logging = scalar.Where(path => path.Nodes.Any(node => Method(node, "InsertLog"))).ToArray();
        Assert.Equal(3, logging.Length);
        Console.WriteLine($"propertyCorpus.compiledOnly={compiledOnly};paths={broad.Paths.Count};scalar={scalar.Length};logging={logging.Length};work={broad.Summary.TraversalWorkUnits}");
        Assert.All(logging, path =>
        {
            Assert.Contains(path.Nodes, node => Constructor(node, "SyntheticChoices"));
            Assert.Contains(path.Nodes, node => Method(node, "get_EmployeeInfo"));
            Assert.Contains(path.Nodes, node => Constructor(node, "SyntheticEmployee"));
            Assert.DoesNotContain(path.Nodes, node => Type(node, "UnrelatedLog"));
            Assert.DoesNotContain(path.Nodes, node => Method(node, "BuildText"));
            var route = path.Nodes.ToList();
            Assert.True(route.FindIndex(node => Constructor(node, "SyntheticChoices")) < route.FindIndex(node => Method(node, "get_EmployeeInfo")));
            Assert.True(route.FindIndex(node => Method(node, "get_EmployeeInfo")) < route.FindIndex(node => Constructor(node, "SyntheticEmployee")));
            Assert.True(route.FindIndex(node => Constructor(node, "SyntheticEmployee")) < route.FindIndex(node => Method(node, "InsertLog")));
            var binding = Assert.IsType<CompiledCommandConfigurationCandidate>(path.Nodes.Last().CommandBinding);
            Assert.Equal("argument-slot", binding.CommandTextOrigin.Kind);
            var text = Assert.IsType<CompiledCommandPathValueBinding>(binding.CommandTextFromPath);
            Assert.Equal("unresolved-operand", text.State);
            Assert.Equal("call-result", text.Origin.Kind);
            var producerBody = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared
                && $"{origin.SourceIndexId}:{fact.FactId}" == text.OriginBodyFactId);
            var producerCall = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved
                && fact.Properties.GetValueOrDefault("ilBodyFactId") == producerBody.FactId
                && fact.Properties.GetValueOrDefault("ilOffset") == text.Origin.Identity);
            Assert.Contains("|method:9:BuildText|", producerCall.Properties["targetIdentity"], StringComparison.Ordinal);
            Assert.Single(text.Steps);
            Assert.Contains("IlCommandOperandValueUnresolved", text.Gaps);
            Assert.Contains("IlCommandVirtualDispatchUnproven", text.Gaps);
            Assert.Equal("method-local-constant", binding.CommandTypeFromPath!.State);
            Assert.Equal("1", binding.CommandTypeFromPath.Origin.Identity);
            Assert.Matches("^[0-9a-f]{64}$", text.GeneratorSha256);
            Assert.Matches("^[0-9a-f]{64}$", text.BoundedInputSha256);
        });
        Assert.Contains(logging, path => path.Nodes.Any(node => Constructor(node, "SyntheticPreferences")));
        Assert.Contains(logging, path => !path.Nodes.Any(node => Constructor(node, "SyntheticPreferences")));
        Assert.Contains(scalar, path => path.Nodes.Any(node => Method(node, "InsertLiteral"))
            && path.Nodes.Last().CommandBinding?.CommandTextFromPath?.State == "constant-on-encoded-call-path");
        var literalMethod = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.TargetSymbol!.Contains("|method:11:LiteralText|", StringComparison.Ordinal));
        var literalBody = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared
            && fact.Properties.GetValueOrDefault("compiledFactId") == literalMethod.FactId);
        var literalReturns = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlReturnValuesObserved
            && fact.Properties.GetValueOrDefault("ilBodyFactId") == literalBody.FactId);
        Assert.Equal("return-operands-candidate", literalReturns.Properties["valueState"]);
        var retainedReturn = Assert.Single(JsonSerializer.Deserialize<IlReturnValueObservation[]>(literalReturns.Properties["returnOrigins"])!);
        var directText = Assert.Single(scalar, path => path.Nodes.Any(node => Method(node, "InsertLiteral")))
            .Nodes.Last().CommandBinding!.CommandTextFromPath!.Origin;
        Assert.Equal(directText.Kind, retainedReturn.Origin.Kind);
        Assert.Equal(directText.Identity, retainedReturn.Origin.Identity);
        // Unlike BuildText(message), this producer returns exactly the same
        // constant as the direct control. Its return evidence is separate
        // from call-stack edges and does not prove virtual endpoint dispatch.
        var returnedLiteral = Assert.Single(scalar, path => path.Nodes.Any(node => Method(node, "InsertReturnedLiteral")));
        var returnedText = returnedLiteral.Nodes.Last().CommandBinding!.CommandTextFromPath!;
        Assert.Equal("constant-on-encoded-call-path", returnedText.State);
        Assert.Equal(directText, returnedText.Origin);
        var returnStep = Assert.Single(returnedText.ReturnSteps!);
        Assert.Equal($"{origin.SourceIndexId}:{literalReturns.FactId}", returnStep.ReturnFactId);
        Assert.Equal($"{origin.SourceIndexId}:{literalBody.FactId}", returnedText.OriginBodyFactId);
        var returnedCall = Assert.Single(scan.Facts, fact => $"{origin.SourceIndexId}:{fact.FactId}" == returnStep.ProducerCallFactId);
        Assert.Contains("|method:11:LiteralText|", returnedCall.Properties["targetIdentity"], StringComparison.Ordinal);
        Assert.Contains("IlCommandVirtualDispatchUnproven", returnedText.Gaps);
        Assert.DoesNotContain(returnedLiteral.Nodes, node => Method(node, "LiteralText"));
        var fill = await Query("DbDataAdapter.Fill");
        Assert.Single(fill.Paths);
        Assert.All(fill.Paths, path =>
        {
            Assert.Equal("DbDataAdapter.Fill", path.Nodes.Last().SurfaceName);
            Assert.DoesNotContain(path.Nodes, node => Method(node, "InsertLog"));
            Assert.Equal("method-local-constant", path.Nodes.Last().CommandBinding!.CommandTextFromPath!.State);
        });
        Assert.DoesNotContain("SELECT LEN('", JsonSerializer.Serialize(broad), StringComparison.Ordinal);
        Assert.DoesNotContain(broad.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Reason is "path" or "work");
        Assert.InRange(broad.Summary.TraversalWorkUnits!.Value, 1, 100_000);
    }

    [Theory]
    [InlineData("InsertReturnedLiteral", "none", null)]
    [InlineData("InsertForwardedLiteral", "none", null)]
    [InlineData("InsertReturnedLiteralTwoHops", "none", null)]
    [InlineData("InsertComposedTextTwoHops", "none", "IlCommandReturnTargetEdgeMissing")]
    [InlineData("InsertReturnedLiteral", "target-missing", "IlCommandReturnTargetEdgeMissing")]
    [InlineData("InsertReturnedLiteral", "target-ambiguous", "IlCommandReturnTargetEdgeMissing")]
    [InlineData("InsertRecursiveText", "none", "IlCommandReturnCycle")]
    [InlineData("InsertVirtualText", "none", "IlCommandReturnVirtualDispatchUnproven")]
    [InlineData("InsertReturnedLiteral", "missing", "IlCommandReturnEvidenceMissingOrAmbiguous")]
    [InlineData("InsertReturnedLiteral", "ambiguous", "IlCommandReturnEvidenceMissingOrAmbiguous")]
    [InlineData("InsertReturnedLiteral", "changed", "IlCommandReturnProvenanceUnavailable")]
    [InlineData("InsertReturnedLiteral", "malformed", "IlCommandReturnEvidenceMalformed")]
    [InlineData("InsertReturnedLiteral", "count", "IlCommandReturnEvidenceUnavailable")]
    [InlineData("InsertReturnedLiteral", "conflict", "IlCommandReturnValuesDisagree")]
    [InlineData("InsertReturnedLiteral", "work", "IlCommandReturnWorkLimit")]
    public async Task Deep_projectless_return_value_projection_requires_exact_bounded_evidence(string entryName, string mutation, string? gap)
    {
        var repo = FindRepo();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var bin = Path.Combine(repo, "samples", "fixture-build", "lazy-constructor", "bin", configuration, "net48");
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(repo, "samples", "messy-dotnet-workspace", "vb-lazy-constructor"),
            Path.Combine(temp.Path, "scan"), CompiledInputPaths: [Path.Combine(bin, "PublicLazy.Framework.dll")], IlBodyEvidence: true));
        AssertScanIdentity(scan);
        var entry = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.Properties.GetValueOrDefault("metadataName") == entryName);
        var literal = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.Properties.GetValueOrDefault("metadataName") == "LiteralText");
        var body = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared
            && fact.Properties.GetValueOrDefault("compiledFactId") == literal.FactId);
        var summary = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlReturnValuesObserved
            && fact.Properties.GetValueOrDefault("ilBodyFactId") == body.FactId);
        var facts = scan.Facts.ToList();
        if (mutation == "target-missing") facts.Remove(literal);
        else if (mutation == "target-ambiguous") facts.Add(literal with { FactId = literal.FactId + "-competitor" });
        else if (mutation == "missing") facts.RemoveAll(fact => fact.FactType == FactTypes.ManagedIlReturnValuesObserved);
        else if (mutation == "ambiguous") facts.Add(summary with { FactId = summary.FactId + "-competitor" });
        else if (mutation != "none")
        {
            var properties = new Dictionary<string, string>(summary.Properties);
            if (mutation == "changed") properties["ilBoundedInputSha256"] = new string('f', 64);
            if (mutation == "malformed") properties["returnOrigins"] = "[";
            if (mutation == "count") properties["returnCount"] = "257";
            if (mutation == "conflict")
            {
                var value = Assert.Single(JsonSerializer.Deserialize<IlReturnValueObservation[]>(properties["returnOrigins"])!);
                properties["returnOrigins"] = JsonSerializer.Serialize(new[] { value, value with { Offset = value.Offset + 1,
                    Origin = new("constant-string-hash", "str:1:" + new string('a', 64)) } });
                properties["returnCount"] = "2";
            }
            if (mutation == "work")
            {
                var value = Assert.Single(JsonSerializer.Deserialize<IlReturnValueObservation[]>(properties["returnOrigins"])!);
                properties["returnOrigins"] = JsonSerializer.Serialize(Enumerable.Range(0, 64)
                    .Select(offset => value with { Offset = offset }).ToArray());
                properties["returnCount"] = "64";
            }
            facts[facts.IndexOf(summary)] = summary with { Properties = properties };
        }
        var index = Path.Combine(temp.Path, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, facts);
        var composition = await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["return-fixture"]));
        var source = Assert.Single(composition.Sources);
        var report = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(
            new CombinedDependencyPathOptions(combined, temp.Path, ToSurface: "database-api", SurfaceName: "SqlCommand.ExecuteScalar", MaxDepth: 20)
            { CompiledOnly = true, ExactFromSymbol = true, MaxTraversalWork = 10_000 },
            [new(source.SourceIndexId, source.ScanId, source.CommitSha, entry.TargetSymbol!)], combinedIndex: true);
        var text = Assert.Single(report.Paths).Nodes.Last().CommandBinding!.CommandTextFromPath!;
        // Duplicate declarations are refused by graph admission before return
        // projection, so no candidate call edge is admitted to the resolver.
        if (mutation == "target-ambiguous") Assert.Contains(report.Gaps, item => item.GapKind == "CompiledIlTargetAmbiguous");
        if (entryName.EndsWith("TwoHops", StringComparison.Ordinal)) Assert.Equal(2, text.Steps.Count);
        var memoryReport = await CombinedDependencyPathReporter.BuildReportAsync(
            new CombinedDependencyPathOptions(combined, temp.Path, FromSymbol: entry.TargetSymbol,
                ToSurface: "database-api", SurfaceName: "SqlCommand.ExecuteScalar", MaxDepth: 20)
            { CompiledOnly = true, ExactFromSymbol = true, MaxTraversalWork = 10_000 });
        var memoryText = Assert.Single(memoryReport.Paths).Nodes.Last().CommandBinding!.CommandTextFromPath!;
        Assert.Equal(JsonSerializer.Serialize(text), JsonSerializer.Serialize(memoryText));
        if (gap is null)
        {
            Assert.Equal("constant-on-encoded-call-path", text.State);
            Assert.Equal("constant-string-hash", text.Origin.Kind);
            Assert.Equal(entryName == "InsertForwardedLiteral" ? 2 : 1, text.ReturnSteps!.Count);
        }
        else
        {
            Assert.Equal("unresolved-operand", text.State);
            Assert.Contains(gap, text.Gaps);
            if (mutation.StartsWith("target-", StringComparison.Ordinal) || entryName == "InsertComposedTextTwoHops")
            {
                Assert.Equal("call-result", text.Origin.Kind);
                Assert.Empty(text.ReturnSteps ?? []);
                Assert.Contains("IlCommandReturnTargetMissingOrAmbiguous", text.Gaps);
                Assert.Contains("IlCommandOperandValueUnresolved", text.Gaps);
            }
        }
        Assert.DoesNotContain("SELECT ", JsonSerializer.Serialize(report), StringComparison.Ordinal);
    }

    private static void AssertScanIdentity(ScanResult scan)
    {
        var sha = scan.Manifest.CommitSha;
        Assert.True(sha.Length == 40 && sha.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'),
            $"LAZY_CORPUS_GIT_IDENTITY_UNAVAILABLE;commitLength={sha.Length};knownGapCount={scan.Manifest.KnownGaps.Count};" +
            "selected-root reporting requires a real Git commit; inspect the bounded Git probe/environment, not SQL route resolution.");
        Assert.False(string.IsNullOrWhiteSpace(scan.Manifest.ScanId));
    }

    private static string FindRepo()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
        throw new InvalidOperationException("Repository unavailable.");
    }
}
