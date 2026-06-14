package com.tracemap.jvm.reporting;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import com.tracemap.jvm.model.CodeFact;
import com.tracemap.jvm.model.EvidenceSpan;
import com.tracemap.jvm.model.EvidenceTiers;
import com.tracemap.jvm.model.FactTypes;
import com.tracemap.jvm.model.RuleIds;
import com.tracemap.jvm.model.ScanManifest;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.Map;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

final class MarkdownReportWriterTest {
    @TempDir
    private Path temp;

    @Test
    void writesNoQueryPatternSectionWhenNoQueryPatternsExist() throws Exception {
        Path reportPath = temp.resolve("report.md");

        MarkdownReportWriter.write(reportPath, manifest(), List.of());

        String report = Files.readString(reportPath);
        assertFalse(report.contains("## Query Patterns"));
        assertTrue(report.contains("## Evidence Sample"));
        assertTrue(report.contains("JVM scans are static evidence only"));
    }

    @Test
    void writesSyntheticSqlShapeQueryPatternsWithoutRawSql() throws Exception {
        Path reportPath = temp.resolve("report.md");
        // Synthetic query-pattern fact: this tests report rendering only.
        CodeFact fact = fact(
            "fact-sql",
            "src/Orders.java",
            21,
            Map.of(
                "operationName", "SELECT",
                "tableName", "orders",
                "columnNames", "id,status,total",
                "sqlSourceKind", "jdbc-literal",
                "queryShapeHash", "abcdef0123456789abcdef0123456789",
                "rawSql", "SELECT id, status, total FROM orders WHERE secret = 'keep-out'"));

        MarkdownReportWriter.write(reportPath, manifest(), List.of(fact));

        String report = Files.readString(reportPath);
        assertTrue(report.contains("## Evidence Sample"));
        assertTrue(report.indexOf("## Evidence Sample") < report.indexOf("## Query Patterns"));
        assertTrue(report.indexOf("## Query Patterns") < report.indexOf("JVM scans are static evidence only"));
        assertTrue(report.contains("SQL shape `SELECT` table `orders` columns `id;status;total` source `jdbc-literal` shape `abcdef0123456789abcdef0123456789`"));
        assertTrue(report.contains("rule `jvm.integration.sql.v1`"));
        assertTrue(report.matches("(?s).*shape `[a-f0-9]{32}`.*"));
        assertTrue(report.contains("static shape evidence"));
        assertTrue(report.contains("runtime execution"));
        assertFalse(report.contains("fields `none`"));
        assertFalse(report.contains("secret"));
        assertFalse(report.contains("SELECT id, status, total FROM orders"));
    }

    @Test
    void writesQueryBuilderFallbackWhenSqlSourceKindIsAbsent() throws Exception {
        Path reportPath = temp.resolve("report.md");
        CodeFact fact = fact(
            "fact-builder",
            "src/Orders.java",
            31,
            Map.of(
                "operationName", "where",
                "filterFields", "status",
                "sortFields", "updated_at",
                "patternHash", "0123456789abcdef0123456789abcdef"));

        MarkdownReportWriter.write(reportPath, manifest(), List.of(fact));

        String report = Files.readString(reportPath);
        assertTrue(report.contains("Query builder `where` fields `status;updated_at` pattern `0123456789abcdef0123456789abcdef`"));
        assertFalse(report.contains("SQL shape `where`"));
    }

    @Test
    void hashesUnsafeIdentifiersAndAbsolutePaths() throws Exception {
        Path reportPath = temp.resolve("report.md");
        // Synthetic query-pattern fact: this tests report rendering only.
        CodeFact fact = fact(
            "fact-unsafe",
            "/tmp/private/Orders.java",
            41,
            Map.of(
                "operationName", "SELECT",
                "tableName", "orders WHERE tenant_id = 1",
                "columnNames", "id,password;status",
                "sqlSourceKind", "jdbc-literal",
                "queryShapeHash", "abcdef0123456789abcdef0123456789"));

        MarkdownReportWriter.write(reportPath, manifest(), List.of(fact));

        String report = Files.readString(reportPath);
        assertTrue(report.contains("unsafe-identifier-hash:"));
        assertTrue(report.contains("columns `id;password;status`"));
        assertTrue(report.contains("absolute-path-hash:"));
        assertFalse(report.contains("/tmp/private"));
        assertFalse(report.contains("WHERE tenant_id"));
    }

    private static CodeFact fact(String factId, String filePath, int line, Map<String, String> properties) {
        return new CodeFact(
            factId,
            "scan-test",
            "repo",
            "abc123",
            null,
            FactTypes.QUERY_PATTERN_DETECTED,
            RuleIds.SQL,
            EvidenceTiers.TIER2_STRUCTURAL,
            null,
            null,
            "query",
            new EvidenceSpan(filePath, line, line, null, "test", "test/1.0"),
            properties);
    }

    private static ScanManifest manifest() {
        return new ScanManifest(
            "scan-test",
            "repo",
            null,
            null,
            "abc123",
            "test",
            "2026-01-01T00:00:00Z",
            "Level1SemanticAnalysis",
            "Succeeded",
            List.of(),
            List.of(),
            List.of(),
            List.of(),
            null,
            null,
            null);
    }
}
