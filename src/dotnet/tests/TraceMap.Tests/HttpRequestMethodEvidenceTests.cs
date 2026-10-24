using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.VisualBasic;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class HttpRequestMethodEvidenceTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Native_partial_scan_preserves_gaps_and_only_independent_method_evidence(string language)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        using var temp = new TempDirectory();
        var source = Path.Combine(temp.Path, "source");
        Directory.CreateDirectory(source);
        var fixture = Path.Combine(root.FullName, "samples/outbound-http-coverage", language);
        foreach (var file in Directory.GetFiles(fixture).Where(f => f.EndsWith("proj", StringComparison.Ordinal)
                     || Path.GetFileNameWithoutExtension(f) == "RequestMethodEvidence"))
        {
            var content = File.ReadAllText(file);
            if (Path.GetFileNameWithoutExtension(file) == "RequestMethodEvidence")
                content = content.Replace("req.GetResponse()", language == "vb"
                    ? "MissingLogger.Write(\"synthetic\")\n        req.GetResponse()"
                    : "MissingLogger.Write(\"synthetic\");\n        req.GetResponse()", StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(source, Path.GetFileName(file)), content);
        }
        var scan = ScanEngine.Scan(new ScanOptions(source, Path.Combine(temp.Path, "scan")));
        Assert.NotEqual("Succeeded", scan.Manifest.BuildStatus);
        Assert.Contains(scan.Facts, f => f.FactType == FactTypes.AnalysisGap
            && f.Properties.GetValueOrDefault("gapKind") == "CompilationDiagnostic");
        var fact = Assert.Single(scan.Facts, f => f.FactType == FactTypes.HttpCallDetected
            && f.SourceSymbol?.Contains("RequestMethodEvidence.Proven(", StringComparison.Ordinal) == true);
        Assert.Equal("POST", fact.Properties["httpMethod"]);
        Assert.Equal(EvidenceTiers.Tier1Semantic, fact.EvidenceTier);
        Assert.Equal("same-block-local-property-assignment", fact.Properties["httpMethodEvidence"]);
        var negative = Assert.Single(scan.Facts, f => f.FactType == FactTypes.HttpCallDetected
            && f.SourceSymbol?.Contains("RequestMethodEvidence.Conditional(", StringComparison.Ordinal) == true);
        Assert.Equal("UNKNOWN", negative.Properties["httpMethod"]);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Native_scan_index_and_handoff_preserve_method_and_assignment_evidence(string language)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(root.FullName, "samples/outbound-http-coverage", language), Path.Combine(temp.Path, "scan")));
        Assert.Equal("Succeeded", scan.Manifest.BuildStatus);
        var fact = Assert.Single(scan.Facts, f => f.FactType == FactTypes.HttpCallDetected
            && f.SourceSymbol?.Contains("RequestMethodEvidence.Proven(", StringComparison.Ordinal) == true);
        Assert.Equal("POST", fact.Properties["httpMethod"]);
        Assert.Equal(RuleIds.HttpClientInvocation, fact.RuleId);
        Assert.Equal(EvidenceTiers.Tier1Semantic, fact.EvidenceTier);
        Assert.Equal("same-block-local-property-assignment", fact.Properties["httpMethodEvidence"]);
        Assert.True(int.Parse(fact.Properties["httpMethodAssignmentStartLine"], System.Globalization.CultureInfo.InvariantCulture) < fact.Evidence.StartLine);
        var negative = Assert.Single(scan.Facts, f => f.FactType == FactTypes.HttpCallDetected
            && f.SourceSymbol?.Contains("RequestMethodEvidence.Conditional(", StringComparison.Ordinal) == true);
        Assert.Equal("UNKNOWN", negative.Properties["httpMethod"]);
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        var report = await CombinedDependencyPathReporter.BuildReportAsync(new(index, temp.Path,
            FromSymbol: fact.SourceSymbol, ToSurface: "http-client", MaxDepth: 8));
        var handoff = GroupedCompiledPathHandoffBuilder.Create(report,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(index))));
        Assert.Contains(handoff.Nodes.Values, n => n.HttpMethod == "POST");
        var repeat = ScanEngine.Scan(new ScanOptions(Path.Combine(root.FullName, "samples/outbound-http-coverage", language), Path.Combine(temp.Path, "repeat")));
        Assert.Equal(scan.Facts.Select(f => System.Text.Json.JsonSerializer.Serialize(f)), repeat.Facts.Select(f => System.Text.Json.JsonSerializer.Serialize(f)));
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (var language in new[] { "cs", "vb" })
        foreach (var shape in new[] { "literal", "constant", "content", "stream", "missing", "dynamic", "conditional",
                     "alias", "escape", "reassign", "overwrite", "after", "loop", "capture", "lowercase", "custom", "try", "parameter" })
            yield return [language, shape, shape is "literal" or "constant" or "content" or "stream" or "try" ? "POST" : "UNKNOWN"];
        yield return ["vb", "resume", "UNKNOWN"];
        foreach (var language in new[] { "cs", "vb" })
        foreach (var shape in new[] { "invalid-log", "invalid-serializer", "invalid-after", "invalid-escape",
                     "invalid-member", "invalid-write", "invalid-condition", "invalid-factory" })
            yield return [language, shape, shape is "invalid-log" or "invalid-serializer" or "invalid-after" ? "POST" : "UNKNOWN"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Same_local_method_requires_bounded_unambiguous_evidence(string language, string shape, string expected)
    {
        var cs = shape switch
        {
            "missing" => "",
            "dynamic" => "req.Method = input;",
            "conditional" => "if (flag) req.Method = \"POST\";",
            "alias" => "var alias = req; req.Method = \"POST\"; alias.Method = input;",
            "escape" => "req.Method = \"POST\"; Escape(req);",
            "reassign" => "req.Method = \"POST\"; req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);",
            "overwrite" => "req.Method = \"POST\"; req.Method = input;",
            "constant" => "const string verb = \"POST\"; req.Method = verb;",
            "content" => "req.Method = \"POST\"; req.ContentType = \"application/json\"; req.Headers.Add(\"X-Test\", input);",
            "stream" => "req.Method = \"POST\"; var stream = req.GetRequestStream(); stream.WriteByte(0);",
            "capture" => "req.Method = \"POST\"; System.Action a = () => req.Method = input; a();",
            "lowercase" => "req.Method = \"post\";",
            "custom" => "req.Method = \"PRIVATE-VERB\";",
            _ => "req.Method = \"POST\";"
        };
        var vb = shape switch
        {
            "missing" => "",
            "dynamic" => "req.Method = input",
            "conditional" => "If flag Then req.Method = \"POST\"",
            "alias" => "Dim aliasRequest = req\nreq.Method = \"POST\"\naliasRequest.Method = input",
            "escape" => "req.Method = \"POST\"\nEscape(req)",
            "reassign" => "req.Method = \"POST\"\nreq = CType(System.Net.WebRequest.Create(url), System.Net.HttpWebRequest)",
            "overwrite" => "req.Method = \"POST\"\nreq.Method = input",
            "constant" => "Const verb As String = \"POST\"\nreq.Method = verb",
            "content" => "req.Method = \"POST\"\nreq.ContentType = \"application/json\"\nreq.Headers.Add(\"X-Test\", input)",
            "stream" => "req.Method = \"POST\"\nDim stream = req.GetRequestStream()\nstream.WriteByte(0)",
            "capture" => "req.Method = \"POST\"\nDim a As System.Action = Sub() req.Method = input\na()",
            "lowercase" => "req.Method = \"post\"",
            "custom" => "req.Method = \"PRIVATE-VERB\"",
            _ => "req.Method = \"POST\""
        };
        var csBody = "var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);\n" + cs + "\nreq.GetResponse();";
        var vbBody = "Dim req = CType(System.Net.WebRequest.Create(url), System.Net.HttpWebRequest)\n" + vb + "\nreq.GetResponse()";
        if (shape == "after")
        {
            csBody = csBody.Replace(cs, "", StringComparison.Ordinal) + "\n" + cs;
            vbBody = vbBody.Replace(vb, "", StringComparison.Ordinal) + "\n" + vb;
        }
        if (shape == "loop") { csBody = "while(flag) { " + csBody + " }"; vbBody = "While flag\n" + vbBody + "\nEnd While"; }
        if (shape == "try") { csBody = "try { " + csBody + " } catch(System.Exception) {}"; vbBody = "Try\n" + vbBody + "\nCatch ex As System.Exception\nEnd Try"; }
        if (shape == "resume") vbBody = "On Error Resume Next\n" + vbBody;
        if (shape == "parameter")
        {
            csBody = csBody.Replace("var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);", "", StringComparison.Ordinal);
            vbBody = vbBody.Replace("Dim req = CType(System.Net.WebRequest.Create(url), System.Net.HttpWebRequest)", "", StringComparison.Ordinal);
        }
        if (shape.StartsWith("invalid-", StringComparison.Ordinal))
        {
            var csError = shape switch
            {
                "invalid-log" or "invalid-after" => "MissingLogger.Write(input);",
                "invalid-serializer" => "var payload = new MissingSerializer().Serialize(input);",
                "invalid-escape" => "MissingHelper.Change(req);",
                "invalid-member" => "req.MissingMethod();",
                "invalid-write" => "req.Method = MissingVerb;",
                "invalid-condition" => "if (MissingCondition) { }",
                _ => ""
            };
            var vbError = shape switch
            {
                "invalid-log" or "invalid-after" => "MissingLogger.Write(input)",
                "invalid-serializer" => "Dim payload = New MissingSerializer().Serialize(input)",
                "invalid-escape" => "MissingHelper.Change(REq)",
                "invalid-member" => "req.MissingMethod()",
                "invalid-write" => "req.Method = MissingVerb",
                "invalid-condition" => "If MissingCondition Then\nEnd If",
                _ => ""
            };
            if (shape == "invalid-after") { csBody += "\n" + csError; vbBody += "\n" + vbError; }
            else
            {
                csBody = csBody.Replace("req.GetResponse();", csError + "\nreq.GetResponse();", StringComparison.Ordinal);
                vbBody = vbBody.Replace("req.GetResponse()", vbError + "\nreq.GetResponse()", StringComparison.Ordinal);
            }
            if (shape == "invalid-factory")
            {
                csBody = csBody.Replace("System.Net.WebRequest.Create(url)", "MissingFactory.Create(url)", StringComparison.Ordinal);
                vbBody = vbBody.Replace("System.Net.WebRequest.Create(url)", "MissingFactory.Create(url)", StringComparison.Ordinal);
            }
        }
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        Compilation compilation;
        SyntaxTree tree;
        if (language == "cs")
        {
            var parameter = shape == "parameter" ? ", System.Net.HttpWebRequest req" : "";
            tree = CSharpSyntaxTree.ParseText("class Sample { static void Escape(object o) {} static void Run(string url, string input, bool flag" + parameter + ") { " + csBody + " } }");
            compilation = CSharpCompilation.Create("Synthetic", [tree], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }
        else
        {
            var parameter = shape == "parameter" ? ", req As System.Net.HttpWebRequest" : "";
            tree = VisualBasicSyntaxTree.ParseText("Public Module Sample\nSub Escape(o As Object)\nEnd Sub\nSub Run(url As String, input As String, flag As Boolean" + parameter + ")\n" + vbBody + "\nEnd Sub\nEnd Module");
            compilation = VisualBasicCompilation.Create("Synthetic", [tree], references, new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }
        if (shape.StartsWith("invalid-", StringComparison.Ordinal))
            Assert.Contains(compilation.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        else
            Assert.DoesNotContain(compilation.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        var model = compilation.GetSemanticModel(tree);
        var call = Assert.Single(tree.GetRoot().DescendantNodes().Select(n => model.GetOperation(n))
            .OfType<IInvocationOperation>(), o => o.TargetMethod.Name == "GetResponse");
        Assert.True(OutboundHttpApi.TryClassify(call.TargetMethod, out var family, out var method));
        var properties = new SortedDictionary<string, string>();
        OutboundHttpApi.AddCallProperties(properties, call, family, method);
        Assert.Equal(expected, properties["httpMethod"]);
        Assert.Equal(expected != "UNKNOWN", properties.ContainsKey("httpMethodAssignmentStartLine"));
        Assert.DoesNotContain("PRIVATE-VERB", string.Join(" ", properties.Values));
    }
}
