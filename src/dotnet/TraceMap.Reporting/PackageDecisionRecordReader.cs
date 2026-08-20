using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record PackageDecisionRecord(
    string DecisionId,
    string DecisionKind,
    string Ecosystem,
    string PackageName,
    string ArtifactVersion,
    string? RegistryOrigin,
    string? ArtifactDigestAlgorithm,
    string? ArtifactDigest,
    string ProducerId,
    string PolicyVersion,
    DateTimeOffset DecisionTimeUtc,
    string RecordDigest,
    string? SupersedesDecisionId = null,
    string? SourceRepoHash = null,
    string? SourceCommitSha = null);

public sealed record PackageDecisionInputGap(
    string GapId,
    string Classification,
    string Message,
    string RuleId,
    string EvidenceTier,
    string? DecisionId = null,
    string? ProducerId = null);

public sealed record PackageDecisionRecordAdmission(
    IReadOnlyList<PackageDecisionRecord> Records,
    IReadOnlyList<PackageDecisionInputGap> Gaps,
    bool Accepted,
    string? EnvelopeDigest = null);

public static partial class PackageDecisionRecordReader
{
    public const string SchemaVersion = "package-decision.v1";
    public const string RuleId = "package.decision.record.v1";
    public const string EvidenceTier = EvidenceTiers.Tier4Unknown;
    public const int MaxRecords = 200;

    private static readonly HashSet<string> Ecosystems = new(StringComparer.Ordinal) { "nuget", "npm", "python", "maven", "gradle", "swift" };
    private static readonly HashSet<string> DecisionKinds = new(StringComparer.Ordinal) { "admit", "reject", "revoke", "quarantine" };
    private static readonly HashSet<string> Algorithms = new(StringComparer.Ordinal) { "sha256", "sha512-base64" };

