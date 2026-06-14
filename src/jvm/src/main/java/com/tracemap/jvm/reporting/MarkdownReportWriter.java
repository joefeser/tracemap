package com.tracemap.jvm.reporting;

import com.tracemap.jvm.model.CodeFact;
import com.tracemap.jvm.model.FactTypes;
import com.tracemap.jvm.model.ScanManifest;
import com.tracemap.jvm.util.Hashes;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Arrays;
import java.util.Comparator;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import java.util.TreeMap;
import java.util.stream.Collectors;

public final class MarkdownReportWriter {
    private MarkdownReportWriter() {
    }

    public static void write(Path path, ScanManifest manifest, List<CodeFact> facts) throws IOException {
        Files.createDirectories(path.getParent());
        Map<String, Long> byType = facts.stream()
            .collect(Collectors.groupingBy(CodeFact::factType, TreeMap::new, Collectors.counting()));
        Map<String, Long> byTier = facts.stream()
            .collect(Collectors.groupingBy(CodeFact::evidenceTier, TreeMap::new, Collectors.counting()));
        StringBuilder builder = new StringBuilder();
        builder.append("# TraceMap JVM Scan Report\n\n");
        builder.append("| Field | Value |\n| --- | --- |\n");
        builder.append("| Repo | `").append(manifest.repoName()).append("` |\n");
        builder.append("| Commit | `").append(manifest.commitSha()).append("` |\n");
        builder.append("| Analysis level | `").append(manifest.analysisLevel()).append("` |\n");
        builder.append("| Build status | `").append(manifest.buildStatus()).append("` |\n");
        builder.append("| Scanner | `").append(manifest.scannerVersion()).append("` |\n\n");
        builder.append("## Fact Counts\n\n| Fact type | Count |\n| --- | ---: |\n");
        byType.forEach((type, count) -> builder.append("| `").append(type).append("` | ").append(count).append(" |\n"));
        builder.append("\n## Evidence Tiers\n\n| Tier | Count |\n| --- | ---: |\n");
        byTier.forEach((tier, count) -> builder.append("| `").append(tier).append("` | ").append(count).append(" |\n"));
        builder.append("\n## Known Gaps\n\n");
        if (manifest.knownGaps().isEmpty()) {
            builder.append("No known gaps recorded.\n");
        } else {
            manifest.knownGaps().stream().sorted().forEach(gap -> builder.append("- ").append(gap).append('\n'));
        }
        builder.append("\n## Evidence Sample\n\n| Fact | Tier | Rule | File |\n| --- | --- | --- | --- |\n");
        facts.stream()
            .sorted(Comparator.comparing(CodeFact::factType).thenComparing(f -> f.evidence().filePath()).thenComparing(f -> f.evidence().startLine()))
            .limit(50)
            .forEach(fact -> builder.append("| `")
                .append(fact.factType()).append("` | `")
                .append(fact.evidenceTier()).append("` | `")
                .append(fact.ruleId()).append("` | `")
                .append(safePath(fact.evidence().filePath())).append(":").append(fact.evidence().startLine()).append("` |\n"));
        List<CodeFact> queryPatternFacts = facts.stream()
            .filter(fact -> FactTypes.QUERY_PATTERN_DETECTED.equals(fact.factType()))
            .sorted(Comparator
                .comparing((CodeFact fact) -> safePath(fact.evidence().filePath()))
                .thenComparing(fact -> fact.evidence().startLine())
                .thenComparing(CodeFact::factId))
            .limit(50)
            .toList();
        if (!queryPatternFacts.isEmpty()) {
            builder.append("\n## Query Patterns\n\n");
            for (CodeFact fact : queryPatternFacts) {
                builder.append(formatQueryPattern(fact)).append('\n');
            }
            builder.append("\n- Query-pattern rows are static shape evidence. They do not prove runtime execution, database schema existence, SQL dialect validity, generated SQL equivalence, or branch feasibility.\n");
        }
        builder.append("\nJVM scans are static evidence only. They do not prove runtime dependency injection, reflection targets, generated source output, or route/serializer runtime configuration.\n");
        Files.writeString(path, builder.toString());
    }

    private static String formatQueryPattern(CodeFact fact) {
        return isSqlShapeQueryPattern(fact) ? formatSqlShapeQueryPattern(fact) : formatQueryBuilderPattern(fact);
    }

    private static boolean isSqlShapeQueryPattern(CodeFact fact) {
        String value = fact.properties().get("sqlSourceKind");
        return value != null && !value.isBlank();
    }

    private static String formatSqlShapeQueryPattern(CodeFact fact) {
        String operation = displayCodeValue(valueOrDefault(fact.properties().get("operationName"), "unknown"));
        String table = displayIdentifierValue(firstPresent(fact.properties().get("tableName"), fact.properties().get("tableNames")), IdentifierKind.TABLE, "unknown");
        String columns = displayIdentifierValue(firstPresent(fact.properties().get("columnNames"), fact.properties().get("fieldNames")), IdentifierKind.COLUMN, "none");
        String sourceKind = displayCodeValue(valueOrDefault(fact.properties().get("sqlSourceKind"), "unknown"));
        String shapeHash = displayCodeValue(valueOrDefault(fact.properties().get("queryShapeHash"), "n/a"));
        String filePath = safePath(fact.evidence().filePath());

        return "- SQL shape `" + operation + "` table `" + table + "` columns `" + columns
            + "` source `" + sourceKind + "` shape `" + shapeHash + "` rule `" + fact.ruleId()
            + "` (" + fact.evidenceTier() + ") at `" + filePath + ":" + fact.evidence().startLine() + "`";
    }

