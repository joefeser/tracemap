using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using VB = Microsoft.CodeAnalysis.VisualBasic;
using VBS = Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TraceMap.Core;

public static partial class LegacyWebFormsExtractor
{
    // Discovery only: never execute configuration, read un-inventoried JS, or
    // turn a virtual path/name match into a semantic/runtime call edge.
    private const int BundleSourceLimit = 1_048_576;
    private const int BundleMemberLimit = 128;
    private const string BundleLimitations = "Syntax-only bundle discovery. Framework identity, registration execution, application-root identity, rendered script selection/order, file existence, JavaScript calls and runtime behavior are not proven. Wildcards, directory includes, aliases, reassignment and custom transforms are unsupported.";

    private static void AddBundleDiscoveryFacts(string repoPath, ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory, List<CodeFact> facts)
    {
        var buffer = new char[BundleSourceLimit + 1];
        foreach (var item in inventory.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            var extension = Path.GetExtension(item.RelativePath).ToLowerInvariant();
            if (extension is not (".cs" or ".vb" or ".aspx" or ".master" or ".ascx" or ".asax")) continue;
            if (item.SizeBytes > BundleSourceLimit)
            {
                facts.Add(BundleFact(manifest, item.RelativePath, 1, 1, "", FactTypes.AnalysisGap,
                    "BundleSourceLimit", null, null));
                continue;
            }
            string text;
            try
            {
                // Bound the actual read too: the inventory size can become stale.
                using var reader = File.OpenText(Path.Combine(repoPath, item.RelativePath));
                var count = reader.ReadBlock(buffer, 0, buffer.Length);
                if (count > BundleSourceLimit)
                {
                    facts.Add(BundleFact(manifest, item.RelativePath, 1, 1, "", FactTypes.AnalysisGap,
                        "BundleSourceLimit", null, null));
                    continue;
                }
                text = new string(buffer, 0, count);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                facts.Add(BundleFact(manifest, item.RelativePath, 1, 1, "", FactTypes.AnalysisGap,
                    "BundleSourceUnavailable", null, null));
                continue;
            }
            if (extension == ".asax")
            {
                try
                {
                    AddApplicationBundles(manifest, item.RelativePath, text, facts);
                }
                catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
                {
                    facts.Add(BundleFact(manifest, item.RelativePath, 1, 1, FactFactory.Hash(text, 64),
                        FactTypes.AnalysisGap, "ApplicationBundleParsingLimit", null, null));
                }
            }
            else if (extension is ".cs" or ".vb")
            {
                if (!text.Contains("ScriptBundle", StringComparison.OrdinalIgnoreCase)) continue;
                AddBundleDeclarations(manifest, item.RelativePath, text, extension == ".vb", facts);
            }
            else
                AddBundleReferences(manifest, item.RelativePath, text, facts);
        }
    }

