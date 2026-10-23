using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TraceMap.Core;

public sealed record TargetFrameworkInfo(string ProjectPath, string TargetFramework, int Line);

public sealed record PackageReferenceInfo(
    string ProjectPath,
    string PackageName,
    string? Version,
    int Line,
    string ManifestKind,
    string DependencyGroup,
    string DependencyScope,
    string? TargetFramework,
    string? VersionOverride = null,
    int? EndLine = null); // span end when the value rides a child element on a later line

public sealed record NuGetLockfileEntry(
    string LockfilePath,
    string PackageName,
    string? ResolvedVersion,
    string? ResolvedVersionHash,
    string DependencyRelation,
    string TargetFramework,
    int Line,
    int DependencyCount,
    string? DependencyNames,
    string LockfileHash);

public sealed record NuGetLockfileGap(string LockfilePath, string Category, string Message, int Line);

public sealed record NuGetLockfileReadResult(
    IReadOnlyList<NuGetLockfileEntry> Entries,
    IReadOnlyList<NuGetLockfileGap> Gaps);

/// A package this repo PRODUCES (a packable project): its identity resolved through the MSBuild default
/// chain PackageId <- AssemblyName <- MSBuildProjectName, with its Version/PackageVersion property.
/// Line is the evidence span END (identity, required signals and version). Declaration evidence from the project
/// file only — no build, no restore, no publish proof (that remains producer-evidence.v0's
/// publicationStatus sidecar). ExplicitPackageId is true only for a LITERAL PackageId declaration;
/// PackageIdSource names the chain link that supplied the id and PackageIdProvenance how that link's
/// value was obtained, so a derived default can never be presented as a declaration.
public sealed record ProducedPackageInfo(
    string ProjectPath,
    string PackageId,
    string? Version,
    bool ExplicitPackageId,
    int Line,
    int SpanStart,
    string PackageIdSource,
    string PackageIdProvenance,
    string? PackSignals,
    IReadOnlyList<string> PackDependencies,
    int UnresolvedPackDependencyCount,
    IReadOnlyList<PackDependencyEvidence> DependencyEvidence,
    int OmittedReferenceCount, int ReferenceLimitLine);

public sealed record ProducedPackageReadGap(string ProjectPath, string Kind);

// Includes stay hashed: arbitrary paths, tokens and private source text never enter facts.
public sealed record PackDependencyEvidence(
    int Line, string IncludeHash, string? TargetProjectPath, string? PackageId,
    string? PackageIdSource, string? PackageIdProvenance, int? TargetStartLine, int? TargetEndLine);

/// Which link of the MSBuild default chain supplied a produced package id.
public static class PackageIdSources
{
    public const string PackageId = "PackageId";
    public const string AssemblyName = "AssemblyName";
    public const string MSBuildProjectName = "MSBuildProjectName";
}

/// How the value of that link was obtained. The three tiers are deliberately distinct fact
/// properties: a consumer must be able to weigh a derived default below an interpolated value
/// and both below a literal declaration.
public static class PackageIdProvenances
{
    /// A literal property value read from the project file.
    public const string Declared = "declared";
    /// A declared value whose only token was $(MSBuildProjectName), evaluated from the file's own name.
    public const string Interpolated = "interpolated";
    /// No identity property at all: the SDK default chain ends at the project file name.
    public const string DerivedDefault = "derived-default";
}

/// A central NuGet version pin declared in an MSBuild props file (central package management):
/// a PackageVersion item in Directory.Packages.props (or equivalent props file). The pin is a
/// declaration in ITS OWN file; effective-version resolution (override > central > project) is a
/// consumer-side derivation and is never asserted here.
public sealed record CentralPackageVersionInfo(
    string PropsPath,
    string PackageName,
    string Version,
    int Line,
    int EndLine); // span covers the item opening through the value element when they differ

