using System.Text.Json;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class LegacyStoryReconciliationTests
{
    [Fact]
    public void Scan_preserves_wcf_remoting_and_legacy_data_evidence_together()
    {
        using var fixture = CreateFixture();
        var facts = fixture.Facts;

        Assert.Contains(facts, fact =>
            fact.FactType == FactTypes.WcfServiceReferenceMapping
            && fact.RuleId == RuleIds.LegacyWcfMapping
            && fact.EvidenceTier == EvidenceTiers.Tier2Structural);
        Assert.Contains(facts, fact =>
            fact.FactType == FactTypes.RemotingServiceTypeRegistered
            && fact.RuleId == RuleIds.LegacyRemotingRegistration
            && fact.EvidenceTier is EvidenceTiers.Tier2Structural or EvidenceTiers.Tier3SyntaxOrTextual);
        Assert.Contains(facts, fact =>
            fact.FactType == FactTypes.RemotingClientActivationDeclared
            && fact.RuleId == RuleIds.LegacyRemotingRegistration
            && fact.EvidenceTier is EvidenceTiers.Tier2Structural or EvidenceTiers.Tier3SyntaxOrTextual);
        Assert.Contains(facts, fact =>
            fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.EvidenceTier == EvidenceTiers.Tier2Structural);
        Assert.Contains(facts, fact =>
            fact.FactType == FactTypes.LegacyDataGeneratedCodeLinked
            && fact.RuleId == RuleIds.LegacyDataGeneratedLink
            && fact.EvidenceTier == EvidenceTiers.Tier2Structural);
    }

    [Fact]
    public void Scan_preserves_legacy_evidence_metadata()
    {
        using var fixture = CreateFixture();

        var relevantFacts = fixture.Facts.Where(fact =>
            fact.RuleId.StartsWith("legacy.wcf.", StringComparison.Ordinal)
            || fact.RuleId.StartsWith("legacy.remoting.", StringComparison.Ordinal)
            || fact.RuleId.StartsWith("legacy.data.", StringComparison.Ordinal));
        Assert.All(relevantFacts, fact =>
        {
            Assert.False(string.IsNullOrWhiteSpace(fact.RuleId));
            Assert.False(string.IsNullOrWhiteSpace(fact.EvidenceTier));
            Assert.False(string.IsNullOrWhiteSpace(fact.CommitSha));
            Assert.False(string.IsNullOrWhiteSpace(fact.Evidence.ExtractorId));
            Assert.False(string.IsNullOrWhiteSpace(fact.Evidence.ExtractorVersion));
            Assert.True(fact.Evidence.StartLine > 0);
        });
    }

    [Fact]
    public void Scan_redacts_legacy_fixture_values_from_facts_and_report()
    {
        using var fixture = CreateFixture();
        var serialized = JsonSerializer.Serialize(fixture.Facts);
        var report = MarkdownReportWriter.Build(fixture.Result);

        foreach (var unsafeValue in fixture.UnsafeValues)
        {
            Assert.DoesNotContain(unsafeValue, serialized, StringComparison.Ordinal);
            Assert.DoesNotContain(unsafeValue, report, StringComparison.Ordinal);
        }

        Assert.Contains("Legacy Remoting Static Evidence", report, StringComparison.Ordinal);
        Assert.Contains("Legacy Data Metadata", report, StringComparison.Ordinal);
    }

    private static LegacyCoexistenceFixture CreateFixture()
    {
        var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        Directory.CreateDirectory(Path.Combine(repo, "Service References", "Rating"));

        var wcfUrl = "https" + "://services.example.test/Rating.svc";
        var remotingUrl = "tcp" + "://remoting.example.test/RemoteService";
        var objectUri = "Remote" + "Service.rem";
        var connectionString = "Server=legacy.example.test;Database=Orders;User Id=app;Password=TopSecret";

        File.WriteAllText(Path.Combine(repo, "App.config"), $$"""
            <configuration>
              <connectionStrings>
                <add name="LegacyOrders" connectionString="{{connectionString}}" providerName="System.Data.SqlClient" />
              </connectionStrings>
              <system.serviceModel>
                <client>
                  <endpoint address="{{wcfUrl}}"
                            binding="basicHttpBinding"
                            contract="Sample.Contracts.IRatingService"
                            name="RatingEndpoint" />
                </client>
              </system.serviceModel>
              <system.runtime.remoting>
                <application name="SyntheticRemotingApplication">
                  <channels>
                    <channel ref="tcp" port="9090" />
                  </channels>
                  <service>
                    <wellknown type="Sample.Remoting.RemoteService, Sample.Legacy" objectUri="{{objectUri}}" mode="Singleton" />
                  </service>
                  <client>
                    <wellknown type="Sample.Remoting.RemoteService, Sample.Legacy" url="{{remotingUrl}}" />
                  </client>
                </application>
              </system.runtime.remoting>
            </configuration>
            """);
        File.WriteAllText(Path.Combine(repo, "Contracts.cs"), """
            using System.ServiceModel;

            namespace Sample.Contracts;

            [ServiceContract]
            public interface IRatingService
            {
                [OperationContract]
                string Rate(RatingRequest request);
            }

            public sealed class RatingRequest { }
            """);
        File.WriteAllText(Path.Combine(repo, "Service References", "Rating", "Reference.cs"), """
            using System.CodeDom.Compiler;
            using System.ServiceModel;

            namespace Sample.Clients;

            [GeneratedCode("svcutil", "4.0")]
            public partial class RatingServiceClient : ClientBase<Sample.Contracts.IRatingService>
            {
                public string Rate(Sample.Contracts.RatingRequest request)
                {
                    return string.Empty;
                }
            }
            """);
        File.WriteAllText(Path.Combine(repo, "Rating.svc"), """
            <%@ ServiceHost Language="C#" Service="Sample.Services.RatingService" %>
            """);
        File.WriteAllText(Path.Combine(repo, "RemotingHost.cs"), $$"""
            using System;
            using System.Runtime.Remoting;
            using System.Runtime.Remoting.Channels;
            using System.Runtime.Remoting.Channels.Tcp;

            namespace Sample.Remoting;

            public sealed class RemoteService : MarshalByRefObject
            {
                public string Ping() => "ok";
            }

            public static class RemotingHost
            {
                public static void Start()
                {
                    var channel = new TcpChannel();
                    ChannelServices.RegisterChannel(channel, false);
                    RemotingConfiguration.RegisterWellKnownServiceType(typeof(RemoteService), "{{objectUri}}", WellKnownObjectMode.Singleton);
                    _ = Activator.GetObject(typeof(RemoteService), "{{remotingUrl}}");
                }
            }
            """);
        File.WriteAllText(Path.Combine(repo, "Orders.dbml"), """
            <Database Name="Orders" Class="OrdersDataContext" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="dbo.Orders" Member="Orders">
                <Type Name="Order">
                  <Column Name="OrderId" Member="OrderId" IsPrimaryKey="true" CanBeNull="false" />
                  <Column Name="Status" Member="Status" />
                </Type>
              </Table>
            </Database>
            """);
        File.WriteAllText(Path.Combine(repo, "Orders.designer.cs"), """
            namespace Sample.Data;
            public partial class Order { }
            public partial class OrdersDataContext { }
            """);

        return new LegacyCoexistenceFixture(
            temp,
            ScanEngine.Scan(new ScanOptions(repo, output)),
            [wcfUrl, remotingUrl, objectUri, connectionString, "TopSecret", "legacy.example.test"]);
    }

    private sealed record LegacyCoexistenceFixture(
        TempDirectory Temp,
        ScanResult Result,
        IReadOnlyList<string> UnsafeValues) : IDisposable
    {
        public IReadOnlyList<CodeFact> Facts => Result.Facts;

        public void Dispose()
        {
            Temp.Dispose();
        }
    }
}
