using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record PackageDecisionAdvisoryClaim(
    string ClaimId,
    string ClaimKind,
    string Ecosystem,
    string PackageName,
    string VersionPredicateKind,
    string? VersionPredicateVersion,
    string Framework,
    string ProducerId,
    string ProducerVersion,
    string ProfileDigest,
    string RuleId,
    string EvidenceTier);

public sealed record PackageDecisionAdvisoryProfile(
    IReadOnlyList<PackageDecisionAdvisoryClaim> Claims,
    IReadOnlyList<PackageDecisionInputGap> Gaps,
    bool Accepted,
    string? ProfileDigest = null);

/// <summary>
/// Reads the closed <c>advisory-profile.v1</c> external claim contract. Claims are bounded,
/// versioned producer opinions; they never become TraceMap facts and never alter correlation.
/// </summary>
public static partial class PackageDecisionAdvisoryProfileReader
{
    public const string SchemaVersion = "advisory-profile.v1";
    public const string RuleId = "package.decision.advisory.v1";
    public const string ClaimEvidenceTier = EvidenceTiers.Tier3SyntaxOrTextual;
    public const int MaxClaims = 200;

    private static readonly HashSet<string> Ecosystems = new(StringComparer.Ordinal) { "nuget", "npm", "python", "maven", "gradle", "swift" };
    private static readonly HashSet<string> ClaimKinds = new(StringComparer.Ordinal) { "framework-implied-server-surface" };
    private static readonly HashSet<string> PredicateKinds = new(StringComparer.Ordinal) { "exact", "any" };

