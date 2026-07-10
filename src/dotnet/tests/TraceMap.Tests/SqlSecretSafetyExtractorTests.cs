using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class SqlSecretSafetyExtractorTests
{
    [Fact]
    public void Extract_classifies_supported_postgresql_protected_material_surfaces()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "protected.sql", """
            CREATE USER MAPPING FOR operator_role SERVER remote_source OPTIONS (user 'fixture_user', password 'fixture_password');
            SELECT dblink('host=fixture.invalid password=fixture_password', 'select 1');
            CREATE SUBSCRIPTION fixture_subscription CONNECTION '${SUBSCRIPTION_CONNECTION}' PUBLICATION fixture_publication;
            CREATE SERVER fixture_server FOREIGN DATA WRAPPER postgres_fdw OPTIONS (host 'fixture.invalid', dbname 'fixture_db');
            SELECT cron.schedule('fixture-job', '0 1 * * *', $$select current_setting('fixture.password')$$);
            """);

        var facts = Extract(temp.Path).Where(IsSafetyFact).ToArray();

        Assert.Equal(5, facts.Length);
        Assert.Contains(facts, fact => fact.Properties["classification"] == "secret-bearing"
            && fact.Properties["categoryCodes"].Contains("user-mapping", StringComparison.Ordinal));
        Assert.Contains(facts, fact => fact.Properties["classification"] == "secret-reference"
            && fact.Properties["categoryCodes"].Contains("subscription-connection", StringComparison.Ordinal));
        Assert.Contains(facts, fact => fact.Properties["categoryCodes"].Contains("remote-query-input", StringComparison.Ordinal));
        Assert.Contains(facts, fact => fact.Properties["categoryCodes"].Contains("connection-material", StringComparison.Ordinal));
        Assert.Contains(facts, fact => fact.Properties["categoryCodes"].Contains("scheduled-command", StringComparison.Ordinal));
        Assert.All(facts, fact =>
        {
            Assert.Null(fact.Evidence.SnippetHash);
            Assert.Equal("span-only", fact.Properties["identityPrecision"]);
            Assert.Equal("secret-owner-review", fact.Properties["stopCondition"]);
            Assert.DoesNotContain(fact.Properties.Keys, key => key.Contains("hash", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Extract_fails_closed_for_dynamic_or_malformed_high_risk_sql()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "gaps.sql", """
            SELECT dblink('host=' || runtime_host, 'select 1');
            CREATE USER MAPPING FOR operator_role SERVER remote_source OPTIONS (password 'unterminated);
            """);

        var gaps = Extract(temp.Path)
            .Where(fact => fact.RuleId == RuleIds.DatabaseSqlSecretSafetyGap)
            .ToArray();

        Assert.Equal(2, gaps.Length);
        Assert.All(gaps, fact =>
        {
            Assert.Equal(FactTypes.AnalysisGap, fact.FactType);
            Assert.Equal("not-established", fact.Properties["classification"]);
            Assert.Equal("reduced", fact.Properties["coverage"]);
            Assert.Contains("dynamic-secret-boundary", fact.Properties["categoryCodes"], StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Extract_covers_alter_forms_and_requires_matching_dollar_quote_tags()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "alter.sql", """
            ALTER USER MAPPING FOR operator_role SERVER remote_source OPTIONS (SET password 'fixture_password');
            ALTER SUBSCRIPTION fixture_subscription CONNECTION 'host=fixture.invalid password=fixture_password';
            ALTER SERVER fixture_server OPTIONS (SET password 'fixture_password');
            SELECT dblink($first$host=fixture.invalid$second$, 'select 1');
            """);

        var facts = Extract(temp.Path).Where(IsSafetyFact).ToArray();

        Assert.Equal(4, facts.Length);
        Assert.Equal(3, facts.Count(fact => fact.Properties["classification"] == "secret-bearing"));
        Assert.Contains(facts, fact => fact.Properties["classification"] == "not-established"
            && fact.Properties["categoryCodes"].Contains("dynamic-secret-boundary", StringComparison.Ordinal));
    }

    [Fact]
    public void Extract_ignores_disabled_and_non_credential_text_and_is_deterministic()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "controls.sql", """
            /* password=disabled-value */
            SELECT 'password=ordinary-string';
            SELECT password_reset_requested FROM audit_events;
            -- ordinary operational comment
            SELECT 1;
            """);

        var first = Extract(temp.Path).Where(IsSafetyFact).ToArray();
        var second = Extract(temp.Path).Where(IsSafetyFact).ToArray();

        Assert.Empty(first);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public async Task Cli_outputs_and_combined_index_never_contain_planted_values_or_their_digests()
    {
        const string host = "public-safe-host-leak-sentinel.invalid";
        const string user = "public-safe-user-leak-sentinel";
        const string password = "public-safe-password-leak-sentinel";
        const string token = "public-safe-token-leak-sentinel";
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var outputPath = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        WriteSql(repo, "protected.sql", $"""
            CREATE USER MAPPING FOR operator_role SERVER remote_source OPTIONS (host '{host}', user '{user}', password '{password}');
            SELECT dblink('host={host} user={user} password={password}', 'select 1');
            -- token={token}
            SELECT 1;
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], output, error);
        Assert.Equal(0, exitCode);

        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions(
            [Path.Combine(outputPath, "index.sqlite")], combinedPath, ["fixture"]));

        var allOutput = new StringBuilder(output.ToString()).Append(error.ToString());
        foreach (var file in Directory.EnumerateFiles(outputPath, "*", SearchOption.AllDirectories).Append(combinedPath))
        {
            allOutput.Append(Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file)));
        }

        var serialized = allOutput.ToString();
        foreach (var sentinel in new[] { host, user, password, token })
        {
            Assert.DoesNotContain(sentinel, serialized, StringComparison.Ordinal);
            Assert.DoesNotContain(sentinel[12..], serialized, StringComparison.Ordinal);
            Assert.DoesNotContain(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sentinel))).ToLowerInvariant(), serialized, StringComparison.Ordinal);
            Assert.DoesNotContain(FactFactory.Hash(sentinel, 32), serialized, StringComparison.Ordinal);
        }

        Assert.Contains("SecretBearingSqlStep", serialized, StringComparison.Ordinal);
        Assert.Contains("possible-secret", serialized, StringComparison.Ordinal);
        Assert.Contains(RuleIds.DatabaseSqlSecretTextCandidate, serialized, StringComparison.Ordinal);
        Assert.Contains("SQL Protected-Material Steps", serialized, StringComparison.Ordinal);
        Assert.Contains("Absence of a finding does not prove absence of secrets", serialized, StringComparison.Ordinal);

        using var connection = new SqliteConnection($"Data Source={Path.Combine(outputPath, "index.sqlite")}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select properties_json from facts where fact_type = 'SecretBearingSqlStep'";
        var properties = Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
        Assert.DoesNotContain("Hash", properties, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secret-owner-review", properties, StringComparison.Ordinal);
    }

    [Fact]
    public void Context_and_sql_shape_facts_omit_hashes_for_protected_statements()
    {
        using var temp = new TempDirectory();
        WriteSql(temp.Path, "protected.sql", "CREATE USER MAPPING FOR operator_role SERVER remote_source OPTIONS (password 'fixture_password');");
        var inventory = FileInventory.Collect(temp.Path);
        var manifest = CreateManifest();

        var context = Assert.Single(
            SqlExecutionContextExtractor.Extract(temp.Path, manifest, inventory),
            fact => fact.FactType == FactTypes.SqlExecutionContextCandidate);

        Assert.Null(context.Evidence.SnippetHash);
        Assert.False(context.Properties.ContainsKey("statementShapeHash"));
        Assert.Equal("protected-sql-material", context.Properties["redactionReason"]);
        Assert.Empty(SqlFileExtractor.Extract(temp.Path, manifest, inventory));
    }

    [Fact]
    public void Embedded_csharp_sql_uses_the_same_category_only_boundary()
    {
        const string sentinel = "public-safe-embedded-password-sentinel";
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Embedded.cs"), $$"""
            public static class Embedded
            {
                public const string Setup = "CREATE USER MAPPING FOR fixture_role SERVER fixture_server OPTIONS (password '{{sentinel}}'); CREATE SUBSCRIPTION fixture_subscription CONNECTION '${SUBSCRIPTION_CONNECTION}' PUBLICATION fixture_publication;";
            }
            """);
        var facts = CSharpIntegrationSyntaxExtractor.Extract(temp.Path, CreateManifest(), FileInventory.Collect(temp.Path));
        var secretFacts = facts.Where(fact => fact.FactType == FactTypes.SecretBearingSqlStep).ToArray();
        var serialized = JsonSerializer.Serialize(facts);

        Assert.Equal(2, secretFacts.Length);
        Assert.Equal(["1", "2"], secretFacts.Select(fact => fact.Properties["statementOrdinal"]).ToArray());
        Assert.Contains(secretFacts, fact => fact.Properties["classification"] == "secret-bearing");
        Assert.Contains(secretFacts, fact => fact.Properties["classification"] == "secret-reference");
        Assert.DoesNotContain(facts, fact => fact.FactType is FactTypes.SqlTextUsed or FactTypes.QueryPatternDetected);
        Assert.DoesNotContain(sentinel, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(FactFactory.Hash(sentinel, 32), serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Typed_dataset_with_protected_command_omits_document_and_element_hashes()
    {
        const string sentinel = "public-safe-tableadapter-password-sentinel";
        using var temp = new TempDirectory();
        var document = $$"""
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                       xmlns:msdata="urn:schemas-microsoft-com:xml-msdata"
                       xmlns:msprop="urn:schemas-microsoft-com:xml-msprop">
              <xs:element name="FixtureDataSet" msdata:IsDataSet="true" msprop:Generator_DataSetName="FixtureDataSet" />
              <xs:annotation><xs:appinfo>
                <TableAdapterCommand Name="Setup" CommandText="CREATE USER MAPPING FOR fixture_role SERVER fixture_server OPTIONS (password '{{sentinel}}');" />
              </xs:appinfo></xs:annotation>
            </xs:schema>
            """;
        File.WriteAllText(Path.Combine(temp.Path, "Fixture.xsd"), document);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var serialized = JsonSerializer.Serialize(result.Facts);

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.SecretBearingSqlStep);
        Assert.DoesNotContain(sentinel, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(FactFactory.Hash(sentinel, 32), serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(FactFactory.Hash(document, 32), serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(
            result.Facts.Where(fact => fact.Evidence.FilePath == "Fixture.xsd"),
            fact => fact.Evidence.SnippetHash is not null);
    }

    [Fact]
    public void Rule_catalog_documents_secret_safety_rules_and_false_negative_limitations()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        foreach (var rule in new[]
                 {
                     RuleIds.DatabaseSqlSecretBearingStep,
                     RuleIds.DatabaseSqlSecretTextCandidate,
                     RuleIds.DatabaseSqlSecretSafetyGap
                 })
        {
            Assert.Contains($"- id: {rule}", catalog, StringComparison.Ordinal);
        }
        Assert.Contains("absence of a finding does not prove absence of secrets", catalog, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("raw secret values are neither stored nor hashed", catalog, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<CodeFact> Extract(string repoPath) =>
        SqlSecretSafetyExtractor.Extract(repoPath, CreateManifest(), FileInventory.Collect(repoPath));

    private static bool IsSafetyFact(CodeFact fact) =>
        fact.FactType == FactTypes.SecretBearingSqlStep || fact.RuleId == RuleIds.DatabaseSqlSecretSafetyGap;

    private static void WriteSql(string directory, string name, string text)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name), text);
    }

    private static ScanManifest CreateManifest() => new(
        "scan-sql-secret-test", "synthetic-sql-secret", null, "test", "0123456789abcdef", "test",
        DateTimeOffset.UnixEpoch, "Level3SyntaxAnalysis", "NotRun", [], [], [], []);

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "rules", "rule-catalog.yml")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException("Unable to find TraceMap repo root.");
    }
}