    private static String formatQueryBuilderPattern(CodeFact fact) {
        String operation = displayCodeValue(valueOrDefault(firstPresent(fact.properties().get("operationName"), fact.contractElement()), "unknown"));
        String patternHash = fact.properties().get("patternHash");
        String hashPart = patternHash == null || patternHash.isBlank() ? "" : " pattern `" + displayCodeValue(patternHash) + "`";
        String filePath = safePath(fact.evidence().filePath());
        return "- Query builder `" + operation + "` fields `" + displayFields(fact) + "`" + hashPart
            + " rule `" + fact.ruleId() + "` (" + fact.evidenceTier() + ") at `"
            + filePath + ":" + fact.evidence().startLine() + "`";
    }

    private static String displayFields(CodeFact fact) {
        String joined = List.of("filterFields", "sortFields", "selectFields", "includeFields", "mutationFields").stream()
            .map(key -> fact.properties().get(key))
            .filter(value -> value != null && !value.isBlank())
            .distinct()
            .collect(Collectors.joining(";"));
        return joined.isBlank() ? "none" : joined;
    }

    private static String displayIdentifierValue(String rawValue, IdentifierKind kind, String missingValue) {
        if (rawValue == null || rawValue.isBlank()) {
            return missingValue;
        }

        List<String> identifiers = Arrays.stream(rawValue.split("[,;|]"))
            .map(String::trim)
            .filter(value -> !value.isBlank())
            .map(value -> displayIdentifier(value, kind))
            .distinct()
            .toList();

        if (identifiers.isEmpty()) {
            return missingValue;
        }

        if (identifiers.size() <= 20) {
            return String.join(";", identifiers);
        }

        return String.join(";", identifiers.subList(0, 20)) + ";... and " + (identifiers.size() - 20) + " more";
    }

    private static String displayIdentifier(String value, IdentifierKind kind) {
        return isSafeIdentifier(value, kind) ? value : "unsafe-identifier-hash:" + Hashes.sha256(value, 32);
    }

    private static boolean isSafeIdentifier(String value, IdentifierKind kind) {
        if (value == null || value.isBlank() || value.length() > maxIdentifierLength(kind)) {
            return false;
        }

        if (value.contains("://") || value.contains("--") || value.contains("/*") || value.contains("*/")) {
            return false;
        }

        for (int index = 0; index < value.length(); index++) {
            char ch = value.charAt(index);
            boolean allowed = Character.isLetterOrDigit(ch) || ch == '_' || ch == '.' || ch == '-' || (kind == IdentifierKind.TABLE && ch == ' ');
            if (!allowed) {
                return false;
            }
        }

        for (String token : value.split("[ .-]+")) {
            if (SQL_KEYWORDS.contains(token.toLowerCase(Locale.ROOT))) {
                return false;
            }
        }

        return true;
    }

    private static int maxIdentifierLength(IdentifierKind kind) {
        return kind == IdentifierKind.TABLE ? 100 : 80;
    }

    private static String safePath(String filePath) {
        if (filePath == null || filePath.isBlank()) {
            return "n/a";
        }

        if (filePath.contains("://")) {
            return "absolute-path-hash:" + Hashes.sha256(filePath, 16);
        }

        boolean absolute = filePath.startsWith("/")
            || filePath.startsWith("\\")
            || filePath.contains(":/")
            || filePath.contains(":\\");
        if (!absolute) {
            try {
                absolute = Path.of(filePath).isAbsolute();
            } catch (RuntimeException exception) {
                return "absolute-path-hash:" + Hashes.sha256(filePath, 16);
            }
        }

        return absolute
            ? "absolute-path-hash:" + Hashes.sha256(filePath, 16)
            : filePath.replace('\\', '/');
    }

    private static String displayCodeValue(String value) {
        return value.replace('`', '\'').replace('\n', ' ').replace('\r', ' ');
    }

    private static String firstPresent(String first, String second) {
        return first != null && !first.isBlank() ? first : second;
    }

    private static String valueOrDefault(String value, String defaultValue) {
        return value == null || value.isBlank() ? defaultValue : value;
    }

    private enum IdentifierKind {
        TABLE,
        COLUMN
    }

    private static final Set<String> SQL_KEYWORDS = Set.of(
        "select",
        "from",
        "insert",
        "into",
        "values",
        "update",
        "set",
        "delete",
        "where",
        "join",
        "having",
        "group",
        "order",
        "by",
        "union",
        "create",
        "alter",
        "drop",
        "truncate",
        "merge",
        "exec");
}
