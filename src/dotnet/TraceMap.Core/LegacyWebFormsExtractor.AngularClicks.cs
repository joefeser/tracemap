using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace TraceMap.Core;

public static partial class LegacyWebFormsExtractor
{
    private const string AngularLimitations = "Static AngularJS markup and included-source candidates. Framework identity, runtime loading, dynamic registrations, inherited scope values and backend execution are unproven.";
    private const RegexOptions AngularOptions = RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
    private const string AngularIdentifier = @"[A-Za-z_$][A-Za-z0-9_$]*";
    private static readonly Lazy<string> JsParserHash = new(() => Convert.ToHexStringLower(
        SHA256.HashData(GeneratorBinaryIdentity.ReadBytes(typeof(Acornima.Parser).Assembly))));
    private static readonly Lazy<string> MarkupParserHash = new(() => Convert.ToHexStringLower(
        SHA256.HashData(GeneratorBinaryIdentity.ReadBytes(typeof(HtmlParser).Assembly))));

    private static void AddAngularClickFacts(string repo, ScanManifest manifest, IReadOnlyList<FileInventoryItem> inventory,
        IReadOnlyList<WebFormsPage> pages, List<CodeFact> facts, WebFormsIisMount? iisMount)
    {
        var candidates = pages.Where(p => p.MarkupSnapshot is { } text && (text.Length > BundleSourceLimit
            || text.Contains("ng-click", StringComparison.OrdinalIgnoreCase))).ToArray();
        if (candidates.Length == 0) return;
        var state = new AngularClickScan(repo, manifest, inventory, pages, facts, iisMount);
        foreach (var page in candidates) state.Extract(page);
    }