    public static async Task<PackageDecisionAdvisoryProfile> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("An advisory profile path is required.", nameof(path));

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            return Read(json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return Failure("DecisionInputReadFailed", "The advisory profile input could not be read.");
        }
    }

    public static PackageDecisionAdvisoryProfile Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Failure("DecisionInputReadFailed", "The advisory profile input was empty.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
        }
        catch (JsonException)
        {
            return Failure("DecisionInputReadFailed", "The advisory profile input was not valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Failure("DecisionInputSchemaUnsupported", "The advisory profile envelope is not an object.");

            if (!root.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(["version", "producer", "claims"]))
                return Failure("DecisionInputSchemaUnsupported", "The advisory profile envelope has unsupported properties.");

            if (!root.TryGetProperty("version", out var versionElement) || versionElement.ValueKind != JsonValueKind.String || !string.Equals(versionElement.GetString(), SchemaVersion, StringComparison.Ordinal))
                return Failure("DecisionInputSchemaUnsupported", "The advisory profile envelope version is unsupported.");

            if (!root.TryGetProperty("producer", out var producer) || producer.ValueKind != JsonValueKind.Object
                || !producer.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(["id", "version"]))
                return Failure("DecisionInputSchemaUnsupported", "The advisory profile producer block is invalid.");

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

            if (!root.TryGetProperty("claims", out var claimsElement) || claimsElement.ValueKind != JsonValueKind.Array || claimsElement.GetArrayLength() == 0)
                return Failure("DecisionInputMalformed", "The advisory profile requires a non-empty claims array.");

            if (claimsElement.GetArrayLength() > MaxClaims)
                return Failure("DecisionInputLimitReached", "The advisory profile exceeds the claim limit.");

            var profileDigest = CanonicalJsonDigest.Compute(json);
            var claims = new List<PackageDecisionAdvisoryClaim>();
            var gaps = new List<PackageDecisionInputGap>();
            var ordinal = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in claimsElement.EnumerateArray())
            {
                var candidate = ParseClaim(element, producerId, producerVersion, profileDigest, ordinal++);
                if (candidate.Claim is not null)
                {
                    if (!seen.Add(candidate.Claim.ClaimId))
                    {
                        gaps.Add(Gap("DecisionInputDuplicateConflict", "An advisory claim identifier was repeated within one producer profile.", candidate.Claim.ClaimId, producerId, $"duplicate:{ordinal:D4}"));
                        continue;
                    }

                    claims.Add(candidate.Claim);
                }
                else if (candidate.Gap is not null)
                {
                    gaps.Add(candidate.Gap);
                }
            }

            if (gaps.Any(gap => gap.Classification == "DecisionInputLimitReached"))
                return new PackageDecisionAdvisoryProfile([], gaps.OrderBy(gap => gap.GapId, StringComparer.Ordinal).ToArray(), false, profileDigest);

            return new PackageDecisionAdvisoryProfile(
                claims.OrderBy(claim => claim.Ecosystem, StringComparer.Ordinal).ThenBy(claim => claim.PackageName, StringComparer.Ordinal).ThenBy(claim => claim.ClaimId, StringComparer.Ordinal).ToArray(),
                gaps.OrderBy(gap => gap.GapId, StringComparer.Ordinal).ToArray(),
                true,
                profileDigest);
        }
    }

    private static ClaimCandidate ParseClaim(JsonElement element, string producerId, string producerVersion, string profileDigest, int ordinal)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return new ClaimCandidate(null, Gap("DecisionInputMalformed", "An advisory claim was not an object.", null, producerId, $"claim-{ordinal:D4}"));

        if (!element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(["claimId", "ecosystem", "packageName", "versionPredicate", "claimKind", "claimParams"]))
            return new ClaimCandidate(null, Gap("DecisionInputMalformed", "An advisory claim has unsupported properties; severity, vulnerability, remediation, runtime, and trust-shaped fields are not part of advisory-profile.v1.", null, producerId, $"claim-{ordinal:D4}"));

        try
        {
            var claimId = Required(element, "claimId", SafeClaimId);
            var ecosystem = Required(element, "ecosystem", value => Ecosystems.Contains(value));
            var packageName = Required(element, "packageName", value => value.Length <= 128);
            if (!SafePackageName(packageName))
                return new ClaimCandidate(null, Gap("DecisionInputIdentityUnsafe", "An advisory claim identity contained unsafe package-name material.", claimId, producerId, $"claim-{ordinal:D4}"));

            if (!element.TryGetProperty("versionPredicate", out var predicate) || predicate.ValueKind != JsonValueKind.Object)
                return new ClaimCandidate(null, Gap("DecisionInputMalformed", "An advisory claim version predicate was invalid.", claimId, producerId, $"claim-{ordinal:D4}"));
            var predicateProperties = predicate.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
            var kind = Required(predicate, "kind", value => PredicateKinds.Contains(value));
            string? predicateVersion = null;
            if (kind == "exact")
            {
                if (!predicateProperties.SetEquals(["kind", "version"]))
                    return new ClaimCandidate(null, Gap("DecisionInputMalformed", "An exact advisory predicate requires exactly one version field.", claimId, producerId, $"claim-{ordinal:D4}"));
                predicateVersion = Required(predicate, "version", SafePredicateVersion);
            }
            else if (!predicateProperties.SetEquals(["kind"]))
            {
                return new ClaimCandidate(null, Gap("DecisionInputMalformed", "An any advisory predicate carries no version field.", claimId, producerId, $"claim-{ordinal:D4}"));
            }

            var claimKind = Required(element, "claimKind", value => ClaimKinds.Contains(value));
            if (!element.TryGetProperty("claimParams", out var parameters) || parameters.ValueKind != JsonValueKind.Object
                || !parameters.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(["framework"]))
                return new ClaimCandidate(null, Gap("DecisionInputMalformed", "An advisory claim parameters block was invalid; framework-implied-server-surface allows only a framework parameter.", claimId, producerId, $"claim-{ordinal:D4}"));
            var framework = Required(parameters, "framework", SafeFramework);

            return new ClaimCandidate(new PackageDecisionAdvisoryClaim(claimId, claimKind, ecosystem, packageName, kind, predicateVersion, framework, producerId, producerVersion, profileDigest, RuleId, ClaimEvidenceTier), null);
        }
        catch (ValidationException exception)
        {
            return new ClaimCandidate(null, Gap(exception.Classification, exception.Message, null, producerId, $"claim-{ordinal:D4}"));
        }
        catch (Exception)
        {
            return new ClaimCandidate(null, Gap("DecisionInputMalformed", "An advisory claim did not satisfy the strict v1 shape.", null, producerId, $"claim-{ordinal:D4}"));
        }
    }

    private static PackageDecisionAdvisoryProfile Failure(string classification, string message) =>
        new([], [Gap(classification, message, null, null, "envelope")], false);

    private static PackageDecisionInputGap Gap(string classification, string message, string? claimId, string? producerId, string discriminator) =>
        new($"pd-advisory:{CombinedReportHelpers.Hash(string.Join('\u001f', classification, producerId ?? "unknown", claimId ?? "unknown", discriminator), 24)}",
            classification, message, RuleId, EvidenceTiers.Tier4Unknown, SafeMetadata(claimId), SafeMetadata(producerId));

    private static string? SafeMetadata(string? value) => value is not null && SafeClaimId(value) ? value : null;

    private static string Required(JsonElement element, string name, Func<string, bool> predicate)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw new ValidationException("DecisionInputMalformed", "A required advisory profile field was missing or invalid.");
        var result = value.GetString();
        if (string.IsNullOrWhiteSpace(result) || result.Length > 256)
            throw new ValidationException("DecisionInputLimitReached", "An advisory profile field exceeded its bound.");
        if (!predicate(result))
            throw new ValidationException("DecisionInputMalformed", "An advisory profile field did not satisfy its closed shape.");
        return result;
    }

    private static bool SafeClaimId(string value) => ClaimId().IsMatch(value);
    private static bool SafeProducerId(string value) => ProducerId().IsMatch(value);
    private static bool SafeProducerVersion(string value) => ProducerVersion().IsMatch(value);
    private static bool SafeFramework(string value) => Framework().IsMatch(value);
    private static bool SafePackageName(string value) => (PackageName().IsMatch(value) || NpmPackageName().IsMatch(value)) && !value.Contains("://", StringComparison.Ordinal);
    private static bool SafePredicateVersion(string value) => Version().IsMatch(value) && !value.Contains("://", StringComparison.Ordinal) && !value.StartsWith("git+", StringComparison.OrdinalIgnoreCase) && !value.Contains("${", StringComparison.Ordinal) && !value.Contains('@', StringComparison.Ordinal) && !value.StartsWith("/", StringComparison.Ordinal) && !value.StartsWith("./", StringComparison.Ordinal) && !value.StartsWith("../", StringComparison.Ordinal) && !value.Contains('^') && !value.Contains('>') && !value.Contains('<') && !value.Contains('~') && !value.Contains('*');

    [GeneratedRegex("^[a-z0-9][a-z0-9._:-]{3,80}$", RegexOptions.CultureInvariant)] private static partial Regex ClaimId();
    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{1,64}$", RegexOptions.CultureInvariant)] private static partial Regex ProducerId();
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._+-]{0,31}$", RegexOptions.CultureInvariant)] private static partial Regex ProducerVersion();
    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{1,40}$", RegexOptions.CultureInvariant)] private static partial Regex Framework();
    [GeneratedRegex("^[A-Za-z0-9_][A-Za-z0-9_.$+-]*(?::[A-Za-z0-9_.$+-]+)?$", RegexOptions.CultureInvariant)] private static partial Regex PackageName();
    [GeneratedRegex("^@[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)] private static partial Regex NpmPackageName();
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._+~-]{0,127}$", RegexOptions.CultureInvariant)] private static partial Regex Version();

    private sealed record ClaimCandidate(PackageDecisionAdvisoryClaim? Claim, PackageDecisionInputGap? Gap);
    private sealed class ValidationException(string classification, string message) : Exception(message)
    {
        public string Classification { get; } = classification;
    }
}