public static class ProjectFileReader
{
    public const int MaxProducerProjectBytes = 1024 * 1024;
    public const int MaxProducerReferences = 256;
    public static IReadOnlyList<TargetFrameworkInfo> ReadTargetFrameworks(string repoPath, IEnumerable<FileInventoryItem> inventory)
    {
        var results = new List<TargetFrameworkInfo>();
        foreach (var project in inventory.Where(item => item.Kind is "Project" or "VisualBasicProject"))
        {
            var fullPath = Path.Combine(repoPath, project.RelativePath);
            foreach (var item in ReadProjectValues(fullPath, "TargetFramework", "TargetFrameworks"))
            {
                foreach (var target in item.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    results.Add(new TargetFrameworkInfo(project.RelativePath, target, item.Line));
                }
            }
        }

        return results
            .OrderBy(item => item.ProjectPath, StringComparer.Ordinal)
            .ThenBy(item => item.TargetFramework, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<PackageReferenceInfo> ReadPackageReferences(string repoPath, IEnumerable<FileInventoryItem> inventory)
    {
        var results = new List<PackageReferenceInfo>();
        foreach (var project in inventory.Where(item => item.Kind is "Project" or "VisualBasicProject"))
        {
            var fullPath = Path.Combine(repoPath, project.RelativePath);
            results.AddRange(ReadPackageReferencesFromProject(
                fullPath,
                project.RelativePath,
                project.Kind == "VisualBasicProject" ? "vbproj" : "csproj"));
        }

        foreach (var packagesConfig in inventory.Where(item => item.Kind == "PackagesConfig"))
        {
            var fullPath = Path.Combine(repoPath, packagesConfig.RelativePath);
            results.AddRange(ReadPackagesConfig(fullPath, packagesConfig.RelativePath));
        }

        return results
            .OrderBy(item => item.ProjectPath, StringComparer.Ordinal)
            .ThenBy(item => item.PackageName, StringComparer.Ordinal)
            .ThenBy(item => item.Version, StringComparer.Ordinal)
            .ToArray();
    }

    /// Reads packages this repo produces from packable projects. Evidence-only rules:
    /// - PROPERTY values only (PropertyGroup scope) — ItemGroup children never count;
    /// - IsPackable: LAST property value wins (MSBuild semantics); explicitly false => never emits;
    /// - identity follows the MSBuild default chain, each link a distinct provenance tier:
    ///   PackageId (declared) > AssemblyName for SDK-STYLE projects only (declared; legacy non-SDK
    ///   projects would invent packages from every AssemblyName — their ids are build artifacts,
    ///   not package declarations) > MSBuildProjectName, the project file name, for SDK-style
    ///   projects that declare no identity but carry pack signals (Authors, Description, Product,
    ///   PackageTags, or a literal IsPackable=true) — derived-default, never promoted to declared;
    /// - $(MSBuildProjectName) is the ONE token evaluated inside PackageId/AssemblyName (the file
    ///   defines it — interpolated); any other property, item, metadata or property-function token
    ///   is unevidenced and the project is SKIPPED: a declared-but-unresolvable identity is never
    ///   replaced by a default further down the chain;
    /// - a Microsoft.NET.Test.Sdk reference defaults IsPackable to false (a known framework
    ///   pattern), so the derived-default tier needs a literal IsPackable=true to apply there;
    /// - PackageVersion outranks Version (NuGet pack semantics: Version is the fallback); every property is last-wins;
    /// - unsafe ids skip the project; unsafe versions emit as unevidenced (null), never projected;
    /// - the evidence span covers identity line through the version line when they differ; a
    ///   derived default has no identity line, so its span covers the pack signals instead;
    /// - NuGet pack turns each ProjectReference (unless PrivateAssets=all) into a dependency on the
    ///   referenced project's package id: references resolving to a produced package above are
    ///   recorded as pack dependencies, anything else (templated/wildcard path, outside the repo,
    ///   not a produced package) retains attributable unresolved evidence. ProjectReferences are never package
    ///   references — in-repo consumption is not PackageReferenced evidence.
    public static IReadOnlyList<ProducedPackageInfo> ReadProducedPackages(string repoPath, IEnumerable<FileInventoryItem> inventory, ICollection<ProducedPackageReadGap>? gaps = null)
    {
        var candidates = new List<ProducedPackageCandidate>();
        foreach (var project in inventory.Where(item => item.Kind is "Project" or "VisualBasicProject"))
        {
            var fullPath = Path.Combine(repoPath, project.RelativePath);
            if (!TryLoadXml(fullPath, out var document, MaxProducerProjectBytes))
            {
                gaps?.Add(new(project.RelativePath, "package-producer-project-unreadable-or-limit"));
                continue;
            }

            if (ReadProducedPackageCandidate(project.RelativePath, document) is { } candidate)
            {
                candidates.Add(candidate);
            }
        }

        // Pack dependencies resolve only against identities proven above: the referenced project's
        // package id is whatever ITS tier established; every declaration retains its own provenance.
        var comparer = CSharpSemanticExtractor.CreateSourcePathComparer(repoPath);
        var candidatesByPath = candidates
            .GroupBy(candidate => candidate.ProjectPath, comparer)
            .ToDictionary(group => group.Key, group => group.First(), comparer);
        var results = new List<ProducedPackageInfo>();
        foreach (var candidate in candidates)
        {
            var dependencies = new SortedSet<string>(StringComparer.Ordinal);
            var unresolved = 0;
            var evidence = new List<PackDependencyEvidence>();
            foreach (var reference in candidate.ProjectReferences)
            {
                if (reference.Path is not null
                    && !comparer.Equals(reference.Path, candidate.ProjectPath)
                    && candidatesByPath.TryGetValue(reference.Path, out var target))
                {
                    dependencies.Add(target.PackageId);
                    evidence.Add(new(reference.Line, reference.IncludeHash, target.ProjectPath, target.PackageId,
                        target.PackageIdSource, target.PackageIdProvenance, target.SpanStart, target.Line));
                }
                else
                {
                    unresolved++;
                    evidence.Add(new(reference.Line, reference.IncludeHash, null, null, null, null, null, null));
                }
            }

            results.Add(new ProducedPackageInfo(
                candidate.ProjectPath,
                candidate.PackageId,
                candidate.Version,
                candidate.ExplicitPackageId,
                candidate.Line,
                candidate.SpanStart,
                candidate.PackageIdSource,
                candidate.PackageIdProvenance,
                candidate.PackSignals,
                dependencies.ToArray(),
                unresolved, evidence, candidate.OmittedReferenceCount, candidate.ReferenceLimitLine));
        }

        return results
            .OrderBy(item => item.ProjectPath, StringComparer.Ordinal)
            .ThenBy(item => item.PackageId, StringComparer.Ordinal)
            .ToArray();
    }

    // Bounded additive supporting evidence; preserve declaration order, including same-ID targets.
    internal static (string Json, int Omitted) ProjectDependencyEvidence(ProducedPackageInfo produced)
    {
        const int maxCharacters = 4096;
        var rows = new List<string>();
        var length = 2; // brackets
        foreach (var edge in produced.DependencyEvidence)
        {
            var row = JsonSerializer.Serialize(edge);
            var added = row.Length + (rows.Count == 0 ? 0 : 1);
            if (length + added > maxCharacters) break;
            rows.Add(row);
            length += added;
        }
        return ("[" + string.Join(',', rows) + "]", produced.DependencyEvidence.Count - rows.Count);
    }

    internal static bool HasDependencyProjectionGap(ProducedPackageInfo produced) =>
        produced.UnresolvedPackDependencyCount > 0
        || produced.OmittedReferenceCount > 0
        || string.Join(',', produced.PackDependencies).Length > 256
        || ProjectDependencyEvidence(produced).Omitted > 0;

    private sealed record ProducedPackageCandidate(
        string ProjectPath,
        string PackageId,
        string? Version,
        bool ExplicitPackageId,
        int Line,
        int SpanStart,
        string PackageIdSource,
        string PackageIdProvenance,
        string? PackSignals,
        IReadOnlyList<ProducedProjectReference> ProjectReferences,
        int OmittedReferenceCount, int ReferenceLimitLine);

    private sealed record ProducedProjectReference(string? Path, int Line, string IncludeHash);

    private static readonly string[] PackSignalProperties = ["Authors", "Description", "PackageTags", "Product"];

    private static bool IsSdkStyle(XDocument document) =>
        !string.IsNullOrWhiteSpace(document.Root?.Attribute("Sdk")?.Value)
        || document.Root?.Elements().Any(element =>
            element.Name.LocalName == "Sdk" && !string.IsNullOrWhiteSpace(element.Attribute("Name")?.Value)
            || element.Name.LocalName == "Import" && !string.IsNullOrWhiteSpace(element.Attribute("Sdk")?.Value)
            || element.Name.LocalName == "ImportGroup" && element.Elements().Any(import =>
                import.Name.LocalName == "Import" && !string.IsNullOrWhiteSpace(import.Attribute("Sdk")?.Value))) == true;

    /// Decisions for <see cref="ReadCiDerivedIdentity"/>. Only <see cref="Derived"/> yields an id;
    /// the "not a producer" decisions are expected non-packages, the rest are unevidenced identities.
    internal static class CiDerivedIdentityDecisions
    {
        public const string Derived = "derived";
        public const string DeclaredId = "declared-id";
        public const string NotSdkStyle = "not-sdk-style";
        public const string NotPackable = "not-packable";
        public const string TestProject = "test-project";
        public const string DeclaresIdentity = "declares-identity";
        public const string InheritedIdentity = "inherited-identity";
        public const string Unreadable = "unreadable-or-limit";
        public const string UnsafeFileName = "unsafe-filename";
        public const string CustomImports = "custom-imports";
        public const string UnprovablePackable = "unprovable-packable";
        public const string ConditionalTestSdk = "conditional-test-sdk";

        // A Condition anywhere on the path (element, its group, or a Choose/When ancestor) makes an
        // assignment unprovable in the bounded view — never definitive (review: conditioned inherited
        // IsPackable skipped producers; conditioned central Test.Sdk silently dropped them; Codex:
        // Choose/When ancestors carry Conditions too).
        internal static bool IsConditioned(XElement element) =>
            element.Attribute("Condition") is not null
            || element.Ancestors().Any(ancestor => ancestor.Attribute("Condition") is not null
                // an Otherwise branch fires only when no When matched — conditional without a Condition (review round-7)
                || string.Equals(ancestor.Name.LocalName, "Otherwise", StringComparison.Ordinal));

        public static bool IsNonProducer(string decision) =>
            decision is NotSdkStyle or NotPackable or TestProject;
    }

    internal sealed record CiDerivedIdentity(string Decision, string? PackageId, string? IdSource = null);

    /// Identity follows three-phase MSBuild precedence (Directory.Build.props -> project body ->
    /// Directory.Build.targets, per-property last-wins): the winning PackageId/AssemblyName is the
    /// packed id, bounded-evaluated ($(MSBuildProjectName) only). The filename default
    /// (MSBuildProjectName) applies only when no phase assigns identity. Never guessed: Conditioned
    /// or Target-scoped winning assignments, winners from import-bearing Directory.Build files,
    /// unreadable/linked files, conditioned or non-literal IsPackable, and Microsoft.NET.Test.Sdk
    /// projects (directly or via Directory.Build; conditioned references gap) without a literal
    /// IsPackable=true all yield typed unevidenced decisions — a gap, never a wrong id.
    internal static CiDerivedIdentity ReadCiDerivedIdentity(string repoPath, string relativePath)
    {
        var root = Path.GetFullPath(repoPath);
        var projectName = Path.GetFileNameWithoutExtension(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!IsUnderRoot(fullPath, root) || !TryLoadXml(fullPath, out var document, MaxProducerProjectBytes))
            return new(CiDerivedIdentityDecisions.Unreadable, null);
        if (!IsSdkStyle(document)) return new(CiDerivedIdentityDecisions.NotSdkStyle, null);
        // A project-level Import can set PackageId/AssemblyName/IsPackable after the project body; the
        // bounded view cannot evaluate it, so the filename default is not provable — an unevidenced
        // decision, never a guessed id (review P1: custom imports). ANY Import with a Project attribute
        // refuses: a textual .props/.targets suffix check is bypassed by $(...) expression imports.
        foreach (var import in document.Descendants().Where(e => e.Name.LocalName == "Import"))
        {
            if (AttributeValue(import, "Project") is not null)
                return new(CiDerivedIdentityDecisions.CustomImports, null);
        }

        bool? packable = null; var packableLiteralTrue = false; var packableUnprovable = false;
        string? packageIdValue = null; string? assemblyNameValue = null; // last-wins raw values — the CI chain evaluates them below
        var packageIdConditioned = false; var assemblyNameConditioned = false; // last-wins Condition state (review: a conditioned identity branch is never a proven id)
        var inherited = InspectNearestDirectoryBuildFiles(root, Path.GetDirectoryName(fullPath));
        if (inherited.Unreadable) return new(CiDerivedIdentityDecisions.InheritedIdentity, null);
        if (inherited.Packable is { } inheritedPackable)
        {
            (packable, packableLiteralTrue) = (inheritedPackable, inherited.PackableLiteralTrue);
            packableUnprovable = inherited.PackableUnprovable;
        }
        else packableUnprovable |= inherited.PackableUnprovable; // a conditioned inherited IsPackable proves nothing either way (review)
        var testSdkReferenced = inherited.TestSdkReferenced; // a centralized Directory.Build reference makes every project a test project
        var conditionalTestSdkReferenced = inherited.ConditionalTestSdk; // conditioned reference: unprovable, never a silent skip (review)
        foreach (var element in document.Descendants())
        {
            if (element.Parent is { Name.LocalName: "PropertyGroup" })
            {
                switch (element.Name.LocalName.ToUpperInvariant())
                {
                    case "PACKAGEID": packageIdValue = element.Value.Trim(); packageIdConditioned = CiDerivedIdentityDecisions.IsConditioned(element) || element.Ancestors().Any(a => a.Name.LocalName == "Target"); break;
                    case "ASSEMBLYNAME": assemblyNameValue = element.Value.Trim(); assemblyNameConditioned = CiDerivedIdentityDecisions.IsConditioned(element) || element.Ancestors().Any(a => a.Name.LocalName == "Target"); break;
                    case "ISPACKABLE":
                        var value = element.Value.Trim();
                        var literalFalse = value.Equals("false", StringComparison.OrdinalIgnoreCase);
                        var literalTrue = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        var conditioned = CiDerivedIdentityDecisions.IsConditioned(element)
                            || element.Ancestors().Any(a => a.Name.LocalName == "Target"); // target-scoped: runs-when-run (review)
                        packable = !literalFalse; // provisional; an unresolved expression, Condition, or Target scope is refused below
                        packableLiteralTrue = literalTrue && !conditioned; // a Conditioned literal true is not provable
                        packableUnprovable = conditioned || (!literalFalse && !literalTrue); // $(PackEnabled), Condition=..., Target-scoped: never guessed (review P1 + round-5)
                        break;
                }
            }
            else if (element.Parent is { Name.LocalName: "ItemGroup" }
                && element.Name.LocalName == "PackageReference"
                && string.Equals(AttributeValue(element, "Include"), "Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase))
            {
                if (CiDerivedIdentityDecisions.IsConditioned(element)) conditionalTestSdkReferenced = true;
                else testSdkReferenced = true;
            }
        }
        // Directory.Build.targets is imported AFTER the project body: its identity/packability wins.
        if (inherited.TargetsPackable is { } targetsPackable)
        {
            (packable, packableLiteralTrue) = (targetsPackable, inherited.TargetsPackableLiteralTrue);
            packableUnprovable = inherited.TargetsPackableUnprovable;
        }
        else packableUnprovable |= inherited.TargetsPackableUnprovable; // conditioned targets-side IsPackable: unprovable (review)
        if (packableUnprovable) return new(CiDerivedIdentityDecisions.UnprovablePackable, null); // BEFORE NotPackable: a conditioned false is unprovable, never a silent skip (review round-6)
        if (packable == false) return new(CiDerivedIdentityDecisions.NotPackable, null);
        if ((testSdkReferenced || conditionalTestSdkReferenced) && !packableLiteralTrue)
            // unconditional reference = test project (silent non-producer by design); a CONDITIONED
            // reference is unprovable either way — a typed gap, never a silent drop (review round-5)
            return new(testSdkReferenced ? CiDerivedIdentityDecisions.TestProject : CiDerivedIdentityDecisions.ConditionalTestSdk, null);
        // FULL MSBuild chain with three-phase PRECEDENCE (props -> body -> targets, per-property
        // last-wins): a props-side PackageId IS the id over a body AssemblyName (PackageId is the
        // chain head wherever it is assigned); a body PackageId overrides a props AssemblyName;
        // a targets-side assignment (imported AFTER the body) overrides both — precise, replacing
        // round-7's conservative mixed-refusal. Bounded evaluation only: $(MSBuildProjectName)
        // resolves from the file's own name; any other token stays unevidenced (never guessed).
        // A CONDITIONED winning assignment's branch is never a proven id (round-6: a Release-only
        // AssemblyName is not a Debug build's identity); a winning value the bounded evaluator
        // cannot resolve remains a typed gap — the guards' honesty is preserved.
        string? finalPackageId = inherited.PropsPackageId; var finalPackageIdConditioned = inherited.PropsPackageIdConditioned;
        string? finalAssemblyName = inherited.PropsAssemblyName; var finalAssemblyNameConditioned = inherited.PropsAssemblyNameConditioned;
        var finalPackageIdFromProps = finalPackageId is not null; var finalAssemblyNameFromProps = finalAssemblyName is not null;
        if (packageIdValue is not null) { finalPackageId = packageIdValue; finalPackageIdConditioned = packageIdConditioned; finalPackageIdFromProps = false; }
        if (assemblyNameValue is not null) { finalAssemblyName = assemblyNameValue; finalAssemblyNameConditioned = assemblyNameConditioned; finalAssemblyNameFromProps = false; }
        // targets is imported AFTER the body: its assignments win (review: targets PackageId ignored)
        if (inherited.TargetsPackageId is not null) { finalPackageId = inherited.TargetsPackageId; finalPackageIdConditioned = inherited.TargetsPackageIdConditioned; finalPackageIdFromProps = false; }
        if (inherited.TargetsAssemblyName is not null) { finalAssemblyName = inherited.TargetsAssemblyName; finalAssemblyNameConditioned = inherited.TargetsAssemblyNameConditioned; finalAssemblyNameFromProps = false; }
        var chainSource = finalPackageId is not null ? "PackageId" : finalAssemblyName is not null ? "AssemblyName" : null;
        var chainValue = finalPackageId ?? finalAssemblyName;
        var chainConditioned = finalPackageId is not null ? finalPackageIdConditioned : finalAssemblyNameConditioned;
        // The winning assignment's SOURCE FILE must not carry imports or Target-nested metadata:
        // an import inside that file can reassign the value after it (MSBuild packs the imported
        // value), and a Target-scoped assignment only applies when the target runs (review: import
        // check + target-scoped identity). Body-file imports were already refused (custom-imports).
        var winnerFromProps = finalPackageId is not null ? finalPackageIdFromProps : finalAssemblyNameFromProps;
        var winnerImportsPresent = winnerFromProps ? inherited.PropsImports : inherited.TargetsImports;
        if (winnerImportsPresent) return new(CiDerivedIdentityDecisions.DeclaresIdentity, null);
        if (chainValue is not null)
        {
            if (chainConditioned
                || EvaluateBoundedIdentity(chainValue, projectName, out var _) is not { } evaluated
                || !IsSafeNuGetPackageId(evaluated))
                return new(CiDerivedIdentityDecisions.DeclaresIdentity, null);
            return new(CiDerivedIdentityDecisions.DeclaredId, evaluated, chainSource!);
        }
        if (!IsSafeNuGetPackageId(projectName)) return new(CiDerivedIdentityDecisions.UnsafeFileName, null);
        return new(CiDerivedIdentityDecisions.Derived, projectName, "MSBuildProjectName");
    }

    private sealed record InheritedProjectView(
        bool Unreadable,
        bool? Packable, bool PackableLiteralTrue, bool PackableUnprovable,
        bool? TargetsPackable, bool TargetsPackableLiteralTrue, bool TargetsPackableUnprovable,
        bool TestSdkReferenced, bool ConditionalTestSdk,
        string? PropsPackageId, bool PropsPackageIdConditioned,
        string? PropsAssemblyName, bool PropsAssemblyNameConditioned,
        string? TargetsPackageId, bool TargetsPackageIdConditioned,
        string? TargetsAssemblyName, bool TargetsAssemblyNameConditioned,
        bool PropsImports, bool TargetsImports);

    private static InheritedProjectView InspectNearestDirectoryBuildFiles(string root, string? projectDirectory)
    {
        var unreadable = false; var testSdkReferenced = false; var conditionalTestSdk = false;
        bool? propsPackable = null, targetsPackable = null;
        bool propsLiteralTrue = false, targetsLiteralTrue = false;
        var propsUnprovable = false; var targetsUnprovable = false;
        string? propsPackageId = null, propsAssemblyName = null, targetsPackageId = null, targetsAssemblyName = null;
        var propsPackageIdConditioned = false; var propsAssemblyNameConditioned = false;
        var targetsPackageIdConditioned = false; var targetsAssemblyNameConditioned = false;
        var propsImports = false; var targetsImports = false;
        foreach (var fileName in new[] { "Directory.Build.props", "Directory.Build.targets" })
        {
            for (var dir = projectDirectory; dir is not null && IsUnderRoot(dir, root); dir = Path.GetDirectoryName(dir))
            {
                var candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate))
                {
                    // A linked Directory.Build file is outside the admitted inventory even though
                    // Exists/Load follow it — an external target must never steer derived identity
                    // (review: linked metadata). Treated as unreadable: identity not provable.
                    try { if (File.GetAttributes(candidate).HasFlag(FileAttributes.ReparsePoint)) { unreadable = true; break; } }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { unreadable = true; break; }
                    if (!TryLoadXml(candidate, out var document, MaxProducerProjectBytes)) { unreadable = true; break; }
                    // an Import inside the file can reassign anything the file declares — the caller
                    // refuses identity won from an import-bearing file (review: import check)
                    if (document.Descendants().Any(e => e.Name.LocalName == "Import" && AttributeValue(e, "Project") is not null))
                    {
                        if (fileName == "Directory.Build.props") propsImports = true; else targetsImports = true;
                    }
                    foreach (var element in document.Descendants())
                    {
                        if (element.Parent is { Name.LocalName: "PropertyGroup" })
                        {
                            switch (element.Name.LocalName.ToUpperInvariant())
                            {
                                case "PACKAGEID" or "ASSEMBLYNAME":
                                    // per-property, per-file capture (last-wins within the file) — the
                                    // caller resolves the three-phase MSBuild precedence precisely.
                                    // Target-scoped assignments apply only when the target runs: unprovable
                                    var identityConditioned = CiDerivedIdentityDecisions.IsConditioned(element)
                                        || element.Ancestors().Any(a => a.Name.LocalName == "Target");
                                    if (fileName == "Directory.Build.props")
                                    {
                                        if (element.Name.LocalName.ToUpperInvariant() == "PACKAGEID") { propsPackageId = element.Value.Trim(); propsPackageIdConditioned = identityConditioned; }
                                        else { propsAssemblyName = element.Value.Trim(); propsAssemblyNameConditioned = identityConditioned; }
                                    }
                                    else
                                    {
                                        if (element.Name.LocalName.ToUpperInvariant() == "PACKAGEID") { targetsPackageId = element.Value.Trim(); targetsPackageIdConditioned = identityConditioned; }
                                        else { targetsAssemblyName = element.Value.Trim(); targetsAssemblyNameConditioned = identityConditioned; }
                                    }
                                    break;
                                case "ISPACKABLE":
                                    var value = element.Value.Trim();
                                    var literalFalse = value.Equals("false", StringComparison.OrdinalIgnoreCase);
                                    var literalTrue = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                                    if (CiDerivedIdentityDecisions.IsConditioned(element))
                                    {
                                        // A Conditioned assignment is neither true nor false in the bounded
                                        // view — unprovable, never a definitive skip (review round-5).
                                        if (fileName == "Directory.Build.props") propsUnprovable = true;
                                        else targetsUnprovable = true;
                                        break;
                                    }
                                    var isPackable = !literalFalse; // provisional; non-literal refused by the caller
                                    if (fileName == "Directory.Build.props") (propsPackable, propsLiteralTrue, propsUnprovable) = (isPackable, literalTrue, !literalFalse && !literalTrue);
                                    else (targetsPackable, targetsLiteralTrue, targetsUnprovable) = (isPackable, literalTrue, !literalFalse && !literalTrue);
                                    break;
                            }
                        }
                        else if (element.Parent is { Name.LocalName: "ItemGroup" }
                            && element.Name.LocalName == "PackageReference"
                            && string.Equals(AttributeValue(element, "Include"), "Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase))
                        {
                            // unconditional: every project under it is a test project (review P1);
                            // conditioned: unprovable either way, the caller gaps it (review round-5)
                            if (CiDerivedIdentityDecisions.IsConditioned(element)) conditionalTestSdk = true;
                            else testSdkReferenced = true;
                        }
                    }
                    break; // MSBuild auto-imports only the nearest file of each name
                }
                if (string.Equals(dir.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal)) break;
            }
        }
        return new(unreadable, propsPackable, propsLiteralTrue, propsUnprovable, targetsPackable, targetsLiteralTrue, targetsUnprovable, testSdkReferenced, conditionalTestSdk,
            propsPackageId, propsPackageIdConditioned, propsAssemblyName, propsAssemblyNameConditioned, targetsPackageId, targetsPackageIdConditioned, targetsAssemblyName, targetsAssemblyNameConditioned,
            propsImports, targetsImports);
    }

    private static bool IsUnderRoot(string fullPath, string root)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar);
        return string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar), normalizedRoot, StringComparison.Ordinal)
            || fullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static ProducedPackageCandidate? ReadProducedPackageCandidate(string relativePath, XDocument document)
    {
        static bool IsProperty(XElement e) => e.Parent is { Name.LocalName: "PropertyGroup" };
        static bool IsItem(XElement e) => e.Parent is { Name.LocalName: "ItemGroup" };
        var sdkStyle = IsSdkStyle(document);
        // $(MSBuildProjectName) is reserved by MSBuild as the project file name without extension:
        // the file defines it, so evaluating it is reading, not guessing.
        var projectName = Path.GetFileNameWithoutExtension(relativePath);

        // MSBuild evaluation order: property redefinitions REPLACE earlier values — every
        // property here is LAST-wins, with its line tracked to the winning assignment.
        bool? packable = null; var packableLine = 1; var packableLiteralTrue = false;
        string? packageId = null; var packageIdLine = 1;
        string? assemblyName = null; var assemblyNameLine = 1;
        string? plainVersion = null; int? plainVersionLine = null;
        string? packageVersion = null; int? packageVersionLine = null;
        var packSignalLines = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var element in document.Descendants().Where(IsProperty))
        {
            var name = element.Name.LocalName;
            var value = element.Value.Trim();
            switch (name.ToUpperInvariant())
            {
                case "ISPACKABLE": packable = !value.Equals("false", StringComparison.OrdinalIgnoreCase); packableLiteralTrue = value.Equals("true", StringComparison.OrdinalIgnoreCase); packableLine = GetLine(element); break;
                case "PACKAGEID": packageId = value; packageIdLine = GetLine(element); break;
                case "ASSEMBLYNAME": assemblyName = value; assemblyNameLine = GetLine(element); break;
                case "VERSION": plainVersion = value; plainVersionLine = GetLine(element); break;
                case "PACKAGEVERSION": packageVersion = value; packageVersionLine = GetLine(element); break;
                default:
                    var signal = PackSignalProperties.FirstOrDefault(property => property.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (signal is not null)
                    {
                        if (value.Length == 0) packSignalLines.Remove(signal);
                        else packSignalLines[signal] = GetLine(element);
                    }
                    break;
            }
        }
        if (packable == false) return null;

        var testSdkReferenced = false;
        var projectReferences = new List<ProducedProjectReference>();
        var omittedReferences = 0;
        var referenceLimitLine = 1;
        foreach (var element in document.Descendants().Where(IsItem))
        {
            switch (element.Name.LocalName)
            {
                case "PackageReference":
                    if (string.Equals(AttributeValue(element, "Include"), "Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase))
                        testSdkReferenced = true;
                    break;
                case "ProjectReference":
                    var include = AttributeValue(element, "Include");
                    if (string.IsNullOrWhiteSpace(include) || IsPrivateAssetsAll(element)) break;
                    // Count without allocating a Split array or retaining/hashing overflow tokens.
                    foreach (var range in include.AsSpan().Split(';'))
                    {
                        var token = include.AsSpan()[range].Trim();
                        if (token.IsEmpty) continue;
                        if (projectReferences.Count == MaxProducerReferences)
                        {
                            if (omittedReferences == 0) referenceLimitLine = GetLine(element);
                            omittedReferences++;
                            continue;
                        }
                        var target = token.ToString();
                        projectReferences.Add(new(ResolveProjectReferencePath(relativePath, target), GetLine(element), FactFactory.Hash(target, 64)));
                    }
                    break;
            }
        }

        string id, source, provenance;
        bool explicitId;
        int spanStart, idLine;
        if (packageId is not null)
        {
            if (EvaluateBoundedIdentity(packageId, projectName, out var interpolated) is not { } evaluated) return null;
            (id, source, idLine) = (evaluated, PackageIdSources.PackageId, packageIdLine);
            provenance = interpolated ? PackageIdProvenances.Interpolated : PackageIdProvenances.Declared;
            explicitId = !interpolated;
            spanStart = idLine;
        }
        else if (assemblyName is not null)
        {
            if (!sdkStyle) return null; // legacy non-SDK: AssemblyName is a build artifact, not a package declaration
            if (EvaluateBoundedIdentity(assemblyName, projectName, out var interpolated) is not { } evaluated) return null;
            // An INTERPOLATED AssemblyName alone is naming, not packing (live 2026-10-21: a repo whose
            // convention is AssemblyName=Prefix.$(MSBuildProjectName) on projects consumed in-solution
            // became false producers — a .Tests sibling included). The interpolated path carries the
            // same pack-intent gate as derived-default: literal IsPackable=true or pack metadata, plus
            // the test-SDK guard. A literal (non-interpolated) AssemblyName keeps legacy behavior —
            // full explicit ids on this estate are deliberate.
            if (interpolated)
            {
                if (packableLiteralTrue) packSignalLines["IsPackable"] = packableLine;
                if (packSignalLines.Count == 0 || (testSdkReferenced && !packableLiteralTrue)) return null;
            }
            (id, source, idLine) = (evaluated, PackageIdSources.AssemblyName, assemblyNameLine);
            provenance = interpolated ? PackageIdProvenances.Interpolated : PackageIdProvenances.Declared;
            explicitId = false;
            spanStart = idLine;
        }
        else
        {
            // Derived default: the chain ends at the file name. Only an SDK-style project with pack
            // signals qualifies — a literal IsPackable=true is itself a signal, and the only one that
            // overrides the test SDK's packable=false default.
            if (packableLiteralTrue) packSignalLines["IsPackable"] = packableLine;
            if (!sdkStyle || packSignalLines.Count == 0 || (testSdkReferenced && !packableLiteralTrue)) return null;
            (id, source, provenance, explicitId) = (projectName, PackageIdSources.MSBuildProjectName, PackageIdProvenances.DerivedDefault, false);
            spanStart = packSignalLines.Values.Min();
            idLine = packSignalLines.Values.Max();
        }
        if (!IsSafeNuGetPackageId(id)) return null;

        // NuGet pack semantics: PackageVersion is the produced version; Version is its fallback.
        // Each property is last-wins within itself; selection is by name, not XML order.
        var version = packageVersion ?? plainVersion;
        var versionLine = version is null ? null : packageVersion is not null ? packageVersionLine : plainVersionLine;
        if (version is not null && !IsSafeNuGetResolvedVersion(version)) { version = null; versionLine = null; } // unevidenced, never projected
        var spanEnd = idLine;
        if (provenance == PackageIdProvenances.Interpolated && source == PackageIdSources.AssemblyName)
        {
            spanStart = Math.Min(spanStart, packSignalLines.Values.Min());
            spanEnd = Math.Max(spanEnd, packSignalLines.Values.Max());
        }
        if (versionLine is { } vl)
        {
            spanStart = Math.Min(spanStart, vl);
            spanEnd = Math.Max(spanEnd, vl);
        }
        var packSignals = provenance == PackageIdProvenances.DerivedDefault
            || (source == PackageIdSources.AssemblyName && provenance == PackageIdProvenances.Interpolated)
            ? string.Join(',', packSignalLines.Keys) : null;
        return new ProducedPackageCandidate(relativePath, id, version, explicitId, spanEnd, spanStart, source, provenance, packSignals, projectReferences, omittedReferences, referenceLimitLine);
    }

    /// Bounded MSBuild evaluation for an identity property: a value without tokens is declared as-is;
    /// a value whose ONLY tokens are $(MSBuildProjectName) resolves from the file's own name; any
    /// other token — another property, @(items), %(metadata), or a property function such as
    /// $(MSBuildProjectName.Replace(...)) — leaves the identity unevidenced (null). Never guessed.
    internal static string? EvaluateBoundedIdentity(string value, string projectName, out bool interpolated)
    {
        interpolated = false;
        if (!ContainsMsBuildToken(value)) return value;
        var evaluated = value.Replace("$(MSBuildProjectName)", projectName, StringComparison.OrdinalIgnoreCase); // MSBuild property names are case-insensitive
        if (ContainsMsBuildToken(evaluated)) return null;
        interpolated = true;
        return evaluated;
    }

    private static bool ContainsMsBuildToken(string value) =>
        value.Contains("$(", StringComparison.Ordinal)
        || value.Contains("@(", StringComparison.Ordinal)
        || value.Contains("%(", StringComparison.Ordinal);

    private static bool IsPrivateAssetsAll(XElement reference)
    {
        var privateAssets = AttributeValue(reference, "PrivateAssets")
            ?? reference.Elements().FirstOrDefault(child => child.Name.LocalName == "PrivateAssets")?.Value.Trim();
        return privateAssets is not null
            && privateAssets.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(token => token.Equals("all", StringComparison.OrdinalIgnoreCase));
    }

    /// Resolves a ProjectReference Include against the referencing project's directory as a pure
    /// repo-relative string: no filesystem probe, no token evaluation. MSBuild accepts '\' as a
    /// separator on every platform, so both separators are normalized before resolution. Null when
    /// the path carries MSBuild tokens or wildcards, is rooted, or climbs out of the repository.
    internal static string? ResolveProjectReferencePath(string projectRelativePath, string include)
    {
        if (ContainsMsBuildToken(include) || include.IndexOfAny(['*', '?']) >= 0) return null;
        var normalizedInclude = include.Replace('\\', '/');
        var driveRooted = normalizedInclude.Length >= 2 && char.IsAsciiLetter(normalizedInclude[0]) && normalizedInclude[1] == ':';
        if (normalizedInclude.StartsWith('/') || driveRooted) return null;
        var directory = FileInventory.NormalizeRelativePath(Path.GetDirectoryName(projectRelativePath) ?? string.Empty);
        var segments = new List<string>();
        foreach (var segment in (directory + "/" + normalizedInclude).Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..")
            {
                if (segments.Count == 0) return null; // escapes the repository: not an in-repo reference
                segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(segment);
        }
        return segments.Count == 0 ? null : string.Join('/', segments);
    }

    /// Reads central package management version pins (PackageVersion items) from MSBuild props files.
    /// Only pins with a safe package identity are returned; unsafe versions are returned verbatim —
    /// safety projection happens at fact emission, identical to project-file versions.
    public static IReadOnlyList<CentralPackageVersionInfo> ReadCentralPackageVersions(string repoPath, IEnumerable<FileInventoryItem> inventory)
    {
        var results = new List<CentralPackageVersionInfo>();
        foreach (var props in inventory.Where(item => item.Kind == "MSBuildProps"))
        {
            var fullPath = Path.Combine(repoPath, props.RelativePath);
            if (!TryLoadXml(fullPath, out var document))
            {
                continue;
            }

            foreach (var element in document.Descendants().Where(element => element.Name.LocalName == "PackageVersion"))
            {
                // MSBuild item identities are semicolon-separable: one pin per id, each safety-checked.
                var idList = AttributeValue(element, "Include") ?? AttributeValue(element, "Update");
                var versionElement = element.Elements().FirstOrDefault(child => child.Name.LocalName == "Version");
                var version = AttributeValue(element, "Version") ?? versionElement?.Value.Trim();
                if (string.IsNullOrWhiteSpace(version))
                {
                    continue; // a pin without a version is not version evidence (e.g. property interpolation)
                }

                // PR-804: when the version rides a child element on a later line, the evidence span
                // must cover BOTH endpoints — start at the item's identity line, end at the value's.
                var endLine = versionElement is not null ? Math.Max(GetLine(element), GetLine(versionElement)) : GetLine(element);
                if (idList is null)
                {
                    continue;
                }
                foreach (var id in idList.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!IsSafeNuGetPackageId(id))
                    {
                        continue;
                    }

                    results.Add(new CentralPackageVersionInfo(props.RelativePath, id, version.Trim(), GetLine(element), endLine));
                }
            }
        }

        return results
            .OrderBy(item => item.PropsPath, StringComparer.Ordinal)
            .ThenBy(item => item.PackageName, StringComparer.Ordinal)
            .ThenBy(item => item.Version, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<(string Value, int Line)> ReadProjectValues(string fullPath, params string[] elementNames)
    {
        if (!TryLoadXml(fullPath, out var document))
        {
            yield break;
        }

        foreach (var element in document.Descendants().Where(element => elementNames.Contains(element.Name.LocalName, StringComparer.Ordinal)))
        {
            var value = element.Value.Trim();
            if (value.Length == 0)
            {
                continue;
            }

            yield return (value, GetLine(element));
        }
    }

    private static IEnumerable<PackageReferenceInfo> ReadPackageReferencesFromProject(string fullPath, string relativePath, string manifestKind)
    {
        if (!TryLoadXml(fullPath, out var document))
        {
            yield break;
        }

        foreach (var element in document.Descendants().Where(element => element.Name.LocalName == "PackageReference"))
        {
            var packageName = AttributeValue(element, "Include") ?? AttributeValue(element, "Update");
            if (string.IsNullOrWhiteSpace(packageName))
            {
                continue;
            }

            var version = AttributeValue(element, "Version")
                ?? element.Elements().FirstOrDefault(child => child.Name.LocalName == "Version")?.Value.Trim();
            var overrideElement = element.Elements().FirstOrDefault(child => child.Name.LocalName == "VersionOverride");
            var versionOverride = AttributeValue(element, "VersionOverride") ?? overrideElement?.Value.Trim();
            var endLine = overrideElement is not null ? Math.Max(GetLine(element), GetLine(overrideElement)) : (int?)null;
            yield return new PackageReferenceInfo(
                relativePath,
                packageName,
                version,
                GetLine(element),
                manifestKind,
                "PackageReference",
                "runtime",
                null,
                versionOverride,
                endLine);
        }
    }

    private static IEnumerable<PackageReferenceInfo> ReadPackagesConfig(string fullPath, string relativePath)
    {
        if (!TryLoadXml(fullPath, out var document))
        {
            yield break;
        }

        foreach (var element in document.Descendants().Where(element => element.Name.LocalName == "package"))
        {
            var packageName = AttributeValue(element, "id");
            if (string.IsNullOrWhiteSpace(packageName))
            {
                continue;
            }

            yield return new PackageReferenceInfo(
                relativePath,
                packageName,
                AttributeValue(element, "version"),
                GetLine(element),
                "packages.config",
                "packages.config",
                "runtime",
                AttributeValue(element, "targetFramework"));
        }
    }

    /// <summary>
    /// Reads checked-in NuGet packages.lock.json files offline. The lockfile's contentHash field is
    /// intentionally never captured: it is not a registry artifact digest, so no artifactDigest
    /// property is ever emitted from this reader.
    /// </summary>
    public static NuGetLockfileReadResult ReadNuGetLockfiles(string repoPath, IEnumerable<FileInventoryItem> inventory)
    {
        var entries = new List<NuGetLockfileEntry>();
        var gaps = new List<NuGetLockfileGap>();
        foreach (var lockfile in inventory.Where(item => item.Kind == "PackagesLock").OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(Path.Combine(repoPath, lockfile.RelativePath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                gaps.Add(new NuGetLockfileGap(lockfile.RelativePath, "packages-lock-read-failed", "packages.lock.json could not be read; resolved-version evidence is unavailable.", 1));
                continue;
            }

            var lockfileHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()[..32];
            var lineCursor = new LockfileLineCursor(bytes);
            try
            {
                ReadNuGetLockfile(lockfile.RelativePath, lockfileHash, bytes, lineCursor, entries, gaps);
            }
            catch (JsonException)
            {
                gaps.Add(new NuGetLockfileGap(lockfile.RelativePath, "packages-lock-parse", "packages.lock.json is malformed or truncated JSON; resolved-version evidence is partial.", 1));
            }
        }

        return new NuGetLockfileReadResult(
            entries
                .OrderBy(entry => entry.LockfilePath, StringComparer.Ordinal)
                .ThenBy(entry => entry.TargetFramework, StringComparer.Ordinal)
                .ThenBy(entry => entry.PackageName, StringComparer.Ordinal)
                .ThenBy(entry => entry.ResolvedVersion ?? string.Empty, StringComparer.Ordinal)
                .ToArray(),
            gaps
                .OrderBy(gap => gap.LockfilePath, StringComparer.Ordinal)
                .ThenBy(gap => gap.Line)
                .ThenBy(gap => gap.Category, StringComparer.Ordinal)
                .ToArray());
    }

    private static void ReadNuGetLockfile(
        string relativePath,
        string lockfileHash,
        byte[] bytes,
        LockfileLineCursor lineCursor,
        List<NuGetLockfileEntry> entries,
        List<NuGetLockfileGap> gaps)
    {
        using (var document = JsonDocument.Parse(bytes))
        {
            if (HasDuplicateJsonProperties(document.RootElement))
                throw new JsonException("packages.lock.json contains duplicate properties.");
        }

        var reader = new Utf8JsonReader(bytes, isFinalBlock: true, default);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("packages.lock.json must contain a JSON object.");

        var sawDependencies = false;
        var lockfileVersion = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("packages.lock.json has an unexpected top-level token.");

            var propertyName = reader.GetString();
            if (propertyName == "version")
            {
                if (!reader.Read() || reader.TokenType is not (JsonTokenType.Number or JsonTokenType.String) || !TryReadLockfileVersion(ref reader, out lockfileVersion))
                    throw new JsonException("packages.lock.json version must be an integer.");
            }
            else if (propertyName == "dependencies")
            {
                sawDependencies = true;
                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException("packages.lock.json dependencies must be an object.");
                ReadNuGetLockfileDependencies(relativePath, lockfileHash, ref reader, lineCursor, entries, gaps);
            }
            else
            {
                if (!reader.Read())
                    throw new JsonException("packages.lock.json ended unexpectedly.");
                SkipLockfileValue(ref reader);
            }
        }

        if (lockfileVersion is not (1 or 2))
        {
            entries.RemoveAll(entry => entry.LockfilePath == relativePath);
            gaps.Add(new NuGetLockfileGap(relativePath, "packages-lock-unsupported", $"packages.lock.json schema version {lockfileVersion} is unsupported; resolved-version evidence is unavailable.", 1));
            return;
        }

        if (!sawDependencies)
            gaps.Add(new NuGetLockfileGap(relativePath, "packages-lock-unsupported", "packages.lock.json did not declare a dependencies object; resolved-version evidence is unavailable.", 1));
    }

    private static bool HasDuplicateJsonProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            return element.EnumerateArray().Any(HasDuplicateJsonProperties);
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Add(property.Name) || HasDuplicateJsonProperties(property.Value))
                return true;
        }

        return false;
    }

    private static bool TryReadLockfileVersion(ref Utf8JsonReader reader, out int version)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return reader.TryGetInt32(out version);
        var raw = reader.GetString();
        return int.TryParse(raw, out version);
    }

    private static void ReadNuGetLockfileDependencies(
        string relativePath,
        string lockfileHash,
        ref Utf8JsonReader reader,
        LockfileLineCursor lineCursor,
        List<NuGetLockfileEntry> entries,
        List<NuGetLockfileGap> gaps)
    {
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("packages.lock.json dependencies has an unexpected token.");

            var targetFramework = reader.GetString();
            if (!reader.Read())
                throw new JsonException("packages.lock.json ended unexpectedly.");
            if (reader.TokenType != JsonTokenType.StartObject || !IsSafeLockfileTargetFramework(targetFramework))
            {
                gaps.Add(new NuGetLockfileGap(relativePath, "packages-lock-group-unsupported", "packages.lock.json target-framework group is unsupported; its resolved-version evidence is unavailable.", lineCursor.LineAt(reader.TokenStartIndex)));
                SkipLockfileValue(ref reader);
                continue;
            }

            ReadNuGetLockfileGroup(relativePath, lockfileHash, targetFramework!, ref reader, lineCursor, entries, gaps);
        }
    }

