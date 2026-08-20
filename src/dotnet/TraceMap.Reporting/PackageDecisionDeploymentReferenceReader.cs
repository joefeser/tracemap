using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record PackageDecisionDeploymentReference(
    string ReferenceId,
    string ReferenceKind,
    string Ecosystem,
    string PackageName,
    string ArtifactVersion,
    string? RegistryOrigin,
    string? ArtifactDigestAlgorithm,
    string? ArtifactDigest,
    string ProducerId,
    string ProducerVersion,
    string? SourceRepoHash,
    string? CommitSha,
    string ReferenceDigest);

public sealed record PackageDecisionDeploymentReferenceAdmission(
    IReadOnlyList<PackageDecisionDeploymentReference> References,
    IReadOnlyList<PackageDecisionInputGap> Gaps,
    bool Accepted,
    string? EnvelopeDigest = null);

/// <summary>
/// Reads the closed <c>package-deployment-reference.v1</c> external reference contract. References
/// are runtime-unproven lineage metadata; they never create facts, upgrade rungs, or count as matches.
/// Runtime-load or observed-execution claims are rejected by the closed vocabulary.
/// </summary>
public static partial class PackageDecisionDeploymentReferenceReader
{
    public const string SchemaVersion = "package-deployment-reference.v1";
    public const string RuleId = PackageDecisionCorrelationReporter.RuleId;
    public const string EvidenceTier = EvidenceTiers.Tier4Unknown;
    public const int MaxReferences = 200;

    private static readonly HashSet<string> Ecosystems = new(StringComparer.Ordinal) { "nuget", "npm", "python", "maven", "gradle", "swift" };
    private static readonly HashSet<string> ReferenceKinds = new(StringComparer.Ordinal) { "build-attachment", "deployment-manifest" };
    private static readonly HashSet<string> Algorithms = new(StringComparer.Ordinal) { "sha256", "sha512-base64" };

