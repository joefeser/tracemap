using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class CSharpGraphCorrectnessFixtureTests
{
    private const string SemanticExtractor = "CSharpSemanticExtractor";
    private const string AssemblyVersion = "1.0.0.0";

    [Fact]
    public void Same_name_types_keep_assembly_aware_ids_and_local_framework_collisions_separate()
    {
        using var temp = new TempDirectory();
        WriteProject(temp.Path, "Alpha");
        WriteProject(temp.Path, "Beta");
        WriteProject(temp.Path, "CollisionApp");
        WriteSource(temp.Path, "Alpha", "Collision.cs", """
            namespace Shared;

            public sealed class Collision
            {
                public void Ping() { }

                public void Invoke()
                {
                    Ping();
                }
            }
            """);
        WriteSource(temp.Path, "Beta", "Collision.cs", """
            namespace Shared;

            public sealed class Collision
            {
                public void Ping() { }

                public void Invoke()
                {
                    Ping();
                }
            }
            """);
        WriteSource(temp.Path, "CollisionApp", "TaskCollision.cs", """
            namespace CollisionApp;

            public sealed class Task
            {
                public void Run() { }
            }

            public sealed class Caller
            {
                public void Execute()
                {
                    var local = new Task();
                    var external = System.Threading.Tasks.Task.CompletedTask;
                    local.Run();
                    external.GetAwaiter();
                }
            }
            """);

        var result = Scan(temp.Path);

        Assert.Equal("Succeeded", result.Manifest.BuildStatus);
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.CSharpSemanticWorkspace);

        var alphaDeclaration = AssertSemanticFact(result, FactTypes.TypeDeclared, "src/Alpha/Collision.cs", 3, 11, "global::Shared.Collision");
        var betaDeclaration = AssertSemanticFact(result, FactTypes.TypeDeclared, "src/Beta/Collision.cs", 3, 11, "global::Shared.Collision");
        Assert.Equal("csharp type Alpha%401.0.0.0 Shared.Collision", alphaDeclaration.Properties["targetSymbolId"]);
        Assert.Equal("csharp type Beta%401.0.0.0 Shared.Collision", betaDeclaration.Properties["targetSymbolId"]);
        Assert.NotEqual(alphaDeclaration.Properties["targetSymbolId"], betaDeclaration.Properties["targetSymbolId"]);

        var alphaCall = AssertSemanticCall(result, "src/Alpha/Collision.cs", 9, "Ping");
        var betaCall = AssertSemanticCall(result, "src/Beta/Collision.cs", 9, "Ping");
        Assert.Equal("Alpha", alphaCall.Properties["callerAssemblyName"]);
        Assert.Equal("Alpha", alphaCall.Properties["calleeAssemblyName"]);
        Assert.Equal("Beta", betaCall.Properties["callerAssemblyName"]);
        Assert.Equal("Beta", betaCall.Properties["calleeAssemblyName"]);
        AssertCallIdentity(
            alphaCall,
            "csharp method csharp%20type%20Alpha%25401.0.0.0%20Shared.Collision Invoke()->void",
            "csharp method csharp%20type%20Alpha%25401.0.0.0%20Shared.Collision Ping()->void",
            "Alpha",
            "Alpha");
        AssertCallIdentity(
            betaCall,
            "csharp method csharp%20type%20Beta%25401.0.0.0%20Shared.Collision Invoke()->void",
            "csharp method csharp%20type%20Beta%25401.0.0.0%20Shared.Collision Ping()->void",
            "Beta",
            "Beta");
        Assert.NotEqual(alphaCall.Properties["targetSymbolId"], betaCall.Properties["targetSymbolId"]);

        var localTask = AssertSemanticFact(result, FactTypes.TypeDeclared, "src/CollisionApp/TaskCollision.cs", 3, 6, "global::CollisionApp.Task");
        Assert.Equal("csharp type CollisionApp%401.0.0.0 CollisionApp.Task", localTask.Properties["targetSymbolId"]);
        var localRun = AssertSemanticCall(result, "src/CollisionApp/TaskCollision.cs", 14, "Run");
        var externalAwaiter = AssertSemanticCall(result, "src/CollisionApp/TaskCollision.cs", 15, "GetAwaiter");
        const string executeId = "csharp method csharp%20type%20CollisionApp%25401.0.0.0%20CollisionApp.Caller Execute()->void";
        AssertCallIdentity(
            localRun,
            executeId,
            "csharp method csharp%20type%20CollisionApp%25401.0.0.0%20CollisionApp.Task Run()->void",
            "CollisionApp",
            "CollisionApp");
        AssertCallIdentity(
            externalAwaiter,
            executeId,
            "csharp method csharp%20type%20System.Runtime%254010.0.0.0%20System.Threading.Tasks.Task GetAwaiter()->System.Runtime%4010.0.0.0%3ASystem.Runtime.CompilerServices.TaskAwaiter",
            "CollisionApp",
            "System.Runtime",
            calleeAssemblyVersion: "10.0.0.0");
        Assert.NotEqual(localRun.Properties["targetSymbolId"], externalAwaiter.Properties["targetSymbolId"]);
    }

    [Fact]
    public void Partial_type_declarations_merge_and_cross_file_call_uses_canonical_member_endpoints()
    {
        using var temp = new TempDirectory();
        WriteProject(temp.Path, "PartialSample");
        WriteSource(temp.Path, "PartialSample", "Worker.State.cs", """
            namespace PartialSample;

            public sealed partial class Worker
            {
                private readonly Helper helper = new();

                public void Run()
                {
                    Execute();
                    helper.Touch();
                }
            }

            public sealed class Helper
            {
                public void Touch() { }
            }
            """);
        WriteSource(temp.Path, "PartialSample", "Worker.Behavior.cs", """
            namespace PartialSample;

            public sealed partial class Worker
            {
                private void Execute()
                {
                    helper.Touch();
                }
            }
            """);

        var result = Scan(temp.Path);

        Assert.Equal("Succeeded", result.Manifest.BuildStatus);
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.CSharpSemanticWorkspace);
        var stateDeclaration = AssertSemanticFact(result, FactTypes.TypeDeclared, "src/PartialSample/Worker.State.cs", 3, 12, "global::PartialSample.Worker");
        var behaviorDeclaration = AssertSemanticFact(result, FactTypes.TypeDeclared, "src/PartialSample/Worker.Behavior.cs", 3, 9, "global::PartialSample.Worker");
        Assert.Equal("csharp type PartialSample%401.0.0.0 PartialSample.Worker", stateDeclaration.Properties["targetSymbolId"]);
        Assert.Equal(stateDeclaration.Properties["targetSymbolId"], behaviorDeclaration.Properties["targetSymbolId"]);

        var crossFileCall = AssertSemanticCall(result, "src/PartialSample/Worker.State.cs", 9, "Execute");
        const string workerRunId = "csharp method csharp%20type%20PartialSample%25401.0.0.0%20PartialSample.Worker Run()->void";
        const string workerExecuteId = "csharp method csharp%20type%20PartialSample%25401.0.0.0%20PartialSample.Worker Execute()->void";
        const string helperTouchId = "csharp method csharp%20type%20PartialSample%25401.0.0.0%20PartialSample.Helper Touch()->void";
        AssertCallIdentity(crossFileCall, workerRunId, workerExecuteId, "PartialSample", "PartialSample");

        var helperCalls = result.Facts
            .Where(fact => IsSemanticCall(fact, "Touch"))
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ToArray();
        Assert.Collection(
            helperCalls,
            call =>
            {
                AssertSpanAndProvenance(call, "src/PartialSample/Worker.Behavior.cs", 7, 7);
                AssertCallIdentity(call, workerExecuteId, helperTouchId, "PartialSample", "PartialSample");
            },
            call =>
            {
                AssertSpanAndProvenance(call, "src/PartialSample/Worker.State.cs", 10, 10);
                AssertCallIdentity(call, workerRunId, helperTouchId, "PartialSample", "PartialSample");
            });
        Assert.Equal(helperCalls[0].Properties["targetSymbolId"], helperCalls[1].Properties["targetSymbolId"]);
    }

    [Fact]
    public void Receiver_resolution_honors_fields_parameters_inline_declarations_and_shadowing_and_reports_unresolved_calls()
    {
        using var temp = new TempDirectory();
        WriteProject(temp.Path, "ReceiverSample");
        WriteSource(temp.Path, "ReceiverSample", "Receivers.cs", """
            namespace Alpha
            {
                public sealed class Receiver { public void Touch() { } }
            }

            namespace Beta
            {
                public sealed class Receiver { public void Touch() { } }
            }

            namespace ReceiverSample
            {
                public sealed class Consumer
                {
                    private readonly Alpha.Receiver receiver = new();
                    public Beta.Receiver Property { get; } = new();

                    public void Exercise(Beta.Receiver parameter, object candidate)
                    {
                        receiver.Touch();
                        parameter.Touch();
                        Property.Touch();
                        if (TryResolve(out var inline)) inline.Touch();
                        if (candidate is Alpha.Receiver patterned) patterned.Touch();
                        {
                            var receiver = new Beta.Receiver();
                            receiver.Touch();
                            this.receiver.Touch();
                        }
                    }

                    private static bool TryResolve(out Alpha.Receiver value)
                    {
                        value = new Alpha.Receiver();
                        return true;
                    }

                    public void Broken(MissingReceiver missing)
                    {
                        missing.Touch();
                    }
                }
            }
            """);

        var result = Scan(temp.Path);

        Assert.Equal("FailedOrPartial", result.Manifest.BuildStatus);
        Assert.Equal("Level1SemanticAnalysisReduced", result.Manifest.AnalysisLevel);
        const string exerciseId = "csharp method csharp%20type%20ReceiverSample%25401.0.0.0%20ReceiverSample.Consumer Exercise(ReceiverSample%401.0.0.0%3ABeta.Receiver%2CSystem.Runtime%4010.0.0.0%3Aobject)->void";
        const string alphaTouchId = "csharp method csharp%20type%20ReceiverSample%25401.0.0.0%20Alpha.Receiver Touch()->void";
        const string betaTouchId = "csharp method csharp%20type%20ReceiverSample%25401.0.0.0%20Beta.Receiver Touch()->void";
        var expectedCalls = new[]
        {
            (Line: 20, TargetId: alphaTouchId),
            (Line: 21, TargetId: betaTouchId),
            (Line: 22, TargetId: betaTouchId),
            (Line: 23, TargetId: alphaTouchId),
            (Line: 24, TargetId: alphaTouchId),
            (Line: 27, TargetId: betaTouchId),
            (Line: 28, TargetId: alphaTouchId)
        };
        foreach (var expected in expectedCalls)
        {
            var call = AssertSemanticCall(result, "src/ReceiverSample/Receivers.cs", expected.Line, "Touch");
            AssertCallIdentity(call, exerciseId, expected.TargetId, "ReceiverSample", "ReceiverSample");
        }

        var gap = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.CSharpSemanticWorkspace
            && fact.Properties.GetValueOrDefault("diagnosticId") == "CS0246"
            && fact.Properties.GetValueOrDefault("diagnosticTokens")?.Split(';').Contains("MissingReceiver") == true);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        AssertSpanAndProvenance(gap, "src/ReceiverSample/Receivers.cs", 38, 38);
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.Evidence.FilePath == "src/ReceiverSample/Receivers.cs"
            && fact.Evidence.StartLine == 40);
        var syntaxFallback = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.RuleId == RuleIds.CSharpSyntaxCallGraph
            && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && fact.TargetSymbol == "Touch"
            && fact.Evidence.FilePath == "src/ReceiverSample/Receivers.cs"
            && fact.Evidence.StartLine == 40);
        Assert.Equal("CSharpSyntaxExtractor", syntaxFallback.Evidence.ExtractorId);
        Assert.Equal(ScannerVersions.CSharpSyntaxExtractor, syntaxFallback.Evidence.ExtractorVersion);
    }

    private static ScanResult Scan(string repoPath)
    {
        var expectedCommitSha = CommitFixture(repoPath);
        var outputPath = Path.Combine(repoPath, ".tracemap");
        var result = ScanEngine.Scan(new ScanOptions(repoPath, outputPath));

        Assert.Matches("^[0-9a-f]{40}$", result.Manifest.CommitSha);
        Assert.Equal(expectedCommitSha, result.Manifest.CommitSha);
        Assert.All(result.Facts, fact => Assert.Equal(expectedCommitSha, fact.CommitSha));

        var jsonlPath = Path.Combine(outputPath, "facts.ndjson");
        var sqlitePath = Path.Combine(outputPath, "index.sqlite");
        JsonlFactWriter.WriteAsync(jsonlPath, result.Facts).GetAwaiter().GetResult();
        SqliteIndexWriter.Write(sqlitePath, result.Manifest, result.Facts);

        var persistedFacts = File.ReadLines(jsonlPath)
            .Select(line => JsonSerializer.Deserialize<CodeFact>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web)))
            .Select(fact => Assert.IsType<CodeFact>(fact))
            .ToArray();
        Assert.Equal(result.Facts.Count, persistedFacts.Length);
        Assert.All(persistedFacts, fact => Assert.Equal(expectedCommitSha, fact.CommitSha));
        AssertSqliteRoundTrip(sqlitePath, result.Manifest, persistedFacts);

        return result with { Facts = persistedFacts };
    }

    private static CodeFact AssertSemanticCall(ScanResult result, string filePath, int line, string contractElement)
    {
        var fact = Assert.Single(result.Facts, fact =>
            IsSemanticCall(fact, contractElement)
            && fact.Evidence.FilePath == filePath
            && fact.Evidence.StartLine == line);
        AssertSpanAndProvenance(fact, filePath, line, line);
        Assert.False(string.IsNullOrWhiteSpace(fact.Properties["sourceSymbolId"]));
        Assert.False(string.IsNullOrWhiteSpace(fact.Properties["targetSymbolId"]));
        return fact;
    }

    private static bool IsSemanticCall(CodeFact fact, string contractElement) =>
        fact.FactType == FactTypes.CallEdge
        && fact.RuleId == RuleIds.CSharpSemanticCallGraph
        && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
        && fact.ContractElement == contractElement;

    private static void AssertCallIdentity(
        CodeFact fact,
        string sourceSymbolId,
        string targetSymbolId,
        string callerAssemblyName,
        string calleeAssemblyName,
        string callerAssemblyVersion = AssemblyVersion,
        string calleeAssemblyVersion = AssemblyVersion)
    {
        Assert.Equal(sourceSymbolId, fact.Properties["sourceSymbolId"]);
        Assert.Equal(targetSymbolId, fact.Properties["targetSymbolId"]);
        Assert.Equal(callerAssemblyName, fact.Properties["callerAssemblyName"]);
        Assert.Equal(callerAssemblyVersion, fact.Properties["callerAssemblyVersion"]);
        Assert.Equal(calleeAssemblyName, fact.Properties["calleeAssemblyName"]);
        Assert.Equal(calleeAssemblyVersion, fact.Properties["calleeAssemblyVersion"]);
    }

    private static CodeFact AssertSemanticFact(
        ScanResult result,
        string factType,
        string filePath,
        int startLine,
        int endLine,
        string targetSymbol)
    {
        var fact = Assert.Single(result.Facts, fact =>
            fact.FactType == factType
            && fact.RuleId == RuleIds.CSharpSemanticDeclarations
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.TargetSymbol == targetSymbol
            && fact.Evidence.FilePath == filePath);
        AssertSpanAndProvenance(fact, filePath, startLine, endLine);
        return fact;
    }

    private static void AssertSpanAndProvenance(CodeFact fact, string filePath, int startLine, int endLine)
    {
        Assert.Equal(filePath, fact.Evidence.FilePath);
        Assert.Equal(startLine, fact.Evidence.StartLine);
        Assert.Equal(endLine, fact.Evidence.EndLine);
        Assert.Equal(SemanticExtractor, fact.Evidence.ExtractorId);
        Assert.Equal(ScannerVersions.CSharpSemanticExtractor, fact.Evidence.ExtractorVersion);
    }

    private static void AssertSqliteRoundTrip(string sqlitePath, ScanManifest manifest, IReadOnlyList<CodeFact> facts)
    {
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        connection.Open();
        Assert.Equal(manifest.CommitSha, ExecuteScalar(connection, "select commit_sha from scan_manifest;"));
        Assert.Equal(facts.Count, long.Parse(ExecuteScalar(connection, "select count(*) from facts;")));

        foreach (var fact in facts.Where(fact =>
            IsSemanticCall(fact, fact.ContractElement ?? string.Empty)
            || (fact.FactType == FactTypes.AnalysisGap && fact.RuleId == RuleIds.CSharpSemanticWorkspace)))
        {
            using var factCommand = connection.CreateCommand();
            factCommand.CommandText = """
                select commit_sha, rule_id, evidence_tier, file_path, start_line, end_line,
                       extractor_id, extractor_version, properties_json
                from facts
                where fact_id = $fact_id;
                """;
            factCommand.Parameters.AddWithValue("$fact_id", fact.FactId);
            using var reader = factCommand.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal(fact.CommitSha, reader.GetString(0));
            Assert.Equal(fact.RuleId, reader.GetString(1));
            Assert.Equal(fact.EvidenceTier, reader.GetString(2));
            Assert.Equal(fact.Evidence.FilePath, reader.GetString(3));
            Assert.Equal(fact.Evidence.StartLine, reader.GetInt32(4));
            Assert.Equal(fact.Evidence.EndLine, reader.GetInt32(5));
            Assert.Equal(fact.Evidence.ExtractorId, reader.GetString(6));
            Assert.Equal(fact.Evidence.ExtractorVersion, reader.GetString(7));
            var properties = JsonSerializer.Deserialize<Dictionary<string, string>>(
                reader.GetString(8),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(properties);
            Assert.Equal(
                fact.Properties.OrderBy(item => item.Key, StringComparer.Ordinal),
                properties.OrderBy(item => item.Key, StringComparer.Ordinal));
            Assert.False(reader.Read());
            reader.Close();

            if (fact.FactType != FactTypes.CallEdge || fact.RuleId != RuleIds.CSharpSemanticCallGraph)
            {
                continue;
            }

            Assert.Equal(fact.Properties["sourceSymbolId"], ReadFactSymbol(connection, fact.FactId, "source"));
            Assert.Equal(fact.Properties["targetSymbolId"], ReadFactSymbol(connection, fact.FactId, "target"));

            using var callCommand = connection.CreateCommand();
            callCommand.CommandText = """
                select commit_sha, rule_id, evidence_tier, caller_assembly_name,
                       caller_assembly_version, callee_assembly_name, callee_assembly_version,
                       file_path, start_line, end_line
                from call_edges
                where fact_id = $fact_id;
                """;
            callCommand.Parameters.AddWithValue("$fact_id", fact.FactId);
            using var callReader = callCommand.ExecuteReader();
            Assert.True(callReader.Read());
            Assert.Equal(fact.CommitSha, callReader.GetString(0));
            Assert.Equal(fact.RuleId, callReader.GetString(1));
            Assert.Equal(fact.EvidenceTier, callReader.GetString(2));
            Assert.Equal(fact.Properties["callerAssemblyName"], callReader.GetString(3));
            Assert.Equal(fact.Properties["callerAssemblyVersion"], callReader.GetString(4));
            Assert.Equal(fact.Properties["calleeAssemblyName"], callReader.GetString(5));
            Assert.Equal(fact.Properties["calleeAssemblyVersion"], callReader.GetString(6));
            Assert.Equal(fact.Evidence.FilePath, callReader.GetString(7));
            Assert.Equal(fact.Evidence.StartLine, callReader.GetInt32(8));
            Assert.Equal(fact.Evidence.EndLine, callReader.GetInt32(9));
            Assert.False(callReader.Read());
        }

        if (facts.Any(fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.CSharpSemanticWorkspace
            && fact.Properties.GetValueOrDefault("diagnosticId") == "CS0246"
            && fact.Properties.GetValueOrDefault("diagnosticTokens")?.Split(';').Contains("MissingReceiver") == true))
        {
            AssertUnresolvedReceiverSqliteRoundTrip(connection, manifest, facts);
        }
    }

    private static void AssertUnresolvedReceiverSqliteRoundTrip(
        SqliteConnection connection,
        ScanManifest manifest,
        IReadOnlyList<CodeFact> facts)
    {
        const string filePath = "src/ReceiverSample/Receivers.cs";
        const int callLine = 40;

        using (var absentSemanticFact = connection.CreateCommand())
        {
            absentSemanticFact.CommandText = """
                select count(*)
                from facts
                where fact_type = $fact_type
                  and rule_id = $rule_id
                  and evidence_tier = $evidence_tier
                  and file_path = $file_path
                  and start_line = $line;
                """;
            absentSemanticFact.Parameters.AddWithValue("$fact_type", FactTypes.CallEdge);
            absentSemanticFact.Parameters.AddWithValue("$rule_id", RuleIds.CSharpSemanticCallGraph);
            absentSemanticFact.Parameters.AddWithValue("$evidence_tier", EvidenceTiers.Tier1Semantic);
            absentSemanticFact.Parameters.AddWithValue("$file_path", filePath);
            absentSemanticFact.Parameters.AddWithValue("$line", callLine);
            Assert.Equal(0L, Assert.IsType<long>(absentSemanticFact.ExecuteScalar()));
        }

        using (var absentSemanticCall = connection.CreateCommand())
        {
            absentSemanticCall.CommandText = """
                select count(*)
                from call_edges
                where rule_id = $rule_id
                  and evidence_tier = $evidence_tier
                  and file_path = $file_path
                  and start_line = $line;
                """;
            absentSemanticCall.Parameters.AddWithValue("$rule_id", RuleIds.CSharpSemanticCallGraph);
            absentSemanticCall.Parameters.AddWithValue("$evidence_tier", EvidenceTiers.Tier1Semantic);
            absentSemanticCall.Parameters.AddWithValue("$file_path", filePath);
            absentSemanticCall.Parameters.AddWithValue("$line", callLine);
            Assert.Equal(0L, Assert.IsType<long>(absentSemanticCall.ExecuteScalar()));
        }

        var expectedGap = Assert.Single(facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.CSharpSemanticWorkspace
            && fact.EvidenceTier == EvidenceTiers.Tier4Unknown
            && fact.Evidence.FilePath == filePath
            && fact.Evidence.StartLine == 38
            && fact.Properties.GetValueOrDefault("diagnosticId") == "CS0246");
        using (var gapCommand = connection.CreateCommand())
        {
            gapCommand.CommandText = """
                select fact_id, commit_sha, end_line, extractor_id, extractor_version, properties_json
                from facts
                where fact_type = $fact_type
                  and rule_id = $rule_id
                  and evidence_tier = $evidence_tier
                  and file_path = $file_path
                  and start_line = $line;
                """;
            gapCommand.Parameters.AddWithValue("$fact_type", FactTypes.AnalysisGap);
            gapCommand.Parameters.AddWithValue("$rule_id", RuleIds.CSharpSemanticWorkspace);
            gapCommand.Parameters.AddWithValue("$evidence_tier", EvidenceTiers.Tier4Unknown);
            gapCommand.Parameters.AddWithValue("$file_path", filePath);
            gapCommand.Parameters.AddWithValue("$line", 38);
            using var gapReader = gapCommand.ExecuteReader();
            Assert.True(gapReader.Read());
            Assert.Equal(expectedGap.FactId, gapReader.GetString(0));
            Assert.Equal(manifest.CommitSha, gapReader.GetString(1));
            Assert.Equal(38, gapReader.GetInt32(2));
            Assert.Equal(SemanticExtractor, gapReader.GetString(3));
            Assert.Equal(ScannerVersions.CSharpSemanticExtractor, gapReader.GetString(4));
            var gapProperties = JsonSerializer.Deserialize<Dictionary<string, string>>(
                gapReader.GetString(5),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(gapProperties);
            Assert.Equal("CS0246", gapProperties["diagnosticId"]);
            Assert.Contains("MissingReceiver", gapProperties["diagnosticTokens"].Split(';'));
            Assert.False(gapReader.Read());
        }

        var expectedSyntaxCall = Assert.Single(facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.RuleId == RuleIds.CSharpSyntaxCallGraph
            && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && fact.Evidence.FilePath == filePath
            && fact.Evidence.StartLine == callLine
            && fact.TargetSymbol == "Touch");
        using (var syntaxFactCommand = connection.CreateCommand())
        {
            syntaxFactCommand.CommandText = """
                select fact_id, commit_sha, source_symbol, target_symbol, end_line,
                       extractor_id, extractor_version, properties_json
                from facts
                where fact_type = $fact_type
                  and rule_id = $rule_id
                  and evidence_tier = $evidence_tier
                  and file_path = $file_path
                  and start_line = $line;
                """;
            syntaxFactCommand.Parameters.AddWithValue("$fact_type", FactTypes.CallEdge);
            syntaxFactCommand.Parameters.AddWithValue("$rule_id", RuleIds.CSharpSyntaxCallGraph);
            syntaxFactCommand.Parameters.AddWithValue("$evidence_tier", EvidenceTiers.Tier3SyntaxOrTextual);
            syntaxFactCommand.Parameters.AddWithValue("$file_path", filePath);
            syntaxFactCommand.Parameters.AddWithValue("$line", callLine);
            using var syntaxFactReader = syntaxFactCommand.ExecuteReader();
            Assert.True(syntaxFactReader.Read());
            Assert.Equal(expectedSyntaxCall.FactId, syntaxFactReader.GetString(0));
            Assert.Equal(manifest.CommitSha, syntaxFactReader.GetString(1));
            Assert.Equal("Broken", syntaxFactReader.GetString(2));
            Assert.Equal("Touch", syntaxFactReader.GetString(3));
            Assert.Equal(callLine, syntaxFactReader.GetInt32(4));
            Assert.Equal("CSharpSyntaxExtractor", syntaxFactReader.GetString(5));
            Assert.Equal(ScannerVersions.CSharpSyntaxExtractor, syntaxFactReader.GetString(6));
            var syntaxProperties = JsonSerializer.Deserialize<Dictionary<string, string>>(
                syntaxFactReader.GetString(7),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(syntaxProperties);
            Assert.Equal("Broken", syntaxProperties["callerName"]);
            Assert.Equal("Touch", syntaxProperties["calleeName"]);
            Assert.Equal("SyntaxInvocation", syntaxProperties["callKind"]);
            Assert.False(syntaxFactReader.Read());
        }

        using var syntaxCallCommand = connection.CreateCommand();
        syntaxCallCommand.CommandText = """
            select fact_id, commit_sha, caller_symbol, callee_symbol, call_kind, end_line
            from call_edges
            where rule_id = $rule_id
              and evidence_tier = $evidence_tier
              and file_path = $file_path
              and start_line = $line;
            """;
        syntaxCallCommand.Parameters.AddWithValue("$rule_id", RuleIds.CSharpSyntaxCallGraph);
        syntaxCallCommand.Parameters.AddWithValue("$evidence_tier", EvidenceTiers.Tier3SyntaxOrTextual);
        syntaxCallCommand.Parameters.AddWithValue("$file_path", filePath);
        syntaxCallCommand.Parameters.AddWithValue("$line", callLine);
        using var syntaxCallReader = syntaxCallCommand.ExecuteReader();
        Assert.True(syntaxCallReader.Read());
        Assert.Equal(expectedSyntaxCall.FactId, syntaxCallReader.GetString(0));
        Assert.Equal(manifest.CommitSha, syntaxCallReader.GetString(1));
        Assert.Equal("Broken", syntaxCallReader.GetString(2));
        Assert.Equal("Touch", syntaxCallReader.GetString(3));
        Assert.Equal("SyntaxInvocation", syntaxCallReader.GetString(4));
        Assert.Equal(callLine, syntaxCallReader.GetInt32(5));
        Assert.False(syntaxCallReader.Read());
    }

    private static string ReadFactSymbol(SqliteConnection connection, string factId, string role)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "select symbol_id from fact_symbols where fact_id = $fact_id and role = $role;";
        command.Parameters.AddWithValue("$fact_id", factId);
        command.Parameters.AddWithValue("$role", role);
        return Assert.IsType<string>(command.ExecuteScalar());
    }

    private static string ExecuteScalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string CommitFixture(string repoPath)
    {
        RunGit(repoPath, "init", "-b", "fixture");
        RunGit(repoPath, "config", "user.email", "tests@example.invalid");
        RunGit(repoPath, "config", "user.name", "TraceMap Tests");
        RunGit(repoPath, "config", "commit.gpgsign", "false");
        RunGit(repoPath, "add", ".");
        RunGit(repoPath, "commit", "-m", "fixture");
        return RunGit(repoPath, "rev-parse", "HEAD").Trim();
    }

    private static string RunGit(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.Environment["GIT_AUTHOR_DATE"] = "2000-01-01T00:00:00Z";
        startInfo.Environment["GIT_COMMITTER_DATE"] = "2000-01-01T00:00:00Z";
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {standardError}");
        return standardOutput;
    }

    private static void WriteProject(string repoPath, string projectName)
    {
        var directory = Path.Combine(repoPath, "src", projectName);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, projectName + ".csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
    }

    private static void WriteSource(string repoPath, string projectName, string fileName, string source)
    {
        File.WriteAllText(Path.Combine(repoPath, "src", projectName, fileName), source);
    }
}
