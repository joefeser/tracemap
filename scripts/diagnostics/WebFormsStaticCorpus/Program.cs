using TraceMap.Core;

// Read-only syntax inventory. Never call ScanEngine/MSBuild/restore/project load
// on these inputs. Only this independently authored diagnostic is compiled.
if (args.Length != 2) throw new ArgumentException("Usage: <repository-root> <expected-commit>");
var root = Path.GetFullPath(args[0]);
var git = GitMetadataProvider.Detect(root);
if (git.CommitSha != args[1]) throw new InvalidOperationException("Pinned commit mismatch");
var manifest = new ScanManifest("static-public-corpus", Path.GetFileName(root), null, null,
    git.CommitSha, ScannerVersions.TraceMap, DateTimeOffset.UnixEpoch,
    "Level3SyntaxAnalysisReduced", "NotAttempted", [], [], [],
    ["Syntax-only public static check; no semantic build or framework override proof"]);
var inventory = FileInventory.Collect(root);
var syntax = CSharpSyntaxExtractor.Extract(root, manifest, inventory);
var facts = LegacyWebFormsExtractor.Extract(root, manifest, inventory, syntax);
Console.WriteLine($"Commit: {git.CommitSha}; files: {inventory.Count}; syntax facts: {syntax.Count}; WebForms facts: {facts.Count}");
Console.WriteLine("Coverage: reduced syntax-only; build not attempted; no runtime execution or HTTP requests");
foreach (var group in facts.GroupBy(f => f.FactType).OrderBy(g => g.Key, StringComparer.Ordinal))
    Console.WriteLine($"{group.Key}: {group.Count()}");
foreach (var group in facts.Where(f => f.FactType == FactTypes.WebFormsEntryRelationship)
    .GroupBy(f => f.ContractElement).OrderBy(g => g.Key, StringComparer.Ordinal))
    Console.WriteLine($"Entry {group.Key}: {group.Count()}");
