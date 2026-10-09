using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using TraceMap.Core;

namespace TraceMap.Cli;

public sealed record WebFormsReviewPreparationManifest(
    string SchemaVersion, string RuleId, string EvidenceTier, string Visibility, string ClaimLevel,
    string GeneratorSha256, string BoundedInputSha256, string SourceCommitSha,
    string AttestationKind, IReadOnlyList<WebFormsReviewInput> Inputs,
    IReadOnlyList<WebFormsReviewSourceMembership> SourceMembership,
    CompiledInputInspection CompiledInspection, WebFormsPublishProvenance PublishInspection,
    IReadOnlyList<WebFormsReviewArtifact> Artifacts, IReadOnlyList<string> Gaps,
    IReadOnlyList<string> Limitations);

public sealed record WebFormsReviewSourceMembership(string SourceRelativePath, string GitCanonicalRelativePath,
    string GitBlobObjectId, string ComparisonKind, string? NormalizationPolicySha256 = null);

/// <summary>Explicit local receipt generation. Never builds, copies binaries, scans source, or modifies input roots.</summary>
public static partial class WebFormsReviewPreparationCommand
{
    public const string RuleId = "workflow.webforms.compiled-review-preparation.v1";
    public const string Schema = "webforms-compiled-review-preparation.v1";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private static readonly StringComparer Paths = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (args.Length != 7 || args[0] != "prepare" || args[1] != "--config" || args[3] != "--out"
                || args[5] != "--attest-exact-source-commit") throw Fail("EXPLICIT_ATTESTATION_AND_ARGUMENTS_REQUIRED");
            var root = WebFormsReviewPreflightCommand.PhysicalPath(args[4]);
            var plan = await WebFormsReviewPreflightCommand.BuildAsync(args[2], root, cancellationToken);
            var config = plan.Configuration;
            if (args[6] != config.SourceCommitSha) throw Fail("ATTESTATION_COMMIT_MISMATCH");
            if (config.BindingReceipts.Length != 0 || config.PublishReceiptRelativePath is not null || config.ReceiptRoot is not null
                || config.PreparationProvenance is not null) throw Fail("CONFIG_ALREADY_HAS_RECEIPTS");
            if (config.PublishSourceRelativePaths is not { Length: > 0 }) throw Fail("EXPLICIT_SOURCE_MEMBERSHIP_REQUIRED");
            var runtime = WebFormsReviewPreflightCommand.PhysicalPath(Path.GetDirectoryName(typeof(WebFormsReviewPreparationCommand).Assembly.Location)!);
            if (Within(runtime, root) || Within(root, runtime)) throw Fail("OUTPUT_OVERLAPS_RUNTIME");
            var git = GitMetadataProvider.Detect(config.SourceRoot);
            if (git.CommitSha != config.SourceCommitSha || string.IsNullOrWhiteSpace(git.RemoteUrl)) throw Fail("SOURCE_REPOSITORY_OR_COMMIT_UNAVAILABLE");
            var sourcePaths = config.PublishSourceRelativePaths.Concat(config.PageRelativePaths)
                .Select(Normalize).Distinct(Paths).Order(StringComparer.Ordinal).ToArray();
            if (sourcePaths.Length < 1 || sourcePaths.Length > Math.Min(16_384, config.Budgets.MaxPublishInputFiles ?? 256)) throw Fail("SOURCE_MEMBERSHIP_LIMIT");
            var membership = await ValidateCommittedSourceAsync(config, sourcePaths, cancellationToken);
            var sourceRows = sourcePaths.Select(path => new SourceFile(path,
                plan.Inputs.Single(input => Paths.Equals(input.Path, WebFormsReviewPreflightCommand.Child(config.SourceRoot, path))).Sha256)).ToArray();
            var selectedPages = config.PageMode == "selected" ? config.PageRelativePaths.Select(Normalize).Order(StringComparer.Ordinal).ToArray()
                : sourcePaths.Where(path => path.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (selectedPages.Length is < 1 or > 2_048) throw Fail("PAGE_RECEIPT_PARTITION_LIMIT");
            var publishedRows = plan.Inputs.Where(input => input.Role is "primary-assembly" or "dependency-assembly" or "page-map")
                .Select(input => new PublishedFile(Normalize(Path.GetRelativePath(config.PublishedRoot, input.Path)), input.Sha256,
                    input.Role == "page-map" ? "compiled-map" : "assembly")).OrderBy(item => item.Path, StringComparer.Ordinal).ToArray();
            if (publishedRows.Length is < 1 or > 4_096) throw Fail("PUBLISHED_RECEIPT_PARTITION_LIMIT");
            var maps = new List<Map>();
            foreach (var item in publishedRows.Where(item => item.Kind == "compiled-map"))
            {
                var bytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(WebFormsReviewPreflightCommand.Child(config.PublishedRoot, item.Path), 1_048_576, cancellationToken);
                if (Digest(bytes) != item.Sha256) throw Fail("INPUT_CHANGED");
                using var stream = new MemoryStream(bytes, writable: false);
                using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1_048_576 });
                var map = XDocument.Load(reader).Root;
                var virtualPath = map?.Attribute("virtualPath")?.Value;
                if (map?.Name.LocalName != "preserve" || !SafeVirtual(virtualPath)) throw Fail("MAP_INVALID");
                // Directory preserves (for example /App_Code/) are retained and hashed
                // publication inputs, not page bindings or application-prefix witnesses.
                if (!virtualPath!.EndsWith('/'))
                    maps.Add(new(item.Path, virtualPath, map.Attribute("assembly")?.Value, map.Attribute("type")?.Value));
            }
            // Only disambiguate when a selected page actually has competing suffix matches;
            // unambiguous selections keep their original exact-suffix behavior.
            var applicationPrefix = selectedPages.Any(page => PageMapMatches(maps, page).Skip(1).Any())
                ? SelectApplicationPrefix(maps,
                    sourcePaths.Where(path => path.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)).Concat(selectedPages)
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray())
                : null;
            var pages = new List<Page>();
            foreach (var page in selectedPages)
            {
                var matches = PageMapMatches(maps, page)
                    .Where(match => applicationPrefix is null || match.Prefix.Equals(applicationPrefix, StringComparison.OrdinalIgnoreCase))
                    .Select(match => match.Map).ToArray();
                if (matches.Length > 1) throw Fail("PAGE_MAP_NOT_UNIQUE");
                if (matches.Length == 0)
                {
                    if (!publishedRows.Any(item => item.Kind == "assembly" && Path.GetFileName(item.Path).StartsWith("App_Web_", StringComparison.OrdinalIgnoreCase)))
                        throw Fail("MAPLESS_WEB_ASSEMBLY_UNAVAILABLE");
                    pages.Add(new("/" + page, page, null, null, null, "mapless-source-type-candidate"));
                }
                else
                {
                    var map = matches[0];
                    if (string.IsNullOrWhiteSpace(map.Assembly) || string.IsNullOrWhiteSpace(map.GeneratedType)
                        || !publishedRows.Any(item => item.Kind == "assembly" && item.Path == "bin/" + map.Assembly + ".dll")) throw Fail("MAPPED_ASSEMBLY_UNAVAILABLE");
                    pages.Add(new(map.VirtualPath, page, map.Assembly, map.GeneratedType, map.Path, null));
                }
            }
            var options = new ScanOptions(config.SourceRoot, "unused-preparation-output",
                CompiledInputPaths: plan.Inputs.Where(input => input.Role == "primary-assembly").Select(input => input.Path).ToArray(),
                CompiledDependencyPaths: plan.Inputs.Where(input => input.Role == "dependency-assembly").Select(input => input.Path).ToArray(),
                CompiledInputLimits: new(MaxArtifactCount: config.Budgets.MaxInputFiles, MaxFileSizeBytes: config.Budgets.MaxAssemblyBytes,
                    MaxTextLength: config.Budgets.MetadataMaxText, MaxTotalWorkUnits: config.Budgets.MetadataMaxWork));
            var inspected = ManagedMetadataExtractor.InspectInputs(options, config.SourceCommitSha, cancellationToken);
            var outcomes = inspected.Provenance?.Outcomes ?? throw Fail("METADATA_INSPECTION_UNAVAILABLE");
            var assemblies = plan.Inputs.Where(input => input.Role.EndsWith("assembly", StringComparison.Ordinal)).ToArray();
            if (inspected.Provenance.OmittedInputCount != 0 || outcomes.Count != assemblies.Length
                || outcomes.Any(item => item.Outcome != "admitted" || item.RawFileSha256 is null || item.AssemblyIdentity is null)
                || assemblies.Any(input => outcomes.Count(item => item.RawFileSha256 == input.Sha256) != 1))
            {
                await output.WriteLineAsync($"webFormsPreparation=gap;phase=metadata;admitted={outcomes.Count(item => item.Outcome == "admitted")};expected={assemblies.Length};omitted={inspected.Provenance.OmittedInputCount};maxWork={config.Budgets.MetadataMaxWork};maxArtifacts={config.Budgets.MaxInputFiles};maxFileBytes={config.Budgets.MaxAssemblyBytes}");
                foreach (var gap in inspected.GapKinds) await output.WriteLineAsync("webFormsPreparationGap=" + gap);
                throw Fail("METADATA_INPUT_NOT_UNIQUELY_ADMITTED");
            }
            var bounded = Digest(JsonSerializer.SerializeToUtf8Bytes(new { root, plan.Configuration, plan.Inputs, sourceMembership = membership, metadataInspection = inspected, attestedCommitSha = args[6] }, JsonOptions));
            var generator = plan.GeneratorSha256;
            var bindingRows = outcomes.Where(item => item.Role == "primary").Select(item => new
            {
                schemaVersion = "compiled-input-binding.v1", safeLocator = item.SafeLocator, artifactSha256 = item.RawFileSha256,
                assemblyIdentity = item.AssemblyIdentity, binarySourceRepository = git.RemoteUrl, binarySourceCommitSha = config.SourceCommitSha,
                binaryBuildIdentity = "operator-attested-existing-publish:" + bounded
            }).ToArray();
            if (bindingRows.Length != config.PrimaryAssemblies.Length) throw Fail("PRIMARY_BINDING_COUNT_MISMATCH");
            var bindingDocument = new { schemaVersion = "compiled-input-binding-set.v1", ruleId = RuleId, visibility = "local-only",
                claimLevel = "operator-attested-review-only-not-build-proof", generatorSha256 = generator, boundedInputSha256 = bounded, bindings = bindingRows };
            // Only a new owned staging directory is written. No input-root copy or modification.
            var parent = Path.GetDirectoryName(root)!;
            Directory.CreateDirectory(parent);
            var staging = Path.Combine(parent, ".webforms-preparation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            await WriteJsonAsync(Path.Combine(staging, "compiled-binding.local.json"), bindingDocument, 1_048_576, cancellationToken);
            var publishArtifacts = await WritePublishReceiptsAsync(staging, config, generator, bounded, sourceRows, publishedRows, pages, cancellationToken);
            var validatedOptions = options with { CompiledBindingReceiptPaths = [Path.Combine(staging, "compiled-binding.local.json")],
                WebFormsPublishReceiptPath = Path.Combine(staging, "publish-receipt.local.json"), WebFormsPublishedRootPath = config.PublishedRoot };
            var checkedBindings = ManagedMetadataExtractor.InspectInputs(validatedOptions, config.SourceCommitSha, cancellationToken);
            if (checkedBindings.Provenance?.Outcomes.Count(item => item.Role == "primary" && item.ProvenanceState == "bound") != bindingRows.Length
                || checkedBindings.Provenance.Outcomes.Any(item => item.Role == "dependency" && item.ProvenanceState != "unbound")) throw Fail("GENERATED_BINDING_REJECTED");
            var checkedPublish = WebFormsPublishInputInspector.Inspect(validatedOptions, config.SourceCommitSha, cancellationToken);
            if (checkedPublish?.Status != "bound" || checkedPublish.SourceFileCount != sourceRows.Length
                || checkedPublish.PublishedFileCount != publishedRows.Length || checkedPublish.PageCount != pages.Count)
            {
                await output.WriteLineAsync($"webFormsPreparation=gap;phase=publish;sourceFiles={sourceRows.Length};publishedFiles={publishedRows.Length};pages={pages.Count};partitions={publishArtifacts.Count - 1}");
                foreach (var gap in checkedPublish?.GapKinds ?? []) await output.WriteLineAsync("webFormsPreparationGap=" + gap);
                throw Fail("GENERATED_PUBLISH_RECEIPT_REJECTED");
            }
            if (checkedBindings.Provenance.GeneratorSha256 != inspected.Provenance.GeneratorSha256
                || checkedPublish.GeneratorSha256 != inspected.Provenance.GeneratorSha256) throw Fail("GENERATOR_CHANGED");
            var preparedConfig = config with { ReceiptRoot = root, BindingReceipts = ["compiled-binding.local.json"],
                PublishReceiptRelativePath = "publish-receipt.local.json", PublishSourceRelativePaths = null,
                PreparationProvenance = new(RuleId, generator, bounded) };
            var publishInputs = plan.Inputs.Count(input => WebFormsReviewPreflightCommand.IsPublishInventoryRole(input.Role));
            var partitionInputs = publishArtifacts.Count - 1;
            if (config.Budgets.MaxPublishInputFiles is { } publishMaximum
                ? publishInputs + partitionInputs > publishMaximum || plan.Inputs.Count - publishInputs + 2 > config.Budgets.MaxInputFiles
                : plan.Inputs.Count + 2 + partitionInputs > config.Budgets.MaxInputFiles) throw Fail("FOLLOW_ON_INPUT_COUNT_LIMIT");
            await WriteJsonAsync(Path.Combine(staging, "review-config.local.json"), preparedConfig, 1_048_576, cancellationToken);
            var artifacts = new List<WebFormsReviewArtifact>();
            foreach (var name in new[] { "compiled-binding.local.json", "review-config.local.json" }.Concat(publishArtifacts).Order(StringComparer.Ordinal))
            {
                var item = await WebFormsReviewPreflightCommand.HashAsync("prepared-artifact", Path.Combine(staging, name), 1_048_576, cancellationToken);
                artifacts.Add(new(name, item.Bytes, item.Sha256));
            }
            var followOnHashBytes = plan.Inputs.Where(input => input.Role != "configuration").Sum(input => input.Bytes)
                + artifacts.Sum(artifact => artifact.Bytes);
            if (followOnHashBytes > config.Budgets.MaxTotalHashBytes) throw Fail("FOLLOW_ON_HASH_BYTES_LIMIT");
            var gaps = checkedBindings.GapKinds.Concat(plan.Gaps.Where(gap => gap is not
                    ("BindingValidationDeferred" or "MissingBindingReceiptHashCandidate" or "AmbiguousBindingReceiptHashCandidate" or "PageMapValidationDeferred")))
                .Concat(["CompilerProvenanceUnavailable", "BuildAuthenticityNotEstablished",
                "SourceLineIdentityNotEstablished", "DeclaredPublishInventoryNotHistoricalBuildClosure"])
                .Concat(config.PageMode == "all" ? ["AllPagesPublicationCompletenessNotEstablished"] : Array.Empty<string>())
                .Concat(membership.Any(item => item.ComparisonKind == "git-built-in-eol-normalized") ? ["GitBuiltInLineEndingNormalizationUsed"] : Array.Empty<string>())
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var receipt = new WebFormsReviewPreparationManifest(Schema, RuleId, EvidenceTiers.Tier2Structural, "local-only",
                "operator-attested-review-only-not-build-proof", generator, bounded, config.SourceCommitSha,
                "explicit-exact-source-commit-for-primary-assemblies", plan.Inputs, membership, checkedBindings, checkedPublish, artifacts, gaps,
                ["Primary bindings record the operator's declaration, not proof of build freshness, authenticity, runtime loading, dispatch or SQL execution.",
                 "Dependencies remain unbound artifact context. Only explicitly declared source, DLL and map membership is inspected; historical build closure is unknown.",
                 "All mode covers the declared receipt pages, not every page in the source repository. Deterministic partitions retain 256 source files, 64 published files and 32 page limits; inventory-only partitions add context, never pages.",
                 "Compiler SHA is the documented unavailable-marker digest, not a claimed compiler binary hash. This local artifact is private and not portable or shareable."]);
            await WriteJsonAsync(Path.Combine(staging, "preparation-manifest.local.json"), receipt, 4_194_304, cancellationToken);
            await WebFormsReviewInputValidation.RecheckAsync(plan, cancellationToken);
            var membershipAfter = await ValidateCommittedSourceAsync(config, sourcePaths, cancellationToken);
            if (!membership.SequenceEqual(membershipAfter)) throw Fail("SOURCE_IDENTITY_CHANGED");
            var gitAfter = GitMetadataProvider.Detect(config.SourceRoot);
            if (gitAfter.CommitSha != git.CommitSha || gitAfter.RemoteUrl != git.RemoteUrl || gitAfter.GitRootPath != git.GitRootPath
                || gitAfter.ScanRootRelativePath != git.ScanRootRelativePath) throw Fail("SOURCE_IDENTITY_CHANGED");
            var generatorAfter = await WebFormsReviewPreflightCommand.HashAsync("generator", typeof(WebFormsReviewPreparationCommand).Assembly.Location, 268_435_456, cancellationToken);
            if (generatorAfter.Sha256 != generator) throw Fail("GENERATOR_CHANGED");
            var coreAfter = await WebFormsReviewPreflightCommand.HashAsync("core-generator", typeof(ManagedMetadataExtractor).Assembly.Location, 268_435_456, cancellationToken);
            if (coreAfter.Sha256 != inspected.Provenance.GeneratorSha256) throw Fail("GENERATOR_CHANGED");
            if (root != WebFormsReviewPreflightCommand.PhysicalPath(root) || Directory.Exists(root) || File.Exists(root)) throw Fail("OUTPUT_CHANGED");
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(staging, root);
            await output.WriteLineAsync($"webFormsPreparation=completed;primaryBindings={bindingRows.Length};contextAssemblies={config.DependencyAssemblies.Length};pages={pages.Count};reviewOnly=true");
            await output.WriteLineAsync($"webFormsPublishInventory=retained;sourceFiles={sourceRows.Length};publishedFiles={publishedRows.Length};partitions={partitionInputs};perPartitionSources=256;perPartitionPublished=64;perPartitionPages=32");
            await output.WriteLineAsync($"webFormsPreparedConfig={Path.Combine(root, "review-config.local.json")}");
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (WebFormsReviewPreflightCommand.PreflightException exception) { await error.WriteLineAsync("error: " + exception.Code); return 1; }
        catch (PreparationException exception) { await error.WriteLineAsync("error: " + exception.Message); return 1; }
        catch (Exception) { await error.WriteLineAsync("error: WEBFORMS_PREPARATION_INPUT_OR_OUTPUT_INVALID"); return 1; }
    }

    internal static async Task<IReadOnlyList<WebFormsReviewSourceMembership>> ValidateCommittedSourceAsync(WebFormsReviewConfig config, string[] sources, CancellationToken token)
    {
        var tree = (await GitAsync(config.SourceRoot, ["ls-tree", "-r", "-z", "HEAD", "--", "."], token)).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var tracked = new Dictionary<string, (string Path, string ObjectId)>(Paths);
        foreach (var entry in tree)
        {
            var separator = entry.IndexOf('\t');
            if (separator < 0) throw Fail("GIT_TREE_INVALID");
            var header = entry[..separator].Split(' ');
            if (header.Length != 3) throw Fail("GIT_TREE_INVALID");
            if (header[1] != "blob") continue;
            var objectId = header[2];
            if (objectId.Length is not (40 or 64) || !objectId.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')) throw Fail("GIT_TREE_INVALID");
            var name = entry[(separator + 1)..];
            if (!tracked.TryAdd(name, (name, objectId))) throw Fail("SOURCE_TRACKING_AMBIGUOUS");
        }
        if (sources.Any(source => !tracked.ContainsKey(source))) throw Fail("SOURCE_MEMBERSHIP_NOT_COMMITTED");
        var memberships = new List<WebFormsReviewSourceMembership>();
        foreach (var source in sources)
        {
            var committed = tracked[source];
            var path = WebFormsReviewPreflightCommand.Child(config.SourceRoot, source);
            var raw = await BlobHashAsync(path, committed.ObjectId.Length, null, token);
            if (raw.Hash == committed.ObjectId)
                memberships.Add(new(source, committed.Path, committed.ObjectId, "exact-git-blob-bytes"));
            else
            {
                // Only Git's built-in CRLF normalization is supported. Never execute
                // arbitrary clean filters to manufacture a commit-byte match.
                var attributes = (await GitAsync(config.SourceRoot,
                    ["check-attr", "-z", "text", "filter", "working-tree-encoding", "--", committed.Path], token)).Split('\0', StringSplitOptions.RemoveEmptyEntries);
                if (attributes.Length != 9 || attributes[1] != "text" || attributes[4] != "filter" || attributes[7] != "working-tree-encoding") throw Fail("GIT_ATTRIBUTES_INVALID");
                var text = attributes[2]; var filter = attributes[5]; var encoding = attributes[8];
                if (filter is not ("unspecified" or "unset") || encoding is not ("unspecified" or "unset")) throw Fail("SOURCE_TRANSFORM_UNSUPPORTED");
                var autocrlf = (await GitAsync(config.SourceRoot, ["config", "--get", "core.autocrlf"], token, allowMissing: true)).Trim().ToLowerInvariant();
                if (raw.HasNul || raw.CrlfCount == 0 || !(text is "set" or "auto" || text == "unspecified" && autocrlf is "true" or "input"))
                    throw Fail("SOURCE_COMMIT_BYTES_MISMATCH");
                var normalized = await BlobHashAsync(path, committed.ObjectId.Length, raw.Length - raw.CrlfCount, token);
                if (normalized.Hash != committed.ObjectId) throw Fail("SOURCE_COMMIT_BYTES_MISMATCH");
                var policy = Digest(JsonSerializer.SerializeToUtf8Bytes(new { text, filter, encoding, autocrlf }, JsonOptions));
                memberships.Add(new(source, committed.Path, committed.ObjectId, "git-built-in-eol-normalized", policy));
            }
        }
        if ((await GitAsync(config.SourceRoot, ["status", "--porcelain=v1", "--untracked-files=all", "-z", "--", "."], token)).Length != 0)
            throw Fail("SOURCE_DIRTY");
        return memberships;
    }

    private static async Task<(string Hash, long Length, long CrlfCount, bool HasNul)> BlobHashAsync(string path, int objectIdLength,
        long? normalizedLength, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var length = stream.Length;
        if (length > 67_108_864) throw Fail("SOURCE_BYTES_LIMIT");
        using var hash = IncrementalHash.CreateHash(objectIdLength == 40 ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.ASCII.GetBytes("blob " + (normalizedLength ?? length).ToString(System.Globalization.CultureInfo.InvariantCulture) + "\0"));
        var buffer = new byte[65_536]; var normalized = new byte[65_537];
        long readBytes = 0, crlf = 0, normalizedBytes = 0;
        var previousCr = false; var pendingCr = false; var hasNul = false; int read;
        while ((read = await stream.ReadAsync(buffer, token)) > 0)
        {
            readBytes += read;
            if (readBytes > 67_108_864) throw Fail("SOURCE_BYTES_LIMIT");
            var written = 0;
            foreach (var value in buffer.AsSpan(0, read))
            {
                if (value == 0) hasNul = true;
                if (previousCr && value == 10) crlf++;
                previousCr = value == 13;
                if (normalizedLength is null) continue;
                if (pendingCr && value != 10) normalized[written++] = 13;
                pendingCr = value == 13;
                if (!pendingCr) normalized[written++] = value;
            }
            if (normalizedLength is null) hash.AppendData(buffer, 0, read);
            else { hash.AppendData(normalized, 0, written); normalizedBytes += written; }
        }
        if (normalizedLength is not null && pendingCr) { hash.AppendData([13]); normalizedBytes++; }
        if (readBytes != length || normalizedLength is not null && normalizedBytes != normalizedLength) throw Fail("INPUT_CHANGED");
        return (Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), length, crlf, hasNul);
    }

    private static async Task<string> GitAsync(string root, string[] args, CancellationToken token, bool allowMissing = false)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var process = new Process { StartInfo = new("git") { WorkingDirectory = root, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true } };
        process.StartInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0"; // Status must not refresh/write the input repository's index.
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        using var registration = timeout.Token.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } });
        try
        {
            var stdout = ReadAsync(process.StandardOutput, timeout.Token);
            var stderr = ReadAsync(process.StandardError, timeout.Token);
            await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(timeout.Token));
            if (allowMissing && process.ExitCode == 1) return string.Empty;
            if (process.ExitCode != 0) throw Fail("GIT_CHECK_FAILED");
            return await stdout;
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }

        static async Task<string> ReadAsync(StreamReader reader, CancellationToken cancellation)
        {
            var text = new StringBuilder(); var buffer = new char[8192]; int read;
            while ((read = await reader.ReadAsync(buffer, cancellation)) > 0)
            {
                if (text.Length > 4_194_304 - read) throw Fail("GIT_OUTPUT_LIMIT");
                text.Append(buffer, 0, read);
            }
            return text.ToString();
        }
    }

    private static async Task WriteJsonAsync(string path, object value, int limit, CancellationToken token)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        if (bytes.Length >= limit) throw Fail("OUTPUT_BYTES_LIMIT");
        await File.WriteAllBytesAsync(path, [.. bytes, (byte)'\n'], token);
    }
    private static bool Within(string root, string path) => Paths.Equals(root, path) || path.StartsWith(root + Path.DirectorySeparatorChar,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static bool SafeVirtual(string? path)
    {
        if (path is null || path.Length < 2 || !path.StartsWith('/') || path.Contains('\\')
            || path.IndexOfAny([':', '?', '#', '%']) >= 0) return false;
        // Permit exactly one trailing directory separator, without normalizing away
        // empty interior segments, traversal, or an empty/root-only virtual path.
        var segments = path.EndsWith('/') ? path[1..^1] : path[1..];
        return segments.Split('/').All(part => part is not ("" or "." or ".."));
    }
    private static string Normalize(string path) => path.Replace('\\', '/');
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static PreparationException Fail(string suffix) => new("WEBFORMS_PREPARATION_" + suffix);
    internal sealed class PreparationException(string code) : Exception(code);
    private static IEnumerable<(Map Map, string Prefix)> PageMapMatches(IEnumerable<Map> maps, string page) =>
        maps.Where(map => map.VirtualPath.EndsWith("/" + page, StringComparison.OrdinalIgnoreCase))
            .Select(map => (map, map.VirtualPath[..^(page.Length + 1)]));

    /// <summary>
    /// A suffix match alone is ambiguous when a root page and a nested page share a file
    /// name (`Default.aspx` and `Admin/Default.aspx`). Select the single application
    /// virtual prefix under which every source page with any map candidate has exactly
    /// one map; return null when no page is ambiguous, and fail closed when no unique
    /// prefix explains the maps.
    /// </summary>
    private static string? SelectApplicationPrefix(IReadOnlyList<Map> maps, IReadOnlyList<string> pages)
    {
        var candidates = pages.Select(page => PageMapMatches(maps, page).ToArray()).Where(items => items.Length > 0).ToArray();
        if (candidates.All(items => items.Length == 1)) return null;
        var consistent = candidates.SelectMany(items => items.Select(item => item.Prefix))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(prefix => candidates.All(items => items.Count(item => item.Prefix.Equals(prefix, StringComparison.OrdinalIgnoreCase)) == 1))
            .ToArray();
        return consistent.Length == 1 ? consistent[0] : throw Fail("PAGE_MAP_NOT_UNIQUE");
    }

    private sealed record SourceFile(string Path, string Sha256);
    private sealed record PublishedFile(string Path, string Sha256, string Kind);
    private sealed record Map(string Path, string VirtualPath, string? Assembly, string? GeneratedType);
    private sealed record Page(string VirtualPath, string SourcePath, string? Assembly, string? GeneratedType, string? MapPath, string? BindingKind);
}