    public static async Task<PackageDecisionRecordAdmission> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A package decision path is required.", nameof(path));

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            return Read(json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return Failure("DecisionInputReadFailed", "The package decision input could not be read.");
        }
    }

    public static PackageDecisionRecordAdmission Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Failure("DecisionInputReadFailed", "The package decision input was empty.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
        }
        catch (JsonException)
        {
            return Failure("DecisionInputReadFailed", "The package decision input was not valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Failure("DecisionInputSchemaUnsupported", "The package decision envelope is not an object.");

            var rootProperties = root.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
            if (!rootProperties.SetEquals(["version", "records"]))
                return Failure("DecisionInputSchemaUnsupported", "The package decision envelope has unsupported properties.");

            if (!root.TryGetProperty("version", out var versionElement) || versionElement.ValueKind != JsonValueKind.String || !string.Equals(versionElement.GetString(), SchemaVersion, StringComparison.Ordinal))
                return Failure("DecisionInputSchemaUnsupported", "The package decision envelope version is unsupported.");

            if (!root.TryGetProperty("records", out var recordsElement) || recordsElement.ValueKind != JsonValueKind.Array || recordsElement.GetArrayLength() == 0)
                return Failure("DecisionInputMalformed", "The package decision envelope requires a non-empty records array.");

            if (recordsElement.GetArrayLength() > MaxRecords)
                return Failure("DecisionInputLimitReached", "The package decision envelope exceeds the record limit.");

            var accepted = new List<(PackageDecisionRecord Record, string Key, string Digest, int Ordinal)>();
            var gaps = new List<PackageDecisionInputGap>();
            var candidates = new List<Candidate>();
            var ordinal = 0;
            foreach (var element in recordsElement.EnumerateArray())
            {
                candidates.Add(ParseCandidate(element, ordinal++));
            }

            foreach (var candidate in candidates.Where(candidate => candidate.Record is not null))
            {
                var record = candidate.Record!;
                var key = $"{record.ProducerId}\u001f{record.DecisionId}";
                accepted.Add((record, key, record.RecordDigest, candidate.Ordinal));
            }

            foreach (var candidate in candidates.Where(candidate => candidate.Gap is not null))
                gaps.Add(candidate.Gap!);

            if (gaps.Any(gap => gap.Classification == "DecisionInputLimitReached"))
                return new PackageDecisionRecordAdmission([], gaps.OrderBy(gap => gap.GapId, StringComparer.Ordinal).ToArray(), false, CanonicalJsonDigest.Compute(json));

            foreach (var group in accepted.GroupBy(item => item.Key, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var distinct = group.Select(item => item.Digest).Distinct(StringComparer.Ordinal).ToArray();
                if (distinct.Length == 1)
                {
                    var first = group.OrderBy(item => item.Ordinal).First();
                    accepted.RemoveAll(item => item.Key == group.Key && item.Ordinal != first.Ordinal);
                    foreach (var duplicate in group.Where(item => item.Ordinal != first.Ordinal).OrderBy(item => item.Ordinal))
                        gaps.Add(Gap("DecisionInputDuplicateConflict", "An identical package decision record was deterministically deduplicated.", duplicate.Record.DecisionId, duplicate.Record.ProducerId, $"duplicate-identical:{duplicate.Record.RecordDigest}"));
                }
                else
                {
                    accepted.RemoveAll(item => item.Key == group.Key);
                    foreach (var duplicate in group.OrderBy(item => item.Ordinal))
                        gaps.Add(Gap("DecisionInputDuplicateConflict", "Conflicting package decision records shared one producer-scoped identity; none were admitted.", duplicate.Record.DecisionId, duplicate.Record.ProducerId, $"duplicate-conflict:{duplicate.Record.RecordDigest}"));
                }
            }

            var records = accepted
                .OrderBy(item => item.Record.ProducerId, StringComparer.Ordinal)
                .ThenBy(item => item.Record.DecisionId, StringComparer.Ordinal)
                .Select(item => item.Record)
                .ToArray();
            return new PackageDecisionRecordAdmission(records, gaps.OrderBy(gap => gap.GapId, StringComparer.Ordinal).ToArray(), true, CanonicalJsonDigest.Compute(json));
        }
    }

    private static Candidate ParseCandidate(JsonElement element, int ordinal)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return Invalid("DecisionInputMalformed", "A package decision record was not an object.", ordinal);

        var names = element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "decisionId", "decisionKind", "ecosystem", "packageName", "artifactVersion", "registryOrigin",
            "artifactDigestAlgorithm", "artifactDigest", "producer", "decisionTimeUtc", "recordDigest", "supersedesDecisionId", "provenance"
        };
        if (names.Any(name => !allowed.Contains(name)))
            return Invalid("DecisionInputMalformed", "A package decision record has unsupported properties.", ordinal);

        try
        {
            var decisionId = Required(element, "decisionId", SafeDecisionId);
            var decisionKind = Required(element, "decisionKind", value => value.Length <= 64);
            if (!DecisionKinds.Contains(decisionKind))
                return Invalid("DecisionInputDecisionKindUnsupported", "A package decision record has an unsupported decision kind.", ordinal, decisionId);
            var ecosystem = Required(element, "ecosystem", value => Ecosystems.Contains(value));
            var packageName = Required(element, "packageName", value => value.Length <= 128);
            if (!SafePackageName(packageName))
                return Invalid("DecisionInputIdentityUnsafe", "A package decision identity contained unsafe package-name material.", ordinal, decisionId);
            var version = Required(element, "artifactVersion", value => value.Length <= 128);
            if (!SafeArtifactVersion(version))
            {
                var unsafeVersion = version.Contains("://", StringComparison.Ordinal) || version.StartsWith("/", StringComparison.Ordinal) || version.StartsWith("./", StringComparison.Ordinal) || version.StartsWith("../", StringComparison.Ordinal) || version.Contains("${", StringComparison.Ordinal) || version.Contains('@', StringComparison.Ordinal);
                return Invalid(unsafeVersion ? "DecisionInputIdentityUnsafe" : "DecisionInputMalformed", "A package decision version did not satisfy its safe exact-version shape.", ordinal, decisionId);
            }
            var origin = Optional(element, "registryOrigin");
            if (origin is not null && !SafeOrigin(origin))
                return Invalid("DecisionInputIdentityUnsafe", "A package decision identity contained unsafe origin material.", ordinal, decisionId);

            var algorithm = Optional(element, "artifactDigestAlgorithm");
            var digest = Optional(element, "artifactDigest");
            if ((algorithm is null) != (digest is null) || (algorithm is not null && !Algorithms.Contains(algorithm)))
                return Invalid("DecisionInputMalformed", "A package decision digest pair was invalid.", ordinal, decisionId);
            if (algorithm == "sha256" && (digest!.Length != 64 || !Sha256().IsMatch(digest)))
                return Invalid("DecisionInputMalformed", "A package decision SHA-256 digest was invalid.", ordinal, decisionId);
            if (algorithm == "sha512-base64" && !IsSha512Base64(digest!))
                return Invalid("DecisionInputMalformed", "A package decision SHA-512 digest was invalid.", ordinal, decisionId);

            if (!element.TryGetProperty("producer", out var producer) || producer.ValueKind != JsonValueKind.Object)
                return Invalid("DecisionInputMalformed", "A package decision producer was invalid.", ordinal, decisionId);
            if (!producer.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(["id", "policyVersion"]))
                return Invalid("DecisionInputMalformed", "A package decision producer had unsupported properties.", ordinal, decisionId);
            var producerId = Required(producer, "id", SafeProducerId);
            var policyVersion = Required(producer, "policyVersion", SafePolicyVersion);
            var timestampText = Required(element, "decisionTimeUtc", TryTimestamp);
            if (!DateTimeOffset.TryParse(timestampText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
                return Invalid("DecisionInputMalformed", "A package decision timestamp was invalid.", ordinal, decisionId);

            var supersedes = Optional(element, "supersedesDecisionId");
            if (supersedes is not null && !SafeDecisionId(supersedes))
                return Invalid("DecisionInputMalformed", "A package decision supersession identifier was invalid.", ordinal, decisionId);

            string? sourceRepoHash = null;
            string? sourceCommit = null;
            if (element.TryGetProperty("provenance", out var provenance))
            {
                if (provenance.ValueKind != JsonValueKind.Object || !provenance.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(["sourceRepo", "sourceCommitSha"]))
                    return Invalid("DecisionInputMalformed", "A package decision provenance block was invalid.", ordinal, decisionId);
                var sourceRepo = Required(provenance, "sourceRepo", value => value.Length <= 256 && !value.Contains('@', StringComparison.Ordinal));
                var sourceCommitValue = Required(provenance, "sourceCommitSha", value => Sha().IsMatch(value));
                sourceRepoHash = $"repo-hash:{CombinedReportHelpers.Hash(sourceRepo, 16)}";
                sourceCommit = sourceCommitValue.ToLowerInvariant();
            }

            var recordDigest = CanonicalJsonDigest.Compute(element.GetRawText(), "recordDigest");
            if (element.TryGetProperty("recordDigest", out var suppliedElement))
            {
                if (suppliedElement.ValueKind != JsonValueKind.String || (suppliedElement.GetString()?.Length ?? 0) > 256)
                    return Invalid("DecisionInputLimitReached", "A package decision field exceeded its bound.", ordinal, decisionId);
                if (!Sha256().IsMatch(suppliedElement.GetString() ?? string.Empty))
                    return Invalid("DecisionInputDigestMismatch", "A package decision self-digest was invalid.", ordinal, decisionId);
                var supplied = Convert.FromHexString(suppliedElement.GetString()!);
                var expected = Convert.FromHexString(recordDigest);
                if (!CryptographicOperations.FixedTimeEquals(supplied, expected))
                    return Invalid("DecisionInputDigestMismatch", "A package decision self-digest did not match canonical content.", ordinal, decisionId);
            }

            return new Candidate(new PackageDecisionRecord(decisionId, decisionKind, ecosystem, packageName, version, origin,
                algorithm, digest, producerId, policyVersion, timestamp, recordDigest, supersedes, sourceRepoHash, sourceCommit), null, ordinal);
        }
        catch (ValidationException exception)
        {
            return Invalid(exception.Classification, exception.Message, ordinal, exception.DecisionId);
        }
        catch (Exception)
        {
            return Invalid("DecisionInputMalformed", "A package decision record did not satisfy the strict v1 shape.", ordinal);
        }
    }

    private static PackageDecisionRecordAdmission Failure(string classification, string message) =>
        new([], [Gap(classification, message, null, null, "envelope")], false);

    private static PackageDecisionInputGap Gap(string classification, string message, string? decisionId, string? producerId, string discriminator) =>
        new($"pd-input:{CombinedReportHelpers.Hash(string.Join('\u001f', classification, producerId ?? "unknown", decisionId ?? "unknown", discriminator), 24)}",
            classification, message, RuleId, EvidenceTier, SafeMetadata(decisionId), SafeMetadata(producerId));

    private static Candidate Invalid(string classification, string message, int ordinal, string? decisionId = null, string? producerId = null) =>
        new(null, Gap(classification, message, SafeMetadata(decisionId), SafeMetadata(producerId), $"record-{ordinal:D4}"), ordinal);

    private static string? SafeMetadata(string? value) => value is not null && SafeDecisionId(value) ? value : null;

    private static string Required(JsonElement element, string name, Func<string, bool> predicate)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw new ValidationException("DecisionInputMalformed", $"A required package decision field was missing or invalid.", null);
        var result = value.GetString();
        if (string.IsNullOrWhiteSpace(result) || result.Length > 256)
            throw new ValidationException("DecisionInputLimitReached", "A package decision field exceeded its bound.", null);
        if (!predicate(result))
            throw new ValidationException("DecisionInputMalformed", "A package decision field did not satisfy its closed shape.", null);
        return result;
    }

    private static string? Optional(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())) throw new ValidationException("DecisionInputMalformed", "An optional package decision field was invalid.", null);
        var result = value.GetString()!;
        if (result.Length > 256) throw new ValidationException("DecisionInputLimitReached", "A package decision field exceeded its bound.", null);
        return result;
    }

    private static bool TryTimestamp(string value) => Rfc3339().IsMatch(value) && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) && parsed.Offset == TimeSpan.Zero;
    private static bool SafeDecisionId(string value) => DecisionId().IsMatch(value);
    private static bool SafeProducerId(string value) => ProducerId().IsMatch(value);
    private static bool SafePolicyVersion(string value) => PolicyVersion().IsMatch(value);
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

    [GeneratedRegex("^[a-z0-9][a-z0-9._:-]{3,80}$", RegexOptions.CultureInvariant)] private static partial Regex DecisionId();
    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{1,64}$", RegexOptions.CultureInvariant)] private static partial Regex ProducerId();
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._+-]{0,31}$", RegexOptions.CultureInvariant)] private static partial Regex PolicyVersion();
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_.$+-]*(?::[A-Za-z0-9_.$+-]+)?$", RegexOptions.CultureInvariant)] private static partial Regex PackageName();
    [GeneratedRegex("^@[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)] private static partial Regex NpmPackageName();
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._+~-]{0,127}$", RegexOptions.CultureInvariant)] private static partial Regex Version();
    [GeneratedRegex("^[A-Za-z0-9.-]+(?::[0-9]{1,5})?$", RegexOptions.CultureInvariant)] private static partial Regex Origin();
    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.CultureInvariant)] private static partial Regex Sha256();
    [GeneratedRegex("^(?:[a-f0-9]{40}|[a-f0-9]{64})$", RegexOptions.CultureInvariant)] private static partial Regex Sha();
    [GeneratedRegex("^[A-Za-z0-9+/]+={0,2}$", RegexOptions.CultureInvariant)] private static partial Regex Base64();
    [GeneratedRegex("^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}(?:\\.\\d{1,7})?Z$", RegexOptions.CultureInvariant)] private static partial Regex Rfc3339();

    private sealed record Candidate(PackageDecisionRecord? Record, PackageDecisionInputGap? Gap, int Ordinal);
    private sealed class ValidationException(string classification, string message, string? decisionId) : Exception(message)
    {
        public string Classification { get; } = classification;
        public string? DecisionId { get; } = decisionId;
    }
}