    public static async Task<PackageDecisionDeploymentReferenceAdmission> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A package deployment reference path is required.", nameof(path));

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            return Read(json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return Failure("DecisionInputReadFailed", "The package deployment reference input could not be read.");
        }
    }

    public static PackageDecisionDeploymentReferenceAdmission Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Failure("DecisionInputReadFailed", "The package deployment reference input was empty.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
        }
        catch (JsonException)
        {
            return Failure("DecisionInputReadFailed", "The package deployment reference input was not valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Failure("DecisionInputSchemaUnsupported", "The package deployment reference envelope is not an object.");

            if (!root.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(["version", "producer", "references"]))
                return Failure("DecisionInputSchemaUnsupported", "The package deployment reference envelope has unsupported properties.");

            if (!root.TryGetProperty("version", out var versionElement) || versionElement.ValueKind != JsonValueKind.String || !string.Equals(versionElement.GetString(), SchemaVersion, StringComparison.Ordinal))
                return Failure("DecisionInputSchemaUnsupported", "The package deployment reference envelope version is unsupported.");

            if (!root.TryGetProperty("producer", out var producer) || producer.ValueKind != JsonValueKind.Object
                || !producer.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(["id", "version"]))
                return Failure("DecisionInputSchemaUnsupported", "The package deployment reference producer block is invalid.");

            string producerId;
            string producerVersion;
            try
            {
                producerId = Required(producer, "id", SafeProducerId);
                producerVersion = Required(producer, "version", SafeProducerVersion);
            }
            catch (ValidationException exception)
            {
                return Failure(exception.Classification, exception.Message);
            }

            if (!root.TryGetProperty("references", out var referencesElement) || referencesElement.ValueKind != JsonValueKind.Array || referencesElement.GetArrayLength() == 0)
                return Failure("DecisionInputMalformed", "The package deployment reference envelope requires a non-empty references array.");

            if (referencesElement.GetArrayLength() > MaxReferences)
                return Failure("DecisionInputLimitReached", "The package deployment reference envelope exceeds the reference limit.");

            var accepted = new List<(PackageDecisionDeploymentReference Reference, string Key, string Digest, int Ordinal)>();
            var gaps = new List<PackageDecisionInputGap>();
            var ordinal = 0;
            foreach (var element in referencesElement.EnumerateArray())
            {
                var candidate = ParseReference(element, producerId, producerVersion, ordinal++);
                if (candidate.Reference is not null)
                    accepted.Add((candidate.Reference, $"{producerId}\u001f{candidate.Reference.ReferenceId}", candidate.Reference.ReferenceDigest, candidate.Ordinal));
                else if (candidate.Gap is not null)
                    gaps.Add(candidate.Gap);
            }

            if (gaps.Any(gap => gap.Classification == "DecisionInputLimitReached"))
                return new PackageDecisionDeploymentReferenceAdmission([], gaps.OrderBy(gap => gap.GapId, StringComparer.Ordinal).ToArray(), false, CanonicalJsonDigest.Compute(json));

            foreach (var group in accepted.GroupBy(item => item.Key, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var distinct = group.Select(item => item.Digest).Distinct(StringComparer.Ordinal).ToArray();
                if (distinct.Length == 1)
                {
                    var first = group.OrderBy(item => item.Ordinal).First();
                    accepted.RemoveAll(item => item.Key == group.Key && item.Ordinal != first.Ordinal);
                    foreach (var duplicate in group.Where(item => item.Ordinal != first.Ordinal).OrderBy(item => item.Ordinal))
                        gaps.Add(Gap("DecisionInputDuplicateConflict", "An identical package deployment reference was deterministically deduplicated.", duplicate.Reference.ReferenceId, producerId, $"duplicate-identical:{duplicate.Reference.ReferenceDigest}"));
                }
                else
                {
                    accepted.RemoveAll(item => item.Key == group.Key);
                    foreach (var duplicate in group.OrderBy(item => item.Ordinal))
                        gaps.Add(Gap("DecisionInputDuplicateConflict", "Conflicting package deployment references shared one producer-scoped identity; none were admitted.", duplicate.Reference.ReferenceId, producerId, $"duplicate-conflict:{duplicate.Reference.ReferenceDigest}"));
                }
            }

            var references = accepted
                .OrderBy(item => item.Reference.Ecosystem, StringComparer.Ordinal)
                .ThenBy(item => item.Reference.PackageName, StringComparer.Ordinal)
                .ThenBy(item => item.Reference.ReferenceId, StringComparer.Ordinal)
                .Select(item => item.Reference)
                .ToArray();
            return new PackageDecisionDeploymentReferenceAdmission(references, gaps.OrderBy(gap => gap.GapId, StringComparer.Ordinal).ToArray(), true, CanonicalJsonDigest.Compute(json));
        }
    }

    private static ReferenceCandidate ParseReference(JsonElement element, string producerId, string producerVersion, int ordinal)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return new ReferenceCandidate(null, Gap("DecisionInputMalformed", "A package deployment reference was not an object.", null, producerId, $"reference-{ordinal:D4}"), ordinal);

        if (!element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).IsSubsetOf(["referenceId", "referenceKind", "ecosystem", "packageName", "artifactVersion", "registryOrigin", "artifactDigestAlgorithm", "artifactDigest", "sourceRepo", "commitSha"]))
            return new ReferenceCandidate(null, Gap("DecisionInputMalformed", "A package deployment reference has unsupported properties; runtime observation, environment, command, and free-text fields are not part of package-deployment-reference.v1.", null, producerId, $"reference-{ordinal:D4}"), ordinal);

        try
        {
            var referenceId = Required(element, "referenceId", SafeReferenceId);
            var referenceKind = Required(element, "referenceKind", value => value.Length <= 64);
            if (!ReferenceKinds.Contains(referenceKind))
                return new ReferenceCandidate(null, Gap("DecisionInputMalformed", "A package deployment reference kind is unsupported; runtime-load and observed-execution claims are rejected because TraceMap cannot prove runtime loads.", referenceId, producerId, $"reference-{ordinal:D4}"), ordinal);
            var ecosystem = Required(element, "ecosystem", value => Ecosystems.Contains(value));
            var packageName = Required(element, "packageName", value => value.Length <= 128);
            if (!SafePackageName(packageName))
                return new ReferenceCandidate(null, Gap("DecisionInputIdentityUnsafe", "A package deployment reference identity contained unsafe package-name material.", referenceId, producerId, $"reference-{ordinal:D4}"), ordinal);
            var version = Required(element, "artifactVersion", value => value.Length <= 128);
            if (!SafeArtifactVersion(version))
            {
                var unsafeVersion = version.Contains("://", StringComparison.Ordinal) || version.StartsWith("/", StringComparison.Ordinal) || version.StartsWith("./", StringComparison.Ordinal) || version.StartsWith("../", StringComparison.Ordinal) || version.Contains("${", StringComparison.Ordinal) || version.Contains('@', StringComparison.Ordinal);
                return new ReferenceCandidate(null, Gap(unsafeVersion ? "DecisionInputIdentityUnsafe" : "DecisionInputMalformed", "A package deployment reference version did not satisfy its safe exact-version shape.", referenceId, producerId, $"reference-{ordinal:D4}"), ordinal);
            }

            var origin = Optional(element, "registryOrigin");
            if (origin is not null && !SafeOrigin(origin))
                return new ReferenceCandidate(null, Gap("DecisionInputIdentityUnsafe", "A package deployment reference identity contained unsafe origin material.", referenceId, producerId, $"reference-{ordinal:D4}"), ordinal);
            var algorithm = Optional(element, "artifactDigestAlgorithm");
            var digest = Optional(element, "artifactDigest");
            if ((algorithm is null) != (digest is null) || (algorithm is not null && !Algorithms.Contains(algorithm)))
                return new ReferenceCandidate(null, Gap("DecisionInputMalformed", "A package deployment reference digest pair was invalid.", referenceId, producerId, $"reference-{ordinal:D4}"), ordinal);
            if (algorithm == "sha256" && (digest!.Length != 64 || !Sha256().IsMatch(digest)))
                return new ReferenceCandidate(null, Gap("DecisionInputMalformed", "A package deployment reference SHA-256 digest was invalid.", referenceId, producerId, $"reference-{ordinal:D4}"), ordinal);
            if (algorithm == "sha512-base64" && !IsSha512Base64(digest!))
                return new ReferenceCandidate(null, Gap("DecisionInputMalformed", "A package deployment reference SHA-512 digest was invalid.", referenceId, producerId, $"reference-{ordinal:D4}"), ordinal);

            string? sourceRepoHash = null;
            if (element.TryGetProperty("sourceRepo", out var sourceRepoElement))
            {
                var sourceRepo = Required(element, "sourceRepo", value => value.Length <= 256 && !value.Contains('@', StringComparison.Ordinal));
                sourceRepoHash = $"repo-hash:{CombinedReportHelpers.Hash(sourceRepo, 16)}";
            }

            string? commitSha = null;
            if (element.TryGetProperty("commitSha", out var commitShaElement))
                commitSha = Required(element, "commitSha", value => Sha().IsMatch(value)).ToLowerInvariant();

            var reference = new PackageDecisionDeploymentReference(referenceId, referenceKind, ecosystem, packageName, version, origin, algorithm, digest, producerId, producerVersion, sourceRepoHash, commitSha, CanonicalJsonDigest.Compute(element.GetRawText()));
            return new ReferenceCandidate(reference, null, ordinal);
        }
        catch (ValidationException exception)
        {
            return new ReferenceCandidate(null, Gap(exception.Classification, exception.Message, null, producerId, $"reference-{ordinal:D4}"), ordinal);
        }
        catch (Exception)
        {
            return new ReferenceCandidate(null, Gap("DecisionInputMalformed", "A package deployment reference did not satisfy the strict v1 shape.", null, producerId, $"reference-{ordinal:D4}"), ordinal);
        }
    }

    private static PackageDecisionDeploymentReferenceAdmission Failure(string classification, string message) =>
        new([], [Gap(classification, message, null, null, "envelope")], false);

    private static PackageDecisionInputGap Gap(string classification, string message, string? referenceId, string? producerId, string discriminator) =>
        new($"pd-deployref:{CombinedReportHelpers.Hash(string.Join('\u001f', classification, producerId ?? "unknown", referenceId ?? "unknown", discriminator), 24)}",
            classification, message, RuleId, EvidenceTier, SafeMetadata(referenceId), SafeMetadata(producerId));

    private static string? SafeMetadata(string? value) => value is not null && SafeReferenceId(value) ? value : null;

    private static string Required(JsonElement element, string name, Func<string, bool> predicate)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw new ValidationException("DecisionInputMalformed", "A required package deployment reference field was missing or invalid.");
        var result = value.GetString();
        if (string.IsNullOrWhiteSpace(result) || result.Length > 256)
            throw new ValidationException("DecisionInputLimitReached", "A package deployment reference field exceeded its bound.");
        if (!predicate(result))
            throw new ValidationException("DecisionInputMalformed", "A package deployment reference field did not satisfy its closed shape.");
        return result;
    }

    private static string? Optional(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())) throw new ValidationException("DecisionInputMalformed", "An optional package deployment reference field was invalid.");
        var result = value.GetString()!;
        if (result.Length > 256) throw new ValidationException("DecisionInputLimitReached", "A package deployment reference field exceeded its bound.");
        return result;
    }

    private static bool SafeReferenceId(string value) => ReferenceId().IsMatch(value);
    private static bool SafeProducerId(string value) => ProducerId().IsMatch(value);
    private static bool SafeProducerVersion(string value) => ProducerVersion().IsMatch(value);
    private static bool SafePackageName(string value) => (PackageName().IsMatch(value) || NpmPackageName().IsMatch(value)) && !value.Contains("://", StringComparison.Ordinal);
    private static bool SafeArtifactVersion(string value) => Version().IsMatch(value) && !value.Contains("://", StringComparison.Ordinal) && !value.StartsWith("git+", StringComparison.OrdinalIgnoreCase) && !value.Contains("${", StringComparison.Ordinal) && !value.Contains('@', StringComparison.Ordinal) && !value.StartsWith("/", StringComparison.Ordinal) && !value.StartsWith("./", StringComparison.Ordinal) && !value.StartsWith("../", StringComparison.Ordinal) && !value.Contains('^') && !value.Contains('>') && !value.Contains('<');
    private static bool SafeOrigin(string value) => value == "unknown" || Origin().IsMatch(value);
    private static bool IsSha512Base64(string value)
    {
        if (value.Length > 128 || !Base64().IsMatch(value)) return false;
        try
        {
            return Convert.FromBase64String(value).Length == 64;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._:-]{3,80}$", RegexOptions.CultureInvariant)] private static partial Regex ReferenceId();
    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{1,64}$", RegexOptions.CultureInvariant)] private static partial Regex ProducerId();
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._+-]{0,31}$", RegexOptions.CultureInvariant)] private static partial Regex ProducerVersion();
    [GeneratedRegex("^[A-Za-z0-9_][A-Za-z0-9_.$+-]*(?::[A-Za-z0-9_.$+-]+)?$", RegexOptions.CultureInvariant)] private static partial Regex PackageName();
    [GeneratedRegex("^@[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)] private static partial Regex NpmPackageName();
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._+~]{0,127}$", RegexOptions.CultureInvariant)] private static partial Regex Version();
    [GeneratedRegex("^[A-Za-z0-9.-]+(?::[0-9]{1,5})?$", RegexOptions.CultureInvariant)] private static partial Regex Origin();
    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.CultureInvariant)] private static partial Regex Sha256();
    [GeneratedRegex("^(?:[a-f0-9]{40}|[a-f0-9]{64})$", RegexOptions.CultureInvariant)] private static partial Regex Sha();
    [GeneratedRegex("^[A-Za-z0-9+/]+={0,2}$", RegexOptions.CultureInvariant)] private static partial Regex Base64();

    private sealed record ReferenceCandidate(PackageDecisionDeploymentReference? Reference, PackageDecisionInputGap? Gap, int Ordinal);
    private sealed class ValidationException(string classification, string message) : Exception(message)
    {
        public string Classification { get; } = classification;
    }
}