    private sealed class AngularClickScan(string repo, ScanManifest manifest, IReadOnlyList<FileInventoryItem> inventory,
        IReadOnlyList<WebFormsPage> pages, List<CodeFact> facts, WebFormsIisMount? iisMount)
    {
        private readonly ILookup<string, CodeFact> masterLinks = facts.Where(f => f.FactType == FactTypes.WebFormsCompositionDeclared
            && f.Properties.GetValueOrDefault("relationshipKind") == "UsesMasterPage").ToLookup(f => f.Evidence.FilePath, StringComparer.Ordinal);
        private readonly ILookup<string, CodeFact> references = facts.Where(f => f.FactType == FactTypes.WebFormsScriptBundleReferenceCandidate)
            .ToLookup(f => f.Evidence.FilePath, StringComparer.Ordinal);
        private readonly ILookup<string, CodeFact> declarations = facts.Where(f => f.FactType == FactTypes.WebFormsScriptBundleDeclaredCandidate)
            .ToLookup(f => f.TargetSymbol ?? "", StringComparer.Ordinal);
        private readonly ILookup<string, CodeFact> members = facts.Where(f => f.FactType == FactTypes.WebFormsScriptBundleMemberCandidate)
            .ToLookup(f => f.Properties["bundleDeclarationFactId"], StringComparer.Ordinal);
        private readonly CodeFact[] bundleGaps = facts.Where(f => f.RuleId == RuleIds.LegacyWebFormsScriptBundles && f.FactType == FactTypes.AnalysisGap).ToArray();
        private readonly Dictionary<string, AngularJsScript> parsed = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FileInventoryItem> scripts = inventory.Where(i => i.Kind == "JavaScript")
            .ToDictionary(i => i.RelativePath, StringComparer.Ordinal);
        private readonly ILookup<string, string>? iisScripts = iisMount is null ? null : inventory.Where(i => i.Kind == "JavaScript")
            .Select(i => i.RelativePath).ToLookup(path => path, StringComparer.OrdinalIgnoreCase);
        private readonly IGrouping<string, FileInventoryItem>[] roots = inventory.Where(i => Path.GetFileName(i.RelativePath).Equals("Global.asax", StringComparison.OrdinalIgnoreCase))
            .GroupBy(i => DirectoryOf(i.RelativePath), StringComparer.Ordinal).OrderByDescending(group => group.Key.Length).ToArray();
        private readonly Dictionary<string, string> failedScripts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> markupHashes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Microsoft.CodeAnalysis.Text.SourceText> markupSources = new(StringComparer.Ordinal);
        private readonly Dictionary<(string, int), int> renderMarkers = new();
        private long scriptBytes;
        private int surfaceCount;

        internal void Extract(WebFormsPage page)
        {
            try
            {
                markupSources.Clear();
                renderMarkers.Clear();
                if (++surfaceCount > 128) throw new AngularClickGap("AngularSurfaceLimit");
                using var markup = ParsePage(page);
                var clicks = markup.QuerySelectorAll("[ng-click],[data-ng-click]").ToArray();
                if (clicks.Length == 0) return;
                if (clicks.Length > 64) throw new AngularClickGap("AngularClickLimit");

                if (page.Gaps.Any(g => g.GapKind.Contains("MasterPage", StringComparison.Ordinal)))
                    throw new AngularClickGap("AngularMasterUnavailable");
                var master = pages.SingleOrDefault(p => p.FilePath == page.MasterPageFile);
                if (master is not null && Root(master.FilePath) != Root(page.FilePath)) throw new AngularClickGap("AngularApplicationRootMismatch");
                if (master?.MasterPageFile is not null) throw new AngularClickGap("NestedAngularMasterUnavailable");
                using var layout = master is null ? null : ParsePage(master);
                var assets = new SortedDictionary<string, HashSet<string>>(StringComparer.Ordinal);
                var supports = new HashSet<string>(StringComparer.Ordinal);
                var inline = new List<AngularJsScript>();
                if (master is not null)
                {
                    var relationship = masterLinks[page.FilePath].SingleOrDefault(f => f.Properties.GetValueOrDefault("targetFilePath") == master.FilePath);
                    if (relationship is null) throw new AngularClickGap("AngularMasterUnavailable");
                    supports.Add(relationship.FactId);
                }
                var surfaceSupportIds = new HashSet<string>(StringComparer.Ordinal);
                string? assetGap = null;
                try
                {
                    var overrides = markup.All.Where(e => e.LocalName == "asp:content").Select(e => e.GetAttribute("ContentPlaceHolderID"))
                        .OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var hasBase = markup.QuerySelector("base[href]") is not null || layout?.QuerySelector("base[href]") is not null;
                    IncludeSurface(page, markup, page.FilePath, hasBase, null, assets, inline, surfaceSupportIds);
                    if (master is not null && layout is not null) IncludeSurface(master, layout, page.FilePath, hasBase, overrides, assets, inline, surfaceSupportIds);
                    if (assets.Count == 0) throw new AngularClickGap("AngularScriptIncludeUnavailable");
                    foreach (var path in assets.Keys) ReadScript(path);
                }
                catch (AngularClickGap ex) { assetGap = ex.Kind; }

                var sources = assetGap is null ? assets.Keys.Select(path => parsed[path]).Concat(inline).ToArray() : [];
                var bindings = new Dictionary<(string, string, string), AngularJsScript.Handler>();
                foreach (var click in clicks)
                {
                    var position = click.SourceReference?.Position;
                    if (position is null) throw new AngularClickGap("AngularClickLocationUnavailable");
                    var id = $"webforms-angular-click:{FactFactory.Hash($"{page.FilePath}|{position.Value.Index}", 32)}";
                    string? method = null;
                    string? expressionGap = null;
                    try { method = ClickMethod(click); }
                    catch (AngularClickGap ex) { expressionGap = ex.Kind; }
                    var clickProperties = new SortedDictionary<string, string>(StringComparer.Ordinal);
                    if (method is not null) clickProperties["method"] = method;
                    var clickFact = Observe(FactTypes.WebFormsAngularClickCandidate, page.FilePath, position.Value.Line,
                        TagEndLine(page.FilePath, position.Value.Index), page.MarkupSnapshot!, SurfaceIdentity(page.FilePath), id, supports, properties: clickProperties);
                    if (method is null)
                    {
                        Gap(page, position.Value.Line, id, expressionGap!, [clickFact.FactId]);
                        continue;
                    }
                    try
                    {
                        var appCount = BindableApplications(click);
                        var placeholder = layout is null ? null : Placeholder(click, layout);
                        if (placeholder is not null) appCount += BindableApplications(placeholder);
                        if (appCount > 1) throw new AngularClickGap("AmbiguousAngularApplicationScope");
                        var ownerIds = new List<string> { clickFact.FactId };
                        var module = Owner(page, click, "app", ownerIds);
                        var controller = Owner(page, click, "controller", ownerIds);
                        if (placeholder is not null && master is not null && module is null)
                        {
                            module = Owner(master, placeholder, "app", ownerIds);
                            controller ??= Owner(master, placeholder, "controller", ownerIds);
                        }
                        if (string.IsNullOrEmpty(module)) throw new AngularClickGap("AngularModuleScopeUnavailable");
                        if (controller is null || !Regex.IsMatch(controller, "^" + AngularIdentifier + @"\z", AngularOptions, RegexBudget.LoadTolerant))
                            throw new AngularClickGap("AngularControllerScopeUnavailable");
                        if (assetGap is not null) throw new AngularClickGap(assetGap);
                        var key = (module, controller, method);
                        if (!bindings.TryGetValue(key, out var handler))
                            bindings[key] = handler = AngularJsScript.Resolve(sources, module, controller, method);
                        if (!assets.ContainsKey(handler.Method.File) || !assets.ContainsKey(handler.Module.File))
                            throw new AngularClickGap("AngularInlineOwnerUnavailable");
                        var handlerId = $"webforms-angular-method:{FactFactory.Hash($"{handler.Method.File}|{handler.Method.Start}", 32)}";
                        var moduleFact = ScriptOwner(handler.Module, "module", module, assets[handler.Module.File]);
                        var registration = ScriptOwner(handler.Registration, "controller", controller, assets[handler.Registration.File].Append(moduleFact.FactId));
                        var evidence = assets[handler.Method.File].Concat(surfaceSupportIds).Concat(ownerIds).Append(registration.FactId);
                        var type = FactTypes.WebFormsAngularClickHandlerCandidate;
                        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["module"] = module, ["controller"] = controller, ["method"] = method, ["clickFactId"] = clickFact.FactId
                        };
                        var gapSupports = surfaceSupportIds.Append(clickFact.FactId);
                        var reasons = inline.Select(s => s.SettingsScriptGap).OfType<string>();
                        if (handler.ThenCallback is { } site)
                        {
                            var callback = Observe(FactTypes.WebFormsAngularThenCallbackCandidate,
                                site.File, site.Line, site.EndLine, parsed[site.File].Hash, registration.TargetSymbol!,
                                $"angular-then-callback:{FactFactory.Hash($"{site.File}|{site.Start}", 32)}", [registration.FactId],
                                hashProvided: true, properties: new() { ["argumentIndex"] = "0", ["invocationStatus"] = "unobserved" });
                            evidence = evidence.Append(callback.FactId);
                            gapSupports = gapSupports.Append(callback.FactId);
                            reasons = reasons.Append("AngularThenCallbackInvocationUnobserved");
                        }
                        foreach (var reason in reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
                        {
                            var gap = Gap(page, position.Value.Line, id, reason, gapSupports.ToArray());
                            evidence = evidence.Append(gap.FactId);
                            type = FactTypes.WebFormsAngularPartialClickHandlerCandidate;
                            properties["coverageStatus"] = "included-source-only";
                        }
                        Observe(type, handler.Method.File, handler.Method.Line, handler.Method.EndLine,
                            parsed[handler.Method.File].Hash, id, handlerId, evidence, hashProvided: true,
                            properties: properties);
                    }
                    catch (AngularClickGap ex) { Gap(page, position.Value.Line, id, ex.Kind, [clickFact.FactId]); }
                }
            }
            catch (AngularClickGap ex) { Gap(page, page.DirectiveLine, SurfaceIdentity(page.FilePath), ex.Kind, []); }
            catch (HtmlParseException) { Gap(page, page.DirectiveLine, SurfaceIdentity(page.FilePath), "UnsupportedAngularMarkup", []); }
            catch (RegexMatchTimeoutException) { Gap(page, page.DirectiveLine, SurfaceIdentity(page.FilePath), "AngularParsingLimit", []); }
        }

        private static string ClickMethod(IElement click)
        {
            var expression = Directive(click, "click")!;
            if (expression.Length > 256) throw new AngularClickGap("UnsupportedAngularClickExpression");
            var match = Regex.Match(expression,
                @"^\s*(?<method>" + AngularIdentifier + @")\s*\(\s*(?:" + AngularIdentifier + @"(?:\." + AngularIdentifier +
                @")*(?:\s*,\s*" + AngularIdentifier + @"(?:\." + AngularIdentifier + @")*)*)?\s*\)\s*\z", AngularOptions, RegexBudget.LoadTolerant);
            if (!match.Success) throw new AngularClickGap("UnsupportedAngularClickExpression");
            return match.Groups["method"].Value;
        }

        private AngleSharp.Html.Dom.IHtmlDocument ParsePage(WebFormsPage page)
        {
            if (page.MarkupSnapshot is not { Length: <= BundleSourceLimit } text) throw new AngularClickGap("AngularMarkupLimit");
            var rendered = references[page.FilePath]
                .Select(f => int.Parse(f.Properties["sourceOffset"], System.Globalization.CultureInfo.InvariantCulture)).ToHashSet();
            var chars = text.ToCharArray();
            var cursor = 0;
            while ((cursor = text.IndexOf("<%", cursor, StringComparison.Ordinal)) >= 0)
            {
                var terminator = text.AsSpan(cursor).StartsWith("<%--", StringComparison.Ordinal) ? "--%>" : "%>";
                var end = text.IndexOf(terminator, cursor + 2, StringComparison.Ordinal);
                if (end < 0) throw new AngularClickGap("MalformedAngularMarkup");
                end += terminator.Length;
                var run = 0;
                var marker = -1;
                for (var i = cursor; i < end; i++)
                {
                    if (chars[i] is '\r' or '\n') { run = 0; continue; }
                    chars[i] = ' ';
                    if (++run == 6 && marker < 0) marker = i - 5;
                }
                // A marker must survive HTML parsing as an element at that exact offset.
                // Output inside a comment, attribute or raw script is not a script inclusion.
                if (rendered.Contains(cursor) && marker >= 0)
                {
                    "<meta>".CopyTo(0, chars, marker, 6);
                    renderMarkers[(page.FilePath, cursor)] = marker;
                }
                cursor = end;
            }
            var masked = new string(chars);
            markupSources[page.FilePath] = Microsoft.CodeAnalysis.Text.SourceText.From(masked);
            var parser = new HtmlParser(new HtmlParserOptions { IsScripting = false, IsKeepingSourceReferences = true, MaximumDepth = 128 });
            var duplicate = false;
            parser.Error += (_, ev) =>
            {
                if (ev is AngleSharp.Html.Dom.Events.HtmlErrorEvent error && error.Code == (int)HtmlParseError.AttributeDuplicateOmitted)
                    duplicate = true;
            };
            var document = parser.ParseDocument(masked);
            if (duplicate)
            {
                document.Dispose();
                throw new AngularClickGap("AmbiguousAngularMarkupAttribute");
            }
            var pending = new Stack<(IElement Element, int Depth)>();
            if (document.DocumentElement is { } root) pending.Push((root, 1));
            var count = 0;
            while (pending.TryPop(out var item))
            {
                if (++count > 32768 || item.Depth >= 128)
                {
                    document.Dispose();
                    throw new AngularClickGap("AngularMarkupLimit");
                }
                foreach (var child in item.Element.Children) pending.Push((child, item.Depth + 1));
            }
            return document;
        }

        private int TagEndLine(string file, int start) => LineAt(markupSources[file], TagEnd(file, start));

        private int TagEnd(string file, int start)
        {
            var source = markupSources[file];
            var quote = '\0';
            for (var i = start; i < source.Length; i++)
            {
                var ch = source[i];
                if (quote != '\0') { if (ch == quote) quote = '\0'; }
                else if (ch is '\'' or '"') quote = ch;
                else if (ch == '>') return i;
            }
            throw new AngularClickGap("MalformedAngularMarkup");
        }

        private static string? Directive(IElement element, string name)
        {
            var bare = element.GetAttribute("ng-" + name);
            var data = element.GetAttribute("data-ng-" + name);
            if (bare is not null && data is not null) throw new AngularClickGap("AmbiguousAngularDirective");
            return (bare ?? data)?.Trim();
        }

        private CodeFact ScriptOwner(AngularJsScript.Site site, string kind, string name, IEnumerable<string> supports) =>
            Observe(FactTypes.WebFormsAngularOwnerCandidate, site.File, site.Line, site.EndLine, parsed[site.File].Hash, "file:" + site.File,
                $"angular-js-owner:{FactFactory.Hash($"{site.File}|{site.Start}|{kind}", 32)}", supports, hashProvided: true,
                properties: new() { ["ownerKind"] = kind, ["ownerName"] = name });

        private string? Owner(WebFormsPage page, IElement element, string name, List<string> supportingIds)
        {
            for (IElement? current = element; current is not null; current = current.ParentElement)
            {
                if (Directive(current, name) is { } found)
                {
                    var pattern = name == "app" ? @"^[a-zA-Z_$][a-zA-Z0-9_$.-]{0,127}\z" : "^" + AngularIdentifier + @"\z";
                    if (found.Length > 128 || !Regex.IsMatch(found, pattern, AngularOptions, RegexBudget.LoadTolerant))
                        throw new AngularClickGap(name == "app" ? "AngularModuleScopeUnavailable" : "AngularControllerScopeUnavailable");
                    var position = current.SourceReference?.Position ?? throw new AngularClickGap("AngularScopeLocationUnavailable");
                    var owner = Observe(FactTypes.WebFormsAngularOwnerCandidate, page.FilePath, position.Line, TagEndLine(page.FilePath, position.Index),
                        page.MarkupSnapshot!, SurfaceIdentity(page.FilePath), $"angular-markup-owner:{FactFactory.Hash($"{page.FilePath}|{position.Index}|{name}", 32)}",
                        [], properties: new() { ["ownerKind"] = "ng-" + name, ["ownerName"] = found });
                    supportingIds.Add(owner.FactId);
                    return found;
                }
                if (name == "controller" && Directive(current, "app") is not null) break;
            }
            return null;
        }

        private static int BindableApplications(IElement element)
        {
            var apps = 0;
            for (IElement? e = element; e is not null; e = e.ParentElement)
            {
                if (Directive(e, "non-bindable") is not null) throw new AngularClickGap("AngularNonBindable");
                if (Directive(e, "app") is not null) apps++;
            }
            return apps;
        }

        private static IElement Placeholder(IElement element, IDocument layout)
        {
            for (IElement? parent = element; parent is not null; parent = parent.ParentElement)
            {
                if (parent.LocalName != "asp:content" || parent.GetAttribute("ContentPlaceHolderID") is not { Length: > 0 } id) continue;
                var matches = layout.All.Where(e => e.LocalName == "asp:contentplaceholder" && e.GetAttribute("ID") == id).Take(2).ToArray();
                if (matches.Length == 1) return matches[0];
                break;
            }
            throw new AngularClickGap("AngularMasterScopeUnavailable");
        }

        private static string DirectoryOf(string path) => path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
        private string? Root(string path)
        {
            var root = roots.FirstOrDefault(group => group.Key.Length == 0 || path.StartsWith(group.Key + "/", StringComparison.Ordinal));
            if (root is null) return null;
            if (root.Count() != 1) throw new AngularClickGap("AmbiguousAngularApplicationRoot");
            return root.Key;
        }

        private string ResolveMountedScript(string page, string surface, string url, WebFormsIisMount mount)
        {
            var prefix = mount.UrlPrefix == "/" ? "/" : mount.UrlPrefix + "/";
            if (!url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new AngularClickGap("AngularIisMountPrefixMismatch");
            var relative = url[prefix.Length..];
            if (!WebFormsIisMount.SafeRelativePath(relative) || !relative.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                throw new AngularClickGap("UnsupportedAngularScriptPath");

            var root = mount.WebRoot == "." ? "" : mount.WebRoot;
            bool InApplication(string file) =>
                (root.Length == 0 || file.StartsWith(root + "/", StringComparison.Ordinal))
                && (Root(file) is not { } knownRoot || knownRoot == root);
            if (!InApplication(page) || !InApplication(surface))
                throw new AngularClickGap("AngularApplicationRootMismatch");
            var target = root.Length == 0 ? relative : root + "/" + relative;
            var matches = iisScripts![target].Take(2).ToArray();
            if (matches.Length == 0) throw new AngularClickGap("AngularScriptOutsideInventory");
            if (matches.Length > 1) throw new AngularClickGap("AmbiguousAngularMountedScript");
            if (!InApplication(matches[0])) throw new AngularClickGap("AngularApplicationRootMismatch");
            return matches[0];
        }

        private string ResolvePath(string surface, string src, bool bundle = false)
        {
            var rooted = src.StartsWith("~/", StringComparison.Ordinal) || src.StartsWith('/');
            if (rooted && (!bundle || !src.StartsWith("~/", StringComparison.Ordinal))) throw new AngularClickGap("UnsupportedAngularScriptPath");
            var relative = src.StartsWith("~/", StringComparison.Ordinal) ? src[2..] : rooted ? src[1..] : src;
            if (!BundleVirtualPath("~/" + relative) || !relative.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                throw new AngularClickGap("UnsupportedAngularScriptPath");
            var folder = rooted ? Root(surface) ?? throw new AngularClickGap("AngularApplicationRootUnavailable") : DirectoryOf(surface);
            var path = folder.Length == 0 ? relative : folder + "/" + relative;
            if (Root(surface) != Root(path)) throw new AngularClickGap("AngularApplicationRootMismatch");
            if (!scripts.ContainsKey(path)) throw new AngularClickGap("AngularScriptOutsideInventory");
            return path;
        }

        private void IncludeSurface(WebFormsPage page, IDocument markup, string pageUrl, bool hasBase, HashSet<string>? overrides, SortedDictionary<string, HashSet<string>> assets, List<AngularJsScript> inline, HashSet<string> supports)
        {
            void Add(string path, params string[] ids)
            {
                if (!assets.TryGetValue(path, out var set)) assets[path] = set = new(StringComparer.Ordinal);
                set.UnionWith(ids);
                if (assets.Count > 32) throw new AngularClickGap("AngularIncludedScriptLimit");
            }
            bool Visible(IElement element)
            {
                for (var e = element; e is not null; e = e.ParentElement)
                {
                    if (e.LocalName is "template" or "noscript") return false;
                    if (e.LocalName == "asp:contentplaceholder" && e.GetAttribute("id") is { } id && overrides?.Contains(id) == true) return false;
                }
                return true;
            }
            foreach (var script in markup.QuerySelectorAll("script").Where(Visible))
            {
                if (string.Equals(script.GetAttribute("runat"), "server", StringComparison.OrdinalIgnoreCase)) continue;
                var type = script.GetAttribute("type")?.Trim().ToLowerInvariant();
                if (type == "module") throw new AngularClickGap("UnsupportedAngularModuleScript");
                if (type is not (null or "" or "text/javascript" or "application/javascript")) continue;
                if (!script.HasAttribute("src"))
                {
                    if (script.TextContent.Length == 0) continue;
                    var offset = script.SourceReference?.Position.Index ?? throw new AngularClickGap("AngularScriptLocationUnavailable");
                    var source = ParseInlineScript(page, offset, script.TextContent, supports, inline.Count);
                    if (source is not null) inline.Add(source);
                    continue;
                }
                if (hasBase) throw new AngularClickGap("AngularScriptBaseUnavailable");
                var src = script.GetAttribute("src")!;
                var mounted = src.StartsWith('/') && !src.StartsWith("//", StringComparison.Ordinal) && iisMount is not null;
                if (mounted)
                {
                    var start = script.SourceReference?.Position.Index ?? throw new AngularClickGap("AngularScriptLocationUnavailable");
                    var length = TagEnd(page.FilePath, start) - start + 1;
                    if (page.MarkupSnapshot!.AsSpan(start, length).Contains("<%", StringComparison.Ordinal))
                        throw new AngularClickGap("UnsupportedAngularScriptPath");
                }
                var path = mounted ? ResolveMountedScript(pageUrl, page.FilePath, src, iisMount!) : ResolvePath(pageUrl, src);
                var line = script.SourceReference?.Position.Line ?? throw new AngularClickGap("AngularScriptLocationUnavailable");
                var include = Observe(FactTypes.WebFormsExternalScriptReferenceCandidate, page.FilePath, line, TagEndLine(page.FilePath, script.SourceReference!.Position.Index), page.MarkupSnapshot!,
                    SurfaceIdentity(page.FilePath), "file:" + path, [], properties: mounted ? new()
                    {
                        ["resolutionSource"] = "operator-iis-mount", ["mountPrefix"] = iisMount!.UrlPrefix,
                        ["repositoryWebRoot"] = iisMount.WebRoot, ["scriptUrl"] = src
                    } : null);
                if (mounted) supports.Add(include.FactId);
                Add(path, include.FactId);
            }
            if (bundleGaps.Any(f => f.Evidence.FilePath == page.FilePath))
                throw new AngularClickGap("AngularBundleRenderIncomplete");
            var activeMarkers = markup.QuerySelectorAll("meta").Where(Visible).Select(e => e.SourceReference?.Position.Index).ToHashSet();
            foreach (var reference in references[page.FilePath])
            {
                var offset = int.Parse(reference.Properties["sourceOffset"], System.Globalization.CultureInfo.InvariantCulture);
                if (!renderMarkers.TryGetValue((page.FilePath, offset), out var marker) || !activeMarkers.Contains(marker)) continue;
                var root = Root(page.FilePath) ?? throw new AngularClickGap("AngularApplicationRootUnavailable");
                if (bundleGaps.Any(f => Root(f.Evidence.FilePath) == root && (f.SourceSymbol?.StartsWith("webforms-bundle-declaration:", StringComparison.Ordinal) == true
                    || Path.GetExtension(f.Evidence.FilePath).Equals(".asax", StringComparison.OrdinalIgnoreCase))))
                    throw new AngularClickGap("AngularBundleScopeIncomplete");
                var matches = declarations[reference.TargetSymbol ?? ""].Where(f => Root(f.Evidence.FilePath) == root).Take(2).ToArray();
                if (matches.Length != 1) throw new AngularClickGap(matches.Length == 0 ? "AngularBundleUnavailable" : "AmbiguousAngularBundle");
                var declaration = matches[0];
                foreach (var member in members[declaration.FactId])
                    Add(ResolvePath(declaration.Evidence.FilePath, member.TargetSymbol!, bundle: true), reference.FactId, declaration.FactId, member.FactId);
            }
        }

        // HTML parsing operates on masked markup. Reconcile the DOM script text with
        // its physical source before admitting server output into a JS initializer.
        private AngularJsScript? ParseInlineScript(WebFormsPage page, int offset, string body, HashSet<string> supports, int inlineCount)
        {
            var id = $"inline:{page.FilePath}#{offset}";
            var start = TagEnd(page.FilePath, offset) + 1;
            var masked = markupSources[page.FilePath];
            var end = start;
            foreach (var ch in body)
            {
                if (end >= masked.Length) throw new AngularClickGap("AngularScriptLocationUnavailable");
                var actual = masked[end++];
                if (ch == '\n' && actual == '\r')
                {
                    if (end < masked.Length && masked[end] == '\n') end++;
                    continue;
                }
                if (ch != actual) throw new AngularClickGap("AngularScriptLocationUnavailable");
            }

            var raw = page.MarkupSnapshot![start..end];
            var cursor = raw.IndexOf("<%", StringComparison.Ordinal);
            if (cursor < 0 && string.IsNullOrWhiteSpace(body)) return null;
            if (inlineCount >= 32) throw new AngularClickGap("AngularIncludedScriptLimit");
            if (cursor < 0) return ParseScript(id, body);
            var projection = masked.ToString(new Microsoft.CodeAnalysis.Text.TextSpan(start, end - start)).ToCharArray();
            var positions = new List<int>();
            var settings = new List<(int Start, int End)>();
            while (cursor >= 0)
            {
                var comment = raw.AsSpan(cursor).StartsWith("<%--", StringComparison.Ordinal);
                var terminator = comment ? "--%>" : "%>";
                var close = raw.IndexOf(terminator, cursor + 2, StringComparison.Ordinal);
                if (close < 0) throw new AngularClickGap("UnsupportedAngularServerInitializer");
                if (!comment)
                {
                    var lineStart = raw.LastIndexOfAny(['\r', '\n'], cursor) + 1;
                    var lineEnd = raw.IndexOfAny(['\r', '\n'], close);
                    if (lineEnd < 0) lineEnd = raw.Length;
                    if (positions.Count + settings.Count >= 64) throw new AngularClickGap("AngularServerInitializerLimit");
                    var match = InlineInitializer.Match(raw[lineStart..lineEnd]);
                    if (match.Success && lineStart + match.Groups["value"].Index == cursor)
                    {
                        projection[cursor] = '0';
                        positions.Add(cursor);
                    }
                    else if (InlineAppSetting.IsMatch(raw[cursor..(close + terminator.Length)]))
                        settings.Add((cursor, close + terminator.Length));
                    else throw new AngularClickGap("UnsupportedAngularServerInitializer");
                }
                cursor = raw.IndexOf("<%", close + terminator.Length, StringComparison.Ordinal);
            }
            var script = ParseScript(id, new string(projection), positions, settings);
            foreach (var position in positions)
            {
                var line = LineAt(masked, start + position);
                var fact = Observe(FactTypes.WebFormsInlineServerInitializerCandidate, page.FilePath, line, line,
                    page.MarkupSnapshot!, SurfaceIdentity(page.FilePath), $"webforms-server-initializer:{page.FilePath}#{start + position}", [],
                    properties: new() { ["valueStatus"] = "unknown" });
                supports.Add(fact.FactId);
            }
            foreach (var setting in settings)
            {
                var line = LineAt(masked, start + setting.Start);
                var fact = Observe(FactTypes.WebFormsInlineServerStringCandidate, page.FilePath, line, line,
                    page.MarkupSnapshot!, SurfaceIdentity(page.FilePath), $"webforms-server-string:{page.FilePath}#{start + setting.Start}", [],
                    properties: new() { ["valueStatus"] = "unknown", ["expressionKind"] = "app-setting" });
                supports.Add(fact.FactId);
            }
            return script;
        }

        private static readonly Regex InlineInitializer = new(
            @"^[ \t]*(?:var|const|let)[ \t]+" + AngularIdentifier +
            @"[ \t]*=[ \t]*(?<value><%=[ \t]*(?:[0-9]+|[A-Za-z_][A-Za-z_0-9]*(?:\.[A-Za-z_][A-Za-z_0-9]*)*)[ \t]*%>)[ \t]*;[ \t]*\z",
            AngularOptions, RegexBudget.LoadTolerant);

        private static readonly Regex InlineAppSetting = new(
            @"^<%=[ \t]*(?:(?:[A-Za-z_][A-Za-z_0-9]*\.){0,8}AppSettings)[ \t]*\([ \t]*""[A-Za-z0-9_.:-]{1,128}""[ \t]*\)[ \t]*%>\z",
            AngularOptions | RegexOptions.IgnoreCase, RegexBudget.LoadTolerant);

        private void ReadScript(string path)
        {
            if (failedScripts.TryGetValue(path, out var reason)) throw new AngularClickGap(reason);
            try { LoadScript(path); }
            catch (AngularClickGap ex) { failedScripts[path] = ex.Kind; throw; }
        }

        private void LoadScript(string path)
        {
            if (parsed.ContainsKey(path)) return;
            if (parsed.Count + failedScripts.Count >= 128 || scriptBytes + scripts[path].SizeBytes > 8 * 1024 * 1024) throw new AngularClickGap("AngularScanScriptLimit");
            if (scripts[path].SizeBytes > BundleSourceLimit) throw new AngularClickGap("AngularScriptSizeLimit");
            try
            {
                using var input = File.OpenRead(Path.Combine(repo, path));
                if (input.Length > BundleSourceLimit) throw new AngularClickGap("AngularScriptSizeLimit");
                var size = checked((int)input.Length);
                scriptBytes += size;
                if (scriptBytes > 8 * 1024 * 1024) throw new AngularClickGap("AngularScanScriptLimit");
                var bytes = new byte[size];
                input.ReadExactly(bytes);
                if (input.ReadByte() != -1) throw new AngularClickGap("AngularScriptChanged");
                using var reader = new StreamReader(new MemoryStream(bytes), new System.Text.UTF8Encoding(false, true));
                parsed[path] = ParseScript(path, reader.ReadToEnd());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.DecoderFallbackException)
            { throw new AngularClickGap("AngularScriptUnavailable"); }
        }

        private static AngularJsScript ParseScript(string path, string text, IReadOnlyList<int>? unknownInitializers = null,
            IReadOnlyList<(int Start, int End)>? settings = null)
        {
            try { return new AngularJsScript(path, text, unknownInitializers, settings); }
            catch (Acornima.ParseErrorException) { throw new AngularClickGap("MalformedAngularScript"); }
            catch (InsufficientExecutionStackException) { throw new AngularClickGap("AngularScriptDepthLimit"); }
        }

        private CodeFact Gap(WebFormsPage page, int line, string source, string reason, string[] supports) =>
            Observe(FactTypes.AnalysisGap, page.FilePath, line, line, page.MarkupSnapshot is { Length: <= BundleSourceLimit } text ? text : "", source, "unresolved", supports,
                properties: new() { ["gapKind"] = reason, ["message"] = reason });

        private CodeFact Observe(string type, string file, int line, int endLine, string text, string source, string target,
            IEnumerable<string> support, bool hashProvided = false, SortedDictionary<string, string>? properties = null)
        {
            var ids = support.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var p = new SortedDictionary<string, string>(properties ?? new(), StringComparer.Ordinal);
            p["sourceSymbolId"] = source;
            p["targetSymbolId"] = target;
            p["supportingFactIds"] = string.Join(",", ids);
            p["ruleLimitations"] = AngularLimitations;
            p["generatorSha256"] = EntryGeneratorSha256.Value;
            p["javascriptParserSha256"] = JsParserHash.Value;
            p["markupParserSha256"] = MarkupParserHash.Value;
            string? hash = hashProvided ? text : null;
            if (!hashProvided && text.Length > 0 && !markupHashes.TryGetValue(file, out hash))
                markupHashes[file] = hash = FactFactory.Hash(text, 64);
            var evidence = new EvidenceSpan(file, line, endLine, hash,
                "LegacyWebFormsExtractor", ScannerVersions.LegacyWebFormsExtractor);
            p["boundedInputSha256"] = FactFactory.Hash(JsonSerializer.Serialize(new { manifest.CommitSha, file, line, endLine, type, source, target, inputs = p.ToArray() }), 64);
            var fact = FactFactory.Create(manifest, type, RuleIds.LegacyWebFormsAngularClick,
                type == FactTypes.AnalysisGap ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier3SyntaxOrTextual,
                evidence, sourceSymbol: source, targetSymbol: target, properties: p);
            facts.Add(fact);
            return fact;
        }
    }
}
