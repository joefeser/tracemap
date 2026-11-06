using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class SqlWrapperRouteTests
{
    [Theory]
    [InlineData("Direct", true, "none")]
    [InlineData("LocalWithFinally", true, "none")]
    [InlineData("CompilerSelected", true, "none")]
    [InlineData("FourPieces", true, "none")]
    [InlineData("Nested", true, "none")]
    [InlineData("ObjectArrayRefused", false, "none")]
    [InlineData("Direct", false, "provenance")]
    [InlineData("Direct", false, "malformed")]
    [InlineData("Direct", false, "oversize")]
    [InlineData("Direct", false, "unknown")]
    public async Task Csharp_compiler_array_shapes_retain_only_string_compositions(string method, bool supported, string tamper)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        var sample = Path.Combine(root!.FullName, "samples/sql-database-matrix/array-build");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(sample, Path.Combine(temp.Path, "scan"),
            CompiledInputPaths: [Path.Combine(sample, "bin", configuration, "net472", "StringArrayCases.dll")], IlBodyEvidence: true));
        var entry = Assert.Single(scan.Facts, f => f.FactType == FactTypes.ManagedMethodDeclared
            && f.Properties.GetValueOrDefault("metadataName") == method);
        var facts = scan.Facts.ToArray();
        if (tamper != "none")
        {
            var body = Assert.Single(facts, f => f.FactType == FactTypes.ManagedIlBodyDeclared
                && f.Properties.GetValueOrDefault("compiledFactId") == entry.FactId);
            var operand = Assert.Single(facts, f => f.FactType == FactTypes.ManagedIlCallValuesObserved
                && f.Properties.GetValueOrDefault("ilBodyFactId") == body.FactId
                && f.Properties["argumentOrigins"].Contains("string-array-elements", StringComparison.Ordinal));
            var properties = new Dictionary<string, string>(operand.Properties);
            if (tamper == "provenance") properties["ilBoundedInputSha256"] = new string('f', 64);
            else
            {
                var inner = tamper == "malformed" ? "not-json" : JsonSerializer.Serialize(Enumerable.Repeat(
                    new CompiledCommandOperandOrigin("unknown", ""), tamper == "oversize" ? 17 : 1));
                properties["argumentOrigins"] = JsonSerializer.Serialize(new[] { new CompiledCommandOperandOrigin("string-array-elements", inner) });
            }
            facts[Array.IndexOf(facts, operand)] = operand with { Properties = properties };
        }
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, facts);
        var report = await CombinedDependencyPathReporter.BuildReportAsync(new CombinedDependencyPathOptions(index, temp.Path,
            FromSymbol: entry.TargetSymbol, ToSurface: "database-api") { ExactFromSymbol = true, CompiledOnly = true });
        var value = Assert.Single(report.Paths).Nodes.Last().CommandBinding!.CommandTextFromPath!;
        Assert.Equal(supported, value.State == "symbolic-string-composition");
        if (supported)
        {
            Assert.Equal("System.String.Concat", value.Composition!.Operation);
            Assert.Contains(value.Composition.Operands, operand => operand.Kind == "constant-string-hash");
            Assert.DoesNotContain(value.Composition.OperandBindings, binding => binding.State == "limit");
        }
        else Assert.NotEmpty(value.Gaps);
    }

    [Theory]
    [InlineData("UpdateWindow", true)]
    [InlineData("ReplaceRows", true)]
    [InlineData("UpdateWindowTextControl", true)]
    [InlineData("UpdateWindow", false)]
    [InlineData("ReplaceRows", false)]
    [InlineData("UpdateWindowTextControl", false)]
    [InlineData("ArrayConcatWindow", true)]
    [InlineData("ArrayConcatLocalWindow", true)]
    [InlineData("ArrayConcatEscapedWindow", true)]
    [InlineData("ArrayConcatWindow", false)]
    public async Task Incremental_text_and_transactional_bulk_copy_keep_route_and_value_evidence_distinct(string method, bool includeProvider)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        var sample = Path.Combine(root!.FullName, "samples/sql-database-matrix");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var binaries = Path.Combine(sample, "wrapper-build/bin", configuration, "net472");
        var inputs = new List<string> { Path.Combine(binaries, "SyntheticWebsite.dll") };
        if (includeProvider) inputs.Add(Path.Combine(binaries, "SyntheticStore.dll"));
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(sample, "wrapper-website"),
            Path.Combine(temp.Path, "scan"), CompiledInputPaths: inputs, IlBodyEvidence: true));
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        var entry = Assert.Single(scan.Facts, f => f.FactType == FactTypes.ManagedMethodDeclared
            && f.Properties.GetValueOrDefault("metadataName") == method
            && f.TargetSymbol!.Contains("WritePage", StringComparison.Ordinal));
        var report = await CombinedDependencyPathReporter.BuildReportAsync(new CombinedDependencyPathOptions(index, temp.Path,
            FromSymbol: entry.TargetSymbol, ToSurface: "database-api") { ExactFromSymbol = true, CompiledOnly = true });
        if (!includeProvider)
        {
            Assert.Empty(report.Paths);
            Assert.NotEmpty(report.Gaps);
            return;
        }
        var commands = report.Paths.SelectMany(p => p.Nodes).Where(n => n.SurfaceName == "SqlCommand.ExecuteNonQuery").ToArray();
        var command = Assert.Single(commands);
        if (method is "UpdateWindowTextControl" or "ArrayConcatWindow" or "ArrayConcatLocalWindow" or "ArrayConcatEscapedWindow")
        {
            var value = Assert.IsType<CompiledCommandPathValueBinding>(command.CommandBinding!.CommandTextFromPath);
            if (method == "ArrayConcatEscapedWindow")
            {
                Assert.Contains("IlCommandCompositionArrayUnavailable", value.Gaps);
                Assert.Null(value.Composition);
                return;
            }
            Assert.Equal("symbolic-string-composition", value.State);
            Assert.NotNull(value.Composition);
            var constants = Constants(value).ToArray();
            Assert.Contains(TextIdentity("UPDATE dbo.PublicWindow "), constants);
            Assert.Contains(TextIdentity("WHERE Code = @code"), constants);
            Assert.Matches("^[0-9a-f]{64}$", value.GeneratorSha256);
            Assert.Matches("^[0-9a-f]{64}$", value.BoundedInputSha256);
        }
        else
        {
            Assert.Null(command.CommandBinding);
            var provider = Assert.Single(scan.Facts, f => f.FactType == FactTypes.ManagedMethodDeclared
                && f.Properties.GetValueOrDefault("metadataName") == (method == "UpdateWindow" ? "ExecuteUpdate" : "ReplaceRows")
                && f.TargetSymbol!.Contains("|names:14:SyntheticStore|arity:0|method:", StringComparison.Ordinal));
            Assert.Contains(scan.Facts, f => f.FactType == FactTypes.AnalysisGap
                && f.RuleId == "dotnet.compiled.il-command-binding.v1"
                && f.TargetSymbol!.StartsWith(provider.TargetSymbol!, StringComparison.Ordinal)
                && f.Properties.GetValueOrDefault("gapKind") == "IlCommandTextBindingUnavailable");
        }
        if (method == "ReplaceRows")
        {
            var bulk = Assert.Single(report.Paths.SelectMany(p => p.Nodes), n => n.SurfaceName == "SqlBulkCopy.WriteToServer");
            Assert.NotNull(bulk.CompiledDatabaseApiEvidence);
            // A call-path endpoint is evidence of the API, not a proven destination or transaction linkage.
            Assert.Null(bulk.CommandBinding);
            var calls = scan.Facts.Where(f => f.FactType == FactTypes.ManagedIlCallObserved).ToArray();
            foreach (var member in new[] { "set_DestinationTableName", "BeginTransaction", "Commit", "Rollback" })
                Assert.Contains(calls, f => f.Properties["targetIdentity"].Contains($"|member:{member.Length}:{member}|", StringComparison.Ordinal));
            var setter = Assert.Single(calls, f => f.Properties["targetIdentity"].Contains("|member:24:set_DestinationTableName|", StringComparison.Ordinal));
            var operands = Assert.Single(scan.Facts, f => f.FactType == FactTypes.ManagedIlCallValuesObserved
                && f.Properties.GetValueOrDefault("ilCallFactId") == setter.FactId);
            var args = JsonSerializer.Deserialize<CompiledCommandOperandOrigin[]>(operands.Properties["argumentOrigins"])!;
            Assert.Equal(new CompiledCommandOperandOrigin("constant-string-hash", TextIdentity("dbo.PublicStage")), Assert.Single(args));
        }
        // Raw SQL stays hashed; absence of its cleartext in facts does not imply absent value evidence.
        Assert.DoesNotContain("UPDATE dbo.PublicWindow", JsonSerializer.Serialize(scan.Facts));
        Assert.DoesNotContain("DELETE FROM dbo.PublicStage", JsonSerializer.Serialize(scan.Facts));

        static IEnumerable<string> Constants(CompiledCommandPathValueBinding binding)
        {
            if (binding.Origin.Kind == "constant-string-hash") yield return binding.Origin.Identity;
            if (binding.Composition is { } composition)
                foreach (var child in composition.OperandBindings)
                    foreach (var constant in Constants(child)) yield return constant;
        }
        static string TextIdentity(string text) => $"str:{text.Length}:{Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.Unicode.GetBytes(text)))}";
    }

    [Theory]
    [InlineData("both", "Save", true)]
    [InlineData("both", "Search", true)]
    [InlineData("caller-only", "Save", false)]
    [InlineData("provider-only", "Save", false)]
    [InlineData("source-only", "Save", false)]
    public async Task Compiled_caller_and_separate_wrapper_require_both_inputs(string mode, string method, bool expected)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        var sample = Path.Combine(root!.FullName, "samples/sql-database-matrix");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var binaries = Path.Combine(sample, "wrapper-build/bin", configuration, "net472");
        var inputs = mode switch
        {
            "both" => new[] { Path.Combine(binaries, "SyntheticWebsite.dll"), Path.Combine(binaries, "SyntheticStore.dll") },
            "caller-only" => new[] { Path.Combine(binaries, "SyntheticWebsite.dll") },
            "provider-only" => new[] { Path.Combine(binaries, "SyntheticStore.dll") },
            _ => Array.Empty<string>()
        };
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(sample, "wrapper-website"),
            Path.Combine(temp.Path, "scan"), CompiledInputPaths:inputs, IlBodyEvidence:true));
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        var rootFact = scan.Facts.SingleOrDefault(f => f.FactType == FactTypes.ManagedMethodDeclared
            && f.Properties.GetValueOrDefault("metadataName") == method);
        var options = new CombinedDependencyPathOptions(index, temp.Path,
            FromSymbol:rootFact?.TargetSymbol ?? "CatalogPage." + method, ToSurface:"database-api")
            { ExactFromSymbol = rootFact is not null };
        var report = await CombinedDependencyPathReporter.BuildReportAsync(options);
        if (!expected)
        {
            Assert.Empty(report.Paths);
            Assert.NotEmpty(report.Gaps);
            return;
        }
        var path = Assert.Single(report.Paths);
        Assert.Equal(method == "Save" ? 4 : 3, path.Nodes.Count);
        Assert.Equal(path.Nodes.Count - 1, path.Edges.Count);
        for (var hop = 0; hop < path.Edges.Count; hop++)
        {
            Assert.Equal(path.Nodes[hop].NodeId, path.Edges[hop].FromNodeId);
            Assert.Equal(path.Nodes[hop + 1].NodeId, path.Edges[hop].ToNodeId);
        }
        Assert.Equal(rootFact!.TargetSymbol, path.Nodes[0].SymbolId);
        Assert.Contains(path.Nodes, n => n.SymbolId?.Contains("|method:4:Save|", StringComparison.Ordinal) == true
            || n.SymbolId?.Contains("|method:6:Search|", StringComparison.Ordinal) == true);
        var wrapper = method == "Save" ? "ExecuteProcedure" : "ExecuteText";
        Assert.Contains(path.Nodes, n => n.SymbolId?.Contains($"|method:{wrapper.Length}:{wrapper}|", StringComparison.Ordinal) == true);
        var terminal = path.Nodes.Last();
        Assert.Equal(method == "Save" ? "SqlCommand.ExecuteNonQuery" : "SqlCommand.ExecuteScalar", terminal.SurfaceName);
        Assert.NotNull(terminal.CommandBinding);
        if (method == "Save")
        {
            Assert.Equal("4", terminal.CommandBinding.CommandTypeFromPath!.Origin.Identity);
            Assert.Equal("constant-on-encoded-call-path", terminal.CommandBinding.CommandTextFromPath!.State);
            Assert.Equal(2, terminal.CommandBinding.CommandTextFromPath.Steps.Count);
        }
        else
        {
            Assert.Equal("symbolic-string-composition", terminal.CommandBinding.CommandTextFromPath!.State);
        }
        var handoff = GroupedCompiledPathHandoffBuilder.Create(report,
            Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(index))));
        Assert.Contains(handoff.BackendExtraction!.Operations, o => o.Family == "database");
        var output = await GroupedCompiledPathReportWriter.WriteAsync(handoff, Path.Combine(temp.Path, "report"));
        Assert.Contains("Backend extraction inventory", await File.ReadAllTextAsync(output.HtmlPath));
        Assert.DoesNotContain("PublicCatalog_Save", JsonSerializer.Serialize(handoff));
    }
}
