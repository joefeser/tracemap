using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;
using Xunit;

namespace TraceMap.Tests;

// The versions verb's output IS the ua scan-cache key material — its contract is determinism,
// completeness, and self-verifying provenance (review: no checked-in test covered the output).
public class VersionsVerbTests
{
    private static (string Output, JsonDocument Doc) Run()
    {
        var writer = new StringWriter();
        Assert.Equal(0, Program.RunVersions(writer));
        var text = writer.ToString();
        return (text, JsonDocument.Parse(text));
    }

    [Fact]
    public void Output_is_deterministic()
    {
        var (first, _) = Run();
        var (second, _) = Run();
        Assert.Equal(first, second); // same process, same constants: byte-identical manifest
    }

    [Fact]
    public void Every_scanner_versions_constant_is_present_and_ordinal_sorted()
    {
        var (_, doc) = Run();
        var expected = typeof(ScannerVersions).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => f.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        var actual = doc.RootElement.EnumerateObject()
            .Select(p => p.Name)
            .Where(n => n is not ("generator" or "generatorSha256" or "setSha256"))
            .ToArray();
        Assert.Equal(expected, actual); // complete AND ordinal — the cache key never reorders silently
    }

    [Fact]
    public void SetSha256_matches_the_canonical_extractor_set()
    {
        var (text, doc) = Run();
        var fields = doc.RootElement.EnumerateObject().Select(p => p.Name)
            .Where(n => n is not ("generator" or "generatorSha256" or "setSha256"))
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => $"  \"{n}\": \"{doc.RootElement.GetProperty(n).GetString()}\"")
            .ToArray();
        var setJson = "{\n" + string.Join(",\n", fields) + "\n}";
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(setJson))).ToLowerInvariant();
        Assert.Equal(expected, doc.RootElement.GetProperty("setSha256").GetString());
        Assert.Contains("\"ci-workflow/", text); // the manifest carries the live extractor set
    }

    [Fact]
    public void Generator_provenance_is_self_consistent()
    {
        var (_, doc) = Run();
        var generator = doc.RootElement.GetProperty("generator").GetString();
        Assert.Equal("tracemap-versions-manifest/1", generator);
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(generator!))).ToLowerInvariant();
        Assert.Equal(expected, doc.RootElement.GetProperty("generatorSha256").GetString());
    }
}
