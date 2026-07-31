using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TraceMap.Access;

public sealed record AccessHiddenIdentityProjectionResult(string OutputDirectory, int IdentityCount);

public static class AccessHiddenIdentityProjection
{
    public const string SchemaVersion = "tracemap.access-hidden-identity.v1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<AccessHiddenIdentityProjectionResult> WriteAsync(
        string baseScanDirectory,
        string designEvidenceDirectory,
        string outputDirectory,
        AccessLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        limits ??= AccessLimits.Default;
        var output = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(output) || File.Exists(output))
            throw new AccessScanException("AccessHiddenIdentityOutputExists");
        var baseScan = await AccessDesignEvidenceComposer.ReadBaseScanAsync(baseScanDirectory, limits, cancellationToken);
        var declared = AccessDesignEvidenceReader.InspectManifest(designEvidenceDirectory, limits);
        var databaseSeed = AccessSafeValues.DatabaseIdentitySeed(
            declared.RepositoryIdentityHash,
            baseScan.Manifest.CommitSha,
            baseScan.DatabasePath,
            baseScan.DatabaseHash);
        if (!AccessSafeValues.FixedHashEquals(databaseSeed, declared.DatabaseIdentityHash)
            || !string.Equals(
                AccessSafeValues.DatabaseStableKey(databaseSeed),
                baseScan.DatabaseStableKey,
                StringComparison.Ordinal))
            throw new AccessScanException("AccessDesignInputDatabaseUnbound");
        var binding = new AccessDesignEvidenceBinding(
            declared.RepositoryIdentityHash,
            baseScan.Manifest.CommitSha,
            baseScan.ManifestSha256,
            databaseSeed,
            baseScan.DatabaseHash);
        using var bundle = AccessDesignEvidenceReader.Read(designEvidenceDirectory, binding, limits);
        if (!bundle.AcceptedForProjection)
            throw new AccessScanException(bundle.Gaps.FirstOrDefault()?.Classification ?? "AccessDesignInputDatabaseUnbound");

        var rows = BuildRows(bundle, databaseSeed, baseScan.Facts);
        var parent = Path.GetDirectoryName(output) ?? throw new AccessScanException("AccessHiddenIdentityOutputInvalid");
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, $".{Path.GetFileName(output)}.access-identities-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(staging);
            var payloadPath = Path.Combine(staging, "access-identities.json");
            var payload = new
            {
                schemaVersion = SchemaVersion,
                claimLevel = "hidden",
                commitSha = baseScan.Manifest.CommitSha,
                baseScanManifestSha256 = baseScan.ManifestSha256,
                designBundleSha256 = bundle.Manifest.BundleSha256,
                identities = rows,
                limitations = new[]
                {
                    "local-owner-review-only",
                    "no-combine-vault-public-or-release-propagation",
                    "no-runtime-reachability-execution-row-state-or-completeness-claim"
                }
            };
            await File.WriteAllTextAsync(
                payloadPath,
                JsonSerializer.Serialize(payload, JsonOptions) + "\n",
                new UTF8Encoding(false),
                cancellationToken);
            var bytes = await File.ReadAllBytesAsync(payloadPath, cancellationToken);
            var manifest = new
            {
                schemaVersion = SchemaVersion,
                claimLevel = "hidden",
                independentlyDeletable = true,
                files = new[]
                {
                    new
                    {
                        path = "access-identities.json",
                        sizeBytes = bytes.LongLength,
                        sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()
                    }
                }
            };
            await File.WriteAllTextAsync(
                Path.Combine(staging, "access-identity-manifest.json"),
                JsonSerializer.Serialize(manifest, JsonOptions) + "\n",
                new UTF8Encoding(false),
                cancellationToken);
            Directory.Move(staging, output);
            return new(output, rows.Count);
        }
        catch
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            throw;
        }
    }

    private static IReadOnlyList<object> BuildRows(
        AccessDesignEvidenceBundle bundle,
        string databaseSeed,
        IReadOnlyList<TraceMap.Core.CodeFact> baseFacts)
    {
        var rows = new List<object>();
        var stableByRecord = new Dictionary<string, string>(StringComparer.Ordinal);
        var emittedCatalogRecords = new HashSet<string>(StringComparer.Ordinal);
        var emittedUiStableKeys = new HashSet<string>(StringComparer.Ordinal);
        var catalogRecords = bundle.Records.Where(record => record.Kind == "catalog-object").ToArray();
        var directBindingIdentifiers = CollectDirectBindingIdentifiers(bundle, catalogRecords);
        foreach (var record in catalogRecords.Where(record =>
                     String(record, "objectRole") is "form" or "report" or "table" or "saved-query"))
        {
            var role = String(record, "objectRole");
            var identity = Optional(record, "identity");
            if (identity is null) continue;
            var calculated = AccessSafeValues.Identity(
                databaseSeed,
                role == "saved-query" ? "query" : role,
                identity,
                disclosurePolicy: AccessIdentityDisclosurePolicy.HashOnly).StableKey;
            var stable = ResolveBaseStableKey(baseFacts, role, identity, null, calculated)
                ?? calculated;
            stableByRecord[record.CanonicalRecordId] = stable;
            if (role is not ("form" or "report")
                && !directBindingIdentifiers.Contains(identity))
                continue;
            emittedCatalogRecords.Add(record.CanonicalRecordId);
            rows.Add(new
            {
                recordKind = record.Kind,
                role,
                stableKey = stable,
                parentStableKey = record.ParentCanonicalRecordId is null
                    ? null
                    : stableByRecord.GetValueOrDefault(record.ParentCanonicalRecordId),
                identity
            });
        }
        foreach (var record in catalogRecords.Where(record =>
                     String(record, "objectRole") is "table-field" or "query-field"))
        {
            var role = String(record, "objectRole");
            var identity = Optional(record, "identity");
            if (identity is null
                || !directBindingIdentifiers.Contains(identity)
                || record.ParentCanonicalRecordId is null
                || !stableByRecord.TryGetValue(record.ParentCanonicalRecordId, out var parentStable))
                continue;
            var calculated = AccessSafeValues.Identity(
                databaseSeed,
                role == "query-field" ? $"query-field-{parentStable}" : $"field-{parentStable}",
                identity,
                role == "query-field" ? OptionalInt(record, "ordinal") ?? 0 : 0,
                AccessIdentityDisclosurePolicy.HashOnly).StableKey;
            var stable = ResolveBaseStableKey(baseFacts, role, identity, parentStable, calculated)
                ?? calculated;
            stableByRecord[record.CanonicalRecordId] = stable;
            rows.Add(new
            {
                recordKind = record.Kind,
                role,
                stableKey = stable,
                parentStableKey = emittedCatalogRecords.Contains(record.ParentCanonicalRecordId)
                    ? parentStable
                    : null,
                identity
            });
        }
        foreach (var surface in bundle.Records.Where(record => record.Kind == "ui-surface"))
        {
            var role = String(surface, "surfaceRole");
            var name = String(surface, "identity");
            var stable = AccessSafeValues.Identity(databaseSeed, role, name, disclosurePolicy: AccessIdentityDisclosurePolicy.HashOnly).StableKey;
            stableByRecord[surface.CanonicalRecordId] = stable;
            emittedUiStableKeys.Add(stable);
            rows.Add(new
            {
                recordKind = surface.Kind,
                role,
                stableKey = stable,
                identity = name,
                bindings = DirectBindings(surface, ["recordSource", "filter", "orderBy"])
            });
            foreach (var control in bundle.Records.Where(record =>
                         record.Kind == "ui-control"
                         && record.ParentCanonicalRecordId == surface.CanonicalRecordId))
            {
                var ordinal = OptionalInt(control, "ordinal") ?? 0;
                var controlName = String(control, "identity");
                var controlStable = AccessSafeValues.Identity(
                    databaseSeed,
                    $"control-{stable}",
                    controlName,
                    ordinal,
                    AccessIdentityDisclosurePolicy.HashOnly).StableKey;
                emittedUiStableKeys.Add(controlStable);
                rows.Add(new
                {
                    recordKind = control.Kind,
                    role = "control",
                    stableKey = controlStable,
                    parentStableKey = stable,
                    identity = controlName,
                    controlType = OptionalInt(control, "controlType"),
                    bindings = DirectBindings(control,
                        ["controlSource", "rowSource", "sourceObject", "linkMasterFields", "linkChildFields"])
                });
            }
        }
        foreach (var document in bundle.Records.Where(record => record.Kind == "ui-design-document"))
        {
            var parent = bundle.Records.FirstOrDefault(record =>
                record.Kind == "catalog-object"
                && record.CanonicalRecordId == document.ParentCanonicalRecordId);
            if (parent is null) continue;
            var role = String(document, "documentRole");
            var surfaceName = String(parent, "identity");
            var parsed = AccessUiTextParser.Parse(
                new StringReader(String(document, "designText")),
                surfaceName,
                role);
            if (parsed.Surface is not { } raw) continue;
            var surfaceStable = AccessSafeValues.Identity(
                databaseSeed,
                role,
                surfaceName,
                disclosurePolicy: AccessIdentityDisclosurePolicy.HashOnly).StableKey;
            if (emittedUiStableKeys.Add(surfaceStable))
            {
                rows.Add(new
                {
                    recordKind = "ui-design-document",
                    role,
                    stableKey = surfaceStable,
                    identity = surfaceName,
                    bindings = DirectBindings(new Dictionary<string, string?>
                    {
                        ["recordSource"] = raw.RecordSource,
                        ["filter"] = raw.Filter,
                        ["orderBy"] = raw.OrderBy
                    })
                });
            }
            foreach (var control in raw.Controls)
            {
                var controlStable = AccessSafeValues.Identity(
                    databaseSeed,
                    $"control-{surfaceStable}",
                    control.Name,
                    control.Ordinal,
                    AccessIdentityDisclosurePolicy.HashOnly).StableKey;
                if (!emittedUiStableKeys.Add(controlStable)) continue;
                rows.Add(new
                {
                    recordKind = "ui-design-document-control",
                    role = "control",
                    stableKey = controlStable,
                    parentStableKey = surfaceStable,
                    identity = control.Name,
                    controlType = control.ControlType,
                    rowSourceType = control.RowSourceType,
                    boundColumn = control.BoundColumn,
                    columnCount = control.ColumnCount,
                    bindings = DirectBindings(new Dictionary<string, string?>
                    {
                        ["controlSource"] = control.ControlSource,
                        ["rowSource"] = control.RowSource,
                        ["sourceObject"] = control.SourceObject,
                        ["linkMasterFields"] = control.LinkMasterFields,
                        ["linkChildFields"] = control.LinkChildFields
                    })
                });
            }
        }
        return rows
            .OrderBy(row => JsonSerializer.Serialize(row), StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, object> DirectBindings(
        AccessDesignEvidenceRecord record,
        IReadOnlyList<string> properties) =>
        DirectBindings(properties.Select(property =>
            new KeyValuePair<string, string?>(property, Optional(record, property))));

    private static IReadOnlyDictionary<string, object> DirectBindings(
        IEnumerable<KeyValuePair<string, string?>> source)
    {
        var values = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var item in source)
        {
            if (string.IsNullOrWhiteSpace(item.Value)) continue;
            if (IsDirectBinding(item.Key, item.Value))
                values[item.Key] = item.Value;
            else
                values[item.Key] = new
                {
                    category = "protected-expression",
                    length = item.Value.Length,
                    sha256 = AccessSafeValues.RoleHash($"access-hidden-{item.Key}", item.Value)
                };
        }
        return values;
    }

    private static HashSet<string> CollectDirectBindingIdentifiers(
        AccessDesignEvidenceBundle bundle,
        IReadOnlyList<AccessDesignEvidenceRecord> catalogRecords)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(IReadOnlyDictionary<string, object> bindings)
        {
            foreach (var value in bindings.Values.OfType<string>())
            {
                foreach (var part in value.Split(
                             [',', ';'],
                             StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var candidate = part;
                    if (candidate.StartsWith("Form.", StringComparison.OrdinalIgnoreCase))
                        candidate = candidate[5..];
                    else if (candidate.StartsWith("Report.", StringComparison.OrdinalIgnoreCase))
                        candidate = candidate[7..];
                    candidate = candidate.Trim();
                    if (candidate.StartsWith('[') && candidate.EndsWith(']'))
                        candidate = candidate[1..^1];
                    if (candidate.Length == 0) continue;
                    result.Add(candidate);
                    var separator = candidate.LastIndexOf('.');
                    if (separator >= 0 && separator + 1 < candidate.Length)
                    {
                        var terminal = candidate[(separator + 1)..].Trim('[', ']');
                        if (terminal.Length > 0) result.Add(terminal);
                    }
                }
            }
        }

        foreach (var surface in bundle.Records.Where(record => record.Kind == "ui-surface"))
        {
            Add(DirectBindings(surface, ["recordSource", "filter", "orderBy"]));
            foreach (var control in bundle.Records.Where(record =>
                         record.Kind == "ui-control"
                         && record.ParentCanonicalRecordId == surface.CanonicalRecordId))
            {
                Add(DirectBindings(control,
                    ["controlSource", "rowSource", "sourceObject", "linkMasterFields", "linkChildFields"]));
            }
        }
        foreach (var document in bundle.Records.Where(record => record.Kind == "ui-design-document"))
        {
            var parent = catalogRecords.FirstOrDefault(record =>
                record.CanonicalRecordId == document.ParentCanonicalRecordId);
            if (parent is null) continue;
            var parsed = AccessUiTextParser.Parse(
                new StringReader(String(document, "designText")),
                String(parent, "identity"),
                String(document, "documentRole"));
            if (parsed.Surface is not { } raw) continue;
            Add(DirectBindings(new Dictionary<string, string?>
            {
                ["recordSource"] = raw.RecordSource,
                ["filter"] = raw.Filter,
                ["orderBy"] = raw.OrderBy
            }));
            foreach (var control in raw.Controls)
            {
                Add(DirectBindings(new Dictionary<string, string?>
                {
                    ["controlSource"] = control.ControlSource,
                    ["rowSource"] = control.RowSource,
                    ["sourceObject"] = control.SourceObject,
                    ["linkMasterFields"] = control.LinkMasterFields,
                    ["linkChildFields"] = control.LinkChildFields
                }));
            }
        }
        return result;
    }

    private static bool IsDirectBinding(string property, string value)
    {
        if (property is "filter" or "orderBy" or "validationRule") return false;
        if (property is "rowSource" or "recordSource")
        {
            if (value.Contains(';')
                || System.Text.RegularExpressions.Regex.IsMatch(
                    value,
                    @"(?i)\b(?:select|transform|parameters|insert|update|delete|execute|exec|from|join|where)\b"))
                return false;
            return IsIdentifier(value);
        }
        if (property == "controlSource")
            return IsIdentifier(value);
        if (property == "sourceObject")
        {
            var candidate = value.StartsWith("Form.", StringComparison.OrdinalIgnoreCase)
                ? value[5..]
                : value.StartsWith("Report.", StringComparison.OrdinalIgnoreCase)
                    ? value[7..]
                    : value;
            return IsIdentifier(candidate);
        }
        if (property is "linkMasterFields" or "linkChildFields")
            return value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .All(IsIdentifier);
        return false;
    }

    private static bool IsIdentifier(string item)
    {
        var candidate = item.Trim();
        if (candidate.StartsWith('[') && candidate.EndsWith(']'))
            candidate = candidate[1..^1];
        return candidate.Length is > 0 and <= 512
            && candidate.All(character =>
                char.IsLetterOrDigit(character)
                || character is '_' or ' ' or '-' or '.');
    }

    private static string? ResolveBaseStableKey(
        IReadOnlyList<TraceMap.Core.CodeFact> baseFacts,
        string role,
        string identity,
        string? parentStableKey,
        string calculated)
    {
        var factType = role switch
        {
            "table" => TraceMap.Core.FactTypes.LegacyDataEntityDeclared,
            "saved-query" => TraceMap.Core.FactTypes.AccessQueryDeclared,
            "table-field" => TraceMap.Core.FactTypes.LegacyDataColumnDeclared,
            "query-field" => TraceMap.Core.FactTypes.AccessQueryOutputDeclared,
            _ => null
        };
        if (factType is null) return null;
        var expectedHash = role switch
        {
            "table" => AccessSafeValues.RoleHash("access-table-name", identity),
            "saved-query" => AccessSafeValues.RoleHash("access-query-name", identity),
            "table-field" when parentStableKey is not null =>
                AccessSafeValues.RoleHash($"access-field-{parentStableKey}-name", identity),
            "query-field" when parentStableKey is not null =>
                AccessSafeValues.RoleHash($"access-query-field-{parentStableKey}-name", identity),
            _ => string.Empty
        };
        var candidates = baseFacts.Where(fact =>
                fact.FactType == factType
                && (parentStableKey is null || fact.SourceSymbol == parentStableKey)
                && (fact.Properties.GetValueOrDefault("objectName") == identity
                    || fact.Properties.GetValueOrDefault("objectNameHash") == expectedHash
                    || fact.TargetSymbol == calculated))
            .Select(fact => fact.TargetSymbol)
            .Where(value => value is not null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static string String(AccessDesignEvidenceRecord record, string name) =>
        record.Payload.GetProperty(name).GetString() ?? string.Empty;

    private static string? Optional(AccessDesignEvidenceRecord record, string name) =>
        record.Payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? OptionalInt(AccessDesignEvidenceRecord record, string name) =>
        record.Payload.TryGetProperty(name, out var value) && value.TryGetInt32(out var result)
            ? result
            : null;
}
