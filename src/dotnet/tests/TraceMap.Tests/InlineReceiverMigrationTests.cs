using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class InlineReceiverMigrationTests
{
    [Theory]
    [InlineData("1", "Optional second As Integer = 0", "", true)]
    [InlineData("1, 2", "Optional second As Integer = 0", "", true)]
    [InlineData("1", "second As Integer", "", false)]
    [InlineData("first:=1", "Optional second As Integer = 0", "", false)]
    [InlineData("1", "ParamArray second() As Integer", "", false)]
    [InlineData("1", "Optional second As Integer = 0", "Public Sub Run(first As Integer)\nEnd Sub", false)]
    public void Inline_receiver_and_optional_tail_preserve_unique_candidate_paths(string arguments, string second, string overload, bool connected)
    {
        using var source = new TempDirectory();
        using var output = new TempDirectory();
        File.WriteAllText(Path.Combine(source.Path, "Inline.vb"), $$"""
            Public Class Widget
                Private Sub Clicked(sender As Object, e As System.EventArgs) Handles button.Click
                    Call (New Worker()).Run({{arguments}})
                End Sub
            End Class
            Public Class Worker
                {{overload}}
                Public Sub Run(first As Integer, {{second}})
                    Dim client As New System.Net.WebClient()
                    client.UploadString("https://example.invalid", "synthetic")
                    Dim command As New System.Data.SqlClient.SqlCommand()
                    command.ExecuteNonQuery()
                    Dim copy As New System.Data.SqlClient.SqlBulkCopy("synthetic")
                    copy.WriteToServer(Nothing)
                End Sub
            End Class
            """);
        var scan = ScanEngine.Scan(new ScanOptions(source.Path, output.Path));
        var call = Assert.Single(scan.Facts, f => f.FactType == FactTypes.CallEdge && f.Properties.GetValueOrDefault("calleeName") == "Run");
        Assert.Equal("inline-object-creation-syntax", call.Properties["receiverTypeResolution"]);
        var report = MarkdownReportWriter.Build(scan);
        Assert.Contains($"Displayed groups with candidate entry paths: {(connected ? 3 : 0)}/3", report);
        Assert.Equal(report, MarkdownReportWriter.Build(scan with { Facts = scan.Facts.Reverse().ToArray() }));
        if (arguments == "1" && connected)
        {
            var oldFacts = scan.Facts.Select(f => f with { Properties = f.Properties.Where(p => p.Key != "parameterOptionalFlags").ToDictionary(p => p.Key, p => p.Value) }).ToArray();
            Assert.Contains("Displayed groups with candidate entry paths: 0/3", MarkdownReportWriter.Build(scan with { Facts = oldFacts }));
        }
    }
}
