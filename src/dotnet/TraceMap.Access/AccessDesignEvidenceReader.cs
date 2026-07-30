using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TraceMap.Access;

/// <summary>
/// Validates an explicit, owner-controlled Access design-evidence directory
/// without launching Access, reading a database, or persisting protected input.
/// </summary>
public static class AccessDesignEvidenceReader
{
    public const string ManifestSchema = "tracemap.access-design-evidence.v1";
    public const string RecordSchema = "tracemap.access-design-evidence.record.v1";
    public const string ManifestFileName = "access-design-manifest.json";
    public const string RecordsFileName = "access-design-records.ndjson";

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly HashSet<string> RecordKinds =
    [
        "catalog-object",
        "ui-design-document",
        "ui-surface",
        "ui-control",
        "vba-module",
        "event-reference",
        "macro-inventory",
        "source-gap"
    ];
    private static readonly HashSet<string> CoordinateStatuses = ["exact-lines", "container-only", "unavailable"];
    private static readonly HashSet<string> CompletenessStatuses = ["complete", "partial", "unavailable"];
    private static readonly HashSet<string> CopyBindings = ["hash-identical", "owner-attested-derived-copy", "unbound"];
    private static readonly HashSet<string> Mechanisms = ["preexisting-text-export", "synthetic-hand-authored"];
    private static readonly HashSet<string> ProducerIds = ["owner-controlled-export", "tracemap-synthetic-fixture"];
    private static readonly HashSet<string> DocumentRoles =
    [
        "catalog-export",
        "form-design-export",
        "report-design-export",
        "vba-module-export",
        "macro-inventory-export",
        "producer-gap"
    ];
    private static readonly HashSet<string> EventRoles =
    [
        "after-update",
        "before-update",
        "on-click",
        "on-current",
        "on-dbl-click",
        "on-load",
        "on-no-data",
        "on-open"
    ];

    private static readonly IReadOnlyDictionary<string, PayloadShape> PayloadShapes =
        new Dictionary<string, PayloadShape>(StringComparer.Ordinal)
        {
            ["catalog-object"] = new(
                ["objectRole", "identity", "stableKey", "parentRole", "ordinal"],
                ["objectRole"],
                ["objectRole", "identity", "stableKey", "parentRole", "ordinal"]),
            ["ui-design-document"] = new(
                ["documentRole", "designText", "documentSha256", "lineCount"],
                ["documentRole", "designText", "documentSha256", "lineCount"],
                ["documentRole"]),
            ["ui-surface"] = new(
                ["surfaceRole", "identity", "ordinal", "modulePresence", "boundState", "recordSource", "filter", "orderBy", "events"],
                ["surfaceRole", "identity"],
                ["surfaceRole", "identity", "ordinal"]),
            ["ui-control"] = new(
                ["identity", "ordinal", "controlType", "controlSource", "rowSource", "validationRule", "events"],
                ["identity", "ordinal", "controlType"],
                ["identity", "ordinal", "controlType"]),
            ["vba-module"] = new(
                ["moduleRole", "identity", "moduleKind", "sourceText", "sourceSha256", "lineCount", "coordinateBasis"],
                ["moduleRole", "identity", "moduleKind", "sourceText", "sourceSha256", "lineCount", "coordinateBasis"],
                ["moduleRole", "identity", "moduleKind"]),
            ["event-reference"] = new(
                ["eventRole", "value", "ordinal"],
                ["eventRole", "value"],
                ["eventRole", "ordinal"]),
            ["macro-inventory"] = new(
                ["macroCategory", "identity", "ownerRole", "ordinal", "startupRole", "bodyStatus"],
                ["macroCategory", "identity", "ordinal", "startupRole", "bodyStatus"],
                ["macroCategory", "identity", "ownerRole", "ordinal"]),
            ["source-gap"] = new(
                ["classification", "affectedScope", "coverageCategory"],
                ["classification", "affectedScope", "coverageCategory"],
                ["classification", "affectedScope"])
        };

