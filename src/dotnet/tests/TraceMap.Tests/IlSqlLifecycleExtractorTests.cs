using System.Security.Cryptography;
using TraceMap.Core;
using Xunit.Abstractions;

namespace TraceMap.Tests;

public sealed class IlSqlLifecycleExtractorTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Malformed_offsets_and_substituted_type_operands_withhold_all_lineage(string language)
    {
        var bodies = Bodies(language);
        var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == "Score");
        foreach (var opcode in new[] { "box", "newarr", "castclass", "isinst", "invalid-offset" })
        {
            var changed = 0;
            var corrupted = bodies.Select(body => body with
            {
                Observation = body.Observation with
                {
                    ObjectBody = body.Observation.ObjectBody! with
                    {
                        Instructions = body.Observation.ObjectBody!.Instructions.Select(line =>
                        {
                            var parts = line.Split(':', 4);
                            if (opcode == "invalid-offset" && body == root) { parts[1] = "not-hex"; changed++; }
                            else if (parts[2] == opcode) { parts[3] = "t:untrusted-type"; changed++; }
                            return string.Join(':', parts);
                        }).ToArray()
                    }
                }
            }).ToArray();
            Assert.True(changed > 0);
            var changedRoot = Assert.Single(corrupted, b => b.Observation.MethodIdentity == root.Observation.MethodIdentity);
            var result = IlSqlLifecycleExtractor.Extract(changedRoot, corrupted, true, new(200_000));
            Assert.NotEmpty(result.Gaps);
            Assert.Empty(result.Executions); Assert.Empty(result.Reads); Assert.Null(result.Returned);
        }
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Unknown_guard_keeps_both_alternatives_and_failure_boundaries_are_not_atomic(string language)
    {
        var bodies = Bodies(language);
        var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == "Score");
        var branches = IlSqlLifecycleExtractor.ExtractGuarded(root, bodies, new(400_000));
        Assert.Equal(2, branches.Count);
        Assert.All(branches, b => { Assert.Empty(b.Result.Gaps); Assert.Equal("argument:0", b.Predicate); });
        var noOverlay = Assert.Single(branches, b => !b.Value).Result;
        var overlay = Assert.Single(branches, b => b.Value).Result;
        Assert.Equal("sql", noOverlay.Returned!.Value.Kind);
        Assert.Equal("application", overlay.Returned!.Value.Kind);
        Assert.Equal(noOverlay.Returned.SchemaExecutions, overlay.Returned.SchemaExecutions);
        var failures = Assert.IsAssignableFrom<IReadOnlyList<IlSqlLifecycleExtractor.Failure>>(overlay.Failures);
        var merges = failures.Where(f => f.Operation == "table-merge").ToArray();
        Assert.Equal(2, merges.Length);
        var firstDestination = overlay.Returned.Table;
        Assert.Equal("execution:0", Assert.Single(merges[1].Before, s => s.Allocation == firstDestination).Execution);
        var changed = Assert.Single(merges[1].NormalCompletion, after => after.Execution == "execution:1"
            && merges[1].Before.Any(before => before.Allocation == after.Allocation && before.RowsCleared));
        Assert.Null(Assert.Single(merges[1].Before, s => s.Allocation == changed.Allocation).Execution);
        var write = Assert.Single(failures, f => f.Operation == "row-write");
        Assert.True(write.MayPartiallyMutate); Assert.True(write.MayHaveCompletedEarlierRows);
        Assert.Empty(Assert.Single(write.Before, s => s.Allocation == firstDestination).Writes);
        Assert.NotEmpty(Assert.Single(write.NormalCompletion, s => s.Allocation == firstDestination).Writes);
        var projection = Assert.Single(failures, f => f.Operation == "list-add");
        Assert.True(projection.MayHaveCompletedEarlierRows);
        Assert.Equal(write.CatchHandler.Body, projection.CatchHandler.Body);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Execution_table_and_projection_identities_survive_refresh_and_reload(string language)
    {
        var bodies = Bodies(language);
        IlSqlLifecycleExtractor.Result Run(string name)
        {
            var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == name);
            var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(200_000));
            Assert.Empty(result.Gaps); Assert.NotNull(result.Returned); return result;
        }
        var first = Run("First"); var second = Run("Second"); var typed = Run("Typed");
        Assert.Equal("execution:0", first.Returned!.Value.Execution);
        Assert.Equal("execution:1", second.Returned!.Value.Execution);
        Assert.NotEqual(first.Returned.Table, second.Returned.Table);
        Assert.Equal(first.Returned.ColumnHash, typed.Returned!.ColumnHash);
        Assert.NotEqual(first.Returned.Site.Body, typed.Returned.Site.Body);
        Assert.Equal("execution:0", typed.Returned.Value.Execution);
        var refreshed = Run("Refresh");
        Assert.Equal(3, refreshed.Executions.Count);
        Assert.Equal("execution:2", refreshed.Returned!.Value.Execution);
        Assert.Equal(first.Returned.Table, refreshed.Returned.Table);
        Assert.Equal(new[] { "execution:0", "execution:2" }, refreshed.Returned.SchemaExecutions);
        var reloaded = Run("Reload");
        Assert.Equal(4, reloaded.Executions.Count);
        Assert.Equal("execution:2", reloaded.Returned!.Value.Execution);
        Assert.NotEqual(first.Returned.Table, reloaded.Returned.Table);
        Assert.Equal("execution:2", Assert.Single(reloaded.Returned.SchemaExecutions));
    }

    [Theory]
    [InlineData("csharp", "CrossRow")]
    [InlineData("vb", "CrossRow")]
    [InlineData("csharp", "LoopCarried")]
    [InlineData("vb", "LoopCarried")]
    [InlineData("csharp", "Escaped")]
    [InlineData("vb", "Escaped")]
    public void Unproven_loop_effects_and_escapes_discard_positive_claims(string language, string method)
    {
        var bodies = Bodies(language);
        var root = Assert.Single(bodies, b => b.Input.Role == "end-to-end" && b.Observation.ObjectBody?.Name == method);
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(200_000));
        Assert.NotEmpty(result.Gaps); Assert.Empty(result.Reads); Assert.Empty(result.Executions); Assert.Null(result.Returned);
    }

    [Theory]
    [InlineData("csharp", false)]
    [InlineData("csharp", true)]
    [InlineData("vb", false)]
    [InlineData("vb", true)]
    public void Successful_symbolic_path_preserves_schema_but_tracks_overlay_value(string language, bool enabled)
    {
        var bodies = Bodies(language);
        var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == "Score");
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, enabled, new(200_000));
        Assert.Empty(result.Gaps);
        Assert.Equal(2, result.Executions.Count);
        var read = Assert.IsType<IlSqlLifecycleExtractor.Read>(result.Returned);
        Assert.Equal("execution:0", Assert.Single(read.SchemaExecutions));
        Assert.Equal(enabled ? "application" : "sql", read.Value.Kind);
        Assert.Equal(enabled ? null : "execution:0", read.Value.Execution);
        Assert.Equal(enabled, read.Value.Assignment is not null);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Framework_catalog_requires_signed_identity_and_admitted_generic_owner(string language)
    {
        var bodies = Bodies(language);
        var owners = bodies.Select(b => b.Observation.ObjectBody!.OwnerType).ToArray();
        var calls = bodies.SelectMany(b => b.Observation.Calls).Select(c => c.TargetIdentity).Distinct().ToArray();
        var constructor = Assert.Single(calls, c => IlSqlLifecycleApis.Resolve(c, owners) == "list-new");
        Assert.Null(IlSqlLifecycleApis.Resolve(constructor, []));
        Assert.Null(IlSqlLifecycleApis.Resolve(constructor.Replace("b77a5c561934e089", "0000000000000000", StringComparison.Ordinal), owners));
        Assert.Contains(calls, c => IlSqlLifecycleApis.Resolve(c, owners) == "row-write");
        Assert.Contains(calls, c => IlSqlLifecycleApis.Resolve(c, owners) == "rows-add");
        Assert.Contains(calls, c => IlSqlLifecycleApis.Resolve(c, owners) == "enumerator-next");
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Lifecycle_bodies_have_dual_reader_agreed_control_flow(string language)
    {
        var bodies = Bodies(language);
        var load = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == "Load");
        Assert.NotEmpty(load.Observation.ObjectBody!.ExceptionRegions!);
        foreach (var body in bodies.Where(b => b.Input.Role == "end-to-end"))
        {
            output.WriteLine(body.Observation.ObjectBody!.Name);
            foreach (var instruction in body.Observation.ObjectBody.Instructions) output.WriteLine(instruction);
            foreach (var call in body.Observation.Calls) output.WriteLine($"CALL {call.Offset:x}: {call.TargetIdentity}");
            foreach (var region in body.Observation.ObjectBody.ExceptionRegions!) output.WriteLine(region);
        }
    }

    internal static IReadOnlyList<IlSqlObjectPathExtractor.Body> Bodies(string language)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var result = new List<IlSqlObjectPathExtractor.Body>();
        foreach (var role in new[] { "end-to-end", "wrapper" })
        {
            var path = Path.Combine(root.FullName, "samples", "sql-compiled-wrapper-evidence", language,
                role, "bin", config, "net48", $"PublicSql.{language}.{role}.dll");
            var bytes = File.ReadAllBytes(path);
            var left = IlBodyEvidenceExtractor.ReadCecilBodies(bytes, new(), new(200_000), CancellationToken.None);
            var right = IlBodyEvidenceExtractor.ReadSystemReflectionMetadataBodies(bytes, new(), new(200_000), CancellationToken.None);
            Assert.Empty(IlBodyEvidenceExtractor.CompareBodies(left, right));
            var agreed = IlBodyEvidenceExtractor.AgreeValueFlows(left.Bodies, right.Bodies);
            Assert.All(agreed, b => Assert.NotNull(b.ObjectBody));
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var input = new IlInputOutcome(Path.GetFileName(path), role, "admitted", "local", hash, hash,
                left.AssemblyIdentity, left.ModuleName, left.ModuleMvid, null, []);
            result.AddRange(agreed.Select(b => new IlSqlObjectPathExtractor.Body(b, input)));
        }
        return result;
    }
}
