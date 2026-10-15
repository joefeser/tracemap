using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Core;
using TraceMap.Combine;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class ExplicitVisualBasicContextTests
{
    [Theory]
    [InlineData("exact", true)]
    [InlineData("retargeted-reference", true)]
    [InlineData("page-chain", true)]
    [InlineData("hash-mismatch", false)]
    [InlineData("identity-mismatch", false)]
    [InlineData("virtual", false)]
    [InlineData("duplicate", false)]
    [InlineData("missing-proof", false)]
    [InlineData("metadata-only-exact", false)]
    [InlineData("metadata-only-hash-mismatch", false)]
    [InlineData("metadata-only-missing-proof", false)]
    [InlineData("metadata-only-duplicate", false)]
    public async Task Explicit_reference_bytes_connect_external_wrapper_without_promoting_provenance(string mode, bool expected)
    {
        var metadataOnly = mode.StartsWith("metadata-only-", StringComparison.Ordinal);
        if (metadataOnly) mode = mode["metadata-only-".Length..];
        using var temp = new TempDirectory(OperatingSystem.IsMacOS() ? "/private/tmp" : null);
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        var callerPath = mode == "page-chain" ? "App_Code/Caller.vb" : "Caller.vb";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(repo, callerPath))!);
        File.WriteAllText(Path.Combine(repo, callerPath), """
            Public Class Caller
                Public Sub Run(command As System.Data.Common.DbCommand)
                    Dim service As New ExternalWrapper()
                    service.Execute(command, "synthetic")
                End Sub
            End Class
            """);
        if (mode == "page-chain")
        {
            File.WriteAllText(Path.Combine(repo, "Page.aspx.vb"), """
                Public Class Page
                    Public Sub Click()
                        Dim worker As New Caller()
                        worker.Run(Nothing)
                    End Sub
                End Class
                """);
            File.WriteAllText(Path.Combine(repo, callerPath), """
                Public Class Caller
                    Public Sub Run(command As System.Data.Common.DbCommand)
                        Save(command)
                    End Sub
                    Private Sub Save(command As System.Data.Common.DbCommand)
                        Dim service As New ExternalWrapper()
                        Dim text As String = "synthetic"
                        service.Execute(command, text)
                    End Sub
                End Class
                """);
        }
        var referencePaths = new[] { typeof(object).Assembly.Location, typeof(System.Data.Common.DbCommand).Assembly.Location,
            System.Reflection.Assembly.Load("System.Runtime").Location, typeof(System.ComponentModel.Component).Assembly.Location };
        var library = Path.Combine(temp.Path, "ExternalWrapper.dll");
        var syntax = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("""
            public class ExternalWrapper {
                public int Execute(System.Data.Common.DbCommand command, string text) {
                    command.CommandText = text;
                    return command.ExecuteNonQuery();
                }
            }
            """.Replace("public int", mode == "virtual" ? "public virtual int" : "public int", StringComparison.Ordinal)
                .Replace("string text", mode is "page-chain" or "retargeted-reference" ? "ref string text" : "string text", StringComparison.Ordinal)
                .Replace("public class ExternalWrapper {", mode is "page-chain" or "retargeted-reference"
                    ? "public class ExternalWrapper { public int Execute(string unused) { return 0; }"
                    : "public class ExternalWrapper {", StringComparison.Ordinal)
                .Replace("return command.ExecuteNonQuery();", mode == "page-chain"
                    ? "return command.ExecuteScalar();" : "return command.ExecuteNonQuery();", StringComparison.Ordinal)
                .Replace("public int Execute(System", mode == "page-chain"
                    ? "public object Execute(System" : "public int Execute(System", StringComparison.Ordinal));
        var compiled = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("ExternalWrapper", [syntax],
            referencePaths.Distinct().Select(path => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(path)),
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
        using (var output = File.Create(library)) Assert.True(compiled.Emit(output).Success);
        if (mode == "retargeted-reference")
        {
            using var image = new MemoryStream(File.ReadAllBytes(library));
            using var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(image);
            var reference = Assert.Single(assembly.MainModule.AssemblyReferences, r => r.Name == "System.Data.Common");
            reference.Version = new Version(2, 0, 0, 0);
            var rewritten = library + ".rewritten";
            assembly.Write(rewritten);
            File.Move(rewritten, library, overwrite: true);
        }
        WebFormsWizardNativeTests.InitGit(repo);
        static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        var context = Path.Combine(temp.Path, "context.json");
        File.WriteAllText(context, JsonSerializer.Serialize(new {
            schemaVersion = "vb-compilation-context.v1", commitSha = GitMetadataProvider.Detect(repo).CommitSha,
            rootNamespace = "", optionStrict = true, optionInfer = true, optionExplicit = true, imports = Array.Empty<string>(),
            sources = new[] { new { path = callerPath, sha256 = Hash(Path.Combine(repo, callerPath)) } },
            references = referencePaths.Append(library).Distinct().Select(path => new { path, sha256 = Hash(path) }).ToArray()
        }));
        var scan = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "scan"), CompiledInputPaths: [library], IlBodyEvidence: !metadataOnly)
            { VisualBasicCompilationContextPath = context });
        var call = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.CallEdge
            && fact.Properties.GetValueOrDefault("explicitReferenceSha256") == Hash(library));
        if (mode == "retargeted-reference")
        {
            var target = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
                && fact.TargetSymbol == call.Properties["targetMetadataIdentity"]);
            Assert.Contains("version:7:2.0.0.0", target.TargetSymbol);
            Assert.Contains("&", target.TargetSymbol);
        }
        Assert.Equal(!metadataOnly, scan.Facts.Any(fact => fact.FactType == FactTypes.ManagedIlCallObserved));
        Assert.DoesNotContain(scan.Facts, fact => fact.FactType == FactTypes.DatabaseOperationCandidate);
        var retained = scan.Facts.Select(fact => {
            if (fact.FactId != call.FactId || mode is not ("hash-mismatch" or "identity-mismatch" or "missing-proof")) return fact;
            var properties = new Dictionary<string, string>(fact.Properties);
            if (mode == "missing-proof") properties.Remove("explicitReferenceSha256");
            else properties[mode == "hash-mismatch" ? "explicitReferenceSha256" : "targetMetadataIdentity"] = new string('0', 64);
            return fact with { Properties = properties };
        }).ToList();
        if (mode == "duplicate") {
            var method = Assert.Single(retained, fact => fact.FactType == FactTypes.ManagedMethodDeclared
                && fact.TargetSymbol == call.Properties["targetMetadataIdentity"]);
            retained.Add(method with { FactId = method.FactId + "-duplicate" });
        }
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, retained);
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["retained"]));
        var root = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.MethodDeclared
            && (mode == "page-chain" ? fact.Properties.GetValueOrDefault("name") == "Click" : fact.ContractElement == "Run"));
        RetainedMethodGraph? observed = null;
        var result = await CombinedDependencyPathReporter.BuildReportAsync(new(combined, Path.Combine(temp.Path, "paths"),
            FromSymbol: root.TargetSymbol, ToSurface: "database-operations", MaxDepth: 16)
            { ExactFromSymbol = true, MethodGraphObserver = metadataOnly ? graph => observed = graph : null });
        Assert.Equal(expected, result.Paths.Count > 0);
        if (metadataOnly)
        {
            Assert.NotNull(observed);
            Assert.Equal(mode == "exact", observed.Edges.Any(edge => edge.EdgeKind == "explicit-reference-member-candidate"));
            Assert.All(observed.Edges.Where(edge => edge.EdgeKind == "explicit-reference-member-candidate"),
                edge => Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, edge.EvidenceTier));
        }
        observed = null;
        var bounded = await CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(
            new(combined, Path.Combine(temp.Path, "bounded-paths"), FromSymbol: root.TargetSymbol,
                ToSurface: "database-operations", MaxDepth: 16)
                { ExactFromSymbol = true, MethodGraphObserver = metadataOnly ? graph => observed = graph : null },
            new ReportInputBudget(250_000, 250_000, 128 * 1024 * 1024));
        Assert.Equal(expected, bounded.Report.Paths.Count > 0);
        if (metadataOnly)
        {
            Assert.NotNull(observed);
            Assert.Equal(mode == "exact", observed.Edges.Any(edge => edge.EdgeKind == "explicit-reference-member-candidate"));
        }
        if (mode == "page-chain")
        {
            Assert.Contains(bounded.Report.Paths, path => path.Nodes.Last().SurfaceName == "DbCommand.ExecuteScalar"
                && path.Edges.Any(edge => edge.EdgeKind == "explicit-reference-member-candidate")
                && path.Edges.Any(edge => edge.EdgeKind == "projectless-vb-receiver-bridge"));
        }
        var compiledOnly = await CombinedDependencyPathReporter.BuildReportAsync(new(combined, Path.Combine(temp.Path, "compiled-only"),
            FromSymbol: root.TargetSymbol, ToSurface: "database-operations", MaxDepth: 16)
            { ExactFromSymbol = true, CompiledOnly = true });
        Assert.Empty(compiledOnly.Paths);
        if (expected) Assert.Contains(result.Paths, path => path.Edges.Any(edge =>
            edge.EdgeKind == "explicit-reference-member-candidate" && edge.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual));
        Assert.All(scan.Facts.Where(fact => fact.FactType == FactTypes.ManagedMethodDeclared), fact =>
            Assert.Equal("unbound", fact.Properties["provenanceState"]));
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("partial")]
    [InlineData("nested")]
    [InlineData("nested-partial")]
    [InlineData("nested-inherited")]
    [InlineData("nested-wrapper")]
    [InlineData("missing")]
    [InlineData("ambiguous")]
    [InlineData("operation-absent")]
    [InlineData("operation-other-caller")]
    [InlineData("operation-no-caller")]
    [InlineData("lookalike")]
    public async Task Syntax_page_reaches_semantic_database_body_only_through_unique_bridge(string scenario)
    {
        using var temp = new TempDirectory(OperatingSystem.IsMacOS() ? "/private/tmp" : null);
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "App_Code"));
        File.WriteAllText(Path.Combine(repo, "Page.aspx.vb"), """
            Public Class Page
                Public Sub Click()
                    Save()
                End Sub
                Private Sub Save()
                    Dim worker As New Worker()
                    worker.Run(Nothing)
                End Sub
            End Class
            """);
        var worker = """
            Public Class Worker
                Public Sub Run(command As System.Data.Common.DbCommand)
                    command.ExecuteNonQuery()
                End Sub
            End Class
            """;
        if (scenario == "lookalike") worker = worker.Replace("System.Data.Common.DbCommand", "FakeCommand", StringComparison.Ordinal)
            + "\nPublic Class FakeCommand\nPublic Sub ExecuteNonQuery()\nEnd Sub\nEnd Class\n";
        if (scenario == "missing") worker = worker.Replace("Sub Run(", "Sub Other(", StringComparison.Ordinal);
        if (scenario == "ambiguous") worker = worker.Replace("End Class", "Public Sub Run(command As String)\nEnd Sub\nEnd Class", StringComparison.Ordinal);
        if (scenario == "partial") worker += "\nPublic Class Broken\nInherits MissingBase\nEnd Class";
        if (scenario.StartsWith("nested", StringComparison.Ordinal)) worker = """
            Public Class Worker
                Private DA As New InnerAccess
                Public Function Run(command As System.Data.Common.DbCommand) As Integer
                    Return DA.Run(command)
                End Function
                Protected Class InnerAccess
                    Public Function Run(command As System.Data.Common.DbCommand) As Integer
                        Return command.ExecuteNonQuery()
                    End Function
                    Public Function Unrelated(command As System.Data.Common.DbCommand) As Integer
                        Return command.ExecuteScalar().GetHashCode()
                    End Function
                End Class
            End Class
            """;
        if (scenario == "nested-partial") worker = worker.Replace("Protected Class InnerAccess", "Protected Class InnerAccess\nInherits MissingBase", StringComparison.Ordinal);
        if (scenario is "nested-inherited" or "nested-wrapper")
        {
            worker = worker.Replace("Protected Class InnerAccess", "Protected Class InnerAccess\nInherits FrameworkBase", StringComparison.Ordinal)
                .Replace("Return command.ExecuteNonQuery()", "Return SQLDA.ExecProc_Scalar(command)", StringComparison.Ordinal)
                + """

                Public Class FrameworkBase
                    Protected SQLDA As New FrameworkWrapper
                End Class
                Public Class FrameworkWrapper
                    Public Function ExecProc_Scalar(command As System.Data.Common.DbCommand) As Integer
                        Return command.ExecuteNonQuery()
                    End Function
                End Class
                """;
            if (scenario == "nested-wrapper") worker = worker.Replace("Return command.ExecuteNonQuery()", "Return 0", StringComparison.Ordinal);
        }
        File.WriteAllText(Path.Combine(repo, "App_Code", "Worker.vb"), worker);
        foreach (var args in new[] { new[] { "init" }, new[] { "add", "." }, new[] { "-c", "user.name=Synthetic", "-c", "user.email=test@example.invalid", "commit", "-m", "fixture" } })
        {
            using var process = Process.Start(new ProcessStartInfo("git", args) { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true })!;
            Assert.True(process.WaitForExit(30_000)); Assert.Equal(0, process.ExitCode);
        }
        static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        var context = Path.Combine(temp.Path, "context.json");
        File.WriteAllText(context, JsonSerializer.Serialize(new
        {
            schemaVersion = "vb-compilation-context.v1", commitSha = GitMetadataProvider.Detect(repo).CommitSha,
            rootNamespace = "", optionStrict = true, optionInfer = true, optionExplicit = true, imports = Array.Empty<string>(),
            sources = new[] { new { path = "App_Code/Worker.vb", sha256 = Hash(Path.Combine(repo, "App_Code", "Worker.vb")) } },
            references = new[] { typeof(object).Assembly.Location, typeof(System.Data.Common.DbCommand).Assembly.Location,
                System.Reflection.Assembly.Load("System.Runtime").Location, typeof(System.ComponentModel.Component).Assembly.Location }
                .Distinct(StringComparer.Ordinal).Select(path => new { path, sha256 = Hash(path) }).ToArray()
        }));
        var scan = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "scan")) { VisualBasicCompilationContextPath = context });
        if (scenario.StartsWith("nested", StringComparison.Ordinal))
        {
            var methods = scan.Facts.Where(f => f.FactType == FactTypes.MethodDeclared
                && f.EvidenceTier == EvidenceTiers.Tier1Semantic && f.ContractElement == "Run").ToArray();
            var inner = Assert.Single(methods, f => f.TargetSymbol!.Contains("InnerAccess", StringComparison.Ordinal));
            var outer = Assert.Single(methods, f => !f.TargetSymbol!.Contains("InnerAccess", StringComparison.Ordinal));
            Assert.Contains(scan.Facts, f => f.FactType == FactTypes.CallEdge
                && f.EvidenceTier == EvidenceTiers.Tier1Semantic
                && f.SourceSymbol == outer.TargetSymbol && f.TargetSymbol == inner.TargetSymbol);
            // A custom wrapper name is never sufficient to classify a DB operation.
            Assert.DoesNotContain(scan.Facts, f => f.FactType == FactTypes.DatabaseOperationCandidate
                && f.TargetSymbol?.Contains("ExecProc_Scalar", StringComparison.Ordinal) == true);
        }
        Assert.DoesNotContain(scan.Facts, f => f.Evidence.FilePath == "Page.aspx.vb" && f.EvidenceTier == EvidenceTiers.Tier1Semantic);
        if (scenario == "lookalike") Assert.DoesNotContain(scan.Facts, f => f.FactType == FactTypes.DatabaseOperationCandidate);
        else Assert.Contains(scan.Facts, f => f.FactType == FactTypes.DatabaseOperationCandidate && f.EvidenceTier == EvidenceTiers.Tier1Semantic);
        var root = Assert.Single(scan.Facts, f => f.FactType == FactTypes.MethodDeclared && f.Properties.GetValueOrDefault("name") == "Click");
        var index = Path.Combine(temp.Path, "index.sqlite");
        // Model retained-input omissions/identity mismatches independently of
        // extraction. The unchanged scan above proves the source API is supported.
        var retainedFacts = scan.Facts.Where(f => scenario != "operation-absent" || f.FactType != FactTypes.DatabaseOperationCandidate)
            .Select(f => f.FactType == FactTypes.DatabaseOperationCandidate ? scenario switch
            {
                "operation-other-caller" => f with { SourceSymbol = "Unrelated.Run()" },
                "operation-no-caller" => f with { SourceSymbol = null },
                _ => f
            } : f).ToArray();
        SqliteIndexWriter.Write(index, scan.Manifest, retainedFacts);
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["retained"]));
        RetainedMethodGraph? diagnostic = null;
        var report = await CombinedDependencyPathReporter.BuildReportAsync(new(combined, Path.Combine(temp.Path, "paths"),
            FromSymbol: root.TargetSymbol, ToSurface: "database-operations", MaxDepth: 16)
            { ExactFromSymbol = true, ZeroPathGraphObserver = graph => diagnostic = graph });
        RetainedMethodGraph? narrowDiagnostic = null;
        var narrow = await CombinedDependencyPathReporter.BuildReportAsync(new(combined, Path.Combine(temp.Path, "narrow-paths"),
            FromSymbol: root.TargetSymbol, ToSurface: "database-api", MaxDepth: 16)
            { ExactFromSymbol = true, ZeroPathGraphObserver = graph => narrowDiagnostic = graph });
        Assert.Empty(narrow.Paths); // Existing compiled-API selector retains its original meaning.
        var audit = Assert.IsType<RetainedDatabaseTerminalAudit>(narrowDiagnostic!.SourceDatabaseAudit);
        Assert.False(audit.RowsTruncated);
        Assert.Equal(scenario is "operation-absent" or "lookalike" ? 0
            : scenario.StartsWith("nested", StringComparison.Ordinal) && scenario != "nested-wrapper" ? 2 : 1, audit.OperationFactCount);
        Assert.Equal(audit.OperationFactCount, audit.ProjectedOperationCount);
        if (scenario is "valid" or "partial" or "nested" or "nested-partial" or "nested-inherited")
        {
            Assert.Null(diagnostic);
            Assert.Equal(1, audit.ReachableOperationCallerCount);
            Assert.Equal(1, audit.ReachableOperationTerminalCount);
            Assert.Single(audit.Operations, operation => operation.State == "operation-terminal-reached");
            if (scenario.StartsWith("nested", StringComparison.Ordinal))
                Assert.Single(audit.Operations, operation => operation.State == "operation-caller-not-reached");
            Assert.NotEmpty(audit.NamedCalls);
            Assert.All(audit.NamedCalls, call => Assert.Single(call.OperationFactIds));
            var sourceReport = await CombinedDependencyPathReporter.BuildReportAsync(new(combined, Path.Combine(temp.Path, "source-paths"),
                FromSymbol: root.TargetSymbol, ToSurface: "sql-persistence", MaxDepth: 16) { ExactFromSymbol = true });
            Assert.NotEmpty(sourceReport.Paths);
            Assert.NotEmpty(report.Paths);
            Assert.Contains(report.Paths, p => p.Edges.Any(e => e.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual)
                && p.Edges.Any(e => e.EvidenceTier == EvidenceTiers.Tier2Structural)
                && p.Edges.Any(e => e.EvidenceTier == EvidenceTiers.Tier1Semantic));
        }
        else
        {
            Assert.Empty(report.Paths);
            Assert.NotNull(diagnostic);
            Assert.NotEmpty(diagnostic.Roots);
            Assert.NotEmpty(diagnostic.Edges);
            Assert.True(diagnostic.Work + report.Summary.TraversalWorkUnits <= 100_000);
            Assert.Equal(0, audit.ReachableOperationTerminalCount);
            if (scenario.StartsWith("operation-", StringComparison.Ordinal) || scenario == "lookalike")
            {
                Assert.NotEmpty(audit.NamedCalls);
                Assert.All(audit.NamedCalls, call => Assert.Empty(call.OperationFactIds));
            }
            if (scenario == "operation-other-caller")
                Assert.Equal("operation-caller-not-reached", Assert.Single(audit.Operations).State);
            if (scenario == "nested-wrapper")
            {
                Assert.Equal("operation-caller-not-reached", Assert.Single(audit.Operations).State);
                Assert.Contains(diagnostic.Nodes, node => node.SymbolId?.Contains("ExecProc_Scalar", StringComparison.Ordinal) == true);
            }
            if (scenario == "operation-no-caller")
                Assert.Equal("projected-terminal-without-exact-caller-edge", Assert.Single(audit.Operations).State);
        }
        RetainedMethodGraph? repeatedDiagnostic = null;
        var repeat = await CombinedDependencyPathReporter.BuildReportAsync(new(combined, Path.Combine(temp.Path, "repeat-paths"),
            FromSymbol: root.TargetSymbol, ToSurface: "database-operations", MaxDepth: 16)
            { ExactFromSymbol = true, ZeroPathGraphObserver = graph => repeatedDiagnostic = graph });
        Assert.Equal(report.Paths.Select(p => p.PathId), repeat.Paths.Select(p => p.PathId));
        if (diagnostic is not null)
        {
            Assert.NotNull(repeatedDiagnostic);
            Assert.Equal(diagnostic.Edges.Select(e => e.EdgeId), repeatedDiagnostic.Edges.Select(e => e.EdgeId));
            Assert.Equal(diagnostic.Cutoffs, repeatedDiagnostic.Cutoffs);
            Assert.Equal(JsonSerializer.Serialize(diagnostic.SourceDatabaseAudit), JsonSerializer.Serialize(repeatedDiagnostic.SourceDatabaseAudit));
        }
        RetainedMethodGraph? exhausted = null;
        await CombinedDependencyPathReporter.BuildReportAsync(new(combined, Path.Combine(temp.Path, "bounded"),
            FromSymbol: root.TargetSymbol, ToSurface: "database-operations", MaxDepth: 16)
            { ExactFromSymbol = true, MaxTraversalWork = 1, ZeroPathGraphObserver = graph => exhausted = graph });
        Assert.Null(exhausted);
    }

    public static IEnumerable<object[]> RegistrationCases()
    {
        foreach (var partial in new[] { false, true })
        foreach (var shape in new[] { "class", "root", "wrong-root", "nested", "interface", "duplicate" })
            yield return new object[] { partial, shape };
    }

    [Theory]
    [MemberData(nameof(RegistrationCases))]
    public void Semantic_app_code_registration_preserves_candidate_boundary(bool partial, string shape)
    {
        using var temp = new TempDirectory(OperatingSystem.IsMacOS() ? "/private/tmp" : null);
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "App_Code"));
        var declaration = shape == "interface" ? "Public Interface Widget\nEnd Interface" : "Public Class Widget\nEnd Class";
        if (shape == "nested") declaration = "Public Class Outer\n" + declaration + "\nEnd Class";
        var source = "Namespace Controls\n" + declaration + "\nEnd Namespace";
        if (partial) source += "\nPublic Class Broken\nInherits MissingBase\nEnd Class";
        File.WriteAllText(Path.Combine(repo, "App_Code", "Widget.vb"), source);
        if (shape == "duplicate") File.WriteAllText(Path.Combine(repo, "App_Code", "Duplicate.vb"), source);
        var ns = shape == "root" ? "SyntheticRoot.Controls" : "Controls";
        File.WriteAllText(Path.Combine(repo, "web.config"), $"<configuration><system.web><pages><controls><add tagPrefix=\"x\" namespace=\"{ns}\" /></controls></pages></system.web></configuration>");
        File.WriteAllText(Path.Combine(repo, "Page.aspx"), $"<%@ Page Language=\"VB\" %><x:Widget ID=\"w\" runat=\"server\" /><%= {ns}.Widget.Value %>");
        foreach (var args in new[] { new[] { "init" }, new[] { "add", "." }, new[] { "-c", "user.name=Synthetic", "-c", "user.email=test@example.invalid", "commit", "-m", "fixture" } })
        {
            using var process = Process.Start(new ProcessStartInfo("git", args) { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true })!;
            Assert.True(process.WaitForExit(30_000));
            Assert.Equal(0, process.ExitCode);
        }
        static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        var reference = Path.Combine(temp.Path, "core.dll");
        File.Copy(typeof(object).Assembly.Location, reference);
        var context = Path.Combine(temp.Path, "context.json");
        File.WriteAllText(context, JsonSerializer.Serialize(new
        {
            schemaVersion = "vb-compilation-context.v1", commitSha = GitMetadataProvider.Detect(repo).CommitSha,
            rootNamespace = shape is "root" or "wrong-root" ? "SyntheticRoot" : "",
            optionStrict = true, optionInfer = true, optionExplicit = true, imports = Array.Empty<string>(),
            sources = Directory.GetFiles(Path.Combine(repo, "App_Code"), "*.vb").Order(StringComparer.Ordinal)
                .Select(path => new { path = "App_Code/" + Path.GetFileName(path), sha256 = Hash(path) }).ToArray(),
            references = new[] { new { path = reference, sha256 = Hash(reference) } }
        }));
        var scan = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")) { VisualBasicCompilationContextPath = context });
        var joins = scan.Facts.Where(f => f.ContractElement == "UsesRegisteredAppCodeControlCandidate").ToArray();
        if (shape is "class" or "root")
        {
            var join = Assert.Single(joins);
            Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, join.EvidenceTier);
            Assert.Equal(ns + ".Widget", join.TargetSymbol);
            Assert.Contains(scan.Facts, f => f.FactType == FactTypes.TypeDeclared && f.EvidenceTier == EvidenceTiers.Tier1Semantic
                && join.Properties["supportingFactIds"].Split(',').Contains(f.FactId));
            var inline = Assert.Single(scan.Facts, f => f.FactType == FactTypes.WebFormsInlineServerExpressionReferenceCandidate);
            Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, inline.EvidenceTier);
            Assert.Equal(ns + ".Widget", inline.Properties["referencedTypeName"]);
        }
        else Assert.Empty(joins);
        if (partial) Assert.Contains(scan.Facts, f => f.Properties.GetValueOrDefault("gapKind") == "CompilationDiagnostic");
        var repeated = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "repeat")) { VisualBasicCompilationContextPath = context });
        Assert.Equal(scan.Facts.Select(f => f.FactId), repeated.Facts.Select(f => f.FactId));
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("root-namespace")]
    [InlineData("imports")]
    [InlineData("partial")]
    [InlineData("source-hash")]
    [InlineData("reference-hash")]
    [InlineData("duplicate-reference")]
    [InlineData("missing-reference")]
    [InlineData("stale-commit")]
    [InlineData("unknown-field")]
    [InlineData("duplicate-field")]
    [InlineData("outside-source")]
    [InlineData("project-conflict")]
    [InlineData("duplicate-source")]
    [InlineData("prepared")]
    [InlineData("tampered-envelope")]
    public async Task Explicit_context_binds_only_verified_inputs(string scenario)
    {
        using var temp = new TempDirectory(OperatingSystem.IsMacOS() ? "/private/tmp" : null);
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        var source = "Public Class Worker\n Public Function Run() As Integer\n Return Helper()\n End Function\n Public Function Helper() As Integer\n Return 42\n End Function\nEnd Class";
        if (scenario == "partial") source += "\nPublic Class Broken\n Inherits MissingBase\nEnd Class";
        if (scenario == "imports") source = source.Replace("As Integer", "As Int32", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(repo, "Worker.vb"), source);
        File.WriteAllText(Path.Combine(repo, "Orphan.vb"), "Public Class Orphan\nEnd Class");
        if (scenario == "project-conflict") File.WriteAllText(Path.Combine(repo, "Test.vbproj"), "<Project />");
        foreach (var arguments in new[] { new[] { "init" }, new[] { "add", "." }, new[] { "-c", "user.name=Synthetic", "-c", "user.email=test@example.invalid", "commit", "-m", "fixture" } })
        {
            using var process = Process.Start(new ProcessStartInfo("git", arguments) { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true })!;
            Assert.True(process.WaitForExit(30_000));
            Assert.Equal(0, process.ExitCode);
        }
        var reference = Path.Combine(temp.Path, "core.dll");
        File.Copy(typeof(object).Assembly.Location, reference);
        static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        var sources = new List<object> { new { path = scenario == "outside-source" ? "../Worker.vb" : "Worker.vb", sha256 = scenario == "source-hash" ? new string('0', 64) : Hash(Path.Combine(repo, "Worker.vb")) } };
        var references = new List<object> { new { path = reference, sha256 = scenario == "reference-hash" ? new string('0', 64) : Hash(reference) } };
        if (scenario == "duplicate-source") sources.Add(sources[0]);
        if (scenario == "duplicate-reference") references.Add(references[0]);
        var context = JsonSerializer.Serialize(new
        {
            schemaVersion = "vb-compilation-context.v1",
            commitSha = scenario == "stale-commit" ? new string('0', 40) : GitMetadataProvider.Detect(repo).CommitSha,
            rootNamespace = scenario == "root-namespace" ? "SyntheticRoot" : "",
            optionStrict = true, optionInfer = true, optionExplicit = true,
            imports = scenario == "imports" ? new[] { "System" } : Array.Empty<string>(), sources, references
        });
        if (scenario == "unknown-field") context = context.Insert(1, "\"extra\":true,");
        if (scenario == "duplicate-field") context = context.Insert(1, "\"rootNamespace\":\"\",");
        if (scenario is "prepared" or "tampered-envelope") context = JsonSerializer.Serialize(new
        {
            schemaVersion = "prepared-vb-compilation-context.v1", generatorSha256 = new string('a', 64),
            ruleId = "workflow.vb-compilation-context-preparation.v1", visibility = "local-only",
            boundedInputSha256 = scenario == "tampered-envelope" ? new string('0', 64) : Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(context))),
            payloadJson = context
        });
        var contextPath = Path.Combine(temp.Path, "context.json");
        File.WriteAllText(contextPath, context);
        if (scenario == "missing-reference") File.Delete(reference);
        var inventory = FileInventory.Collect(repo);
        var options = new ScanOptions(repo, Path.Combine(temp.Path, "out")) { VisualBasicCompilationContextPath = contextPath };
        var result = VisualBasicSemanticExtractor.Extract(repo, inventory, options);
        var accepted = scenario is "valid" or "root-namespace" or "imports" or "partial" or "prepared";
        Assert.True(result.Attempted);
        if (!accepted)
        {
            Assert.Empty(result.Facts);
            Assert.Empty(result.AnalyzedFiles!);
            Assert.Equal("ExplicitCompilationContextRefused", Assert.Single(result.GapFacts).Properties!["gapKind"]);
            return;
        }
        Assert.Contains("Worker.vb", result.AnalyzedFiles!);
        Assert.Contains(result.Facts, f => f.FactType == FactTypes.TypeDeclared && f.EvidenceTier == EvidenceTiers.Tier1Semantic);
        Assert.All(result.Facts, f => Assert.Equal("explicit-projectless-inputs", f.Properties!["compilationContextKind"]));
        Assert.Equal(scenario == "partial", result.ReducedCoverage);
        if (scenario == "root-namespace") Assert.Contains(result.Facts, f => f.SourceSymbol?.Contains("SyntheticRoot.Worker", StringComparison.Ordinal) == true || f.TargetSymbol?.Contains("SyntheticRoot.Worker", StringComparison.Ordinal) == true);
        if (scenario == "partial") Assert.Contains(result.GapFacts, f => f.Properties!["gapKind"] == "CompilationDiagnostic");
        var scan = ScanEngine.Scan(options);
        Assert.Contains(scan.Facts, f => f.EvidenceTier == EvidenceTiers.Tier1Semantic && f.Evidence.FilePath == "Worker.vb");
        Assert.DoesNotContain(scan.Facts, f => f.Properties.GetValueOrDefault("gapKind") == "NoVisualBasicProjectOrSolution");
        Assert.Contains(scan.Facts, f => f.Evidence.FilePath == "Orphan.vb" && f.Properties.GetValueOrDefault("gapKind") == "SemanticAnalysisUnavailable");
        Assert.DoesNotContain(scan.Facts, f => f.Evidence.FilePath == "Orphan.vb" && f.EvidenceTier == EvidenceTiers.Tier1Semantic);
        Assert.Contains(scan.Facts, f => f.FactType == FactTypes.CallEdge && f.EvidenceTier == EvidenceTiers.Tier1Semantic);
        if (scenario == "valid")
        {
            foreach (var name in new[] { "cli-one", "cli-two" })
            {
                using var output = new StringWriter();
                using var error = new StringWriter();
                Assert.Equal(0, await TraceMap.Cli.TraceMapCommand.RunAsync(
                    ["scan", "--repo", repo, "--out", Path.Combine(temp.Path, name), "--vb-compilation-context", contextPath], output, error));
                foreach (var artifact in new[] { "facts.ndjson", "index.sqlite", "report.md", "scan-manifest.json", "logs/analyzer.log" })
                    Assert.True(File.Exists(Path.Combine(temp.Path, name, artifact)));
            }
            Assert.Equal(File.ReadAllBytes(Path.Combine(temp.Path, "cli-one", "facts.ndjson")),
                File.ReadAllBytes(Path.Combine(temp.Path, "cli-two", "facts.ndjson")));
        }
    }
}
