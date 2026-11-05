using System.Text.Json;
using System.Text.Json.Nodes;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsWizardWorkspaceTests
{
    [Fact]
    public async Task Ordinary_flat_width_guard_offers_stream_profile_without_editing_SQL()
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture(); await fixture.Prepare();
        var text = "CREATE TABLE dbo.WideItems (" + string.Join(",", Enumerable.Range(0, 256).Select(i => $"C{i} varchar(50) NULL")) + ");\nGO\n";
        var sql = Path.Combine(fixture.WizardRoot, "sql"); Directory.CreateDirectory(sql);
        var file = Path.Combine(sql, "export.sql"); File.WriteAllText(file, text); var bytes = File.ReadAllBytes(file);
        var manifest = JsonNode.Parse(File.ReadAllText(fixture.Manifest))!;
        manifest["files"]![0]!["path"] = "sql/export.sql";
        manifest["files"]![0]!["sha256"] = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        var path = Path.Combine(fixture.WizardRoot, "wide.local.json"); File.WriteAllText(path, manifest.ToJsonString());
        var original = File.ReadAllBytes(path);
        using var input = new StringReader($"workspace\nskip\n{path}\nstream\ncreate\nlater\n");
        Assert.True(await WebFormsWizardCommand.RunAsync(["wizard", "--root", fixture.WizardRoot, "--continue"], input, fixture.Output, fixture.Error) == 0, fixture.Error.ToString() + fixture.Output);
        Assert.Contains("legacy-total-open-constructs", fixture.Output.ToString());
        Assert.Contains("observed=129", fixture.Output.ToString());
        Assert.Equal(original, File.ReadAllBytes(path)); Assert.Equal(bytes, File.ReadAllBytes(file));
        Assert.Equal(0, await WorkspaceCommand.RunAsync(["verify", "--root", fixture.WizardRoot], fixture.Output, fixture.Error));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Database_context_can_be_repaired_from_verified_SQL_without_editing_originals(bool ordinary)
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        var text = "USE [ExampleCatalog];\nGO\nCREATE PROC dbo.ReadItems AS SELECT 1 AS Id;\nGO\n";
        var sqlFolder = Path.Combine(fixture.WizardRoot, "sql"); Directory.CreateDirectory(sqlFolder);
        var file = Path.Combine(sqlFolder, "export.sql");
        File.WriteAllText(file, text, new System.Text.UnicodeEncoding(false, true, true));
        var bytes = File.ReadAllBytes(file);
        var manifest = JsonNode.Parse(File.ReadAllText(fixture.Manifest))!;
        manifest["version"] = ordinary ? SqlServerLocalDefinitions.InputVersion : SqlServerLocalDefinitions.StreamInputVersion;
        manifest["files"]![0]!["path"] = "sql/export.sql";
        manifest["files"]![0]!["sha256"] = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        var path = Path.Combine(fixture.WizardRoot, "context.local.json"); File.WriteAllText(path, manifest.ToJsonString());
        var original = File.ReadAllBytes(path);
        using var input = new StringReader($"workspace\nskip\n{path}\n{(ordinary ? "stream\n" : "")}use-sql\ncreate\nlater\n");
        Assert.True(await WebFormsWizardCommand.RunAsync(["wizard", "--root", fixture.WizardRoot, "--continue"], input, fixture.Output, fixture.Error) == 0, fixture.Error.ToString() + fixture.Output);
        Assert.Equal(original, File.ReadAllBytes(path)); Assert.Equal(bytes, File.ReadAllBytes(file));
        var copies = Directory.GetFiles(fixture.WizardRoot, "sql-streamed-*.local.json").Select(copy => JsonNode.Parse(File.ReadAllText(copy))!).ToArray();
        var repaired = Assert.Single(copies, copy => copy["database"]!.GetValue<string>() == "ExampleCatalog");
        Assert.Equal(manifest["files"]!.ToJsonString(), repaired["files"]!.ToJsonString());
        Assert.Contains("Database context corrected", fixture.Output.ToString());
        Assert.Equal(0, await WorkspaceCommand.RunAsync(["verify", "--root", fixture.WizardRoot], fixture.Output, fixture.Error));
    }

    [Fact]
    public async Task Multiple_SQL_databases_pause_without_offering_single_database_repair()
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture(); await fixture.Prepare();
        var text = "USE [ExampleCatalog];\nGO\nCREATE PROC dbo.ReadItems AS SELECT 1 AS Id;\nGO\nUSE [OtherCatalog];\nGO\n";
        var sqlFolder = Path.Combine(fixture.WizardRoot, "sql"); Directory.CreateDirectory(sqlFolder);
        var file = Path.Combine(sqlFolder, "export.sql"); File.WriteAllText(file, text);
        var manifest = JsonNode.Parse(File.ReadAllText(fixture.Manifest))!;
        manifest["version"] = SqlServerLocalDefinitions.StreamInputVersion;
        manifest["files"]![0]!["path"] = "sql/export.sql";
        manifest["files"]![0]!["sha256"] = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)));
        var path = Path.Combine(fixture.WizardRoot, "context.local.json"); File.WriteAllText(path, manifest.ToJsonString());
        using var input = new StringReader($"workspace\nskip\n{path}\n");
        Assert.Equal(2, await WebFormsWizardCommand.RunAsync(["wizard", "--root", fixture.WizardRoot, "--continue"], input, fixture.Output, fixture.Error));
        Assert.Contains("multiple databases", fixture.Output.ToString());
        Assert.Contains("OtherCatalog", fixture.Output.ToString());
        Assert.DoesNotContain("[use-sql/later]", fixture.Output.ToString());
        Assert.False(File.Exists(Path.Combine(fixture.WizardRoot, "workspace.local.json")));
        Assert.Empty(Directory.GetFiles(fixture.WizardRoot, "sql-streamed-*.local.json"));
    }

    [Theory]
    [InlineData("utf8", 667158)]
    [InlineData("utf16", 667158)]
    [InlineData("utf16", 1000)]
    public async Task Actual_export_can_opt_into_streaming_without_editing_the_original_manifest(string encodingName, int size)
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        var sql = Path.Combine(fixture.WizardRoot, "sql"); Directory.CreateDirectory(sql);
        var suffix = "\nCREATE PROC dbo.ReadItems AS SELECT 1 AS Id;\nGO\n";
        var encoding = encodingName == "utf16" ? (System.Text.Encoding)new System.Text.UnicodeEncoding(false, true, true) : new System.Text.UTF8Encoding(false, true);
        var chars = encodingName == "utf16" ? (size - 2) / 2 : size;
        var text = "--" + new string('x', chars - suffix.Length - 2) + suffix;
        var file = Path.Combine(sql, "export.sql"); File.WriteAllText(file, text, encoding);
        var manifest = JsonNode.Parse(File.ReadAllText(fixture.Manifest))!;
        manifest["files"]![0]!["path"] = "sql/export.sql";
        manifest["files"]![0]!["sha256"] = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)));
        var path = Path.Combine(fixture.WizardRoot, "sql.local.json"); File.WriteAllText(path, manifest.ToJsonString());
        var before = File.ReadAllBytes(path);
        using var input = new StringReader($"workspace\nskip\n{path}\nstream\ncreate\nlater\n");
        Assert.True(await WebFormsWizardCommand.RunAsync(["wizard", "--root", fixture.WizardRoot, "--continue"], input, fixture.Output, fixture.Error) == 0, fixture.Error.ToString() + fixture.Output);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(size, new FileInfo(file).Length);
        var changed = Assert.Single(Directory.GetFiles(fixture.WizardRoot, "sql-streamed-*.local.json"));
        using var json = JsonDocument.Parse(File.ReadAllText(changed));
        Assert.Equal(SqlServerLocalDefinitions.StreamInputVersion, json.RootElement.GetProperty("version").GetString());
        Assert.Equal(0, await WorkspaceCommand.RunAsync(["verify", "--root", fixture.WizardRoot], fixture.Output, fixture.Error));
    }

    [Theory]
    [InlineData("retry")]
    [InlineData("same")]
    public async Task Unchanged_SQL_validation_failure_pauses_after_one_requested_action(string action)
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        var manifest = JsonNode.Parse(File.ReadAllText(fixture.Manifest))!;
        manifest["files"]![0]!["path"] = "../schema.sql";
        File.WriteAllText(fixture.Manifest, manifest.ToJsonString());
        var correction = action == "same" ? $"change\n{fixture.Manifest}\n" : "retry\n";
        using var input = new StringReader($"workspace\nskip\n{fixture.Manifest}\n{correction}");
        Assert.Equal(2, await WebFormsWizardCommand.RunAsync(["wizard", "--root", fixture.WizardRoot, "--continue"], input, fixture.Output, fixture.Error));
        Assert.Contains("same SQL validation failure", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, fixture.Output.ToString().Split("[change/retry/later]", StringSplitOptions.None).Length - 1);
        Assert.False(File.Exists(Path.Combine(fixture.WizardRoot, "workspace.local.json")));
    }

    [Theory]
    [InlineData("retry")]
    [InlineData("same")]
    public async Task Oversize_SQL_reports_file_and_limit_and_pauses_instead_of_looping(string action)
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        var sql = Path.Combine(fixture.WizardRoot, "sql");
        Directory.CreateDirectory(sql);
        var file = Path.Combine(sql, "oversize.sql");
        File.WriteAllBytes(file, new byte[262145]);
        var manifest = JsonNode.Parse(File.ReadAllText(fixture.Manifest))!;
        manifest["files"]![0]!["path"] = "sql/oversize.sql";
        var path = Path.Combine(fixture.WizardRoot, "sql-limit.local.json");
        File.WriteAllText(path, manifest.ToJsonString());
        var correction = action == "same" ? $"select\n{path}\n" : "retry\n";
        using var input = new StringReader($"workspace\nskip\n{path}\n{correction}");
        Assert.Equal(2, await WebFormsWizardCommand.RunAsync(["wizard", "--root", fixture.WizardRoot, "--continue"], input, fixture.Output, fixture.Error));
        Assert.Contains("observedBytes=262145", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("limitBytes=262144", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("oversize.sql", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("[change/retry/later]", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(fixture.WizardRoot, "workspace.local.json")));
        Assert.Equal(262145, new FileInfo(file).Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stale_or_malformed_workspace_answers_can_restart_without_website_repair(bool malformed)
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        var original = File.ReadAllBytes(Path.Combine(fixture.WizardRoot, "site", "project.config.json"));
        var path = Path.Combine(fixture.WizardRoot, "site", "workspace-answers.local.json");
        var answers = new WorkspaceWizardAnswers("site", new string('f', 64), null, null, null, null);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(answers, json)));
        var envelope = new WorkspaceWizardAnswerFile("tracemap-workspace-wizard-answers.v1", WebFormsWizardWorkspace.RuleId,
            "local-only", new string('a', 64), hash, answers);
        File.WriteAllText(path, malformed ? "{broken" : JsonSerializer.Serialize(envelope, json));
        var retainedBytes = File.ReadAllBytes(path);
        using var input = new StringReader("workspace\nrestart\nskip\nlater\n");
        Assert.Equal(2, await WebFormsWizardCommand.RunAsync(["wizard", "--root", fixture.WizardRoot, "--continue"], input, fixture.Output, fixture.Error));
        Assert.Contains("[restart/later]", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("INPUT_OR_CONFIGURATION_INVALID", fixture.Error.ToString(), StringComparison.Ordinal);
        var retained = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!, "workspace-answers.local.json.retained-*"));
        Assert.Equal(retainedBytes, File.ReadAllBytes(retained));
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(fixture.WizardRoot, "site", "project.config.json")));
        using var saved = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Null(saved.RootElement.GetProperty("answers").GetProperty("sqlManifest").GetString());
        Assert.False(File.Exists(Path.Combine(fixture.WizardRoot, "workspace.local.json")));
    }

    [Fact]
    public async Task Invalid_manifest_entry_is_reported_before_requesting_another_folder()
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        var manifest = JsonNode.Parse(File.ReadAllText(fixture.Manifest))!;
        manifest["files"]![0]!["path"] = "../schema.sql";
        File.WriteAllText(fixture.Manifest, manifest.ToJsonString());
        using var store = WebFormsWizardStore.Open(fixture.WizardRoot, true);
        using var input = new StringReader($"skip\n{fixture.Manifest}\n");
        Assert.Equal(2, await WebFormsWizardWorkspace.RunAsync(store, "site", input, fixture.Output, fixture.Error, default));
        Assert.Contains("WEBFORMS_WORKSPACE_SQL_MANIFEST_ENTRY_INVALID", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("[change/retry/later]", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Enter an alternate BASE folder", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(fixture.WizardRoot, "workspace.local.json")));
    }

    [Fact]
    public async Task Full_wizard_can_change_an_invalid_saved_sql_manifest_without_website_repair()
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        var bad = Path.Combine(fixture.WizardRoot, "bad-sql.json");
        var json = JsonNode.Parse(File.ReadAllText(fixture.Manifest))!;
        json["files"]![0]!["path"] = "../schema.sql";
        File.WriteAllText(bad, json.ToJsonString());
        using (var input = new StringReader($"workspace\nskip\n{bad}\nlater\n"))
            Assert.Equal(2, await WebFormsWizardCommand.RunAsync(["wizard", "--root", fixture.WizardRoot, "--continue"], input, fixture.Output, fixture.Error));
        var original = File.ReadAllBytes(Path.Combine(fixture.WizardRoot, "site", "project.config.json"));
        using (var input = new StringReader($"workspace\nchange\n{fixture.Manifest}\n{fixture.SqlRoot}\ncreate\nlater\n"))
            Assert.True(await WebFormsWizardCommand.RunAsync(["wizard", "--root", fixture.WizardRoot, "--continue"], input, fixture.Output, fixture.Error) == 0, fixture.Error.ToString());
        Assert.DoesNotContain("INPUT_OR_CONFIGURATION_INVALID", fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(fixture.WizardRoot, "workspace.local.json")));
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(fixture.WizardRoot, "site", "project.config.json")));
        Assert.True(File.Exists(bad));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Manifest_beside_root_resolves_nested_sql_and_repairs_an_old_doubled_base(bool oldSavedBase)
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        var sqlFolder = Path.Combine(fixture.WizardRoot, "sql");
        Directory.CreateDirectory(sqlFolder);
        File.Copy(Path.Combine(fixture.SqlRoot, "schema.sql"), Path.Combine(sqlFolder, "schema.sql"));
        var manifest = JsonNode.Parse(File.ReadAllText(fixture.Manifest))!;
        manifest["files"]![0]!["path"] = "sql/schema.sql";
        var manifestPath = Path.Combine(fixture.WizardRoot, "sql-definitions.local.json");
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        if (oldSavedBase)
        {
            using var store = WebFormsWizardStore.Open(fixture.WizardRoot, true);
            var answers = new WorkspaceWizardAnswers("site", store.ReadProject("site").Native!.Sha256, "", "",
                WebFormsWizardStore.Physical(manifestPath), WebFormsWizardStore.Physical(sqlFolder));
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
            var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(answers, json)));
            var envelope = new WorkspaceWizardAnswerFile("tracemap-workspace-wizard-answers.v1", WebFormsWizardWorkspace.RuleId,
                "local-only", new string('a', 64), hash, answers);
            File.WriteAllText(Path.Combine(fixture.WizardRoot, "site", "workspace-answers.local.json"), JsonSerializer.Serialize(envelope, json));
        }
        using (var store = WebFormsWizardStore.Open(fixture.WizardRoot, true))
        using (var input = new StringReader(oldSavedBase ? "create\nlater\n" : $"skip\n{manifestPath}\ncreate\nlater\n"))
            Assert.True(await WebFormsWizardWorkspace.RunAsync(store, "site", input, fixture.Output, fixture.Error, default) == 0, fixture.Error.ToString());
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.WizardRoot, "workspace.local.json")));
        Assert.Equal(WebFormsWizardStore.Physical(fixture.WizardRoot), config.RootElement.GetProperty("sqlEvidence").GetProperty("sourceRoot").GetString());
        Assert.Contains("sql/schema.sql ->", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Enter an alternate BASE folder", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, await WorkspaceCommand.RunAsync(["verify", "--root", fixture.WizardRoot], fixture.Output, fixture.Error));
        var forbidden = Path.Combine(sqlFolder, "output");
        Assert.Equal(1, await WorkspaceCommand.RunAsync(["collect", "--root", fixture.WizardRoot, "--out", forbidden], fixture.Output, fixture.Error));
        Assert.Contains("WORKSPACE_SQL_OUTPUT_OVERLAPS_INPUT", fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(forbidden));
    }

    [Fact]
    public async Task Dependency_source_can_be_skipped_and_sql_setup_resumed_without_an_invented_repository()
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        var nativeBefore = File.ReadAllBytes(Path.Combine(fixture.WizardRoot, "site", "project.config.json"));
        using (var store = WebFormsWizardStore.Open(fixture.WizardRoot, true))
        using (var input = new StringReader("skip\nlater\n"))
            Assert.Equal(2, await WebFormsWizardWorkspace.RunAsync(store, "site", input, fixture.Output, fixture.Error, default));
        Assert.Contains("Existing SQL manifest FILE", fixture.Output.ToString(), StringComparison.Ordinal);
        fixture.Output.GetStringBuilder().Clear();
        using (var store = WebFormsWizardStore.Open(fixture.WizardRoot, true))
        using (var input = new StringReader($"{fixture.Manifest}\n{fixture.SqlRoot}\ncreate\n"))
            Assert.True(await WebFormsWizardWorkspace.RunAsync(store, "site", input, fixture.Output, fixture.Error, default) == 0, fixture.Error.ToString());
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.WizardRoot, "workspace.local.json")));
        Assert.Equal(1, config.RootElement.GetProperty("repositories").GetArrayLength());
        Assert.Equal(fixture.Site, config.RootElement.GetProperty("repositories")[0].GetProperty("path").GetString());
        Assert.Equal(WebFormsWizardStore.Physical(fixture.Manifest), config.RootElement.GetProperty("sqlEvidence").GetProperty("manifestPath").GetString());
        Assert.Contains("dependency-source coverage is not added", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Framework SOURCE repository folder", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.Equal(nativeBefore, File.ReadAllBytes(Path.Combine(fixture.WizardRoot, "site", "project.config.json")));
        Assert.Equal(0, await WorkspaceCommand.RunAsync(["verify", "--root", fixture.WizardRoot], fixture.Output, fixture.Error));
    }

    [Fact]
    public async Task Existing_wizard_continuation_offers_workspace_without_a_switch_list_or_report_resume()
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        using var input = new StringReader($"workspace\n{fixture.Framework}\nlater\n");
        var calls = 0;
        Task<int> NoNative(string[] args, TextWriter output, TextWriter error, CancellationToken token)
        { calls++; throw new InvalidOperationException("workspace setup must not resume a scan or native reports"); }
        Assert.Equal(2, await WebFormsWizardCommand.RunAsync(["wizard", "--root", fixture.WizardRoot, "--continue"],
            input, fixture.Output, fixture.Error, nativeRunner: NoNative));
        Assert.Equal(0, calls);
        Assert.True(File.Exists(Path.Combine(fixture.WizardRoot, "site", "workspace-answers.local.json")), fixture.Output.ToString());
    }

    [Fact]
    public async Task Pause_remembers_answers_and_resume_creates_workspace_in_the_same_root_without_reasking()
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        using (var store = WebFormsWizardStore.Open(fixture.WizardRoot, true))
        using (var input = new StringReader($"{fixture.Framework}\nFramework.csproj\nlater\n"))
            Assert.Equal(2, await WebFormsWizardWorkspace.RunAsync(store, "site", input, fixture.Output, fixture.Error, default));
        var path = Path.Combine(fixture.WizardRoot, "site", "workspace-answers.local.json");
        using (var document = JsonDocument.Parse(File.ReadAllText(path)))
        {
            Assert.Equal("local-only", document.RootElement.GetProperty("visibility").GetString());
            Assert.Matches("^[0-9a-f]{64}$", document.RootElement.GetProperty("generatorSha256").GetString()!);
            Assert.Matches("^[0-9a-f]{64}$", document.RootElement.GetProperty("boundedInputSha256").GetString()!);
        }
        var projectBefore = File.ReadAllBytes(Path.Combine(fixture.WizardRoot, "site", "project.config.json"));
        fixture.Output.GetStringBuilder().Clear();
        using (var store = WebFormsWizardStore.Open(fixture.WizardRoot, true))
        using (var input = new StringReader($"{fixture.Manifest}\n{fixture.SqlRoot}\ncreate\n"))
            Assert.True(await WebFormsWizardWorkspace.RunAsync(store, "site", input, fixture.Output, fixture.Error, default) == 0, fixture.Error.ToString());
        Assert.DoesNotContain("Framework SOURCE repository folder", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Framework project/solution: choose", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(fixture.WizardRoot, "workspace.local.json")));
        Assert.False(Directory.Exists(Path.Combine(fixture.WizardRoot, "sources")));
        Assert.Contains("SQLite evidence is created by collection, not configuration", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("[collect/later]", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.Equal(projectBefore, File.ReadAllBytes(Path.Combine(fixture.WizardRoot, "site", "project.config.json")));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    public async Task Change_one_saved_choice_keeps_other_inputs_and_does_not_create_early(string field)
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        using (var store = WebFormsWizardStore.Open(fixture.WizardRoot, true))
        using (var input = new StringReader($"{fixture.Framework}\nFramework.csproj\n{fixture.Manifest}\n{fixture.SqlRoot}\nlater\n"))
            Assert.Equal(2, await WebFormsWizardWorkspace.RunAsync(store, "site", input, fixture.Output, fixture.Error, default));
        using (var store = WebFormsWizardStore.Open(fixture.WizardRoot, true))
        using (var input = new StringReader($"change\n{field}\nlater\n"))
            Assert.Equal(2, await WebFormsWizardWorkspace.RunAsync(store, "site", input, fixture.Output, fixture.Error, default));
        using var saved = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.WizardRoot, "site", "workspace-answers.local.json")));
        var answers = saved.RootElement.GetProperty("answers");
        Assert.Equal(field == "1" ? JsonValueKind.Null : JsonValueKind.String, answers.GetProperty("frameworkRoot").ValueKind);
        Assert.Equal(field is "1" or "2" ? JsonValueKind.Null : JsonValueKind.String, answers.GetProperty("frameworkTarget").ValueKind);
        Assert.Equal(field == "3" ? JsonValueKind.Null : JsonValueKind.String, answers.GetProperty("sqlManifest").ValueKind);
        Assert.Equal(field == "4" ? JsonValueKind.Null : JsonValueKind.String, answers.GetProperty("sqlRoot").ValueKind);
        Assert.False(File.Exists(Path.Combine(fixture.WizardRoot, "workspace.local.json")));
    }

    [Fact]
    public async Task Tampered_answers_are_refused_and_preserved()
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        using (var store = WebFormsWizardStore.Open(fixture.WizardRoot, true))
        using (var input = new StringReader($"{fixture.Framework}\nlater\n"))
            Assert.Equal(2, await WebFormsWizardWorkspace.RunAsync(store, "site", input, fixture.Output, fixture.Error, default));
        var path = Path.Combine(fixture.WizardRoot, "site", "workspace-answers.local.json");
        var changed = JsonNode.Parse(File.ReadAllText(path))!;
        changed["answers"]!["frameworkRoot"] = "different-folder";
        File.WriteAllText(path, changed.ToJsonString());
        var bytes = File.ReadAllBytes(path);
        using var resumed = WebFormsWizardStore.Open(fixture.WizardRoot, true);
        using var empty = new StringReader("");
        Assert.Equal(2, await WebFormsWizardWorkspace.RunAsync(resumed, "site", empty, fixture.Output, fixture.Error, default));
        Assert.Contains("ANSWERS_INVALID", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task Existing_workspace_can_be_verified_without_asking_for_all_inputs_again()
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        using (var store = WebFormsWizardStore.Open(fixture.WizardRoot, true))
        using (var input = new StringReader($"{fixture.Framework}\nFramework.csproj\n{fixture.Manifest}\n{fixture.SqlRoot}\ncreate\n"))
            Assert.True(await WebFormsWizardWorkspace.RunAsync(store, "site", input, fixture.Output, fixture.Error, default) == 0, fixture.Error.ToString());
        var path = Path.Combine(fixture.WizardRoot, "workspace.local.json");
        var original = File.ReadAllBytes(path);
        fixture.Output.GetStringBuilder().Clear();
        using (var store = WebFormsWizardStore.Open(fixture.WizardRoot, true))
        using (var input = new StringReader("verify\n"))
            Assert.Equal(0, await WebFormsWizardWorkspace.RunAsync(store, "site", input, fixture.Output, fixture.Error, default));
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.DoesNotContain("Framework SOURCE repository folder", fixture.Output.ToString(), StringComparison.Ordinal);
    }
}
