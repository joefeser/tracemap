using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record SqlValidationTargetContext(
    string Engine,
    string ServerRole,
    string DatabaseRole,
    string SchemaRole,
    string ExecutionMode);

public sealed record SqlValidationObservation(
    string ObservationId,
    string ArtifactId,
    string ArtifactDigest,
    string Repository,
    string CommitSha,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset EvaluatedAt,
    SqlValidationTargetContext TargetContext,
    string ValidatorId,
    string ValidatorVersion,
    string AssertionCode,
    string Status,
    string PublicClaimLevel,
    string RuleId,
    IReadOnlyList<string> Limitations);

public sealed record SqlValidationIngestionGap(
    string GapId,
    string Code,
    string RuleId,
    string ArtifactId,
    string Message,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);

public sealed record SqlValidationComposition(
    IReadOnlyList<SqlValidationObservation> Observations,
    IReadOnlyList<SqlValidationIngestionGap> Gaps)
{
    public static SqlValidationComposition Empty { get; } = new([], []);
}

public sealed record SqlValidationExpectedSource(
    string SourceLabel,
    string Repository,
    string CommitSha,
    DateTimeOffset EvaluatedAt,
    IReadOnlyList<SqlValidationTargetContext> Contexts);

public static partial class SqlValidationSummaryReader
{
    public const string SchemaVersion = "sql-validation-summary/v1";
    public const string ValidatorId = "tracemap.sql-validation-harness";
    public const string ValidatorVersion = "1.0.0";
    public const string PublicClaimLevel = "public-safe";
    public const string DigestAlgorithm = "sha256-canonical-json-v1";

    private static readonly HashSet<string> Engines = new(StringComparer.Ordinal) { "postgresql" };
    private static readonly HashSet<string> ServerRoles = new(StringComparer.Ordinal) { "admin", "source", "archive-target", "application-primary", "co-located", "validation" };
    private static readonly HashSet<string> DatabaseRoles = new(StringComparer.Ordinal) { "admin", "source-data", "archive-data", "application-data", "validation-only" };
    private static readonly HashSet<string> SchemaRoles = new(StringComparer.Ordinal) { "extension", "application", "archive", "validation", "unspecified" };
    private static readonly HashSet<string> ExecutionModes = new(StringComparer.Ordinal) { "manual", "scheduled", "validation-only", "migration-runner" };
    private static readonly HashSet<string> AssertionCodes = new(StringComparer.Ordinal)
    {
        "postgres.server-version-compatible",
        "postgres.required-extension-available",
        "postgres.target-schema-compatible",
        "postgres.migration-schema-present",
        "postgres.archive-link-connectivity",
        "postgres.permission-probe-authorized",
        "postgres.archive-function-callable",
        "postgres.scheduled-job-registered",
        "postgres.validation-query-expected-shape",
        "postgres.cleanup-probe-observed"
    };
    private static readonly HashSet<string> ResultStatuses = new(StringComparer.Ordinal)
    {
        "observed-pass", "observed-fail", "observed-indeterminate", "not-run"
    };
    private static readonly HashSet<string> LimitationCodes = new(StringComparer.Ordinal)
    {
        "point-in-time-observation",
        "does-not-establish-continuing-state",
        "does-not-approve-release",
        "does-not-establish-safe-to-run"
    };

