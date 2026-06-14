package com.tracemap.jvm;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import com.tracemap.jvm.extract.SqlShapeExtractor;
import com.tracemap.jvm.util.Hashes;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.regex.Pattern;
import org.junit.jupiter.api.Test;

final class SqlShapeExtractorTest {
    @Test
    void matchesPythonV1GoldenFixtureForRepresentativeCases() throws IOException {
        String json = Files.readString(repoRoot().resolve("samples/sql-shape-fixtures/sql-shape-v1.json"));
        assertFixture(json, "simple-select");
        assertFixture(json, "escaped-quote");
        assertFixture(json, "crlf-tabs");
    }

    @Test
    void withCteIsShapeHashOnly() throws IOException {
        Fixture fixture = fixture(Files.readString(repoRoot().resolve("samples/sql-shape-fixtures/sql-shape-v1.json")), "with-cte");
        SqlShapeExtractor.Shape shape = SqlShapeExtractor.queryShape(fixture.sql());

        assertEquals(fixture.queryShapeHash(), shape.queryShapeHash());
        assertEquals("", shape.operationName());
        assertTrue(shape.tableNames().isEmpty());
        assertTrue(shape.columnNames().isEmpty());
    }

    private static void assertFixture(String json, String name) {
        Fixture fixture = fixture(json, name);
        SqlShapeExtractor.Shape shape = SqlShapeExtractor.queryShape(fixture.sql());

        assertEquals(fixture.textHash(), Hashes.sha256(fixture.sql(), 32));
        assertEquals(fixture.queryShapeHash(), shape.queryShapeHash());
        assertEquals(fixture.operationName(), shape.operationName());
        assertEquals(fixture.tableNames(), String.join(";", shape.tableNames()));
        assertEquals(fixture.columnNames(), String.join(";", shape.columnNames()));
    }

    private static Fixture fixture(String json, String name) {
        var objectPattern = Pattern.compile("\\{\\s*\"name\":\\s*\"" + Pattern.quote(name) + "\".*?\\n    \\}", Pattern.DOTALL);
        var matcher = objectPattern.matcher(json);
        if (!matcher.find()) {
            throw new IllegalArgumentException("Missing SQL fixture " + name);
        }
        String object = matcher.group(0);
        return new Fixture(
            stringProperty(object, "sql"),
            stringProperty(object, "textHash"),
            stringProperty(object, "queryShapeHash"),
            stringProperty(object, "operationName"),
            stringProperty(object, "tableNames"),
            stringProperty(object, "columnNames"));
    }

    private static String stringProperty(String json, String key) {
        var matcher = Pattern.compile("\"" + key + "\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"").matcher(json);
        return matcher.find()
            ? matcher.group(1)
                .replace("\\r", "\r")
                .replace("\\n", "\n")
                .replace("\\t", "\t")
                .replace("\\\"", "\"")
                .replace("\\\\", "\\")
            : "";
    }

    private static Path repoRoot() {
        return Path.of(System.getProperty("user.dir")).toAbsolutePath().normalize().getParent().getParent();
    }

    private record Fixture(String sql, String textHash, String queryShapeHash, String operationName, String tableNames, String columnNames) {
    }
}
