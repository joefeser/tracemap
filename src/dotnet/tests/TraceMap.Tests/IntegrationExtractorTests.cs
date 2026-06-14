using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class IntegrationExtractorTests
{
    [Fact]
    public void Scan_extracts_http_database_and_sql_facts_from_csharp_syntax()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Integrations.cs"), """
            using System.Net.Http;

            public sealed class CustomerContext : DbContext
            {
                public DbSet<Customer> Customers { get; set; }

                public async Task SaveAsync(HttpClient http, IHttpClientFactory factory, HttpContent content, dynamic connection)
                {
                    await http.GetAsync("/customers");
                    await http.PostAsJsonAsync("/customers", new Customer());
                    await content.ReadFromJsonAsync<Customer>();
                    var named = factory.CreateClient("billing");
                    connection.Query<Customer>("select * from Customers");
                    await connection.ExecuteAsync("update Customers set Name = @Name where Id = @Id");
                    SaveChanges();
                    using var command = new SqlCommand("select Id, Name from Customers");
                }
            }

            public sealed class Customer
            {
                public string Name { get; set; } = "";
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.HttpCallDetected && fact.TargetSymbol == "GetAsync");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.HttpCallDetected && fact.TargetSymbol == "PostAsJsonAsync");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.HttpCallDetected && fact.TargetSymbol == "ReadFromJsonAsync");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.HttpClientCreated && fact.TargetSymbol == "billing");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.DbContextDeclared && fact.TargetSymbol == "CustomerContext");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.DbSetDeclared && fact.TargetSymbol == "Customers");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.DbChangeSaved && fact.TargetSymbol == "SaveChanges");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.DapperCallDetected && fact.TargetSymbol == "Query");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.DapperCallDetected && fact.TargetSymbol == "ExecuteAsync");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.SqlCommandDetected && fact.TargetSymbol == "SqlCommand");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.SqlTextUsed && fact.Properties.ContainsKey("textHash"));

        Assert.All(
            result.Facts.Where(fact => fact.RuleId is RuleIds.HttpClientInvocation or RuleIds.DatabaseDapperInvocation or RuleIds.DatabaseSqlText),
            fact => Assert.Contains(fact.EvidenceTier, new[] { EvidenceTiers.Tier3SyntaxOrTextual, EvidenceTiers.Tier4Unknown }));
        Assert.Contains(result.Facts.Where(fact => fact.RuleId == RuleIds.DatabaseEntityFramework), fact => fact.EvidenceTier == EvidenceTiers.Tier2Structural);
    }

    [Fact]
    public void Scan_extracts_config_keys_and_connection_strings()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "appsettings.Production.json"), """
            {
              "ConnectionStrings": {
                "DefaultConnection": "Server=.;Database=TraceMap;Trusted_Connection=True"
              },
              "FeatureFlags": {
                "UseBilling": true
              }
            }
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Web.config"), """
            <configuration>
              <appSettings>
                <add key="ApiBaseUrl" value="https://example.test" />
              </appSettings>
              <connectionStrings>
                <add name="LegacyDb" connectionString="Server=.;Database=Legacy" providerName="System.Data.SqlClient" />
              </connectionStrings>
            </configuration>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "packages.config"), """
            <packages>
              <package id="Dapper" version="2.1.66" targetFramework="net48" />
            </packages>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.ConfigKeyDeclared && fact.TargetSymbol == "FeatureFlags:UseBilling");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.ConfigKeyDeclared && fact.TargetSymbol == "appSettings:ApiBaseUrl");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.ConfigKeyDeclared && fact.TargetSymbol == "packages:Dapper");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.ConnectionStringDeclared
            && fact.TargetSymbol == "DefaultConnection"
            && fact.Properties.ContainsKey("valueHash"));
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.ConnectionStringDeclared
            && fact.TargetSymbol == "LegacyDb"
            && fact.Properties.TryGetValue("providerName", out var provider)
            && provider == "System.Data.SqlClient");
    }

    [Fact]
    public void Scan_records_repeated_json_property_lines_by_full_path()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "appsettings.json"), """
            {
              "Logging": {
                "LogLevel": {
                  "Default": "Information"
                }
              },
              "ConnectionStrings": {
                "Default": "Server=.;Database=TraceMap"
              }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.ConfigKeyDeclared
            && fact.TargetSymbol == "Logging:LogLevel:Default"
            && fact.Evidence.StartLine == 4);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.ConfigKeyDeclared
            && fact.TargetSymbol == "ConnectionStrings:Default"
            && fact.Evidence.StartLine == 8);
    }

    [Fact]
    public void Scan_detects_sql_in_raw_string_literals_without_storing_text()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "RawSql.cs"), """"
            public sealed class RawSql
            {
                public string Query()
                {
                    return """
                        select Id, Name
                        from Customers
                        where IsActive = 1
                        """;
                }
            }
            """");

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        var sqlFact = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.SqlTextUsed
            && fact.Evidence.FilePath == "RawSql.cs");
        Assert.Contains("textHash", sqlFact.Properties.Keys);
        Assert.Contains("textLength", sqlFact.Properties.Keys);
        Assert.DoesNotContain("text", sqlFact.Properties.Keys);
    }

    [Fact]
    public void Scan_emits_sql_shape_facts_for_dotnet_literals_and_sql_files()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Repository.cs"), """
            public sealed class Repository
            {
                public void Load(dynamic connection, dynamic db, string table)
                {
                    connection.Query("SELECT id, status FROM orders WHERE id = @id");
                    connection.Execute("UPDATE orders SET status = @status WHERE id = @id");
                    db.Orders.FromSqlRaw("SELECT id, status FROM orders");
                    using var command = new SqlCommand($"SELECT id FROM {table}");
                }
            }
            """);
        File.WriteAllText(Path.Combine(temp.Path, "orders.sql"), "SELECT id, status FROM orders;\n");

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));
        var shapeFacts = result.Facts
            .Where(fact => fact.FactType == FactTypes.QueryPatternDetected && fact.RuleId == RuleIds.DatabaseSqlShape)
            .ToArray();

        Assert.Contains(shapeFacts, fact =>
            fact.Properties["sqlSourceKind"] == "literal-string"
            && fact.Properties["operationName"] == "SELECT"
            && fact.Properties["tableName"] == "orders"
            && fact.Properties["columnNames"] == "id;status"
            && fact.SourceSymbol == "Load");
        Assert.Contains(shapeFacts, fact =>
            fact.Properties["sqlSourceKind"] == "orm-text"
            && fact.Properties["queryShapeHash"].Length == 32);
        Assert.Contains(shapeFacts, fact =>
            fact.Evidence.FilePath == "orders.sql"
            && fact.Properties["sqlSourceKind"] == "sql-file");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("sqlSourceKind") == "dynamic-boundary"
            && fact.Properties.GetValueOrDefault("methodName") == "SqlCommand");
        Assert.DoesNotContain(result.Facts, fact => fact.Properties.ContainsKey("rawSql") || fact.Properties.ContainsKey("text"));
    }
}
