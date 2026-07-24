using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TraceMap.SqlValidation;

public static partial class SqlValidationPlanReader
{
    private const int MaxPlanBytes = 64 * 1024;
    private static readonly HashSet<string> ServerRoles = new(StringComparer.Ordinal) { "admin", "source", "archive-target", "application-primary", "co-located", "validation" };
    private static readonly HashSet<string> DatabaseRoles = new(StringComparer.Ordinal) { "admin", "source-data", "archive-data", "application-data", "validation-only" };
    private static readonly HashSet<string> SchemaRoles = new(StringComparer.Ordinal) { "extension", "application", "archive", "validation", "unspecified" };
    private static readonly HashSet<string> ExecutionModes = new(StringComparer.Ordinal) { "manual", "scheduled", "validation-only", "migration-runner" };

    public static SqlValidationPlan Read(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length is <= 0 or > MaxPlanBytes)
                throw new SqlValidationHarnessException("SqlValidationPlanUnavailable");
            return Parse(File.ReadAllText(path));
        }
        catch (SqlValidationHarnessException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new SqlValidationHarnessException("SqlValidationPlanUnavailable");
        }
    }

    public static SqlValidationPlan Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16
            });
            var root = document.RootElement;
            RequireObject(root);
            RequireProperties(root, "schemaVersion", "artifactId", "repository", "commitSha", "observedAt", "expiresAt", "targetContext", "checks");
            if (RequiredString(root, "schemaVersion") != SqlValidationContract.PlanSchemaVersion)
                throw new SqlValidationHarnessException("SqlValidationPlanUnsupportedSchema");

            var artifactId = RequiredString(root, "artifactId");
            var repository = RequiredString(root, "repository");
            var commitSha = RequiredString(root, "commitSha");
            if (!SafeArtifactId().IsMatch(artifactId) || !SafeRepository().IsMatch(repository) || !CommitSha().IsMatch(commitSha))
                throw new SqlValidationHarnessException("SqlValidationPlanInvalidIdentity");

            var observedAt = RequiredTimestamp(root, "observedAt");
            var expiresAt = RequiredTimestamp(root, "expiresAt");
            if (expiresAt <= observedAt)
                throw new SqlValidationHarnessException("SqlValidationPlanInvalidTimeWindow");

            var contextElement = root.GetProperty("targetContext");
            RequireObject(contextElement);
            RequireProperties(contextElement, "engine", "serverRole", "databaseRole", "schemaRole", "executionMode");
            var context = new SqlValidationTargetContext(
                RequiredString(contextElement, "engine"),
                RequiredString(contextElement, "serverRole"),
                RequiredString(contextElement, "databaseRole"),
                RequiredString(contextElement, "schemaRole"),
                RequiredString(contextElement, "executionMode"));
            if (context.Engine != "postgresql" || !ServerRoles.Contains(context.ServerRole)
                || !DatabaseRoles.Contains(context.DatabaseRole) || !SchemaRoles.Contains(context.SchemaRole)
                || !ExecutionModes.Contains(context.ExecutionMode))
                throw new SqlValidationHarnessException("SqlValidationPlanInvalidContext");

            var checksElement = root.GetProperty("checks");
            if (checksElement.ValueKind != JsonValueKind.Array || checksElement.GetArrayLength() is < 1 or > 6)
                throw new SqlValidationHarnessException("SqlValidationPlanInvalidChecks");
            var checks = checksElement.EnumerateArray().Select(ParseCheck).OrderBy(check => check.Code, StringComparer.Ordinal).ToArray();
            if (checks.Select(check => check.Code).Distinct(StringComparer.Ordinal).Count() != checks.Length)
                throw new SqlValidationHarnessException("SqlValidationPlanDuplicateCheck");

            return new SqlValidationPlan(artifactId, repository, commitSha, observedAt, expiresAt, context, checks);
        }
        catch (SqlValidationHarnessException) { throw; }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            throw new SqlValidationHarnessException("SqlValidationPlanMalformed");
        }
    }

    private static SqlValidationPlanCheck ParseCheck(JsonElement element)
    {
        RequireObject(element);
        var code = RequiredString(element, "code");
        if (!SqlValidationContract.ExecutableAssertionCodes.Contains(code))
            throw new SqlValidationHarnessException("SqlValidationPlanUnsupportedCheck");

        if (code == "postgres.server-version-compatible")
        {
            RequireProperties(element, "code", "expectedMajor");
            if (!element.TryGetProperty("expectedMajor", out var value) || !value.TryGetInt32(out var major) || major is < 10 or > 99)
                throw new SqlValidationHarnessException("SqlValidationPlanInvalidExpectedMajor");
            return new SqlValidationPlanCheck(code, major, []);
        }

        RequireProperties(element, "code", "identifiers");
        var identifiersElement = element.GetProperty("identifiers");
        if (identifiersElement.ValueKind != JsonValueKind.Array || identifiersElement.GetArrayLength() is < 1 or > 32)
            throw new SqlValidationHarnessException("SqlValidationPlanInvalidIdentifiers");
        var identifiers = identifiersElement.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : string.Empty).ToArray();
        if (identifiers.Distinct(StringComparer.Ordinal).Count() != identifiers.Length || identifiers.Any(identifier => !IsValidIdentifier(code, identifier)))
            throw new SqlValidationHarnessException("SqlValidationPlanInvalidIdentifiers");
        return new SqlValidationPlanCheck(code, null, identifiers.Order(StringComparer.Ordinal).ToArray());
    }

    private static bool IsValidIdentifier(string code, string value) => code switch
    {
        "postgres.required-extension-available" => ExtensionName().IsMatch(value),
        "postgres.migration-schema-present" => QualifiedName().IsMatch(value),
        "postgres.permission-probe-authorized" => SimpleName().IsMatch(value),
        "postgres.archive-function-callable" => FunctionSignature().IsMatch(value),
        "postgres.scheduled-job-registered" => JobName().IsMatch(value),
        _ => false
    };

    private static void RequireObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new SqlValidationHarnessException("SqlValidationPlanMalformed");
    }

    private static void RequireProperties(JsonElement element, params string[] names)
    {
        var expected = names.ToHashSet(StringComparer.Ordinal);
        var actual = element.EnumerateObject().Select(property => property.Name).ToArray();
        if (actual.Length != expected.Count || actual.Any(name => !expected.Contains(name)) || expected.Any(name => !actual.Contains(name, StringComparer.Ordinal)))
            throw new SqlValidationHarnessException("SqlValidationPlanUnexpectedProperty");
    }

    private static string RequiredString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new SqlValidationHarnessException("SqlValidationPlanMalformed");
        return value.GetString()!;
    }

    private static DateTimeOffset RequiredTimestamp(JsonElement element, string name)
    {
        var text = RequiredString(element, name);
        if (!Rfc3339().IsMatch(text) || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
            throw new SqlValidationHarnessException("SqlValidationPlanInvalidTimestamp");
        return timestamp;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,95}$", RegexOptions.CultureInvariant)] private static partial Regex SafeArtifactId();
    [GeneratedRegex("^[A-Za-z0-9._-]+(?:/[A-Za-z0-9._-]+)?$", RegexOptions.CultureInvariant)] private static partial Regex SafeRepository();
    [GeneratedRegex("^[0-9a-f]{40,64}$", RegexOptions.CultureInvariant)] private static partial Regex CommitSha();
    [GeneratedRegex("^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}(?:\\.\\d+)?(?:Z|[+-]\\d{2}:\\d{2})$", RegexOptions.CultureInvariant)] private static partial Regex Rfc3339();
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,62}$", RegexOptions.CultureInvariant)] private static partial Regex ExtensionName();
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_$]{0,62}$", RegexOptions.CultureInvariant)] private static partial Regex SimpleName();
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_$]{0,62}\\.[A-Za-z_][A-Za-z0-9_$]{0,62}$", RegexOptions.CultureInvariant)] private static partial Regex QualifiedName();
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_$]{0,62}\\.[A-Za-z_][A-Za-z0-9_$]{0,62}\\([A-Za-z0-9_ ,\\[\\]]{0,120}\\)$", RegexOptions.CultureInvariant)] private static partial Regex FunctionSignature();
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._ -]{0,127}$", RegexOptions.CultureInvariant)] private static partial Regex JobName();
}
