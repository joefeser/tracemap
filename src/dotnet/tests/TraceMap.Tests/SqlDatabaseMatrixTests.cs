using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mono.Cecil;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class SqlDatabaseMatrixTests
{
    private static string Repo()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName,"AGENTS.md"))) repo = repo.Parent;
        return repo!.FullName;
    }
    private static string? Caller(CodeFact fact) => Regex.Match(fact.SourceSymbol ?? "", @"\.(Case\d+|Control\w+)(?:\(|<|$)") is { Success:true } match ? match.Groups[1].Value : null;
    public static IEnumerable<object[]> Cells() => new[] { "net20", "net35", "net40", "net45", "net452", "net472", "net48", "net10.0" }
        .SelectMany(tfm => new[] { "csharp", "vb" }.Select(language => new object[] { tfm, language }));

    [Theory]
    [MemberData(nameof(Cells))]
    public async Task Public_overloads_survive_every_applicable_mode_and_retained_report(string tfm, string language)
    {
        var sample = Path.Combine(Repo(),"samples/sql-database-matrix",tfm,language);
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var binary = Path.Combine(sample,"bin",configuration,tfm,$"SqlMatrix.{tfm}.{language}.dll");
        using var module = ModuleDefinition.ReadModule(binary);
        var methods = module.Types.Single(t => t.Name == "SqlMatrix").Methods.Where(m => m.Name.StartsWith("Case",StringComparison.Ordinal)).ToArray();
        var rows = Regex.Matches(await File.ReadAllTextAsync(Path.Combine(sample,"../OVERLOADS.md")), @"\| (Case\d+) \| ([^|]+) \| ``([^`]+)`` \|").ToDictionary(m => m.Groups[1].Value,m => (Assembly:m.Groups[2].Value.Trim(), Signature:m.Groups[3].Value));
        var inventory = Regex.Matches(await File.ReadAllTextAsync(Path.Combine(sample,"../OVERLOADS.md")), @"\| (Case\d+) \|").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(inventory.Length, inventory.Distinct().Count());
        Assert.Equal(inventory.Order(), methods.Select(m => m.Name).Order());
        using var temp = new TempDirectory();
        var semantic = ScanEngine.Scan(new ScanOptions(sample,Path.Combine(temp.Path,"semantic")));
        Assert.True(semantic.Manifest.BuildStatus == "Succeeded",
            JsonSerializer.Serialize(new { semantic.Manifest.BuildStatus, semantic.Manifest.KnownGaps,
                Gaps = semantic.Facts.Where(f => f.FactType == FactTypes.AnalysisGap).ToArray() }));
        var calls = semantic.Facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate && f.EvidenceTier == EvidenceTiers.Tier1Semantic).ToArray();
        var projectless = Path.Combine(temp.Path,"source");Directory.CreateDirectory(projectless);
        foreach (var file in Directory.GetFiles(sample, language == "vb" ? "*.vb" : "*.cs")) File.Copy(file,Path.Combine(projectless,Path.GetFileName(file)));
        WebFormsWizardNativeTests.InitGit(projectless);
        var syntax = ScanEngine.Scan(new ScanOptions(projectless,Path.Combine(temp.Path,"syntax")));
        var candidates = syntax.Facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate).ToArray();
        Assert.All(candidates, f => Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual,f.EvidenceTier));
        var empty = Path.Combine(temp.Path,"empty");Directory.CreateDirectory(empty);
        File.WriteAllText(Path.Combine(empty,"README.md"),"Public compiled-only fixture input. No methods executed.");WebFormsWizardNativeTests.InitGit(empty);
        var compiled = ScanEngine.Scan(new ScanOptions(empty,Path.Combine(temp.Path,"compiled"),CompiledInputPaths:[binary],IlBodyEvidence:true));
        Assert.DoesNotContain(compiled.Facts,f => f.FactType == FactTypes.DatabaseOperationCandidate);
        var index = Path.Combine(temp.Path,"index.sqlite");SqliteIndexWriter.Write(index,semantic.Manifest,semantic.Facts);
        var hash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(index)));
        var syntaxIndex = Path.Combine(temp.Path,"syntax.sqlite");SqliteIndexWriter.Write(syntaxIndex,syntax.Manifest,syntax.Facts);
        var compiledIndex = Path.Combine(temp.Path,"compiled.sqlite");SqliteIndexWriter.Write(compiledIndex,compiled.Manifest,compiled.Facts);
        foreach (var method in methods)
        {
            var expectedMethod = Regex.Match(rows[method.Name].Signature, @"\.(\w+)\(").Groups[1].Value;
            var references = method.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().ToArray();
            var emitted = Assert.Single(references, r => r.Name == expectedMethod);
            Assert.All(references.Where(r => r != emitted), r =>
            {
                Assert.Equal("vb",language); Assert.Equal("System.Runtime.CompilerServices.RuntimeHelpers",r.DeclaringType.FullName);
                Assert.Equal("GetObjectValue",r.Name); Assert.Contains(method.Parameters,p => p.ParameterType.FullName == "System.Object");
            });
            var reference = Assert.IsType<AssemblyNameReference>(emitted.DeclaringType.Scope);
            var call = Assert.Single(calls,f => f.SourceSymbol?.Contains("." + method.Name + "(",StringComparison.Ordinal) == true);
            Assert.Equal(emitted.Name,call.Properties["methodName"]);
            Assert.Equal(rows[method.Name].Assembly,call.Properties["providerAssemblyIdentity"]);
            string TypeName(TypeReference type) => type.FullName.Replace("System.Int32", "int", StringComparison.Ordinal).Replace("System.String", "string", StringComparison.Ordinal).Replace("System.Object", "object", StringComparison.Ordinal);
            var declaration = $"{method.Parameters[0].ParameterType.FullName}.{emitted.Name}({string.Join(", ",method.Parameters.Skip(1).Select(p => TypeName(p.ParameterType)))})";
            Assert.Equal(rows[method.Name].Signature.Replace("params ", "", StringComparison.Ordinal),declaration);
            Assert.Contains(emitted.Name,call.Properties["originalSignature"],StringComparison.Ordinal);
            Assert.Contains(call.Properties["targetAssemblyPublicKeyToken"],rows[method.Name].Assembly,StringComparison.Ordinal);
            Assert.Equal(RuleIds.DatabaseOperationCallPattern,call.RuleId);
            var candidate = Assert.Single(candidates,f => Caller(f) == method.Name);
            var il = Assert.Single(compiled.Facts,f => f.FactType == FactTypes.ManagedIlCallObserved
                && f.TargetSymbol?.Contains($"method:{method.Name.Length}:{method.Name}|",StringComparison.Ordinal) == true
                && f.Properties["targetIdentity"].Contains($"|member:{emitted.Name.Length}:{emitted.Name}|",StringComparison.Ordinal));
            var signature = ManagedMetadataExtractor.MethodSignature(ManagedMetadataExtractor.FormatType(emitted.ReturnType),
                emitted.Parameters.Select(p => ManagedMetadataExtractor.FormatType(p.ParameterType)),emitted.GenericParameters.Count,"default",emitted.HasThis,emitted.ExplicitThis);
            Assert.EndsWith("|" + signature,il.Properties["targetIdentity"],StringComparison.Ordinal);
            Assert.Matches("^[0-9a-f]{64}$",il.Properties["ilGeneratorSha256"]);
            Assert.Matches("^[0-9a-f]{64}$",il.Properties["ilBoundedInputSha256"]);
            var surface = call.Properties["operationKind"] is "select-candidate" or "scalar-candidate" or "data-adapter-fill" ? "sql-query" : "sql-persistence";
            var paths = await CombinedDependencyPathReporter.BuildReportAsync(new(index,temp.Path,FromSymbol:call.SourceSymbol,ToSurface:surface));
            var handoff = GroupedCompiledPathHandoffBuilder.Create(paths,hash);
            Assert.Contains(handoff.Nodes.Values,n => n.DatabaseCallEvidence?.MethodName == emitted.Name);
            Assert.Contains(handoff.BackendExtraction!.Operations,o => o.Family == "database");
            var output = await GroupedCompiledPathReportWriter.WriteAsync(handoff,Path.Combine(temp.Path,method.Name));
            Assert.Contains(emitted.Name,await File.ReadAllTextAsync(output.HtmlPath));
            Assert.Matches("^[0-9a-f]{64}$",handoff.GeneratorSha256);
            foreach (var (mode, modeIndex, root, targetSurface) in new[] { ("syntax",syntaxIndex,candidate.SourceSymbol,surface), ("compiled",compiledIndex,method.Name,"database-api") })
            {
                var modePaths = await CombinedDependencyPathReporter.BuildReportAsync(new(modeIndex,temp.Path,FromSymbol:root,ToSurface:targetSurface));
                var modeHandoff = GroupedCompiledPathHandoffBuilder.Create(modePaths,Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(modeIndex))));
                Assert.Contains(modeHandoff.BackendExtraction!.Operations,o => o.Family == "database" && o.ConnectionState == "candidate-query-path");
                if (mode == "syntax") Assert.Contains(modeHandoff.Nodes.Values,n => n.DatabaseCallEvidence?.MethodName == emitted.Name);
                else
                {
                    var terminal = Assert.Single(modeHandoff.Nodes.Values,n => n.SurfaceKind == "database-api" && n.OperationName == emitted.Name && n.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual);
                    Assert.Equal(emitted.Name switch
                    {
                        "Fill" => "compiled-data-adapter-fill-candidate",
                        "FillSchema" => "compiled-data-adapter-schema-candidate",
                        "Update" => "compiled-data-adapter-update-candidate",
                        "WriteToServer" or "WriteToServerAsync" => "compiled-bulk-write-boundary",
                        _ => "compiled-command-execute-candidate"
                    }, terminal.SurfaceSubtype);
                    Assert.Equal(il.Properties["targetIdentity"],terminal.CompiledDatabaseApiEvidence!.OriginalTargetIdentity);
                    Assert.Equal(il.Properties["ilGeneratorSha256"],terminal.CompiledDatabaseApiEvidence.IlGeneratorSha256);
                    Assert.Equal(il.Properties["ilBoundedInputSha256"],terminal.CompiledDatabaseApiEvidence.IlBoundedInputSha256);
                    Assert.Equal("unbound",terminal.CompiledDatabaseApiEvidence.ProvenanceState);
                }
                var modeOutput = await GroupedCompiledPathReportWriter.WriteAsync(modeHandoff,Path.Combine(temp.Path,mode+method.Name));
                Assert.Contains(emitted.Name,await File.ReadAllTextAsync(modeOutput.HtmlPath));
                Assert.Equal(JsonSerializer.Serialize(modePaths),JsonSerializer.Serialize(GroupedCompiledPathHandoffBuilder.Restore(modeHandoff)));
            }

        }
        var controls = JsonSerializer.Deserialize<Dictionary<string,Dictionary<string,int>>>(await File.ReadAllTextAsync(Path.Combine(Repo(),"samples/sql-database-matrix/CONTROL-EXPECTATIONS.json")))!;
        foreach (var (name,expectation) in controls)
        {
            Assert.True(expectation["semantic"] == calls.Count(f => Caller(f) == name), $"{tfm}/{language}/{name}: semantic expected {expectation["semantic"]}, actual {calls.Count(f => Caller(f) == name)}");
            Assert.Equal(expectation["projectless"],candidates.Count(f => Caller(f) == name));
        }
        Assert.All(calls,f => Assert.True(f.SourceSymbol?.Contains(".Case",StringComparison.Ordinal) == true || controls.Keys.Any(n => Caller(f) == n)));
        Assert.All(candidates,f => Assert.True(f.SourceSymbol?.Contains(".Case",StringComparison.Ordinal) == true || controls.Keys.Any(n => Caller(f) == n)));
        Assert.DoesNotContain("PublicProcedure",JsonSerializer.Serialize(calls));
        Assert.DoesNotContain("SELECT PublicId",JsonSerializer.Serialize(calls));
    }

    [Fact]
    public async Task Projectless_vb_wrapper_reaches_database_migration_inventory()
    {
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(Repo(),"samples/sql-database-matrix/website-projectless"),Path.Combine(temp.Path,"scan")));
        Assert.Equal(2, scan.Facts.Count(f => f.FactType == FactTypes.DatabaseOperationCandidate));
        Assert.DoesNotContain(scan.Facts, f => f.FactType == FactTypes.DatabaseOperationCandidate
            && (f.SourceSymbol?.Contains(".Lifecycle", StringComparison.Ordinal) == true
                || f.SourceSymbol?.Contains(".Unrelated", StringComparison.Ordinal) == true));
        Assert.Contains("Showing 2 of 2 candidates", MarkdownReportWriter.Build(scan));
        var index = Path.Combine(temp.Path,"index.sqlite");SqliteIndexWriter.Write(index,scan.Manifest,scan.Facts);
        var paths = await CombinedDependencyPathReporter.BuildReportAsync(new(index,temp.Path,FromSymbol:"PublicFeature.Entry",ToSurface:"sql-query"));
        var handoff = GroupedCompiledPathHandoffBuilder.Create(paths,Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(index))));
        Assert.Contains(handoff.BackendExtraction!.Operations,o => o.Family == "database" && o.ConnectionState == "candidate-query-path");
        Assert.Contains(handoff.Nodes.Values,n => n.DatabaseCallEvidence?.MethodName == "ExecuteScalar");
        var outputs = await GroupedCompiledPathReportWriter.WriteAsync(handoff,Path.Combine(temp.Path,"grouped"));
        Assert.Contains("Database operation: ExecuteScalar",await File.ReadAllTextAsync(outputs.HtmlPath));
    }
}
