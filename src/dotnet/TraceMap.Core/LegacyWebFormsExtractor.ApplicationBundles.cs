using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using VBS = Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TraceMap.Core;

public static partial class LegacyWebFormsExtractor
{
    private static void AddApplicationBundles(ScanManifest manifest, string path, string text, List<CodeFact> facts)
    {
        if (!text.Contains("ScriptBundle", StringComparison.OrdinalIgnoreCase)) return;
        var markup = Regex.Replace(text, ServerCommentRegex().ToString(),
            match => new string(match.Value.Select(ch => ch is '\r' or '\n' ? ch : ' ').ToArray()),
            BundleMarkupOptions | RegexOptions.Singleline, RegexBudget.LoadTolerant);
        if (!markup.Contains("ScriptBundle", StringComparison.OrdinalIgnoreCase)) return;

        // Admit one inline server block and Application/Import directives only.
        // Reject unfamiliar surrounding markup instead of extracting code out of a lookalike tag.
        var scriptStart = markup.IndexOf("<script", StringComparison.OrdinalIgnoreCase);
        var script = scriptStart < 0 ? Match.Empty : Regex.Match(markup[scriptStart..], ApplicationBundleScript, BundleMarkupOptions | RegexOptions.Singleline, RegexBudget.LoadTolerant);
        var hash = FactFactory.Hash(text, 64);
        var header = script.Success ? markup[..scriptStart] : "";
        var directives = Regex.Matches(header, ApplicationBundleDirective, BundleMarkupOptions, RegexBudget.LoadTolerant);
        var applications = directives.Where(m => m.Groups["kind"].Value.Equals("Application", StringComparison.OrdinalIgnoreCase)).ToArray();
        var attributes = applications.Length == 1 ? BundleAttributes(applications[0].Groups["attrs"].Value) : null;
        var scriptAttributes = script.Success ? BundleAttributes(script.Groups["attrs"].Value) : null;
        var language = scriptAttributes?.GetValueOrDefault("language") ?? attributes?.GetValueOrDefault("language");
        var applicationLanguage = attributes?.GetValueOrDefault("language");
        if (attributes is null || scriptAttributes is null || !IsServerControl(scriptAttributes)
            || applicationLanguage is not null && !SameVbName(applicationLanguage, "VB")
            || directives.Any(m => SameVbName(m.Groups["kind"].Value, "Import") &&
                string.IsNullOrWhiteSpace(BundleAttributes(m.Groups["attrs"].Value)?.GetValueOrDefault("Namespace")))
            || scriptAttributes.ContainsKey("src") || !string.Equals(language, "VB", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(Regex.Replace(header, ApplicationBundleDirective, "", BundleMarkupOptions, RegexBudget.LoadTolerant))
            || !string.IsNullOrWhiteSpace(markup[(scriptStart + script.Length)..]))
        {
            facts.Add(BundleFact(manifest, path, 1, 1, hash, FactTypes.AnalysisGap, "UnsupportedApplicationBundleSource", null, null));
            return;
        }

        const string prefix = "Class SyntheticApplicationHost\n";
        var body = script.Groups["body"];
        var bodyOffset = scriptStart + body.Index;
        var source = SourceText.From(text);
        AddBundleDeclarations(manifest, path, prefix + body.Value + "\nEnd Class", true, facts,
            source.Lines.GetLineFromPosition(bodyOffset).LineNumber - 1, bodyOffset - prefix.Length, hash);
    }

    private static Dictionary<string, string>? BundleAttributes(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var end = 0;
        foreach (Match match in Regex.Matches(text, AttributeRegex().ToString(), BundleMarkupOptions, RegexBudget.LoadTolerant))
        {
            if (match.Index == end || !text.AsSpan(end, match.Index - end).IsWhiteSpace()) return null;
            var value = match.Groups["dq"].Success ? match.Groups["dq"].Value : match.Groups["sq"].Value;
            if (!result.TryAdd(match.Groups["name"].Value, value)) return null;
            end = match.Index + match.Length;
        }
        return text.AsSpan(end).IsWhiteSpace() ? result : null;
    }

    private static string? CollectLocalBundleIncludes(SyntaxNode initializer, List<SyntaxNode> members)
    {
        var declarator = initializer.Parent?.Parent as VBS.VariableDeclaratorSyntax;
        if (declarator?.Parent is not VBS.LocalDeclarationStatementSyntax declaration) return null;
        if (declaration.Parent is not VBS.MethodBlockSyntax method || declarator.Names.Count != 1
            || declaration.Declarators.Count != 1 || method.ContainsDirectives)
            return "AmbiguousBundleLocal";

        var name = declarator.Names[0].Identifier.ValueText;
        bool SameName(string value) => name.Equals(value, StringComparison.OrdinalIgnoreCase);
        if (method.DescendantNodes().OfType<VBS.ModifiedIdentifierSyntax>().Count(id => SameName(id.Identifier.ValueText)) != 1)
            return "AmbiguousBundleLocal";

        foreach (var use in method.DescendantNodes().OfType<VBS.IdentifierNameSyntax>().Where(id => SameName(id.Identifier.ValueText)))
        {
            if (use.SpanStart < declaration.Span.End) return "AmbiguousBundleLocal";
            var invocation = use.Ancestors().OfType<VBS.InvocationExpressionSyntax>().FirstOrDefault();
            if (invocation?.Parent is not (VBS.ExpressionStatementSyntax or VBS.CallStatementSyntax) || invocation.Parent.Parent != method
                || invocation.Expression is not VBS.MemberAccessExpressionSyntax access)
                return "UnsupportedBundleLocalUse";
            if (access.Expression == use && SameVbName(access.Name.Identifier.ValueText, "Include"))
            {
                var args = BundleArguments(invocation);
                if (args.Length == 0) return "UnsupportedBundleInclude";
                if (members.Count + args.Length > BundleMemberLimit) return "BundleMemberLimit";
                members.AddRange(args);
                continue;
            }

            // Registration is a permitted use of the local, never an execution claim.
            if (invocation.ArgumentList?.Arguments.Count == 1
                && invocation.ArgumentList.Arguments[0] is VBS.SimpleArgumentSyntax { NameColonEquals: null } arg
                && arg.Expression == use && (SameVbName(access.ToString(), "BundleTable.Bundles.Add")
                    || SameVbName(access.ToString(), "bundles.Add"))) continue;
            return "UnsupportedBundleLocalUse";
        }

        return null;
    }

    private static bool SameVbName(string text, string expected) => text.Equals(expected, StringComparison.OrdinalIgnoreCase);

    private const RegexOptions BundleMarkupOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
    private const string ApplicationBundleDirective = @"<%@\s*(?<kind>Application|Import)(?<attrs>(?:\s[^%>]*)?)%>";
    private const string ApplicationBundleScript = @"\A<script(?<attrs>\s[^>]*)>(?<body>.*?)</script\s*>";
}
