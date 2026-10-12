using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class ProjectlessVisualBasicHttpTests
{
    [Theory]
    [InlineData("Dim client As New System.Net.WebClient()", "client.UploadString(\"https://example.invalid\", \"data\")")]
    [InlineData("Dim client = New System.Net.WebClient()", "client.UploadString(\"https://example.invalid\", \"data\")")]
    [InlineData("", "(New System.Net.WebClient()).UploadString(\"https://example.invalid\", \"data\")")]
    public void Explicit_local_and_inline_construction_are_supported(string declaration, string call)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Input.vb"), $"""
            Public Class Example
                Public Sub Run()
                    {declaration}
                    Call {call}
                End Sub
            End Class
            """);
        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.HttpCallDetected);
    }

    [Theory]
    [InlineData("Imports System.Net", "WebClient", "UploadString", true)]
    [InlineData("Imports System.Net\nImports System", "WebClient", "DownloadString", true)]
    [InlineData("Imports WC = System.Net.WebClient", "WC", "UploadData", true)]
    [InlineData("Imports N = System.Net", "N.WebClient", "OpenRead", true)]
    [InlineData("", "Global.System.Net.Http.HttpClient", "getasync", true)]
    [InlineData("", "System.Net.HttpWebRequest", "GetResponse", true)]
    [InlineData("Imports System.Net", "Fake.WebClient", "UploadString", false)]
    [InlineData("", "Object", "UploadString", false)]
    [InlineData("Imports System.Net", "WebClient", "ReadFromJsonAsync", false)]
    public void Explicit_receivers_are_candidates_not_framework_proof(string imports, string type, string method, bool expected)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Input.vb"), $"""
            {imports}
            Public Class Example
                Public Sub Run(client As {type})
                    client.{method}("https://example.invalid/private-token")
                End Sub
            End Class
            """);
        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var calls = scan.Facts.Where(f => f.FactType == FactTypes.HttpCallDetected).ToArray();
        Assert.Equal(expected ? 1 : 0, calls.Length);
        Assert.All(calls, fact =>
        {
            Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, fact.EvidenceTier);
            Assert.Equal(RuleIds.VisualBasicSyntaxHttpCandidate, fact.RuleId);
            Assert.Equal("unresolved", fact.Properties["methodFamily"]);
            Assert.Equal("UNKNOWN", fact.Properties["httpMethod"]);
            Assert.DoesNotContain("private-token", string.Join(";", fact.Properties.Values));
        });
    }

    [Fact]
    public void Local_fake_type_shadows_import()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Input.vb"), """
            Imports System.Net
            Public Class WebClient
                Public Sub UploadString(value As String)
                End Sub
            End Class
            Public Class Example
                Public Sub Run(client As WebClient)
                    client.UploadString("not an HTTP operation")
                End Sub
            End Class
            """);
        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        Assert.DoesNotContain(scan.Facts, fact => fact.FactType == FactTypes.HttpCallDetected);
    }
}
