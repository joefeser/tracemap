using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class PropertyChainMigrationTests
{
    [Theory]
    [InlineData("Public Shared Property Current As Holder", "Public Property Store As Worker", true)]
    [InlineData("Public Shared Current As Holder", "Public Store As Worker", true)]
    [InlineData("Public Property Current As Holder", "Public Property Store As Worker", false)]
    [InlineData("Public Shared Property Current As Object", "Public Property Store As Worker", false)]
    [InlineData("Public Shared Property Current As Holder", "Public Property Store(index As Integer) As Worker", false)]
    [InlineData("Public Shared Property Current As Holder", "Public Property Store As Worker\nPublic Store As Worker", false)]
    [InlineData("Public Shared Property Current As Holder", "Public Store() As Worker", false)]
    public void Explicit_chain_requires_unique_nonindexed_declarations(string root, string member, bool connected)
    {
        using var source = new TempDirectory();
        using var output = new TempDirectory();
        File.WriteAllText(Path.Combine(source.Path, "Widget.vb"), """
            Public Class Widget
                Private Sub Clicked(sender As Object, e As System.EventArgs) Handles button.Click
                    Controller.Current.Store.Save()
                End Sub
            End Class
            """);
        File.WriteAllText(Path.Combine(source.Path, "Services.vb"), $$"""
            Public Class Controller
                {{root}}
            End Class
            Public Class Holder
                {{member}}
            End Class
            Public Class Worker
                Public Sub Save()
                    Dim command As New System.Data.SqlClient.SqlCommand()
                    command.ExecuteNonQuery()
                    Dim copy As New System.Data.SqlClient.SqlBulkCopy("synthetic")
                    copy.WriteToServer(Nothing)
                End Sub
            End Class
            """);
        var scan = ScanEngine.Scan(new ScanOptions(source.Path, output.Path));
        var report = MarkdownReportWriter.Build(scan);
        Assert.Contains($"Displayed groups with candidate entry paths: {(connected ? 2 : 0)}/2", report);
        Assert.Equal(report, MarkdownReportWriter.Build(scan with { Facts = scan.Facts.Reverse().ToArray() }));
        if (connected)
        {
            Assert.Contains("receiver declarations", report);
            var withoutTypes = scan.Facts.Select(f => f with { Properties = f.Properties.Where(p => p.Key != "propertyType" && p.Key != "fieldType").ToDictionary(p => p.Key, p => p.Value) }).ToArray();
            Assert.Contains("Displayed groups with candidate entry paths: 0/2", MarkdownReportWriter.Build(scan with { Facts = withoutTypes }));
            var original = File.ReadAllText(Path.Combine(source.Path, "Widget.vb"));
            foreach (var text in new[] {
                original.Replace("Controller.Current.Store.Save()", "Dim local As Holder\nlocal.Store.Save()", StringComparison.Ordinal),
                original.Replace("Public Class Widget", "Public Class Widget\nPrivate state As Holder", StringComparison.Ordinal)
                    .Replace("Controller.Current.Store", "Me.state.Store", StringComparison.Ordinal) })
            {
                File.WriteAllText(Path.Combine(source.Path, "Widget.vb"), text);
                var positive = ScanEngine.Scan(new ScanOptions(source.Path, output.Path));
                Assert.Contains("Displayed groups with candidate entry paths: 2/2", MarkdownReportWriter.Build(positive));
            }
            foreach (var expression in new[] { "Controller.Current.Store", "Factory().Current.Store", "Controller.Current(0).Store" })
            {
                var text = original.Replace("Controller.Current.Store", expression, StringComparison.Ordinal);
                if (expression == "Controller.Current.Store") text = text.Replace("Controller.Current.Store.Save()", "Dim Controller As Object\nController.Current.Store.Save()", StringComparison.Ordinal);
                File.WriteAllText(Path.Combine(source.Path, "Widget.vb"), text);
                var rejected = ScanEngine.Scan(new ScanOptions(source.Path, output.Path));
                Assert.Contains("Displayed groups with candidate entry paths: 0/2", MarkdownReportWriter.Build(rejected));
            }
        }
    }
}
