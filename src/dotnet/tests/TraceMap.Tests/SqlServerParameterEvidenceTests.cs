using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class SqlServerParameterEvidenceTests
{
    private const string Revision = "1111111111111111111111111111111111111111";
    private static readonly SqlServerObjectIdentity Object = new("SyntheticServer", "SyntheticDatabase", "dbo", "ReadItem");
    private static SqlServerDeclarationParameter Parameter => new("@Name", "NVarChar", 20, "utf16-code-units", "Input", false, null, null, null, 2);
    private static SqlServerDeclarations Declarations => new("sqlserver-declarations/1", "synthetic", Revision,
        "independent-synthetic-declarations", "ordinal-exact-no-aliases", [new(Object, "declarations.json", 1, 10, [Parameter])]);
    private static SqlServerSchemaExport Export(SqlServerDeclarations? d = null) => SqlServerSchemaEvidence.Produce(SqlServerSchemaEvidence.Json(d ?? Declarations));
    private static CodeFact Fact => new("fact", "scan", "synthetic", Revision, null, FactTypes.DatabaseOperationCandidate,
        RuleIds.DatabaseOperationCallPattern, EvidenceTiers.Tier1Semantic, "Example.Read()", null, null,
        new("Calls.cs", 5, 5, null, "CSharpSemanticExtractor", ScannerVersions.CSharpSemanticExtractor),
        new Dictionary<string, string> { ["sqlServerParameterState"] = SqlServerSchemaEvidence.Json(new SqlServerCallState("sqlserver-call/1", "bounded", "StoredProcedure",
            SqlServerSchemaEvidence.Hash(System.Text.Encoding.UTF8.GetBytes("dbo.ReadItem")), [new("@Name", "NVarChar", 20, "Input", 3, "utf16-code-units")], null)) });
    private static SqlServerBindings Bindings(CodeFact f, SqlServerObjectIdentity? identity = null) => new("sqlserver-bindings/1", f.Repo, f.CommitSha,
        [new(f.FactId, (identity ?? Object).Schema + "." + (identity ?? Object).Procedure, identity ?? Object, SqlServerSchemaEvidence.CallDigest(f))]);
    private static SqlServerComparisonReport Compare(CodeFact? fact = null, SqlServerDeclarations? declarations = null, SqlServerBindings? bindings = null)
    {
        var f = fact ?? Fact;
        return SqlServerSchemaEvidence.Compare(Export(declarations), bindings ?? Bindings(f), [f], f.Repo, f.CommitSha);
    }

    [Fact]
    public void Export_roundtrip_is_deterministic_and_binds_exact_generator_and_public_projection()
    {
        var d = SqlServerSchemaEvidence.Json(Declarations);
        var export = Export();
        Assert.Equal(SqlServerSchemaEvidence.GeneratorSha256, export.GeneratorSha256);
        Assert.Equal(SqlServerSchemaEvidence.Hash(File.ReadAllBytes(typeof(SqlServerSchemaEvidence).Assembly.Location)), export.GeneratorSha256);
        Assert.Equal(SqlServerSchemaEvidence.Digest(Declarations), export.BoundedInputSha256);
        Assert.Equal(SqlServerSchemaEvidence.Json(export), SqlServerSchemaEvidence.Json(SqlServerSchemaEvidence.Produce("\n" + d + "\n")));
        Assert.Equal(SqlServerSchemaEvidence.Json(export), SqlServerSchemaEvidence.Json(SqlServerSchemaEvidence.Admit(SqlServerSchemaEvidence.Json(export), d, "synthetic", Revision)));
        var report = Compare();
        Assert.Equal("bounded-match", Assert.Single(report.Comparisons).Status);
        Assert.Equal("partial", report.Coverage);
        Assert.Equal("no-aggregate-clean-verdict", report.Verdict);
        Assert.Equal(SqlServerSchemaEvidence.Json(report), SqlServerSchemaEvidence.Json(Compare()));
        Assert.Equal(SqlServerSchemaEvidence.Json(report), SqlServerSchemaEvidence.Json(SqlServerSchemaEvidence.Read<SqlServerComparisonReport>(SqlServerSchemaEvidence.Json(report))));
        Assert.DoesNotContain("scannedAt", SqlServerSchemaEvidence.Json(report));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("generator")]
    [InlineData("input-hash")]
    [InlineData("payload")]
    [InlineData("revision")]
    [InlineData("substitution")]
    public void Export_admission_refuses_tampering_and_unexpected_inputs(string mode)
    {
        var e = Export(); var d = Declarations;
        e = mode switch { "version" => e with { Version = "sqlserver-schema/2" }, "generator" => e with { GeneratorSha256 = new('0', 64) },
            "input-hash" => e with { BoundedInputSha256 = new('0', 64) }, "payload" => e with { Declarations = d with { Origin = "runtime" } }, _ => e };
        if (mode == "substitution") d = d with { Procedures = [d.Procedures[0] with { Parameters = [Parameter with { Size = 30 }] }] };
        Assert.Throws<ArgumentException>(() => SqlServerSchemaEvidence.Admit(SqlServerSchemaEvidence.Json(e), SqlServerSchemaEvidence.Json(d), "synthetic", mode == "revision" ? new('2', 40) : Revision));
    }

    [Theory]
    [InlineData("duplicate-object")]
    [InlineData("duplicate-parameter")]
    [InlineData("null-object")]
    [InlineData("null-parameter")]
    [InlineData("invalid-location")]
    [InlineData("unsupported-type")]
    [InlineData("output-only")]
    [InlineData("wrong-units")]
    [InlineData("missing-size")]
    public void Malformed_or_ambiguous_declarations_are_not_admitted(string mode)
    {
        var p = Declarations.Procedures[0];
        var d = mode switch
        {
            "duplicate-object" => Declarations with { Procedures = [p, p] },
            "null-object" => Declarations with { Procedures = [null!] },
            "duplicate-parameter" => Declarations with { Procedures = [p with { Parameters = [Parameter, Parameter with { Name = "@name" }] }] },
            "null-parameter" => Declarations with { Procedures = [p with { Parameters = [null!] }] },
            "invalid-location" => Declarations with { Procedures = [p with { FilePath = "../private.sql" }] },
            _ => Declarations with { Procedures = [p with { Parameters = [mode switch
            {
                "unsupported-type" => Parameter with { SqlType = "Alias" }, "output-only" => Parameter with { Direction = "Output" },
                "wrong-units" => Parameter with { Units = "bytes" }, _ => Parameter with { Size = null }
            }] }] }
        };
        Assert.Throws<ArgumentException>(() => Export(d));
    }

    [Theory]
    [InlineData("{\"version\":\"a\",\"version\":\"b\"}")]
    [InlineData("{\"version\":\"sqlserver-declarations/1\"}")]
    [InlineData("null")]
    public void Strict_json_rejects_duplicates_missing_fields_and_null(string json) => Assert.ThrowsAny<Exception>(() => SqlServerSchemaEvidence.Produce(json));

    [Theory]
    [InlineData("server")]
    [InlineData("database")]
    [InlineData("schema")]
    [InlineData("procedure")]
    [InlineData("missing")]
    public void Wrong_or_missing_identity_cannot_join_by_short_name(string mode)
    {
        var i = mode switch { "server" => Object with { Server = "Other" }, "database" => Object with { Database = "Other" },
            "schema" => Object with { Schema = "other" }, "procedure" => Object with { Procedure = "Other" }, _ => Object };
        var b = Bindings(Fact, i);
        if (mode == "missing") b = b with { Calls = [] };
        var row = Assert.Single(Compare(bindings: b).Comparisons);
        Assert.Equal("unknown", row.Status);
        Assert.Null(row.Declaration);
    }

    [Fact]
    public void Conflicting_duplicate_stale_and_wrong_revision_bindings_fail_closed()
    {
        var b = Bindings(Fact);
        Assert.Throws<ArgumentException>(() => Compare(bindings: b with { Calls = [b.Calls[0], b.Calls[0]] }));
        Assert.Throws<ArgumentException>(() => Compare(bindings: b with { Calls = [b.Calls[0] with { FactId = "stale" }] }));
        Assert.Throws<ArgumentException>(() => Compare(bindings: b with { Revision = new('2', 40) }));
        Assert.Throws<ArgumentException>(() => Compare(bindings: b with { Calls = [b.Calls[0] with { SourceEvidenceSha256 = new('0', 64) }] }));
        Assert.Equal("unknown", Assert.Single(Compare(fact: Fact with { Evidence = Fact.Evidence with { ExtractorVersion = "old" } }).Comparisons).Status);
    }

    [Fact]
    public void Public_input_digest_ignores_unprojected_private_properties_and_is_sensitive_to_bound_evidence()
    {
        var report = Compare();
        var props = new Dictionary<string, string>(Fact.Properties) { ["rawSql"] = "PRIVATE", ["connectionString"] = "SECRET" };
        var projected = Compare(Fact with { Properties = props, Evidence = Fact.Evidence with { SnippetHash = new('f', 64) } });
        Assert.Equal(report.BoundedInputSha256, projected.BoundedInputSha256);
        Assert.DoesNotContain("PRIVATE", SqlServerSchemaEvidence.Json(projected));
        Assert.DoesNotContain("SECRET", SqlServerSchemaEvidence.Json(projected));
        Assert.NotEqual(report.BoundedInputSha256, Compare(Fact with { Evidence = Fact.Evidence with { StartLine = 4 } }).BoundedInputSha256);
    }

    [Theory]
    [InlineData("precision")]
    [InlineData("scale")]
    [InlineData("collation")]
    public void Unsupported_declaration_facets_remain_unknown(string facet)
    {
        var parameter = facet switch { "precision" => Parameter with { Precision = 10 }, "scale" => Parameter with { Scale = 2 }, _ => Parameter with { Collation = "Latin1_General_CI_AS" } };
        Assert.Equal("unknown", Assert.Single(Compare(declarations: Declarations with { Procedures = [Declarations.Procedures[0] with { Parameters = [parameter] }] }).Comparisons).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unsupported_facets_preserve_proven_type_and_direction_reasons(bool decimalType)
    {
        var parameter = decimalType
            ? Parameter with { SqlType = "Decimal", Size = null, Units = "not-applicable", Direction = "InputOutput", Precision = 10, Scale = 2 }
            : Parameter with { SqlType = "VarChar", Units = "bytes", Direction = "InputOutput", Collation = "Latin1_General_CI_AS" };
        var row = Assert.Single(Compare(declarations: Declarations with { Procedures = [Declarations.Procedures[0] with { Parameters = [parameter] }] }).Comparisons);
        Assert.Equal("unknown", row.Status);
        Assert.Equal(decimalType
            ? new[] { "direction:@Name", "type:@Name", "unsupported-facets:@Name" }
            : new[] { "direction:@Name", "type:@Name", "unsupported-facets:@Name", "varchar-provider-length-policy-unsupported:@Name" }, row.Reasons);
    }

    [Fact]
    public async Task New_output_publication_has_one_winner_and_preserves_existing_files()
    {
        using var temp = new TempDirectory();
        var file = Path.Combine(temp.Path, "export.json");
        var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() =>
        {
            try { SqlServerSchemaCommand.PublishNewFile(file, i.ToString(System.Globalization.CultureInfo.InvariantCulture)); return true; }
            catch (IOException) { return false; }
        })));
        Assert.Equal(1, attempts.Count(success => success));
        var original = File.ReadAllText(file);
        Assert.Throws<IOException>(() => SqlServerSchemaCommand.PublishNewFile(file, "replacement"));
        Assert.Equal(original, File.ReadAllText(file));
        Assert.Single(Directory.GetFileSystemEntries(temp.Path));

        var directory = Path.Combine(temp.Path, "report");
        using var barrier = new Barrier(2);
        var winners = await Task.WhenAll(Enumerable.Range(0, 2).Select(i => Task.Run(() =>
        {
            try
            {
                SqlServerSchemaCommand.PublishNewDirectory(directory, staging =>
                {
                    var value = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    File.WriteAllText(Path.Combine(staging, "parameter-comparison.json"), value);
                    File.WriteAllText(Path.Combine(staging, "report.md"), value);
                    Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(30)));
                });
                return true;
            }
            catch (IOException) { return false; }
        })));
        Assert.Equal(1, winners.Count(success => success));
        Assert.Equal(File.ReadAllText(Path.Combine(directory, "parameter-comparison.json")), File.ReadAllText(Path.Combine(directory, "report.md")));
        Assert.Equal(2, Directory.GetFileSystemEntries(temp.Path).Length);
    }

    [Fact]
    public void Failed_report_publication_removes_staging_and_allows_retry()
    {
        using var temp = new TempDirectory();
        var destination = Path.Combine(temp.Path, "report");
        Assert.Throws<IOException>(() => SqlServerSchemaCommand.PublishNewDirectory(destination, staging =>
        {
            File.WriteAllText(Path.Combine(staging, "parameter-comparison.json"), "partial");
            throw new IOException("injected second-write failure");
        }));
        Assert.Empty(Directory.GetFileSystemEntries(temp.Path));
        SqlServerSchemaCommand.PublishNewDirectory(destination, staging =>
        {
            File.WriteAllText(Path.Combine(staging, "parameter-comparison.json"), "complete");
            File.WriteAllText(Path.Combine(staging, "report.md"), "complete");
        });
        Assert.Equal(2, Directory.GetFiles(destination).Length);
    }

    [Fact]
    public void Cleanup_failure_preserves_primary_exception_and_secondary_evidence()
    {
        using var temp = new TempDirectory();
        var primary = new IOException("writer failed");
        var secondary = new IOException("cleanup failed");
        var caught = Assert.Throws<IOException>(() => SqlServerSchemaCommand.PublishNewDirectory(
            Path.Combine(temp.Path, "report"), _ => throw primary, _ => throw secondary));
        Assert.Same(primary, caught);
        Assert.Same(secondary, caught.Data["SqlServerOutputCleanupFailure"]);
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "report")));
    }

    [Fact]
    public void Durable_writer_flushes_encoded_bytes_before_disk_flush()
    {
        using var temp = new TempDirectory();
        using var stream = new FlushObservingStream(Path.Combine(temp.Path, "bytes"));
        SqlServerSchemaCommand.WriteDurably(stream, "SQL café\n");
        Assert.True(stream.DiskFlushObserved);
        stream.Position = 0;
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes("SQL café\n"), bytes);
    }

    private sealed class FlushObservingStream(string path) : FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)
    {
        public bool DiskFlushObserved { get; private set; }
        public override void Flush(bool flushToDisk)
        {
            if (flushToDisk)
            {
                Assert.True(Length > 0);
                DiskFlushObserved = true;
            }
            base.Flush(flushToDisk);
        }
    }

    [Theory]
    [InlineData("cs", "good")]
    [InlineData("cs", "argument-command")]
    [InlineData("vb", "argument-command")]
    [InlineData("cs", "argument-parameter")]
    [InlineData("vb", "argument-parameter")]
    [InlineData("cs", "argument-default")]
    [InlineData("vb", "argument-default")]
    [InlineData("cs", "indexed-mutation")]
    [InlineData("cs", "inline-mutation")]
    [InlineData("cs", "assigned-add")]
    [InlineData("cs", "alias")]
    [InlineData("cs", "reassigned")]
    [InlineData("cs", "clear")]
    [InlineData("cs", "wrapper")]
    [InlineData("cs", "conditional")]
    [InlineData("cs", "loop")]
    [InlineData("cs", "dynamic-name")]
    [InlineData("cs", "parameter-limit")]
    [InlineData("cs", "operation-limit")]
    [InlineData("vb", "good")]
    [InlineData("vb", "indexed-mutation")]
    [InlineData("vb", "inline-mutation")]
    [InlineData("vb", "assigned-add")]
    [InlineData("vb", "alias")]
    [InlineData("vb", "reassigned")]
    [InlineData("vb", "clear")]
    [InlineData("vb", "wrapper")]
    [InlineData("vb", "conditional")]
    [InlineData("vb", "loop")]
    [InlineData("vb", "dynamic-name")]
    [InlineData("vb", "parameter-limit")]
    [InlineData("vb", "operation-limit")]
    [InlineData("cs", "collection-alias")]
    [InlineData("cs", "array-alias")]
    [InlineData("cs", "constructor-escape")]
    [InlineData("vb", "collection-alias")]
    [InlineData("vb", "array-alias")]
    [InlineData("vb", "constructor-escape")]
    [InlineData("cs", "cross-command-alias")]
    [InlineData("vb", "cross-command-alias")]
    [InlineData("cs", "initializer")]
    [InlineData("vb", "initializer")]
    [InlineData("vb", "with-command")]
    [InlineData("vb", "with-parameter")]
    public void Extractor_refuses_unsupported_mutation_and_work_bounds(string language, string shape)
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "AGENTS.md"))) repository = repository.Parent;
        using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository!.FullName, "samples/sqlserver-parameter-evidence/csharp/obj/project.assets.json")));
        var package = assets.RootElement.GetProperty("libraries").GetProperty("System.Data.SqlClient/4.9.0").GetProperty("path").GetString()!;
        var compile = assets.RootElement.GetProperty("targets").GetProperty("net10.0").GetProperty("System.Data.SqlClient/4.9.0").GetProperty("compile").EnumerateObject().Single(p => p.Name.EndsWith(".dll", StringComparison.Ordinal)).Name;
        var provider = assets.RootElement.GetProperty("packageFolders").EnumerateObject().Select(p => Path.Combine(p.Name, package, compile)).First(File.Exists);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(provider).Distinct(StringComparer.Ordinal).Select(p => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(p));
        var cs = "var command = new System.Data.SqlClient.SqlCommand(\"dbo.ReadItem\"); command.CommandType = System.Data.CommandType.StoredProcedure; var parameter = command.Parameters.Add(\"@Name\", System.Data.SqlDbType.NVarChar, 20);";
        var vb = "Dim command = New System.Data.SqlClient.SqlCommand(\"dbo.ReadItem\")\ncommand.CommandType = System.Data.CommandType.StoredProcedure\nDim parameter = command.Parameters.Add(\"@Name\", System.Data.SqlDbType.NVarChar, 20)\n";
        if (shape == "initializer")
        {
            cs = cs.Replace("(\"dbo.ReadItem\");", "(\"dbo.ReadItem\") { CommandText = \"dbo.Other\" };", StringComparison.Ordinal);
            vb = vb.Replace("(\"dbo.ReadItem\")\n", "(\"dbo.ReadItem\") With {.CommandText = \"dbo.Other\"}\n", StringComparison.Ordinal);
        }
        var changes = shape switch
        {
            "with-command" => ("", "With command\n.CommandText = \"dbo.Other\"\nEnd With"),
            "with-parameter" => ("", "With parameter\n.Direction = System.Data.ParameterDirection.Output\nEnd With"),
            "cross-command-alias" => ("var second = new System.Data.SqlClient.SqlCommand(); second.Parameters.Add(parameter);", "Dim second = New System.Data.SqlClient.SqlCommand()\nsecond.Parameters.Add(parameter)"),
            "collection-alias" => ("var collection = command.Parameters;", "Dim collection = command.Parameters"),
            "array-alias" => ("var array = new object[] { command };", "Dim array = New Object() { command }"),
            "constructor-escape" => ("var holder = new Mutator(command);", "Dim holder = New Mutator(command)"),
            "indexed-mutation" => ("command.Parameters[0].Direction = System.Data.ParameterDirection.Output;", "command.Parameters(0).Direction = System.Data.ParameterDirection.Output"),
            "inline-mutation" => ("command.Parameters.Add(\"@Other\", System.Data.SqlDbType.Int).Direction = System.Data.ParameterDirection.Output;", "command.Parameters.Add(\"@Other\", System.Data.SqlDbType.Int).Direction = System.Data.ParameterDirection.Output"),
            "assigned-add" => ("System.Data.SqlClient.SqlParameter next; next = command.Parameters.Add(\"@Other\", System.Data.SqlDbType.Int); next.Direction = System.Data.ParameterDirection.Output;", "Dim nextParameter As System.Data.SqlClient.SqlParameter\nnextParameter = command.Parameters.Add(\"@Other\", System.Data.SqlDbType.Int)\nnextParameter.Direction = System.Data.ParameterDirection.Output"),
            "alias" => ("var alias = parameter; alias.Direction = System.Data.ParameterDirection.Output;", "Dim aliasParameter = parameter\naliasParameter.Direction = System.Data.ParameterDirection.Output"),
            "reassigned" => ("parameter = new System.Data.SqlClient.SqlParameter();", "parameter = New System.Data.SqlClient.SqlParameter()"),
            "clear" => ("command.Parameters.Clear();", "command.Parameters.Clear()"),
            "wrapper" => ("Helper(command);", "Helper(command)"),
            "conditional" => ("if (flag) parameter.Direction = System.Data.ParameterDirection.Output;", "If flag Then parameter.Direction = System.Data.ParameterDirection.Output"),
            "loop" => ("while (flag) { parameter.Direction = System.Data.ParameterDirection.Output; }", "While flag\nparameter.Direction = System.Data.ParameterDirection.Output\nEnd While"),
            "dynamic-name" => ("command.Parameters.Add(input, System.Data.SqlDbType.Int);", "command.Parameters.Add(input, System.Data.SqlDbType.Int)"),
            "parameter-limit" => (string.Concat(Enumerable.Range(0, 129).Select(i => $"command.Parameters.Add(\"@P{i}\", System.Data.SqlDbType.Int);")), string.Join("\n", Enumerable.Range(0, 129).Select(i => $"command.Parameters.Add(\"@P{i}\", System.Data.SqlDbType.Int)"))),
            "operation-limit" => (string.Concat(Enumerable.Range(0, 6000).Select(i => $"int unused{i} = 1;")), string.Join("\n", Enumerable.Range(0, 6000).Select(i => $"Dim unused{i} = 1"))),
            _ => ("", "")
        };
        Microsoft.CodeAnalysis.Compilation compilation;
        Microsoft.CodeAnalysis.SyntaxTree tree;
        var argument = shape switch { "argument-command" => "Mutate(command)", "argument-parameter" => "Mutate(parameter)", "argument-default" => language == "cs" ? "default(System.Threading.CancellationToken)" : "Nothing", _ => null };
        var execution = argument is null ? "command.ExecuteScalar()" : "command.ExecuteScalarAsync(" + argument + ")";
        if (language == "cs")
        {
            tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("class Mutator { public Mutator(object o) {} } class Example { static void Helper(object o) {} static System.Threading.CancellationToken Mutate(object o) { return default; } static void Run(string input, bool flag) { " + cs + changes.Item1 + execution + "; } }");
            compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("Synthetic", [tree], references, new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
        }
        else
        {
            tree = Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxTree.ParseText("Class Mutator\nPublic Sub New(o As Object)\nEnd Sub\nEnd Class\nModule Example\nSub Helper(o As Object)\nEnd Sub\nFunction Mutate(o As Object) As System.Threading.CancellationToken\nReturn Nothing\nEnd Function\nSub Run(input As String, flag As Boolean)\n" + vb + changes.Item2 + "\n" + execution + "\nEnd Sub\nEnd Module");
            compilation = Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilation.Create("Synthetic", [tree], references, new Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
        }
        Assert.DoesNotContain(compilation.GetDiagnostics(), d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        var model = compilation.GetSemanticModel(tree);
        var call = Assert.Single(tree.GetRoot().DescendantNodes().Select(n => model.GetOperation(n)).OfType<Microsoft.CodeAnalysis.Operations.IInvocationOperation>(), o => o.TargetMethod.Name is "ExecuteScalar" or "ExecuteScalarAsync");
        var properties = new SortedDictionary<string, string>();
        SqlServerCommandParameters.Add(properties, call);
        var state = SqlServerSchemaEvidence.Read<SqlServerCallState>(properties["sqlServerParameterState"]);
        Assert.Equal(shape is "good" or "argument-default" ? "bounded" : "unknown", state.Coverage);
        if (shape is "argument-command" or "argument-parameter") Assert.Equal("unsupported-execution-argument", state.Gap);
        if (shape == "operation-limit") Assert.Equal("operation-limit", state.Gap);
        if (shape == "parameter-limit") Assert.Equal("parameter-limit-or-no-command", state.Gap);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Native_synthetic_matrix_preserves_exact_command_parameter_state_and_report_seam(string language)
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "AGENTS.md"))) repo = repo.Parent;
        var sample = Path.Combine(repo!.FullName, "samples/sqlserver-parameter-evidence");
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(sample, Path.Combine(temp.Path, "scan"),
            ProjectPaths: [Path.Combine(sample, language, language == "vb" ? "Parameters.vbproj" : "Parameters.csproj")]));
        Assert.Equal("Succeeded", scan.Manifest.BuildStatus);
        var calls = scan.Facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate && f.EvidenceTier == EvidenceTiers.Tier1Semantic).ToArray();
        Assert.Equal(26, calls.Length);
        var expected = new Dictionary<string, string>
        {
            ["Good"] = "bounded-match", ["SizeMismatch"] = "mismatch", ["TypeMismatch"] = "mismatch", ["DirectionMismatch"] = "mismatch",
            ["Max"] = "bounded-match", ["MissingSize"] = "unknown", ["Ansi"] = "unknown", ["Decimal"] = "unknown", ["CaseName"] = "unknown",
            ["InputOutput"] = "bounded-match", ["Channels"] = "unknown", ["DynamicName"] = "unknown", ["Conditional"] = "unknown", ["Alias"] = "unknown",
            ["ParameterAlias"] = "unknown", ["Mutation"] = "unknown", ["ParameterReassignment"] = "unknown", ["HelperBoundary"] = "unknown", ["Text"] = "unknown", ["DynamicCommand"] = "unknown",
            ["Duplicate"] = "unknown", ["Extra"] = "mismatch", ["Missing"] = "mismatch", ["DefaultOmitted"] = "unknown"
        };
        foreach (var (name, status) in expected)
        {
            var fact = Assert.Single(calls, f => f.SourceSymbol!.Contains("." + name + "(", StringComparison.Ordinal));
            var parameter = name switch
            {
                "Max" => Parameter with { Size = -1 }, "Ansi" => Parameter with { SqlType = "VarChar", Units = "bytes" },
                "Decimal" => Parameter with { SqlType = "Decimal", Size = null, Units = "not-applicable", Precision = 10, Scale = 2 },
                "InputOutput" or "Channels" => Parameter with { Direction = "InputOutput" }, "DefaultOmitted" => Parameter with { HasDefault = true }, _ => Parameter
            };
            var declarations = Declarations with { Repo = fact.Repo, Revision = fact.CommitSha,
                Procedures = [Declarations.Procedures[0] with { Parameters = [parameter] }] };
            var report = Compare(fact, declarations);
            var row = Assert.Single(report.Comparisons);
            Assert.True(row.Status == status, name + ": " + SqlServerSchemaEvidence.Json(row));
            Assert.Equal(SqlServerSchemaEvidence.RuleId, row.RuleId);
            Assert.Equal("partial", report.Coverage);
            Assert.Contains("No aggregate clean verdict", SqlServerParameterReportWriter.Build(report));
            if (name == "Good")
            {
                var parameterEvidence = Assert.Single(row.CallState!.Parameters);
                Assert.NotNull(parameterEvidence.EndLine);
                Assert.True(parameterEvidence.DirectionLine > parameterEvidence.Line);
                Assert.True(parameterEvidence.DirectionEndLine <= row.SourceEndLine);
            }
            if (name == "Channels") Assert.Contains(row.CallState!.Parameters, p => p.Direction == "ReturnValue");
        }
        var two = calls.Where(f => f.SourceSymbol!.Contains(".TwoCommands(", StringComparison.Ordinal)).OrderBy(f => f.Evidence.StartLine).ToArray();
        Assert.Equal(2, two.Length);
        var states = two.Select(f => SqlServerSchemaEvidence.Read<SqlServerCallState>(f.Properties["sqlServerParameterState"])).ToArray();
        Assert.All(states, s => Assert.Equal("bounded", s.Coverage));
        Assert.Equal(40, Assert.Single(states[0].Parameters).Size);
        Assert.Equal(20, Assert.Single(states[1].Parameters).Size);

        // Native command, independent expected declaration input and retained fact serialization.
        var good = Assert.Single(calls, f => f.SourceSymbol!.Contains(".Good(", StringComparison.Ordinal));
        var d = Declarations with { Repo = good.Repo, Revision = good.CommitSha };
        var declarationPath = Path.Combine(temp.Path, "declarations.json"); File.WriteAllText(declarationPath, SqlServerSchemaEvidence.Json(d));
        var exportPath = Path.Combine(temp.Path, "schema.json");
        var output = new StringWriter(); var error = new StringWriter();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["sqlserver-schema", "produce", "--declarations", declarationPath, "--out", exportPath], output, error));
        var bindingsPath = Path.Combine(temp.Path, "bindings.json"); File.WriteAllText(bindingsPath, SqlServerSchemaEvidence.Json(Bindings(good)));
        var factsPath = Path.Combine(temp.Path, "facts.ndjson"); await JsonlFactWriter.WriteAsync(factsPath, calls);
        var destination = Path.Combine(temp.Path, "report");
        var command = new[] { "sqlserver-schema", "compare", "--declarations", declarationPath, "--schema", exportPath, "--bindings", bindingsPath,
            "--facts", factsPath, "--repo", good.Repo, "--revision", good.CommitSha, "--out", destination };
        var code = await TraceMapCommand.RunAsync(command, output, error);
        Assert.True(code == 0, error.ToString());
        var readback = SqlServerSchemaEvidence.Read<SqlServerComparisonReport>(File.ReadAllText(Path.Combine(destination, "parameter-comparison.json")));
        Assert.Equal(26, readback.Comparisons.Count);
        Assert.Equal("bounded-match", Assert.Single(readback.Comparisons, r => r.FactId == good.FactId).Status);
        Assert.Equal(25, readback.Comparisons.Count(r => r.Status == "unknown"));
        Assert.Contains("binding-missing", File.ReadAllText(Path.Combine(destination, "report.md")));
        command[^1] = Path.Combine(temp.Path, "report-repeat");
        Assert.Equal(0, await TraceMapCommand.RunAsync(command, output, error));
        Assert.Equal(File.ReadAllBytes(Path.Combine(destination, "parameter-comparison.json")), File.ReadAllBytes(Path.Combine(command[^1], "parameter-comparison.json")));
    }
}
