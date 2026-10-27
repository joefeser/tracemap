using System.Security.Cryptography;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class IlSqlObjectPathExtractorTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Native_pipeline_emits_exact_root_body_hash_links_and_separate_tier3_paths(string language)
    {
        var caller = AssemblyPath(language, "caller");
        var wrapper = AssemblyPath(language, "wrapper");
        var result = ScanEngine.Scan(new ScanOptions(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(caller))))!,
            IlBodyEvidenceExtractorShared.TempOutput(), CompiledInputPaths: [caller, wrapper], IlBodyEvidence: true));
        var paths = result.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlSqlObjectPathCandidate).ToArray();
        Assert.Equal(5, paths.Length);
        Assert.Equal(5, result.Facts.Count(fact => fact.FactType == FactTypes.AnalysisGap && fact.RuleId == RuleIds.DotNetIlSqlObjectPath));
        var byId = result.Facts.ToDictionary(fact => fact.FactId);
        foreach (var path in paths)
        {
            Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, path.EvidenceTier);
            Assert.Equal(RuleIds.DotNetIlSqlObjectPath, path.RuleId);
            Assert.Equal(result.Manifest.IlBodyProvenance!.GeneratorSha256, path.Properties["ilGeneratorSha256"]);
            Assert.Equal(result.Manifest.IlBodyProvenance.BoundedInputSha256, path.Properties["ilBoundedInputSha256"]);
            Assert.Equal(path.TargetSymbol, byId[path.Properties["rootBodyFactId"]].TargetSymbol);
            using var sites = System.Text.Json.JsonDocument.Parse(path.Properties["sites"]);
            foreach (var site in sites.RootElement.EnumerateArray())
            {
                var body = byId[site.GetProperty("bodyFactId").GetString()!];
                Assert.Equal(FactTypes.ManagedIlBodyDeclared, body.FactType);
                Assert.Equal(body.TargetSymbol, site.GetProperty("BodyIdentity").GetString());
                Assert.Equal(body.Properties["rawFileSha256"], site.GetProperty("BinarySha256").GetString());
            }
            Assert.DoesNotContain("@Name", path.Properties["parameterShapes"], StringComparison.Ordinal);
            Assert.DoesNotContain("public.lookup", path.Properties["commandText"], StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Binary_ambiguity_missing_body_untrusted_provenance_and_budgets_refuse(string language)
    {
        var bodies = Bodies(language);
        var root = bodies.Single(body => body.Input.Role == "caller" && body.Observation.ObjectBody!.Name == "Direct");
        var wrapper = bodies.Single(body => body.Input.Role == "wrapper" && body.Observation.ObjectBody!.Name == "Direct");
        var variants = new[]
        {
            bodies.Where(body => body != wrapper).ToArray(),
            bodies.Append(wrapper).ToArray(),
            bodies.Append(wrapper with { Input = wrapper.Input with { RawFileSha256 = new string('a', 64) } }).ToArray(),
            bodies.Select(body => body == wrapper ? body with { Observation = body.Observation with { ObjectBody = null } } : body).ToArray(),
            bodies.Select(body => body == wrapper ? body with { Input = body.Input with { Outcome = "partial" } } : body).ToArray(),
            bodies.Select(body => body == wrapper ? body with { Input = body.Input with { RawFileSha256 = "invalid" } } : body).ToArray(),
            bodies.Select(body => body == wrapper ? body with { Observation = body.Observation with { ObjectBody = body.Observation.ObjectBody! with { IsVirtual = true } } } : body).ToArray(),
            bodies.Select(body => body == wrapper ? body with { Observation = body.Observation with { ExceptionRegionCount = 1 } } : body).ToArray(),
            bodies.Select(body => body == wrapper ? body with { Observation = body.Observation with { ObjectBody = body.Observation.ObjectBody! with { IsGeneric = true } } } : body).ToArray(),
            bodies.Select(body => body == wrapper ? body with { Input = body.Input with { ModuleMvid = "invalid" } } : body).ToArray()
        };
        foreach (var variant in variants)
        {
            var result = IlSqlObjectPathExtractor.Extract(root, variant, new(100_000));
            Assert.Empty(result.Paths); Assert.NotEmpty(result.Gaps);
        }
        var budget = new IlBodyEvidenceExtractor.IlWorkBudget(100_000);
        Assert.Single(IlSqlObjectPathExtractor.Extract(root, bodies, budget).Paths);
        Assert.Single(IlSqlObjectPathExtractor.Extract(root, bodies, new(budget.Consumed)).Paths);
        Assert.Empty(IlSqlObjectPathExtractor.Extract(root, bodies, new(budget.Consumed - 1)).Paths);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Field_ownership_wrong_framework_key_metadata_agreement_and_instruction_bounds_refuse(string language)
    {
        var bodies = Bodies(language);
        var root = bodies.Single(body => body.Input.Role == "caller" && body.Observation.ObjectBody!.Name == "MergeFresh");
        var meta = root.Observation.ObjectBody!;
        var variants = new[]
        {
            root.Observation with { ObjectBody = meta with { PrivateInstanceFields = [] } },
            root.Observation with { ObjectBody = meta with { Instructions = Enumerable.Repeat("0:0:nop:", 257).ToArray() } },
            root.Observation with { ObjectBody = meta with { Instructions = ["0:invalid:nop:"] } },
            root.Observation with { ObjectBody = meta with { Instructions = ["0:0:calli:"] } },
            root.Observation with { Calls = root.Observation.Calls.Select(call => call with
                { TargetIdentity = call.TargetIdentity.Replace("b77a5c561934e089", "0000000000000000", StringComparison.Ordinal) }).ToArray() },
        };
        foreach (var changed in variants)
        {
            var altered = root with { Observation = changed };
            var result = IlSqlObjectPathExtractor.Extract(altered, bodies.Select(body => body == root ? altered : body).ToArray(), new(100_000));
            Assert.Empty(result.Paths);
            // Wrong framework identity is not even a recognized root; no false positive.
            if (IlSqlObjectPathExtractor.IsRoot(changed)) Assert.NotEmpty(result.Gaps);
        }
        var disagreed = IlBodyEvidenceExtractor.AgreeValueFlows([root.Observation],
            [root.Observation with { ObjectBody = meta with { PrivateInstanceFields = [] } }]);
        Assert.Null(Assert.Single(disagreed).ObjectBody);
        Assert.Equal(root.Observation.ValueFlow, disagreed[0].ValueFlow);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Recursive_exact_body_and_nonfirst_table_are_explicit_unknown(string language)
    {
        var bodies = Bodies(language);
        var root = bodies.Single(body => body.Input.Role == "caller" && body.Observation.ObjectBody!.Name == "Direct");
        var wrapper = bodies.Single(body => body.Input.Role == "wrapper" && body.Observation.ObjectBody!.Name == "Direct");
        var original = wrapper.Observation;
        var first = original.Calls.First();
        var recursive = wrapper with { Observation = original with
        {
            Calls = original.Calls.Select(call => call == first ? call with
            { Opcode = "call", TargetIdentity = original.MethodIdentity, StackShape = new(1, false, true, true) } : call).ToArray(),
            ObjectBody = original.ObjectBody! with { Instructions = original.ObjectBody!.Instructions.Select(line =>
                line.Split(':', 4)[1] == first.Offset.ToString("x", System.Globalization.CultureInfo.InvariantCulture)
                    ? line.Replace(":newobj:", ":call:", StringComparison.Ordinal) : line).ToArray() }
        } };
        var result = IlSqlObjectPathExtractor.Extract(root, bodies.Select(body => body == wrapper ? recursive : body).ToArray(), new(100_000));
        Assert.Empty(result.Paths);
        Assert.Contains("IlSqlObjectRecursionOrDepth", result.Gaps);
        var selector = root with { Observation = root.Observation with { ObjectBody = root.Observation.ObjectBody! with
        { Instructions = root.Observation.ObjectBody!.Instructions.Select(line => line.Replace(":ldc.i4.0:", ":ldc.i4.1:", StringComparison.Ordinal)).ToArray() } } };
        result = IlSqlObjectPathExtractor.Extract(selector, bodies.Select(body => body == root ? selector : body).ToArray(), new(100_000));
        Assert.Empty(result.Paths);
        Assert.Contains("IlSqlObjectSelectorUnsupported", result.Gaps);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Independently_authored_twenty_root_matrix_preserves_positive_and_negative_legs(string language)
    {
        var bodies = Bodies(language);
        var roots = bodies.Where(body => body.Input.Role == "caller" && IlSqlObjectPathExtractor.IsRoot(body.Observation)).ToArray();
        Assert.Equal(10, roots.Length);
        var positives = new[] { "Direct", "Forwarded", "FieldObject", "PropertyObject", "MergeFresh" };
        foreach (var root in roots)
        {
            var name = root.Observation.ObjectBody!.Name;
            var result = IlSqlObjectPathExtractor.Extract(root, bodies, new(100_000));
            if (!positives.Contains(name, StringComparer.Ordinal))
            {
                Assert.Empty(result.Paths);
                Assert.NotEmpty(result.Gaps);
                continue;
            }
            Assert.True(result.Gaps.Count == 0, name + ": " + string.Join(",", result.Gaps) + "\n" +
                string.Join("\n", bodies.Where(b => b.Observation.ObjectBody!.Name == "Direct").SelectMany(b => b.Observation.Calls).Select(c => c.TargetIdentity)));
            var path = Assert.Single(result.Paths);
            var parameter = Assert.Single(path.Parameters);
            Assert.Equal(new IlValueOrigin("constant-int32", "12"), parameter.SqlDbType);
            Assert.Equal(new IlValueOrigin("constant-int32", "40"), parameter.Size);
            Assert.Equal("constant-string-hash", parameter.Name.Kind);
            Assert.Equal("constant-string-hash", path.CommandText.Kind);
            Assert.Single(path.Sites, site => site.Action == "fill");
            Assert.Single(path.Sites, site => site.Action == "column-consumer");
            Assert.Equal(2, path.Sites.Select(site => site.BinarySha256).Distinct().Count());
            if (name == "MergeFresh")
            {
                Assert.NotEqual(path.FilledDataSetIdentity, path.ConsumerDataSetIdentity);
                Assert.Equal("fresh-empty-schema-union-candidate", path.Relation);
            }
            else Assert.Equal(path.FilledDataSetIdentity, path.ConsumerDataSetIdentity);
        }
    }

    private static IReadOnlyList<IlSqlObjectPathExtractor.Body> Bodies(string language)
    {
        var output = new List<IlSqlObjectPathExtractor.Body>();
        foreach (var role in new[] { "caller", "wrapper" })
        {
            var path = AssemblyPath(language, role);
            var bytes = File.ReadAllBytes(path);
            var left = IlBodyEvidenceExtractor.ReadCecilBodies(bytes, new(), new(200_000), CancellationToken.None);
            var right = IlBodyEvidenceExtractor.ReadSystemReflectionMetadataBodies(bytes, new(), new(200_000), CancellationToken.None);
            Assert.Empty(IlBodyEvidenceExtractor.CompareBodies(left, right));
            var agreed = IlBodyEvidenceExtractor.AgreeValueFlows(left.Bodies, right.Bodies);
            Assert.All(agreed, body => Assert.NotNull(body.ObjectBody));
            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var input = new IlInputOutcome(Path.GetFileName(path), role, "admitted", "local", sha, sha,
                left.AssemblyIdentity, left.ModuleName, left.ModuleMvid, null, []);
            output.AddRange(agreed.Select(body => new IlSqlObjectPathExtractor.Body(body, input)));
        }
        return output;
    }

    private static string AssemblyPath(string language, string role)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        return Path.Combine(root.FullName, "samples", "sql-compiled-wrapper-evidence", language,
            role, "bin", config, "net48", $"PublicSql.{language}.{role}.dll");
    }
}