    public static AccessDesignEvidenceBundle Read(
        string inputDirectory,
        AccessDesignEvidenceBinding binding,
        AccessLimits? limits = null)
    {
        limits ??= AccessLimits.Default;
        ValidateLimits(limits);

        var directory = ValidateDirectory(inputDirectory);
        var manifestPath = Path.Combine(directory, ManifestFileName);
        var recordsPath = Path.Combine(directory, RecordsFileName);
        ValidateMembers(directory, manifestPath, recordsPath);

        var manifestBytes = ReadBoundedFile(manifestPath, limits.MaxDesignManifestBytes, "AccessDesignInputManifestLimitReached");
        var recordsBytes = ReadBoundedFile(recordsPath, limits.MaxDesignBundleBytes, "AccessDesignInputBundleLimitReached", allowEmpty: true);

        JsonDocument manifestDocument;
        try
        {
            manifestDocument = JsonDocument.Parse(manifestBytes, DocumentOptions());
            RejectDuplicateProperties(manifestDocument.RootElement);
        }
        catch (AccessScanException)
        {
            throw;
        }
        catch
        {
            throw new AccessScanException("AccessDesignInputManifestInvalid");
        }

        var documents = new List<JsonDocument> { manifestDocument };
        try
        {
            var manifest = ParseManifest(manifestDocument.RootElement);
            if (manifest.CountsByKind.Values.Sum(value => (long)value) > limits.MaxDesignRecords)
                throw new AccessScanException("AccessDesignInputRecordLimitReached");
            var recordsHash = HashBytes(recordsBytes);
            if (!FixedHashEquals(manifest.BundleSha256, recordsHash))
                throw new AccessScanException("AccessDesignInputHashMismatch");

            var rawRecords = ReadRecords(recordsBytes, limits, documents);
            ValidateManifestCounts(manifest, rawRecords);

            var bindingGap = BindingGap(manifest, binding);
            if (bindingGap is not null)
                return new(false, manifest, [], [bindingGap], documents.ToArray());

            var normalized = NormalizeRecords(rawRecords, binding.DatabaseIdentityHash);
            var gaps = new List<AccessDesignEvidenceGap>();
            var acceptedRecords = new List<AccessDesignEvidenceRecord>();
            var rejectedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in normalized.GroupBy(item => item.CanonicalRecordId, StringComparer.Ordinal)
                         .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                foreach (var rejection in group.SelectMany(item => item.RejectionClassifications)
                             .Distinct(StringComparer.Ordinal)
                             .OrderBy(value => value, StringComparer.Ordinal))
                    gaps.Add(new(rejection, "design-record", group.Key));

                var eligible = group.Where(item => item.RejectionClassifications.Count == 0).ToArray();
                var variants = eligible.Select(item => item.CanonicalContentHash).Distinct(StringComparer.Ordinal).ToArray();
                if (variants.Length == 0)
                {
                    rejectedIds.Add(group.Key);
                    continue;
                }
                if (variants.Length != 1)
                {
                    gaps.Add(new("AccessDesignInputDuplicateConflict", "design-record", group.Key));
                    rejectedIds.Add(group.Key);
                    continue;
                }
                acceptedRecords.Add(eligible.OrderBy(item => item.CanonicalContentHash, StringComparer.Ordinal).First().Record);
            }

            RejectInvalidCoordinates(acceptedRecords, rejectedIds, gaps);
            while (true)
            {
                var rejectedChildren = acceptedRecords
                    .Where(item => item.ParentCanonicalRecordId is not null && rejectedIds.Contains(item.ParentCanonicalRecordId))
                    .ToArray();
                if (rejectedChildren.Length == 0)
                    break;
                foreach (var child in rejectedChildren)
                {
                    acceptedRecords.Remove(child);
                    if (rejectedIds.Add(child.CanonicalRecordId))
                        gaps.Add(new("AccessDesignInputParentRejected", "design-record", child.CanonicalRecordId));
                }
            }

            if (manifest.CopyBinding == "owner-attested-derived-copy")
                gaps.Add(new("AccessDesignInputCopyOwnerAttested", "design-bundle"));

            return new(
                true,
                manifest,
                acceptedRecords
                    .OrderBy(item => item.Kind, StringComparer.Ordinal)
                    .ThenBy(item => item.ParentCanonicalRecordId, StringComparer.Ordinal)
                    .ThenBy(item => item.CanonicalRecordId, StringComparer.Ordinal)
                    .ToArray(),
                gaps.OrderBy(item => item.Classification, StringComparer.Ordinal)
                    .ThenBy(item => item.CanonicalRecordId, StringComparer.Ordinal)
                    .ToArray(),
                documents.ToArray());
        }
        catch
        {
            foreach (var document in documents)
                document.Dispose();
            throw;
        }
    }

    private static AccessDesignEvidenceManifestProjection ParseManifest(JsonElement root)
    {
        RequireObject(root, "AccessDesignInputManifestInvalid");
        RequireOnlyProperties(root,
            ["schema", "producer", "repository", "baseScan", "sourceCopy", "records", "capabilities", "exportedAtUtc"],
            "AccessDesignInputManifestInvalid");
        RequireString(root, "schema", ManifestSchema, "AccessDesignInputSchemaUnsupported");

        var producer = RequireObjectProperty(root, "producer", "AccessDesignInputManifestInvalid");
        RequireOnlyProperties(producer, ["id", "version", "mechanism"], "AccessDesignInputManifestInvalid");
        var producerId = RequireClosedString(producer, "id", ProducerIds, "AccessDesignInputProducerUnsupported");
        var producerVersion = RequireSafeToken(producer, "version", 64, "AccessDesignInputManifestInvalid");
        var mechanism = RequireClosedString(producer, "mechanism", Mechanisms, "AccessDesignInputProducerUnsupported");

        var repository = RequireObjectProperty(root, "repository", "AccessDesignInputManifestInvalid");
        RequireOnlyProperties(repository, ["identityHash", "commitSha"], "AccessDesignInputManifestInvalid");
        var repositoryIdentityHash = RequireSha256(repository, "identityHash", "AccessDesignInputManifestInvalid");
        var commitSha = RequireCommitSha(repository, "commitSha");

        var baseScan = RequireObjectProperty(root, "baseScan", "AccessDesignInputManifestInvalid");
        RequireOnlyProperties(baseScan, ["manifestSha256", "databaseIdentityHash"], "AccessDesignInputManifestInvalid");
        var baseManifestHash = RequireSha256(baseScan, "manifestSha256", "AccessDesignInputManifestInvalid");
        var databaseIdentityHash = RequireSha256(baseScan, "databaseIdentityHash", "AccessDesignInputManifestInvalid");

        var sourceCopy = RequireObjectProperty(root, "sourceCopy", "AccessDesignInputManifestInvalid");
        RequireOnlyProperties(sourceCopy, ["sha256", "binding"], "AccessDesignInputManifestInvalid");
        var sourceCopyHash = RequireSha256(sourceCopy, "sha256", "AccessDesignInputManifestInvalid");
        var copyBinding = RequireClosedString(sourceCopy, "binding", CopyBindings, "AccessDesignInputManifestInvalid");

        var records = RequireObjectProperty(root, "records", "AccessDesignInputManifestInvalid");
        RequireOnlyProperties(records, ["sha256", "count", "countsByKind"], "AccessDesignInputManifestInvalid");
        var bundleHash = RequireSha256(records, "sha256", "AccessDesignInputManifestInvalid");
        var count = RequireNonNegativeInt(records, "count", "AccessDesignInputManifestInvalid");
        var countsElement = RequireObjectProperty(records, "countsByKind", "AccessDesignInputManifestInvalid");
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var property in countsElement.EnumerateObject())
        {
            if (!RecordKinds.Contains(property.Name) || !property.Value.TryGetInt32(out var value) || value < 0)
                throw new AccessScanException("AccessDesignInputManifestInvalid");
            counts.Add(property.Name, value);
        }
        if (counts.Values.Sum(value => (long)value) != count)
            throw new AccessScanException("AccessDesignInputManifestInvalid");

        var capabilities = RequireObjectProperty(root, "capabilities", "AccessDesignInputManifestInvalid");
        RequireOnlyProperties(capabilities, ["coordinates", "catalogCompleteness", "identityDisclosure"], "AccessDesignInputManifestInvalid");
        var coordinateCapability = RequireClosedString(
            capabilities,
            "coordinates",
            ["exact-lines", "container-only", "unavailable", "mixed"],
            "AccessDesignInputManifestInvalid");
        var catalogCompleteness = RequireClosedString(
            capabilities,
            "catalogCompleteness",
            ["complete", "declared-partial", "unavailable"],
            "AccessDesignInputManifestInvalid");
        RequireString(capabilities, "identityDisclosure", "hash-only", "AccessDesignInputIdentityDisclosureUnsupported");

        if (root.TryGetProperty("exportedAtUtc", out var exported))
        {
            if (exported.ValueKind != JsonValueKind.String
                || !DateTimeOffset.TryParse(exported.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var timestamp)
                || timestamp.Offset != TimeSpan.Zero)
                throw new AccessScanException("AccessDesignInputManifestInvalid");
        }

        return new(
            producerId,
            producerVersion,
            mechanism,
            bundleHash,
            copyBinding,
            counts)
        {
            RepositoryIdentityHash = repositoryIdentityHash,
            CommitSha = commitSha,
            BaseScanManifestSha256 = baseManifestHash,
            DatabaseIdentityHash = databaseIdentityHash,
            SourceCopySha256 = sourceCopyHash,
            CatalogCompleteness = catalogCompleteness,
            CoordinateCapability = coordinateCapability
        };
    }

    private static IReadOnlyList<RawRecord> ReadRecords(
        byte[] bytes,
        AccessLimits limits,
        List<JsonDocument> documents)
    {
        var records = new List<RawRecord>();
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new StreamReader(stream, StrictUtf8, detectEncodingFromByteOrderMarks: false, bufferSize: 64 * 1024, leaveOpen: false);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    throw new AccessScanException("AccessDesignInputRecordMalformed");
                if (StrictUtf8.GetByteCount(line) > limits.MaxDesignBundleBytes)
                    throw new AccessScanException("AccessDesignInputRecordLimitReached");
                if (records.Count >= limits.MaxDesignRecords)
                    throw new AccessScanException("AccessDesignInputRecordLimitReached");

                JsonDocument document;
                try
                {
                    document = JsonDocument.Parse(line, DocumentOptions());
                    RejectDuplicateProperties(document.RootElement);
                }
                catch (AccessScanException)
                {
                    throw;
                }
                catch
                {
                    throw new AccessScanException("AccessDesignInputRecordMalformed");
                }
                documents.Add(document);
                records.Add(ParseRecord(document.RootElement, limits));
            }
        }
        catch (AccessScanException)
        {
            throw;
        }
        catch (DecoderFallbackException)
        {
            throw new AccessScanException("AccessDesignInputEncodingInvalid");
        }
        catch
        {
            throw new AccessScanException("AccessDesignInputReadFailed");
        }
        return records;
    }

    private static RawRecord ParseRecord(JsonElement root, AccessLimits limits)
    {
        RequireObject(root, "AccessDesignInputRecordMalformed");
        RequireString(root, "schema", RecordSchema, "AccessDesignInputSchemaUnsupported");
        var kind = RequireClosedString(root, "kind", RecordKinds, "AccessDesignInputRecordUnsupported");
        var recordId = RequireSafeToken(root, "recordId", 128, "AccessDesignInputRecordMalformed");
        var parentRecordId = OptionalSafeToken(root, "parentRecordId", 128, "AccessDesignInputRecordMalformed");
        var completeness = RequireClosedString(root, "completeness", CompletenessStatuses, "AccessDesignInputRecordMalformed");
        var source = UnavailableSource;
        var payload = RequireObjectProperty(root, "payload", "AccessDesignInputRecordMalformed");
        var rejections = new SortedSet<string>(StringComparer.Ordinal);
        try
        {
            source = ParseSource(RequireObjectProperty(root, "source", "AccessDesignInputRecordMalformed"));
            RequireOnlyProperties(root, ["schema", "kind", "recordId", "parentRecordId", "source", "completeness", "payload"],
                "AccessDesignInputFieldRejected");
            ValidatePayload(kind, payload, limits);
            ValidateSourceRole(kind, source, payload);
            if (kind is "ui-design-document" or "vba-module")
            {
                var hashProperty = kind == "ui-design-document" ? "documentSha256" : "sourceSha256";
                var payloadHash = RequireSha256(payload, hashProperty, "AccessDesignInputRecordMalformed");
                if (source.DocumentSha256 is not null && !FixedHashEquals(source.DocumentSha256, payloadHash))
                    throw new AccessScanException("AccessDesignInputCoordinateUnavailable");
                var lineCount = RequireNonNegativeInt(payload, "lineCount", "AccessDesignInputRecordMalformed");
                if (source.EndLine is int endLine && endLine > lineCount)
                    throw new AccessScanException("AccessDesignInputCoordinateUnavailable");
            }
        }
        catch (AccessScanException ex) when (IsScopedRecordRejection(ex.Classification))
        {
            rejections.Add(ex.Classification);
        }
        return new(kind, recordId, parentRecordId, source, completeness, payload, rejections.ToArray());
    }

    private static AccessDesignEvidenceSource UnavailableSource =>
        new("producer-gap", "unavailable", null, null, null);

    private static bool IsScopedRecordRejection(string classification) => classification is
        "AccessDesignInputFieldRejected"
        or "AccessDesignInputRecordLimitReached"
        or "AccessDesignInputHashMismatch"
        or "AccessDesignInputCoordinateUnavailable";

    private static AccessDesignEvidenceSource ParseSource(JsonElement source)
    {
        RequireOnlyProperties(source, ["documentRole", "documentSha256", "coordinateStatus", "startLine", "endLine"],
            "AccessDesignInputFieldRejected");
        var role = RequireClosedString(source, "documentRole", DocumentRoles, "AccessDesignInputRecordMalformed");
        var status = RequireClosedString(source, "coordinateStatus", CoordinateStatuses, "AccessDesignInputRecordMalformed");
        var hash = OptionalSha256(source, "documentSha256", "AccessDesignInputCoordinateUnavailable");
        var start = OptionalPositiveInt(source, "startLine", "AccessDesignInputCoordinateUnavailable");
        var end = OptionalPositiveInt(source, "endLine", "AccessDesignInputCoordinateUnavailable");
        if (status == "exact-lines")
        {
            if (hash is null || start is null || end is null || end < start)
                throw new AccessScanException("AccessDesignInputCoordinateUnavailable");
        }
        else if (start is not null || end is not null)
        {
            throw new AccessScanException("AccessDesignInputCoordinateUnavailable");
        }
        return new(role, status, hash, start, end);
    }

    private static void RejectInvalidCoordinates(
        List<AccessDesignEvidenceRecord> acceptedRecords,
        HashSet<string> rejectedIds,
        List<AccessDesignEvidenceGap> gaps)
    {
        var sources = acceptedRecords
            .Where(record => record.Kind is "ui-design-document" or "vba-module")
            .Select(record => new ValidatedSourceDocument(
                record.Kind == "ui-design-document"
                    ? $"{record.Payload.GetProperty("documentRole").GetString()}-design-export"
                    : "vba-module-export",
                record.Kind == "ui-design-document"
                    ? record.Payload.GetProperty("documentSha256").GetString()!
                    : record.Payload.GetProperty("sourceSha256").GetString()!,
                record.Payload.GetProperty("lineCount").GetInt32()))
            .ToArray();
        var byCanonicalId = acceptedRecords.ToDictionary(record => record.CanonicalRecordId, StringComparer.Ordinal);
        var invalid = new List<AccessDesignEvidenceRecord>();

        foreach (var record in acceptedRecords.Where(item => item.Source.CoordinateStatus == "exact-lines"))
        {
            if (record.Kind is "ui-design-document" or "vba-module")
                continue;

            var source = record.Source;
            var documentValidated = sources.Any(document =>
                string.Equals(document.DocumentRole, source.DocumentRole, StringComparison.Ordinal)
                && FixedHashEquals(document.DocumentSha256, source.DocumentSha256!)
                && source.EndLine <= document.LineCount);
            var parentValidated = record.ParentCanonicalRecordId is null
                || !byCanonicalId.TryGetValue(record.ParentCanonicalRecordId, out var parent)
                || parent.Source.CoordinateStatus != "exact-lines"
                || (FixedHashEquals(parent.Source.DocumentSha256!, source.DocumentSha256!)
                    && source.StartLine >= parent.Source.StartLine
                    && source.EndLine <= parent.Source.EndLine);

            if (!documentValidated || !parentValidated)
                invalid.Add(record);
        }

        foreach (var record in invalid)
        {
            acceptedRecords.Remove(record);
            if (rejectedIds.Add(record.CanonicalRecordId))
                gaps.Add(new("AccessDesignInputCoordinateUnavailable", "design-record", record.CanonicalRecordId));
        }
    }

    private static void ValidatePayload(string kind, JsonElement payload, AccessLimits limits)
    {
        var shape = PayloadShapes[kind];
        RequireOnlyProperties(payload, shape.Allowed, "AccessDesignInputFieldRejected");
        foreach (var required in shape.Required)
            if (!payload.TryGetProperty(required, out _))
                throw new AccessScanException("AccessDesignInputRecordMalformed");
        if (shape.Required.Contains("identity", StringComparer.Ordinal)
            && string.IsNullOrWhiteSpace(RequireStringValue(payload, "identity", "AccessDesignInputRecordMalformed")))
            throw new AccessScanException("AccessDesignInputRecordMalformed");

        foreach (var property in payload.EnumerateObject())
        {
            switch (property.Name)
            {
                case "ordinal":
                case "lineCount":
                case "controlType":
                    if (!property.Value.TryGetInt32(out var number) || number < 0)
                        throw new AccessScanException("AccessDesignInputRecordMalformed");
                    break;
                case "events":
                    ValidateEvents(property.Value, limits);
                    break;
                default:
                    if (property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                        throw new AccessScanException("AccessDesignInputRecordMalformed");
                    if (property.Value.ValueKind == JsonValueKind.String
                        && (property.Value.GetString()?.Length ?? 0) > limits.MaxStringLength
                        && property.Name is not ("designText" or "sourceText"))
                        throw new AccessScanException("AccessDesignInputRecordLimitReached");
                    break;
            }
        }

        if (kind == "ui-design-document")
        {
            ValidateProtectedText(payload, "designText", "documentSha256", "lineCount",
                limits.MaxUiDesignTextLength, limits.MaxUiDesignLines);
            RequireClosedString(payload, "documentRole", ["form", "report"], "AccessDesignInputRecordMalformed");
        }
        if (kind == "vba-module")
        {
            ValidateProtectedText(payload, "sourceText", "sourceSha256", "lineCount",
                limits.MaxVbaModuleTextLength, limits.MaxVbaModuleLines);
            RequireClosedString(payload, "moduleKind", ["standard", "class", "form", "report"], "AccessDesignInputRecordMalformed");
            RequireClosedString(payload, "coordinateBasis", ["module-relative"], "AccessDesignInputRecordMalformed");
        }
        if (kind == "catalog-object")
        {
            RequireClosedString(payload, "objectRole",
                ["table", "saved-query", "form", "report", "module", "macro", "table-field"],
                "AccessDesignInputRecordMalformed");
            var hasIdentity = payload.TryGetProperty("identity", out var identity)
                && identity.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(identity.GetString());
            var hasStableKey = payload.TryGetProperty("stableKey", out var stableIdentity)
                && stableIdentity.ValueKind == JsonValueKind.String
                && IsSafeStableKey(stableIdentity.GetString());
            if (!hasIdentity && !hasStableKey)
                throw new AccessScanException("AccessDesignInputRecordMalformed");
            if (payload.TryGetProperty("stableKey", out var stableKey)
                && stableKey.ValueKind != JsonValueKind.Null
                && (stableKey.ValueKind != JsonValueKind.String
                    || !IsSafeStableKey(stableKey.GetString())))
                throw new AccessScanException("AccessDesignInputRecordMalformed");
        }
        if (kind == "ui-surface")
        {
            RequireClosedString(payload, "surfaceRole", ["form", "report"], "AccessDesignInputRecordMalformed");
            OptionalClosedString(payload, "modulePresence", ["present", "absent", "unknown"], "AccessDesignInputRecordMalformed");
            OptionalClosedString(payload, "boundState", ["bound", "unbound", "unknown"], "AccessDesignInputRecordMalformed");
        }
        if (kind == "ui-control")
        {
            if (!payload.TryGetProperty("controlType", out var controlType)
                || !controlType.TryGetInt32(out var controlTypeValue)
                || !AccessUiTextParser.IsSupportedControlType(controlTypeValue))
                throw new AccessScanException("AccessDesignInputRecordMalformed");
        }
        if (kind == "event-reference")
            RequireClosedString(payload, "eventRole", EventRoles, "AccessDesignInputRecordMalformed");
        if (kind == "macro-inventory")
        {
            RequireClosedString(payload, "macroCategory", ["named", "ui", "data", "embedded"], "AccessDesignInputRecordMalformed");
            RequireClosedString(payload, "bodyStatus", ["protected-omitted", "unavailable"], "AccessDesignInputFieldRejected");
            RequireClosedString(payload, "startupRole", ["autoexec", "not-autoexec", "unknown"], "AccessDesignInputRecordMalformed");
        }
        if (kind == "source-gap")
        {
            RequireClosedString(payload, "classification",
                ["catalog-partial", "coordinates-unavailable", "source-unavailable", "unsupported-object", "producer-limit-reached"],
                "AccessDesignInputRecordMalformed");
            RequireClosedString(payload, "affectedScope",
                ["database", "catalog", "ui", "vba", "macro"],
                "AccessDesignInputRecordMalformed");
            RequireClosedString(payload, "coverageCategory",
                ["source-declared-partial", "source-unavailable", "unsupported", "limit-reached"],
                "AccessDesignInputRecordMalformed");
        }
    }

    private static void ValidateSourceRole(
        string kind,
        AccessDesignEvidenceSource source,
        JsonElement payload)
    {
        var allowed = kind switch
        {
            "catalog-object" => new[] { "catalog-export" },
            "ui-design-document" or "ui-surface" or "ui-control" => new[] { "form-design-export", "report-design-export" },
            "vba-module" => new[] { "vba-module-export" },
            "event-reference" => new[] { "form-design-export", "report-design-export", "vba-module-export" },
            "macro-inventory" => new[] { "macro-inventory-export" },
            "source-gap" => new[] { "producer-gap" },
            _ => []
        };
        if (!allowed.Contains(source.DocumentRole, StringComparer.Ordinal))
            throw new AccessScanException("AccessDesignInputRecordMalformed");

        string? role = kind switch
        {
            "ui-design-document" => payload.GetProperty("documentRole").GetString(),
            "ui-surface" => payload.GetProperty("surfaceRole").GetString(),
            _ => null
        };
        if (role is not null
            && !string.Equals(source.DocumentRole, $"{role}-design-export", StringComparison.Ordinal))
            throw new AccessScanException("AccessDesignInputRecordMalformed");
    }

    private static void ValidateEvents(JsonElement events, AccessLimits limits)
    {
        if (events.ValueKind != JsonValueKind.Object)
            throw new AccessScanException("AccessDesignInputRecordMalformed");
        if (events.EnumerateObject().Count() > limits.MaxChildrenPerObject)
            throw new AccessScanException("AccessDesignInputRecordLimitReached");
        foreach (var property in events.EnumerateObject())
        {
            if (!EventRoles.Contains(property.Name)
                || property.Value.ValueKind != JsonValueKind.String
                || (property.Value.GetString()?.Length ?? 0) > limits.MaxStringLength)
                throw new AccessScanException("AccessDesignInputRecordMalformed");
        }
    }

    private static void ValidateProtectedText(
        JsonElement payload,
        string textProperty,
        string hashProperty,
        string linesProperty,
        int maxLength,
        int maxLines)
    {
        var text = RequireStringValue(payload, textProperty, "AccessDesignInputRecordMalformed");
        if (text.Length > maxLength)
            throw new AccessScanException("AccessDesignInputRecordLimitReached");
        var expectedHash = RequireSha256(payload, hashProperty, "AccessDesignInputRecordMalformed");
        var actualHash = Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(text))).ToLowerInvariant();
        if (!FixedHashEquals(expectedHash, actualHash))
            throw new AccessScanException("AccessDesignInputHashMismatch");
        var actualLines = text.Length == 0 ? 0 : text.Count(character => character == '\n') + 1;
        var declaredLines = RequireNonNegativeInt(payload, linesProperty, "AccessDesignInputRecordMalformed");
        if (declaredLines != actualLines)
            throw new AccessScanException("AccessDesignInputCoordinateUnavailable");
        if (actualLines > maxLines)
            throw new AccessScanException("AccessDesignInputRecordLimitReached");
    }

    private static IReadOnlyList<NormalizedRecord> NormalizeRecords(
        IReadOnlyList<RawRecord> records,
        string databaseIdentityHash)
    {
        var byProducerId = new Dictionary<string, RawRecord>(StringComparer.Ordinal);
        foreach (var record in records)
            if (!byProducerId.TryAdd(record.RecordId, record))
                throw new AccessScanException("AccessDesignInputDuplicateProducerId");
        foreach (var control in records.Where(record => record.Kind == "ui-control"))
        {
            if (control.ParentRecordId is null
                || !byProducerId.TryGetValue(control.ParentRecordId, out var parent)
                || parent.Kind != "ui-surface")
                throw new AccessScanException("AccessDesignInputParentUnavailable");
        }
        foreach (var eventReference in records.Where(record => record.Kind == "event-reference"))
        {
            if (eventReference.ParentRecordId is null
                || !byProducerId.TryGetValue(eventReference.ParentRecordId, out var parent)
                || parent.Kind is not ("ui-surface" or "ui-control"))
                throw new AccessScanException("AccessDesignInputParentUnavailable");
        }

        var canonicalByProducerId = new Dictionary<string, string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        string Resolve(RawRecord record)
        {
            if (canonicalByProducerId.TryGetValue(record.RecordId, out var existing))
                return existing;
            if (!visiting.Add(record.RecordId))
                throw new AccessScanException("AccessDesignInputParentCycle");
            string? parentCanonical = null;
            if (record.ParentRecordId is not null)
            {
                if (!byProducerId.TryGetValue(record.ParentRecordId, out var parent))
                    throw new AccessScanException("AccessDesignInputParentUnavailable");
                parentCanonical = Resolve(parent);
            }
            var shape = PayloadShapes[record.Kind];
            var identityParts = shape.IdentityProperties
                .Select(name => CanonicalIdentityProperty(record.Payload, name))
                .ToArray();
            var material = string.Join('|',
                "access-design-record/v1",
                databaseIdentityHash,
                record.Kind,
                parentCanonical ?? "root",
                string.Join('|', identityParts));
            var canonical = $"access-design-record-{AccessSafeValues.RoleHash("access-design-record-identity", material)[..32]}";
            canonicalByProducerId.Add(record.RecordId, canonical);
            visiting.Remove(record.RecordId);
            return canonical;
        }

        var result = new List<NormalizedRecord>();
        foreach (var record in records)
        {
            var canonicalId = Resolve(record);
            var parentCanonical = record.ParentRecordId is null ? null : canonicalByProducerId[record.ParentRecordId];
            var contentHash = AccessSafeValues.RoleHash("access-design-record-content",
                string.Join('|', record.Kind, parentCanonical, record.Source.DocumentRole, record.Source.CoordinateStatus,
                    record.Source.DocumentSha256, record.Source.StartLine, record.Source.EndLine,
                    record.Completeness, CanonicalJson(record.Payload, PayloadShapes[record.Kind].IdentityProperties)));
            result.Add(new(
                new(record.Kind, canonicalId, parentCanonical, record.Source, record.Completeness, record.Payload),
                canonicalId,
                contentHash,
                record.RejectionClassifications));
        }
        return result;
    }

    private static string CanonicalIdentityProperty(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return $"{name}:null";
        return value.ValueKind switch
        {
            JsonValueKind.String => $"{name}:hash:{AccessSafeValues.RoleHash($"access-design-{name}", NormalizeIdentityString(name, value.GetString()))}",
            JsonValueKind.Number => $"{name}:int:{value.GetInt32()}",
            _ => throw new AccessScanException("AccessDesignInputRecordMalformed")
        };
    }

    private static string CanonicalJson(JsonElement element, IReadOnlyList<string> identityProperties)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            WriteCanonical(element, writer, identityProperties.ToHashSet(StringComparer.Ordinal));
        return StrictUtf8.GetString(stream.ToArray());
    }

    private static void WriteCanonical(
        JsonElement element,
        Utf8JsonWriter writer,
        IReadOnlySet<string> identityProperties,
        string? propertyName = null)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(property.Value, writer, identityProperties, property.Name);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonical(item, writer, identityProperties);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(propertyName is not null && identityProperties.Contains(propertyName)
                    ? NormalizeIdentityString(propertyName, element.GetString())
                    : element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText());
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new AccessScanException("AccessDesignInputRecordMalformed");
        }
    }

    private static string NormalizeIdentityString(string propertyName, string? value)
    {
        var normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormC).Trim();
        return propertyName == "identity" ? normalized.ToUpperInvariant() : normalized;
    }

    private static AccessDesignEvidenceGap? BindingGap(
        AccessDesignEvidenceManifestProjection manifest,
        AccessDesignEvidenceBinding binding)
    {
        if (!FixedHashEquals(manifest.RepositoryIdentityHash, binding.RepositoryIdentityHash))
            return new("AccessDesignInputRepositoryMismatch", "design-bundle");
        if (!string.Equals(manifest.CommitSha, binding.CommitSha, StringComparison.OrdinalIgnoreCase))
            return new("AccessDesignInputCommitMismatch", "design-bundle");
        if (!FixedHashEquals(manifest.BaseScanManifestSha256, binding.BaseScanManifestSha256))
            return new("AccessDesignInputBaseScanMismatch", "design-bundle");
        if (!FixedHashEquals(manifest.DatabaseIdentityHash, binding.DatabaseIdentityHash))
            return new("AccessDesignInputDatabaseUnbound", "design-bundle");
        if (manifest.CopyBinding == "unbound")
            return new("AccessDesignInputDatabaseUnbound", "design-bundle");
        if (manifest.CopyBinding == "hash-identical"
            && (!FixedHashEquals(manifest.SourceCopySha256, binding.DatabaseSha256)))
            return new("AccessDesignInputDatabaseUnbound", "design-bundle");
        return null;
    }

    private static void ValidateManifestCounts(
        AccessDesignEvidenceManifestProjection manifest,
        IReadOnlyList<RawRecord> records)
    {
        var actual = records.GroupBy(item => item.Kind, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        if (manifest.CountsByKind.Values.Sum() != records.Count
            || manifest.CountsByKind.Count != actual.Count
            || manifest.CountsByKind.Any(item => !actual.TryGetValue(item.Key, out var count) || count != item.Value))
            throw new AccessScanException("AccessDesignInputRecordCountMismatch");
    }

    private static string ValidateDirectory(string inputDirectory)
    {
        if (string.IsNullOrWhiteSpace(inputDirectory))
            throw new AccessScanException("AccessDesignInputPathMissing");
        var fullPath = Path.GetFullPath(inputDirectory);
        if (!Directory.Exists(fullPath))
            throw new AccessScanException("AccessDesignInputUnavailable");
        RejectReparsePoint(fullPath);
        return fullPath;
    }

    internal static void ValidateMembers(string directory, string manifestPath, string recordsPath)
    {
        try
        {
            var entries = Directory.EnumerateFileSystemEntries(directory).Select(Path.GetFileName)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (!entries.SequenceEqual([ManifestFileName, RecordsFileName], StringComparer.Ordinal))
                throw new AccessScanException("AccessDesignInputMembersInvalid");
            foreach (var path in new[] { manifestPath, recordsPath })
            {
                if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.Directory) != 0)
                    throw new AccessScanException("AccessDesignInputUnavailable");
                RejectReparsePoint(path);
            }
        }
        catch (AccessScanException) { throw; }
        catch
        {
            throw new AccessScanException("AccessDesignInputReadFailed");
        }
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new AccessScanException("AccessDesignInputReparsePointRejected");
    }

    internal static byte[] ReadBoundedFile(string path, long maximum, string classification, bool allowEmpty = false)
    {
        try
        {
            var length = new FileInfo(path).Length;
            if ((!allowEmpty && length <= 0) || length > maximum)
                throw new AccessScanException(classification);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var buffer = new MemoryStream(length > 0 && length <= int.MaxValue ? (int)length : 0);
            var chunk = new byte[64 * 1024];
            long observed = 0;
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                observed += read;
                if (observed > maximum)
                    throw new AccessScanException(classification);
                buffer.Write(chunk, 0, read);
            }
            if (!allowEmpty && observed == 0)
                throw new AccessScanException(classification);
            return buffer.ToArray();
        }
        catch (AccessScanException) { throw; }
        catch
        {
            throw new AccessScanException("AccessDesignInputReadFailed");
        }
    }

    private static string HashBytes(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void ValidateLimits(AccessLimits limits)
    {
        if (limits.MaxDesignManifestBytes <= 0 || limits.MaxDesignBundleBytes <= 0 || limits.MaxDesignRecords <= 0
            || limits.MaxStringLength <= 0 || limits.MaxChildrenPerObject <= 0
            || limits.MaxUiDesignTextLength <= 0 || limits.MaxUiDesignLines <= 0
            || limits.MaxVbaModuleTextLength <= 0 || limits.MaxVbaModuleLines <= 0)
            throw new AccessScanException("AccessInvalidLimitConfiguration");
    }

    private static JsonDocumentOptions DocumentOptions() => new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 32
    };

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new AccessScanException("AccessDesignInputDuplicateProperty");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                RejectDuplicateProperties(item);
        }
    }

    private static void RequireObject(JsonElement value, string classification)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new AccessScanException(classification);
    }

    private static JsonElement RequireObjectProperty(JsonElement parent, string name, string classification)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
            throw new AccessScanException(classification);
        return value;
    }

    private static void RequireOnlyProperties(JsonElement value, IEnumerable<string> allowed, string classification)
    {
        var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);
        if (value.EnumerateObject().Any(property => !allowedSet.Contains(property.Name)))
            throw new AccessScanException(classification);
    }

    private static void RequireString(JsonElement parent, string name, string expected, string classification)
    {
        if (!parent.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String
            || !string.Equals(value.GetString(), expected, StringComparison.Ordinal))
            throw new AccessScanException(classification);
    }

    private static string RequireStringValue(JsonElement parent, string name, string classification)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw new AccessScanException(classification);
        return value.GetString() ?? string.Empty;
    }

    private static string RequireClosedString(
        JsonElement parent,
        string name,
        IEnumerable<string> allowed,
        string classification)
    {
        var value = RequireStringValue(parent, name, classification);
        if (!allowed.Contains(value, StringComparer.Ordinal))
            throw new AccessScanException(classification);
        return value;
    }

    private static void OptionalClosedString(
        JsonElement parent,
        string name,
        IEnumerable<string> allowed,
        string classification)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return;
        if (value.ValueKind != JsonValueKind.String || !allowed.Contains(value.GetString() ?? string.Empty, StringComparer.Ordinal))
            throw new AccessScanException(classification);
    }

    private static string RequireSafeToken(JsonElement parent, string name, int maximum, string classification)
    {
        var value = RequireStringValue(parent, name, classification);
        if (!IsSafeToken(value, maximum))
            throw new AccessScanException(classification);
        return value;
    }

    private static string? OptionalSafeToken(JsonElement parent, string name, int maximum, string classification)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.String || !IsSafeToken(value.GetString(), maximum))
            throw new AccessScanException(classification);
        return value.GetString();
    }

    private static bool IsSafeToken(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= maximum
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    private static bool IsSafeStableKey(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && value.StartsWith("access-", StringComparison.Ordinal)
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');

    private static string RequireSha256(JsonElement parent, string name, string classification)
    {
        var value = RequireStringValue(parent, name, classification);
        if (!IsHex(value, 64))
            throw new AccessScanException(classification);
        return value.ToLowerInvariant();
    }

    private static string? OptionalSha256(JsonElement parent, string name, string classification)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.String || !IsHex(value.GetString(), 64))
            throw new AccessScanException(classification);
        return value.GetString()!.ToLowerInvariant();
    }

    private static string RequireCommitSha(JsonElement parent, string name)
    {
        var value = RequireStringValue(parent, name, "AccessDesignInputManifestInvalid");
        if (!IsHex(value, 40) && !IsHex(value, 64))
            throw new AccessScanException("AccessDesignInputManifestInvalid");
        return value.ToLowerInvariant();
    }

    private static bool IsHex(string? value, int length) =>
        value?.Length == length && value.All(Uri.IsHexDigit);

    private static bool FixedHashEquals(string left, string right)
    {
        if (!IsHex(left, 64) || !IsHex(right, 64))
            return false;
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(left),
            Convert.FromHexString(right));
    }

    private static int RequireNonNegativeInt(JsonElement parent, string name, string classification)
    {
        if (!parent.TryGetProperty(name, out var value) || !value.TryGetInt32(out var number) || number < 0)
            throw new AccessScanException(classification);
        return number;
    }

    private static int? OptionalPositiveInt(JsonElement parent, string name, string classification)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (!value.TryGetInt32(out var number) || number <= 0)
            throw new AccessScanException(classification);
        return number;
    }

    private sealed record PayloadShape(
        IReadOnlyList<string> Allowed,
        IReadOnlyList<string> Required,
        IReadOnlyList<string> IdentityProperties);

    private sealed record RawRecord(
        string Kind,
        string RecordId,
        string? ParentRecordId,
        AccessDesignEvidenceSource Source,
        string Completeness,
        JsonElement Payload,
        IReadOnlyList<string> RejectionClassifications);

    private sealed record NormalizedRecord(
        AccessDesignEvidenceRecord Record,
        string CanonicalRecordId,
        string CanonicalContentHash,
        IReadOnlyList<string> RejectionClassifications);

    private sealed record ValidatedSourceDocument(
        string DocumentRole,
        string DocumentSha256,
        int LineCount);
}
