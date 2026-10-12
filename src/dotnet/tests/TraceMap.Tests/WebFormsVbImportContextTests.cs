using System.Text;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsVbImportContextTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Alternate_provider_location_requires_identical_trusted_bytes(bool changed)
    {
        using var temp = new TempDirectory();
        var root = RealTempPath(temp.Path);
        Directory.CreateDirectory(Path.Combine(root, "App_Code"));
        File.WriteAllText(Path.Combine(root, "App_Code/Entry.vb"), "Public Class Entry\nEnd Class");
        var context = WriteContext(root, new string('a', 40), Path.Combine(root, "captured"), ["App_Code/Entry.vb"]);
        var trusted = Path.Combine(root, "trusted.dll");
        File.Copy(Path.Combine(root, "captured/System.dll"), trusted);
        if (changed) File.AppendAllText(trusted, "different trusted bytes");
        WebFormsVbImportContext Read() => WebFormsVbImportContext.ReadWithTrustedProvider(root, new string('a', 40), context,
            [new("App_Code/Entry.vb", "VisualBasic", 0)], () => trusted);
        if (changed) Assert.Throws<ImportContextRejectedException>(() => Read());
        else Assert.NotEmpty(Read().Namespaces);
    }

    internal static string WriteContext(string root, string commit, string directory, string[] paths,
        string scenario = "valid")
    {
        Directory.CreateDirectory(directory);
        var config = Path.Combine(directory, "machine.config");
        var provider = Path.Combine(directory, "System.dll");
        File.WriteAllText(config, "<configuration />");
        // Test-only observer assertion. Not a real provider or native proof.
        File.WriteAllText(provider, "public synthetic provider bytes");
        var payload = JsonSerializer.Serialize(new
        {
            sourceCommitSha = scenario == "wrong-commit" ? "other" : commit,
            sourceRootPathSha256 = scenario == "wrong-root" ? new string('0', 64) : WebFormsVbImportContext.RootHash(root),
            providerType = scenario == "custom-provider" ? "Other.Provider" : "Microsoft.VisualBasic.VBCodeProvider",
            providerAssemblyPath = provider,
            providerAssemblySha256 = WebFormsVbImportContext.Hash(File.ReadAllBytes(provider)),
            runtimeVersion = "4.0.30319",
            processArchitecture = "x64",
            evidenceKind = scenario == "build-claim" ? "authenticated-build" : WebFormsVbImportContext.Kind,
            mappingKind = scenario == "iis-claim" ? "effective-iis-application" : "standalone-root",
            namespaces = scenario == "alias" ? new[] { "List=Other.List" } : scenario == "duplicate" ? new[] { "System", "system" }
                : scenario == "limit" ? Enumerable.Range(0, 129).Select(i => "Synthetic.N" + i).ToArray()
                : new[] { "System", "System.Collections.Generic", "System.Web.UI", "Other" },
            sourceFiles = paths.Select(p => new { path = scenario == "escape" ? "App_Code/../../Escape.vb" : p,
                sha256 = WebFormsVbImportContext.Hash(File.ReadAllBytes(Path.Combine(root, p))) }),
            configurationFiles = new[] { config, Path.Combine(root, "Web.config") }.Where(File.Exists)
                .Select(p => new { path = p, sha256 = WebFormsVbImportContext.Hash(File.ReadAllBytes(p)) })
        });
        if (scenario == "duplicate-json") payload = payload.Replace("\"runtimeVersion\":", "\"runtimeVersion\":\"4.0\",\"runtimeVersion\":");
        var contextPath = Path.Combine(directory, "context.json");
        File.WriteAllText(contextPath, JsonSerializer.Serialize(new
        {
            schemaVersion = WebFormsVbImportContext.Schema,
            visibility = scenario == "public" ? "public" : "local-only",
            generatorSha256 = new string('a', 64),
            boundedInputSha256 = scenario == "tamper" ? new string('0', 64) : WebFormsVbImportContext.Hash(Encoding.UTF8.GetBytes(payload)),
            payloadJson = payload
        }));
        if (scenario == "stale-config") File.AppendAllText(config, " ");
        if (scenario == "stale-provider") File.AppendAllText(provider, " ");
        if (scenario == "stale-source") File.AppendAllText(Path.Combine(root, paths[0]), "\n'changed");
        if (scenario == "new-root-config") File.WriteAllText(Path.Combine(root, "Web.config"), "<configuration />");
        return contextPath;
    }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("wrong-commit", false)]
    [InlineData("wrong-root", false)]
    [InlineData("custom-provider", false)]
    [InlineData("build-claim", false)]
    [InlineData("iis-claim", false)]
    [InlineData("alias", false)]
    [InlineData("duplicate", false)]
    [InlineData("limit", false)]
    [InlineData("escape", false)]
    [InlineData("duplicate-json", false)]
    [InlineData("public", false)]
    [InlineData("tamper", false)]
    [InlineData("stale-config", false)]
    [InlineData("stale-provider", false)]
    [InlineData("stale-source", false)]
    [InlineData("new-root-config", false)]
    public void Environment_context_is_local_bound_and_does_not_claim_build_proof(string scenario, bool admitted)
    {
        using var temp = new TempDirectory();
        var directory = RealTempPath(temp.Path);
        var root = Path.Combine(directory, "source");
        Directory.CreateDirectory(Path.Combine(root, "App_Code"));
        const string path = "App_Code/Entry.vb";
        File.WriteAllText(Path.Combine(root, path), "Public Class Entry\nPublic Sub Start(items As List(Of Integer))\nEnd Sub\nEnd Class");
        var commit = new string('a', 40);
        var context = WriteContext(root, commit, Path.Combine(directory, "context"), [path], scenario);
        var manifest = new ScanManifest("synthetic-imports", "synthetic", null, "test", commit, ScannerVersions.TraceMap,
            DateTimeOffset.UnixEpoch, "Level3SyntaxAnalysis", "NotRun", [], [], [], []);
        var inventory = new FileInventoryItem[] { new(path, "VisualBasic", 100) };
        // Receipt validation uses an independent test trust anchor. Production
        // extraction must reject this synthetic provider, even with a valid hash.
        if (admitted)
        {
            var accepted = WebFormsVbImportContext.ReadWithTrustedProvider(root, commit, context, inventory,
                () => Path.Combine(directory, "context", "System.dll"));
            Assert.Equal(WebFormsVbImportContext.Hash(File.ReadAllBytes(context)), accepted.ReceiptSha256);
        }
        else
        {
            var rejected = Assert.Throws<ImportContextRejectedException>(() => WebFormsVbImportContext.ReadWithTrustedProvider(
                root, commit, context, inventory, () => Path.Combine(directory, "context", "System.dll")));
            if (scenario == "escape") { Assert.Equal("escaped", rejected.Category); Assert.Equal("source", rejected.Scope); }
            if (scenario == "stale-source") { Assert.Equal("stale", rejected.Category); Assert.Equal("source", rejected.Scope); }
            if (scenario == "stale-config") { Assert.Equal("stale", rejected.Category); Assert.Equal("configuration", rejected.Scope); }
            if (scenario == "custom-provider") { Assert.Equal("provider-mismatch", rejected.Category); Assert.Equal("provider", rejected.Scope); }
            if (scenario == "duplicate-json") Assert.Equal("malformed", rejected.Category);
        }
        var facts = VisualBasicSyntaxExtractor.Extract(root, manifest, inventory, null,
            webFormsVbImportContextPath: context);
        var method = Assert.Single(facts, f => f.FactType == FactTypes.MethodDeclared);
        Assert.False(method.Properties.ContainsKey("effectiveImportedNamespaces"));
        var gap = Assert.Single(facts, f => f.Properties.GetValueOrDefault("gapKind") == "WebFormsVbImportContextRejected");
        Assert.Equal(WebFormsVbImportContext.Hash(File.ReadAllBytes(context)), gap.Properties["receiptSha256"]);
        Assert.Contains(gap.Properties["inspectedScope"], new[] { "receipt", "provider", "configuration", "source", "namespaces" });
        Assert.DoesNotContain(directory, JsonSerializer.Serialize(gap));
    }

    internal static string RealTempPath(string path) => OperatingSystem.IsMacOS() && (path.StartsWith("/var/", StringComparison.Ordinal)
        || path.StartsWith("/tmp/", StringComparison.Ordinal)) ? "/private" + path : path;
}
