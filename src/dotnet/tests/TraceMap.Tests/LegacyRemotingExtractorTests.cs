using System.Text.Json;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class LegacyRemotingExtractorTests
{
    [Fact]
    public void Scan_extracts_remoting_code_and_config_without_raw_endpoint_values()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        var unsafeUrl = "tcp" + "://" + "synthetic.invalid" + "/" + "RemoteService";
        var unsafeObjectUri = "Object" + "Uri";
        var unsafeApplicationName = "Synthetic" + "RemotingApplication";

        File.WriteAllText(Path.Combine(repo, "Host.cs"), $$"""
            using System;
            using System.Runtime.Remoting;
            using System.Runtime.Remoting.Channels;
            using System.Runtime.Remoting.Channels.Tcp;

            namespace Synthetic.Legacy;

            public sealed partial class RemoteService : MarshalByRefObject
            {
                public string Ping() => "ok";
            }

            public static class Host
            {
                public static void Start()
                {
                    var channel = new TcpChannel(new System.Collections.Hashtable { ["port"] = "{{UnsafePort()}}" }, null, null);
                    ChannelServices.RegisterChannel(channel, false);
                    RemotingConfiguration.RegisterWellKnownServiceType(typeof(RemoteService), "{{unsafeObjectUri}}", WellKnownObjectMode.Singleton);
                }
            }

            public static class Client
            {
                public static object Connect()
                {
                    ChannelServices.RegisterChannel(new TcpChannel(), false);
                    return Activator.GetObject(typeof(RemoteService), "{{unsafeUrl}}");
                }
            }
            """);
        File.WriteAllText(Path.Combine(repo, "Client.config"), $$"""
            <configuration>
              <system.runtime.remoting>
                <application name="{{unsafeApplicationName}}">
                  <channels>
                    <channel ref="tcp" port="{{UnsafePort()}}" />
                    <serverProviders>
                      <formatter ref="binary" typeFilterLevel="Full" />
                    </serverProviders>
                  </channels>
                  <service>
                    <wellknown type="Synthetic.Legacy.RemoteService, Synthetic.Legacy" objectUri="{{unsafeObjectUri}}" mode="Singleton" />
                    <activated type="Synthetic.Legacy.RemoteService, Synthetic.Legacy" />
                  </service>
                  <client>
                    <wellknown type="Synthetic.Legacy.RemoteService, Synthetic.Legacy" url="{{unsafeUrl}}" />
                    <activated type="Synthetic.Legacy.RemoteService, Synthetic.Legacy" />
                  </client>
                </application>
              </system.runtime.remoting>
            </configuration>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.RemotingApiUsageDeclared && fact.RuleId == RuleIds.LegacyRemotingApi);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.RemotingMarshalByRefObjectDeclared);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.RemotingChannelDeclared);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.RemotingChannelRegistered && fact.Properties.GetValueOrDefault("linkKind") == "same-method-single-local");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.RemotingServiceTypeRegistered && fact.Properties.GetValueOrDefault("objectMode") == "Singleton");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.RemotingClientActivationDeclared);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.RemotingConfigSectionDeclared);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.RemotingConfigChannelDeclared);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.RemotingConfigServiceDeclared
            && fact.Properties.GetValueOrDefault("typeName") == "Synthetic.Legacy.RemoteService"
            && fact.Properties.GetValueOrDefault("assemblyName") == "Synthetic.Legacy");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.RemotingConfigClientDeclared);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.RemotingConfigProviderDeclared);
        Assert.All(result.Facts.Where(fact => fact.FactType.StartsWith("Remoting", StringComparison.Ordinal)), fact =>
        {
            Assert.False(string.IsNullOrWhiteSpace(fact.RuleId));
            Assert.False(string.IsNullOrWhiteSpace(fact.EvidenceTier));
            Assert.False(string.IsNullOrWhiteSpace(fact.CommitSha));
            Assert.Equal("LegacyRemotingExtractor", fact.Evidence.ExtractorId);
            Assert.False(string.IsNullOrWhiteSpace(fact.Evidence.ExtractorVersion));
            Assert.True(fact.Evidence.StartLine > 0);
        });

        var serialized = JsonSerializer.Serialize(result.Facts);
        Assert.DoesNotContain(unsafeUrl, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(unsafeObjectUri, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(unsafeApplicationName, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(UnsafePort(), serialized, StringComparison.Ordinal);
        Assert.Contains("urlHash", serialized, StringComparison.Ordinal);
        Assert.Contains("objectUriHash", serialized, StringComparison.Ordinal);

        var report = MarkdownReportWriter.Build(result);
        Assert.Contains("Legacy Remoting Static Evidence", report, StringComparison.Ordinal);
        Assert.Contains("static evidence", report, StringComparison.Ordinal);
        Assert.DoesNotContain(unsafeUrl, report, StringComparison.Ordinal);
        Assert.DoesNotContain(unsafeObjectUri, report, StringComparison.Ordinal);
        Assert.DoesNotContain(UnsafePort(), report, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_ignores_comments_strings_and_inactive_regions_for_api_usage()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Noise.cs"), """
            namespace Synthetic;

            public sealed class Noise
            {
                // System.Runtime.Remoting.Channels.Tcp
                private const string Text = "RemotingConfiguration";

            #if false
                private RemotingConfiguration Disabled;
            #endif
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType.StartsWith("Remoting", StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_keeps_project_defined_lookalikes_at_syntax_tier()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Lookalike.cs"), """
            namespace Synthetic;

            public sealed class RemotingConfiguration
            {
                public static void RegisterWellKnownServiceType() { }
            }

            public static class Runner
            {
                public static void Run()
                {
                    RemotingConfiguration.RegisterWellKnownServiceType();
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.RemotingApiUsageDeclared
            && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && fact.Properties.GetValueOrDefault("limitation")!.Contains("lookalikes", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType.StartsWith("Remoting", StringComparison.Ordinal)
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic);
    }

    [Fact]
    public void Scan_emits_gaps_for_dynamic_registration_and_malformed_or_unsupported_config()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Dynamic.cs"), """
            using System.Runtime.Remoting.Channels;

            namespace Synthetic;

            public static class DynamicHost
            {
                public static void Register(IChannel channel)
                {
                    ChannelServices.RegisterChannel(channel, false);
                    RemotingConfiguration.RegisterActivatedServiceType(typeof(DynamicHost));
                    _ = Activator.GetObject(typeof(DynamicHost), "tcp" + "://" + "synthetic.invalid" + "/" + "Dynamic");
                    _ = Activator.CreateInstance(typeof(DynamicHost));
                }
            }
            """);
        File.WriteAllText(Path.Combine(repo, "Unsupported.config"), """
            <configuration>
              <system.runtime.remoting>
                <customRemotingShape />
              </system.runtime.remoting>
            </configuration>
            """);
        File.WriteAllText(Path.Combine(repo, "Broken.config"), "<configuration><system.runtime.remoting>");

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyRemotingChannel
            && fact.Properties.GetValueOrDefault("classification") == "UnsupportedRemotingChannelRegistrationLink");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyRemotingRegistration
            && fact.Properties.GetValueOrDefault("limitation") == "activated-type-registration-v1-deferred");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyRemotingConfig
            && fact.Properties.GetValueOrDefault("classification") == "UnsupportedRemotingConfigChildren");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyRemotingConfig
            && fact.Properties.GetValueOrDefault("classification") == "MalformedRemotingConfig");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType.StartsWith("Remoting", StringComparison.Ordinal)
            && fact.ContractElement == "Activator.CreateInstance");
    }

    [Fact]
    public void Scan_keeps_wcf_and_remoting_fact_families_separate()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        var wcfAddress = "http" + "://" + "synthetic.invalid" + "/" + "service";
        File.WriteAllText(Path.Combine(repo, "Mixed.config"), $$"""
            <configuration>
              <system.serviceModel>
                <client>
                  <endpoint address="{{wcfAddress}}" binding="basicHttpBinding" contract="Synthetic.IService" />
                </client>
              </system.serviceModel>
              <system.runtime.remoting>
                <channels>
                  <channel ref="ipc" />
                </channels>
              </system.runtime.remoting>
            </configuration>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WcfClientEndpointDeclared);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.RemotingConfigChannelDeclared);
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType.StartsWith("Wcf", StringComparison.Ordinal)
            && fact.RuleId.StartsWith("legacy.remoting", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType.StartsWith("Remoting", StringComparison.Ordinal)
            && fact.RuleId.StartsWith("legacy.wcf", StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_checked_in_remoting_sample_deduplicates_semantic_and_syntax_marshal_evidence()
    {
        using var temp = new TempDirectory();
        var sample = Path.Combine(FindRepositoryRoot(), "samples", "dotnet-remoting-sample");
        var output = Path.Combine(temp.Path, "out");

        var result = ScanEngine.Scan(new ScanOptions(sample, output));
        var marshalFacts = result.Facts
            .Where(fact => fact.FactType == FactTypes.RemotingMarshalByRefObjectDeclared)
            .ToArray();

        var fact = Assert.Single(marshalFacts);
        Assert.Equal(EvidenceTiers.Tier1Semantic, fact.EvidenceTier);
        Assert.Contains(result.Facts, item => item.FactType == FactTypes.RemotingChannelRegistered);
        Assert.Contains(result.Facts, item => item.FactType == FactTypes.RemotingConfigSectionDeclared);
    }

    [Fact]
    public void Scan_does_not_link_channel_locals_across_methods()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Host.cs"), """
            using System.Runtime.Remoting.Channels;
            using System.Runtime.Remoting.Channels.Tcp;

            namespace Synthetic.Legacy;

            public static class Host
            {
                public static void Create()
                {
                    var channel = new TcpChannel();
                }

                public static void Register(IChannel channel)
                {
                    ChannelServices.RegisterChannel(channel, false);
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.RemotingChannelRegistered
            && fact.Properties.GetValueOrDefault("linkKind") == "same-method-single-local");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.RemotingChannelRegistered
            && fact.Properties.GetValueOrDefault("linkKind") == "unsupported-dynamic-or-nonlocal");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("classification") == "UnsupportedRemotingChannelRegistrationLink");
    }

    [Fact]
    public void Scan_does_not_promote_project_defined_marshalbyrefobject_to_semantic_remoting()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Synthetic.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Lookalike.cs"), """
            namespace Synthetic;

            public class MarshalByRefObject
            {
            }

            public sealed class LooksRemote : MarshalByRefObject
            {
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.RemotingMarshalByRefObjectDeclared
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.RemotingMarshalByRefObjectDeclared
            && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual);
    }

    [Fact]
    public void Scan_promotes_activator_getobject_when_type_has_semantic_marshalbyref_evidence()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Synthetic.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "RemoteService.cs"), """
            namespace Synthetic.Legacy;

            public sealed class RemoteService : System.MarshalByRefObject
            {
            }
            """);
        File.WriteAllText(Path.Combine(repo, "Client.cs"), """
            using System;

            namespace Synthetic.Legacy;

            public static class Client
            {
                public static object Connect()
                {
                    return Activator.GetObject(typeof(RemoteService), "tcp" + "://" + "synthetic.invalid" + "/" + "RemoteService");
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.RemotingMarshalByRefObjectDeclared
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.RemotingClientActivationDeclared
            && fact.Properties.GetValueOrDefault("targetTypeName") == "RemoteService"
            && fact.Properties.GetValueOrDefault("coverage") == "syntax-fallback-semantic-marshal-target");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("classification") == "ActivatorGetObjectNeedsRemotingContext");
    }

    private static string UnsafePort()
    {
        return "90" + "50";
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && Directory.Exists(Path.Combine(directory.FullName, "samples")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate TraceMap repository root for sample scan test.");
    }
}