    private static void ReadNuGetLockfileGroup(
        string relativePath,
        string lockfileHash,
        string targetFramework,
        ref Utf8JsonReader reader,
        LockfileLineCursor lineCursor,
        List<NuGetLockfileEntry> entries,
        List<NuGetLockfileGap> gaps)
    {
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("packages.lock.json dependency group has an unexpected token.");

            var packageName = reader.GetString();
            var line = lineCursor.LineAt(reader.TokenStartIndex);
            if (!reader.Read())
                throw new JsonException("packages.lock.json ended unexpectedly.");
            if (reader.TokenType != JsonTokenType.StartObject || !IsSafeNuGetPackageId(packageName))
            {
                gaps.Add(new NuGetLockfileGap(relativePath, "packages-lock-entry-unsafe", "packages.lock.json entry is malformed or has an unsafe package identity; its evidence is omitted.", line));
                SkipLockfileValue(ref reader);
                continue;
            }

            ReadNuGetLockfileEntry(relativePath, lockfileHash, targetFramework, packageName!, line, ref reader, lineCursor, entries, gaps);
        }
    }

    private static void ReadNuGetLockfileEntry(
        string relativePath,
        string lockfileHash,
        string targetFramework,
        string packageName,
        int line,
        ref Utf8JsonReader reader,
        LockfileLineCursor lineCursor,
        List<NuGetLockfileEntry> entries,
        List<NuGetLockfileGap> gaps)
    {
        string? type = null;
        string? resolved = null;
        List<string>? dependencyNames = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("packages.lock.json package entry has an unexpected token.");

            var propertyName = reader.GetString();
            if (!reader.Read())
                throw new JsonException("packages.lock.json ended unexpectedly.");
            if (propertyName == "type" && reader.TokenType == JsonTokenType.String)
            {
                type = reader.GetString();
            }
            else if (propertyName == "resolved" && reader.TokenType == JsonTokenType.String)
            {
                resolved = reader.GetString()?.Trim();
            }
            else if (propertyName == "dependencies" && reader.TokenType == JsonTokenType.StartObject)
            {
                dependencyNames = ReadNuGetLockfileEntryDependencies(ref reader);
            }
            else
            {
                // contentHash, requested, and unknown fields are deliberately ignored: contentHash is
                // package-content metadata, not a registry artifact digest, and is never emitted.
                SkipLockfileValue(ref reader);
            }
        }

        // NuGet emits project-to-project references in packages.lock.json as
        // type "Project" entries without a resolved package version. They are
        // not package artifacts and are indexed through project-reference
        // evidence elsewhere, so do not misclassify them as malformed packages.
        if (string.Equals(type?.Trim(), "Project", StringComparison.OrdinalIgnoreCase))
            return;

        if (string.IsNullOrWhiteSpace(resolved))
        {
            gaps.Add(new NuGetLockfileGap(relativePath, "packages-lock-entry-resolved-missing", $"packages.lock.json entry for a package in {targetFramework} did not provide a resolved version.", line));
            return;
        }

        var dependencyRelation = type?.Trim().ToLowerInvariant() switch
        {
            "direct" => "direct",
            "transitive" => "transitive",
            _ => "unknown"
        };
        var safeDependencyNames = (dependencyNames ?? [])
            .Where(IsSafeNuGetPackageId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var dependencyNamesJoined = safeDependencyNames.Length == 0 || safeDependencyNames.Sum(name => name.Length + 1) > 256
            ? null
            : string.Join(',', safeDependencyNames);
        var unsafeResolved = !IsSafeNuGetResolvedVersion(resolved);
        entries.Add(new NuGetLockfileEntry(
            relativePath,
            packageName,
            unsafeResolved ? null : resolved,
            unsafeResolved ? FactFactory.Hash(resolved, 32) : null,
            dependencyRelation,
            targetFramework,
            line,
            safeDependencyNames.Length,
            dependencyNamesJoined,
            lockfileHash));
    }

    private static List<string> ReadNuGetLockfileEntryDependencies(ref Utf8JsonReader reader)
    {
        var names = new List<string>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("packages.lock.json entry dependencies has an unexpected token.");
            names.Add(reader.GetString() ?? string.Empty);
            if (!reader.Read())
                throw new JsonException("packages.lock.json ended unexpectedly.");
            SkipLockfileValue(ref reader);
        }

        return names;
    }

    private static void SkipLockfileValue(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is not (JsonTokenType.StartObject or JsonTokenType.StartArray))
            return;

        var depth = 1;
        while (reader.Read())
        {
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                depth++;
            else if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                depth--;
                if (depth == 0)
                    break;
            }
        }
    }

    // Shared with the CI workflow producer lane so both source kinds apply one id/version policy.
    internal static bool IsSafeNuGetPackageId(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= 128
            && !value.Contains("..", StringComparison.Ordinal)
            && SafeNuGetPackageIdPattern.IsMatch(value);
    }

    private static bool IsSafeLockfileTargetFramework(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= 128
            && SafeLockfileTargetFrameworkPattern.IsMatch(value);
    }

    internal static bool IsSafeNuGetResolvedVersion(string value)
    {
        return value.Length is > 0 and <= 128
            && SafeNuGetResolvedVersionPattern.IsMatch(value);
    }

    private sealed class LockfileLineCursor(byte[] bytes)
    {
        private long _offset;
        private int _line = 1;

        public int LineAt(long offset)
        {
            if (offset < _offset)
                return _line;
            for (; _offset < offset && _offset < bytes.Length; _offset++)
            {
                if (bytes[_offset] == (byte)'\n')
                    _line++;
            }

            return _line;
        }
    }

    // Anchored character-class validators: NonBacktracking keeps them linear, and the
    // generous bound is a safety net that thread starvation under parallel load must
    // not trip (#857: tight wall-clock regex budgets failed full-suite runs).
    private static readonly Regex SafeNuGetPackageIdPattern = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromSeconds(2));
    private static readonly Regex SafeLockfileTargetFrameworkPattern = new("^[A-Za-z0-9][A-Za-z0-9._/+,-]{0,127}$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromSeconds(2));
    private static readonly Regex SafeNuGetResolvedVersionPattern = new("^[0-9]+(?:\\.[0-9]+){0,3}(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?(?:\\+[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromSeconds(2));

    private static bool TryLoadXml(string fullPath, out XDocument document, int? maxBytes = null)
    {
        try
        {
            if (maxBytes is { } limit)
            {
                using var stream = File.OpenRead(fullPath);
                if (stream.Length > limit)
                {
                    document = new XDocument();
                    return false;
                }
                using var reader = XmlReader.Create(stream, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = limit
                });
                document = XDocument.Load(reader, LoadOptions.SetLineInfo);
            }
            else document = XDocument.Load(fullPath, LoadOptions.SetLineInfo);
            return true;
        }
        catch
        {
            document = new XDocument();
            return false;
        }
    }

    private static string? AttributeValue(XElement element, string name)
    {
        return element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == name)?.Value.Trim();
    }

    private static int GetLine(XObject node)
    {
        return node is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
            ? Math.Max(1, lineInfo.LineNumber)
            : 1;
    }
}