    public static async Task<SqlValidationComposition> ReadAsync(
        IReadOnlyList<string> paths,
        IReadOnlyList<SqlValidationExpectedSource> expectedSources,
        CancellationToken cancellationToken = default)
    {
        if (paths.Count == 0)
            return SqlValidationComposition.Empty;

        var candidates = new List<Candidate>();
        var gaps = new List<SqlValidationIngestionGap>();
        foreach (var input in paths.Select((path, index) => (Path: path, Ordinal: index)))
        {
            var inputKey = $"input-{input.Ordinal:D4}";
            try
            {
                var json = await File.ReadAllTextAsync(input.Path, cancellationToken);
                var parsed = Parse(json);
                if (parsed.Candidate is not null)
                    candidates.Add(parsed.Candidate);
                gaps.AddRange(parsed.Gaps.Select(gap => DiscriminateGap(gap, inputKey, input.Ordinal)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                gaps.Add(Gap("MalformedSummary", "unidentified", "The supplied SQL validation summary could not be read as the strict v1 contract.", inputKey,
                    [new KeyValuePair<string, string>("inputOrdinal", input.Ordinal.ToString("D4"))]));
            }
        }

        var deduped = new List<Candidate>();
        foreach (var group in candidates.GroupBy(candidate => candidate.ArtifactId, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var digests = group.Select(candidate => candidate.Digest).Distinct(StringComparer.Ordinal).ToArray();
            if (digests.Length > 1)
            {
                gaps.Add(Gap("ConflictingSummary", group.Key, "The artifact ID was reused with conflicting content; no observation from that artifact was accepted."));
                continue;
            }

            if (group.Count() > 1)
                gaps.Add(Gap("DuplicateSummary", group.Key, "An exact duplicate SQL validation summary was supplied and deterministically deduplicated."));
            deduped.Add(group.OrderBy(candidate => candidate.Digest, StringComparer.Ordinal).First());
        }

        var observations = new List<SqlValidationObservation>();
        foreach (var candidate in deduped)
        {
            var sourceCandidates = expectedSources.Where(expected =>
                    string.Equals(expected.Repository, candidate.Repository, StringComparison.Ordinal)
                    && string.Equals(expected.CommitSha, candidate.CommitSha, StringComparison.Ordinal))
                .OrderBy(expected => expected.SourceLabel, StringComparer.Ordinal)
                .ToArray();
            if (sourceCandidates.Length == 0)
            {
                gaps.Add(Gap("SourceMismatch", candidate.ArtifactId, "The summary repository and commit did not match a selected scan source."));
                continue;
            }

            var contextMatches = sourceCandidates
                .Where(expected => expected.Contexts.Contains(candidate.Context))
                .ToArray();
            if (contextMatches.Length == 0)
            {
                gaps.Add(Gap("ContextMismatch", candidate.ArtifactId, "The categorical target context did not match cataloged static SQL context."));
                continue;
            }
            if (contextMatches.Length > 1)
            {
                gaps.Add(Gap("AmbiguousSource", candidate.ArtifactId, "The summary repository, commit, and categorical target context matched more than one selected source and was not accepted."));
                continue;
            }

            var expected = contextMatches[0];
            if (candidate.ObservedAt >= candidate.ExpiresAt || candidate.ObservedAt > expected.EvaluatedAt)
            {
                gaps.Add(Gap("InvalidObservationWindow", candidate.ArtifactId, "The observation timestamps were inconsistent with deterministic scan time."));
                continue;
            }
            if (candidate.ExpiresAt < expected.EvaluatedAt)
            {
                gaps.Add(Gap("ExpiredSummary", candidate.ArtifactId, "The SQL validation summary had expired at the deterministic scan time."));
                continue;
            }

            foreach (var assertion in candidate.Assertions.OrderBy(assertion => assertion.Code, StringComparer.Ordinal))
            {
                observations.Add(new SqlValidationObservation(
                    StableId("observation", candidate.ArtifactId, assertion.Code, assertion.Status),
                    candidate.ArtifactId,
                    candidate.Digest,
                    candidate.Repository,
                    candidate.CommitSha,
                    candidate.ObservedAt,
                    candidate.ExpiresAt,
                    expected.EvaluatedAt,
                    candidate.Context,
                    candidate.ValidatorId,
                    candidate.ValidatorVersion,
                    assertion.Code,
                    assertion.Status,
                    candidate.PublicClaimLevel,
                    RuleIds.DatabaseSqlValidationSummaryObservation,
                    candidate.Limitations));
            }
        }

        var conflicted = observations
            .GroupBy(observation => string.Join('\u001f', observation.Repository, observation.CommitSha,
                ContextKey(observation.TargetContext), observation.AssertionCode), StringComparer.Ordinal)
            .Where(group => group.Select(observation => observation.ArtifactId).Distinct(StringComparer.Ordinal).Skip(1).Any())
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var key in conflicted.Order(StringComparer.Ordinal))
        {
            var matching = observations.Where(observation => ObservationKey(observation) == key).OrderBy(observation => observation.ArtifactId, StringComparer.Ordinal).ToArray();
            var artifactId = matching[0].ArtifactId;
            gaps.Add(Gap("ConflictingAssertion", artifactId,
                "Multiple artifacts supplied the same source, context, and assertion identity; none were accepted.",
                StableId("assertion-identity", key),
                [new KeyValuePair<string, string>("assertionCode", matching[0].AssertionCode)]));
        }
        observations.RemoveAll(observation => conflicted.Contains(ObservationKey(observation)));

        return new SqlValidationComposition(
            observations.DistinctBy(observation => observation.ObservationId).OrderBy(observation => observation.ObservationId, StringComparer.Ordinal).ToArray(),
            gaps.DistinctBy(gap => gap.GapId).OrderBy(gap => gap.GapId, StringComparer.Ordinal).ToArray());
    }

    public static string ComputeDigest(string json)
    {
        var node = JsonNode.Parse(json) ?? throw new JsonException("JSON root is required.");
        if (node["artifact"] is not JsonObject artifact || !artifact.ContainsKey("digest"))
            throw new JsonException("artifact.digest is required.");
        artifact["digest"] = string.Empty;
        var canonical = Canonicalize(node).ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static bool TryParseTimestamp(string? text, out DateTimeOffset value)
    {
        value = default;
        return !string.IsNullOrWhiteSpace(text)
            && Rfc3339Timestamp().IsMatch(text)
            && DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out value);
    }

    private static ParseResult Parse(string json)
    {
        string artifactId = "unidentified";
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            var root = document.RootElement;
            RequireObject(root, "summary");
            RequireProperties(root, "schemaVersion", "artifactId", "repository", "commitSha", "observedAt", "expiresAt", "targetContext", "validator", "artifact", "publicClaimLevel", "assertions", "limitations");
            artifactId = RequiredString(root, "artifactId");
            if (!SafeId().IsMatch(artifactId)) return Reject("MalformedSummary", artifactId, "The artifact ID was outside the safe token contract.");
            if (RequiredString(root, "schemaVersion") != SchemaVersion) return Reject("UnsupportedSchema", artifactId, "The SQL validation summary schema version is unsupported.");
            var repository = RequiredString(root, "repository");
            if (!SafeRepository().IsMatch(repository)) return Reject("MalformedSummary", artifactId, "The repository identity was outside the safe token contract.");
            var commitSha = RequiredString(root, "commitSha");
            if (!CommitSha().IsMatch(commitSha)) return Reject("MalformedSummary", artifactId, "The commit SHA was not a full hexadecimal identity.");
            var observedAt = RequiredTimestamp(root, "observedAt");
            var expiresAt = RequiredTimestamp(root, "expiresAt");
            if (observedAt >= expiresAt)
                return Reject("InvalidObservationWindow", artifactId, "The observation expiry must be strictly later than the observation time.");

            var contextElement = root.GetProperty("targetContext");
            RequireObject(contextElement, "targetContext");
            RequireProperties(contextElement, "engine", "serverRole", "databaseRole", "schemaRole", "executionMode");
            var context = new SqlValidationTargetContext(RequiredString(contextElement, "engine"), RequiredString(contextElement, "serverRole"), RequiredString(contextElement, "databaseRole"), RequiredString(contextElement, "schemaRole"), RequiredString(contextElement, "executionMode"));
            if (!Engines.Contains(context.Engine) || !ServerRoles.Contains(context.ServerRole) || !DatabaseRoles.Contains(context.DatabaseRole)
                || !SchemaRoles.Contains(context.SchemaRole) || !ExecutionModes.Contains(context.ExecutionMode))
                return Reject("UnsupportedContext", artifactId, "The categorical target context contained an unsupported value.");

            var validator = root.GetProperty("validator");
            RequireObject(validator, "validator");
            RequireProperties(validator, "id", "version");
            var validatorId = RequiredString(validator, "id");
            var validatorVersion = RequiredString(validator, "version");
            if (validatorId != ValidatorId || validatorVersion != ValidatorVersion)
                return Reject("UnsupportedValidator", artifactId, "The validator identity or version is not approved for v1 ingestion.");

            var artifact = root.GetProperty("artifact");
            RequireObject(artifact, "artifact");
            RequireProperties(artifact, "algorithm", "digest");
            if (RequiredString(artifact, "algorithm") != DigestAlgorithm)
                return Reject("UnsupportedDigest", artifactId, "The artifact digest algorithm is unsupported.");
            var digest = RequiredString(artifact, "digest");
            if (!Digest().IsMatch(digest) || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(digest), Convert.FromHexString(ComputeDigest(json))))
                return Reject("DigestMismatch", artifactId, "The SQL validation summary digest did not match its canonical content.");

            var claimLevel = RequiredString(root, "publicClaimLevel");
            if (claimLevel != PublicClaimLevel) return Reject("UnsupportedClaimLevel", artifactId, "The summary claim level is unsupported.");

            var assertionsElement = root.GetProperty("assertions");
            if (assertionsElement.ValueKind != JsonValueKind.Array || assertionsElement.GetArrayLength() == 0)
                return Reject("MalformedSummary", artifactId, "At least one closed validation assertion is required.");
            var assertions = new List<Assertion>();
            foreach (var item in assertionsElement.EnumerateArray())
            {
                RequireObject(item, "assertion");
                RequireProperties(item, "code", "status");
                var code = RequiredString(item, "code");
                var status = RequiredString(item, "status");
                if (!AssertionCodes.Contains(code)) return Reject("UnsupportedAssertion", artifactId, "The summary contained an unsupported assertion code.");
                if (!ResultStatuses.Contains(status)) return Reject("UnsupportedStatus", artifactId, "The summary contained an unsupported result status.");
                assertions.Add(new Assertion(code, status));
            }
            if (assertions.Select(assertion => assertion.Code).Distinct(StringComparer.Ordinal).Count() != assertions.Count)
                return Reject("DuplicateAssertion", artifactId, "The summary repeated an assertion code.");

            var limitationsElement = root.GetProperty("limitations");
            if (limitationsElement.ValueKind != JsonValueKind.Array || limitationsElement.GetArrayLength() == 0)
                return Reject("MalformedSummary", artifactId, "At least one closed limitation code is required.");
            var limitations = limitationsElement.EnumerateArray().Select(element => element.GetString() ?? string.Empty).ToArray();
            if (limitations.Length != LimitationCodes.Count
                || limitations.Distinct(StringComparer.Ordinal).Count() != limitations.Length
                || limitations.Any(limitation => !LimitationCodes.Contains(limitation)))
                return Reject("UnsupportedLimitation", artifactId, "The summary contained an unsupported limitation code.");

            return new ParseResult(new Candidate(artifactId, digest, repository, commitSha, observedAt, expiresAt, context,
                validatorId, validatorVersion, claimLevel, assertions, limitations.Order(StringComparer.Ordinal).ToArray()), []);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
        {
            return Reject("MalformedSummary", artifactId, "The supplied SQL validation summary did not satisfy the strict v1 shape.");
        }
    }

    private static ParseResult Reject(string code, string artifactId, string message) => new(null, [Gap(code, artifactId, message)]);
    private static SqlValidationIngestionGap Gap(
        string code,
        string artifactId,
        string message,
        string discriminator = "default",
        IReadOnlyList<KeyValuePair<string, string>>? metadata = null) => new(
        StableId("gap", code, artifactId, discriminator), code, RuleIds.DatabaseSqlValidationSummaryGap, artifactId, message,
        new[] { new KeyValuePair<string, string>("artifactId", artifactId) }
            .Concat(metadata ?? []).OrderBy(pair => pair.Key, StringComparer.Ordinal).ThenBy(pair => pair.Value, StringComparer.Ordinal).ToArray());
    private static SqlValidationIngestionGap DiscriminateGap(SqlValidationIngestionGap gap, string discriminator, int inputOrdinal) => gap with
    {
        GapId = StableId("gap", gap.Code, gap.ArtifactId, discriminator),
        Metadata = gap.Metadata.Append(new KeyValuePair<string, string>("inputOrdinal", inputOrdinal.ToString("D4")))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal).ThenBy(pair => pair.Value, StringComparer.Ordinal).ToArray()
    };
    private static string StableId(params string[] parts) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', parts)))).ToLowerInvariant()[..24];
    private static string ContextKey(SqlValidationTargetContext context) => string.Join('|', context.Engine, context.ServerRole, context.DatabaseRole, context.SchemaRole, context.ExecutionMode);
    private static string ObservationKey(SqlValidationObservation observation) => string.Join('\u001f', observation.Repository, observation.CommitSha, ContextKey(observation.TargetContext), observation.AssertionCode);