    private static void AddBundleDeclarations(ScanManifest manifest, string path, string text, bool vb, List<CodeFact> facts,
        int lineOffset = 0, int positionOffset = 0, string? originalHash = null)
    {
        var inputHash = originalHash ?? FactFactory.Hash(text, 64);
        var root = vb ? VB.VisualBasicSyntaxTree.ParseText(text).GetRoot() : CSharpSyntaxTree.ParseText(text).GetRoot();
        if (originalHash is not null && root.ContainsDiagnostics)
        {
            var diagnostic = root.GetDiagnostics().First();
            var line = Math.Max(1, diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1 + lineOffset);
            facts.Add(BundleFact(manifest, path, line, line, inputHash, FactTypes.AnalysisGap, "MalformedBundleSource", null, null));
            return;
        }
        var declarationCount = 0;
        foreach (var node in root.DescendantNodes().Where(node => node is ObjectCreationExpressionSyntax or VBS.ObjectCreationExpressionSyntax))
        {
            var type = node switch
            {
                ObjectCreationExpressionSyntax creation => creation.Type.ToString(),
                VBS.ObjectCreationExpressionSyntax creation => creation.Type.ToString(),
                _ => ""
            };
            string[] typeNames = ["ScriptBundle", "System.Web.Optimization.ScriptBundle", "global::System.Web.Optimization.ScriptBundle", "Global.System.Web.Optimization.ScriptBundle"];
            if (!typeNames.Contains(type, vb ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)) continue;
            if (++declarationCount > 256)
            {
                facts.Add(BundleFact(manifest, path, 1, 1, inputHash, FactTypes.AnalysisGap, "BundleDeclarationLimit", null, null));
                break;
            }
            var span = node.GetLocation().GetLineSpan();
            var start = span.StartLinePosition.Line + 1 + lineOffset;
            var args = BundleArguments(node);
            var bundle = args.Length == 1 ? BundleLiteral(args[0]) : null;
            var id = $"webforms-bundle-declaration:{FactFactory.Hash($"{path}|{node.SpanStart + positionOffset}", 24)}";
            var chain = node;
            var members = new List<SyntaxNode>();
            var supported = true;
            while (chain.Parent is MemberAccessExpressionSyntax or VBS.MemberAccessExpressionSyntax)
            {
                var access = chain.Parent;
                var name = access switch
                {
                    MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                    VBS.MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                    _ => ""
                };
                if (access.Parent is not (InvocationExpressionSyntax or VBS.InvocationExpressionSyntax)) { supported = false; break; }
                chain = access.Parent;
                if (!string.Equals(name, "Include", vb ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) supported = false;
                members.AddRange(BundleArguments(chain));
            }
            var localGap = vb
                ? CollectLocalBundleIncludes(chain, members)
                : null;
            var values = members.Select(BundleLiteral).ToArray();
            var reason = root.ContainsDiagnostics ? "MalformedBundleSource"
                : !BundleVirtualPath(bundle) ? "UnsupportedBundlePath"
                : localGap is not null ? localGap
                : !supported || members.Count == 0 ? "UnsupportedBundleInclude"
                : members.Count > BundleMemberLimit ? "BundleMemberLimit"
                : values.Any(value => !BundleVirtualPath(value) || !value!.EndsWith(".js", StringComparison.OrdinalIgnoreCase)) ? "UnsupportedBundleMember"
                : null;
            if (reason is not null)
            {
                facts.Add(BundleFact(manifest, path, start, start, inputHash, FactTypes.AnalysisGap, reason, id, null));
                continue;
            }
            var declaration = BundleFact(manifest, path, start, span.EndLinePosition.Line + 1 + lineOffset, inputHash,
                FactTypes.WebFormsScriptBundleDeclaredCandidate, "bundle-declaration", id, bundle);
            facts.Add(declaration);
            // Keep include order and repeated declarations; do not claim final runtime order.
            for (var index = 0; index < members.Count; index++)
            {
                var memberSpan = members[index].GetLocation().GetLineSpan();
                facts.Add(BundleFact(manifest, path, memberSpan.StartLinePosition.Line + 1 + lineOffset, memberSpan.EndLinePosition.Line + 1 + lineOffset,
                    inputHash, FactTypes.WebFormsScriptBundleMemberCandidate, "bundle-member", id, values[index],
                    declaration.FactId, index));
            }
        }
    }

    private static SyntaxNode[] BundleArguments(SyntaxNode node) => node switch
    {
        ObjectCreationExpressionSyntax creation => creation.ArgumentList?.Arguments.Select(argument => (SyntaxNode)argument).ToArray() ?? [],
        InvocationExpressionSyntax invocation => invocation.ArgumentList.Arguments.Select(argument => (SyntaxNode)argument).ToArray(),
        VBS.ObjectCreationExpressionSyntax creation => creation.ArgumentList?.Arguments.Select(argument => (SyntaxNode)argument).ToArray() ?? [],
        VBS.InvocationExpressionSyntax invocation => invocation.ArgumentList?.Arguments.Select(argument => (SyntaxNode)argument).ToArray() ?? [],
        _ => []
    };

    private static string? BundleLiteral(SyntaxNode node) => node switch
    {
        ArgumentSyntax { NameColon: null, Expression: LiteralExpressionSyntax literal } when literal.IsKind(SyntaxKind.StringLiteralExpression)
            && ((ArgumentSyntax)node).RefKindKeyword.RawKind == 0 => literal.Token.ValueText,
        VBS.SimpleArgumentSyntax { NameColonEquals: null, Expression: VBS.LiteralExpressionSyntax literal }
            when literal.IsKind(VB.SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
        _ => null
    };

    private static bool BundleVirtualPath(string? value) => value is { Length: > 2 and <= 1024 }
        && value.StartsWith("~/", StringComparison.Ordinal)
        && !value.Any(character => char.IsControl(character) || character is '\\' or '*' or '?' or '{' or '}' or '#' or '%' or ':')
        && value[2..].Split('/').All(part => part.Length > 0 && part is not ("." or ".."));

    private static void AddBundleReferences(ScanManifest manifest, string path, string text, List<CodeFact> facts)
    {
        var inputHash = FactFactory.Hash(text, 64);
        var source = SourceText.From(text);
        var masked = MaskServerComments(text);
        // Only server output blocks are eligible; comments and JS strings elsewhere
        // mentioning Scripts.Render must not become page references.
        var referenceCount = 0;
        foreach (System.Text.RegularExpressions.Match match in BundleOutputBlockRegex().Matches(masked))
        {
            if (++referenceCount > 256)
            {
                facts.Add(BundleFact(manifest, path, 1, 1, inputHash, FactTypes.AnalysisGap, "BundleRenderBlockLimit", SurfaceIdentity(path), null));
                break;
            }
            var expression = match.Groups["expression"].Value.Trim();
            var vb = path.EndsWith(".vb", StringComparison.OrdinalIgnoreCase)
                || System.Text.RegularExpressions.Regex.IsMatch(masked, "Language\\s*=\\s*[\"']VB[\"']", System.Text.RegularExpressions.RegexOptions.IgnoreCase, RegexBudget.LoadTolerant);
            var parsed = vb ? (SyntaxNode)VB.SyntaxFactory.ParseExpression(expression) : SyntaxFactory.ParseExpression(expression);
            var target = parsed switch
            {
                InvocationExpressionSyntax invocation => invocation.Expression.ToString(),
                VBS.InvocationExpressionSyntax invocation => invocation.Expression.ToString(),
                _ => ""
            };
            if (!target.Equals("Scripts.Render", vb ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                && !target.Equals("System.Web.Optimization.Scripts.Render", vb ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) continue;
            var args = BundleArguments(parsed);
            var values = args.Select(BundleLiteral).ToArray();
            var line = LineAt(source, match.Index);
            if (parsed.ContainsDiagnostics || args.Length == 0 || args.Length > BundleMemberLimit || values.Any(value => !BundleVirtualPath(value)))
            {
                facts.Add(BundleFact(manifest, path, line, line, inputHash, FactTypes.AnalysisGap,
                    "UnsupportedBundleRender", SurfaceIdentity(path), null));
                continue;
            }
            for (var index = 0; index < values.Length; index++)
                facts.Add(BundleFact(manifest, path, line, LineAt(source, match.Index + match.Length - 1), inputHash,
                    FactTypes.WebFormsScriptBundleReferenceCandidate, "bundle-reference", SurfaceIdentity(path), values[index], ordinal: index, offset: match.Index));
        }
    }

    private static CodeFact BundleFact(ScanManifest manifest, string path, int line, int endLine, string inputHash,
        string type, string kind, string? source, string? target, string? declaration = null, int? ordinal = null, int? offset = null)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["generatorSha256"] = EntryGeneratorSha256.Value,
            ["coverageLabel"] = "bounded-static-webforms-bundle-discovery",
            ["ruleLimitations"] = BundleLimitations,
            ["sourceSymbolId"] = source ?? $"file:{path}",
            ["targetSymbolId"] = target is null ? "unresolved" : $"webforms-virtual-path-candidate:{FactFactory.Hash(path, 24)}:{target}",
            ["relationshipKind"] = kind,
            ["resolution"] = "declared-syntax-only"
        };
        if (inputHash.Length != 0) properties["boundedInputSha256"] = inputHash;
        if (type == FactTypes.AnalysisGap) properties["gapKind"] = kind;
        if (offset is not null) properties["sourceOffset"] = offset.Value.ToString(CultureInfo.InvariantCulture);
        if (declaration is not null) properties["bundleDeclarationFactId"] = declaration;
        if (ordinal is not null) properties["declaredOrdinal"] = ordinal.Value.ToString(CultureInfo.InvariantCulture);
        if (target is not null) properties["declaredVirtualPath"] = target;
        return FactFactory.Create(manifest, type, RuleIds.LegacyWebFormsScriptBundles,
            type == FactTypes.AnalysisGap ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(path, line, endLine, inputHash.Length == 0 ? null : inputHash,
                "LegacyWebFormsExtractor", ScannerVersions.LegacyWebFormsExtractor), sourceSymbol: source,
            targetSymbol: target, properties: properties);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"<%[:=]\s*(?<expression>.*?)%>",
        System.Text.RegularExpressions.RegexOptions.Singleline, 2000)]
    private static partial System.Text.RegularExpressions.Regex BundleOutputBlockRegex();
}