    private static JsonNode Canonicalize(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var sorted = new JsonObject();
            foreach (var property in obj.OrderBy(property => property.Key, StringComparer.Ordinal))
                sorted[property.Key] = property.Value is null ? null : Canonicalize(property.Value);
            return sorted;
        }
        if (node is JsonArray array)
            return new JsonArray(array.Select(item => item is null ? null : Canonicalize(item)).ToArray());
        return node.DeepClone();
    }

    private static void RequireObject(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new JsonException($"{name} must be an object.");
    }
    private static void RequireProperties(JsonElement element, params string[] names)
    {
        var expected = names.ToHashSet(StringComparer.Ordinal);
        var actual = element.EnumerateObject().Select(property => property.Name).ToArray();
        if (actual.Length != expected.Count || actual.Any(name => !expected.Contains(name)) || expected.Any(name => !actual.Contains(name, StringComparer.Ordinal)))
            throw new JsonException("Object properties did not match the closed contract.");
    }
    private static string RequiredString(JsonElement element, string name)
    {
        var value = element.GetProperty(name);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())) throw new JsonException($"{name} must be a non-empty string.");
        return value.GetString()!;
    }
    private static DateTimeOffset RequiredTimestamp(JsonElement element, string name)
    {
        var text = RequiredString(element, name);
        if (!TryParseTimestamp(text, out var value))
            throw new JsonException($"{name} must use RFC3339 with an explicit offset.");
        return value;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,95}$", RegexOptions.CultureInvariant)] private static partial Regex SafeId();
    [GeneratedRegex("^[A-Za-z0-9._-]+(?:/[A-Za-z0-9._-]+)?$", RegexOptions.CultureInvariant)] private static partial Regex SafeRepository();
    [GeneratedRegex("^[0-9a-f]{40,64}$", RegexOptions.CultureInvariant)] private static partial Regex CommitSha();
    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)] private static partial Regex Digest();
    [GeneratedRegex("^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}(?:\\.\\d{1,7})?(?:Z|[+-]\\d{2}:\\d{2})$", RegexOptions.CultureInvariant)] private static partial Regex Rfc3339Timestamp();

    private sealed record Assertion(string Code, string Status);
    private sealed record Candidate(string ArtifactId, string Digest, string Repository, string CommitSha, DateTimeOffset ObservedAt,
        DateTimeOffset ExpiresAt, SqlValidationTargetContext Context, string ValidatorId, string ValidatorVersion,
        string PublicClaimLevel, IReadOnlyList<Assertion> Assertions, IReadOnlyList<string> Limitations);
    private sealed record ParseResult(Candidate? Candidate, IReadOnlyList<SqlValidationIngestionGap> Gaps);
}
