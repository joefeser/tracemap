package com.tracemap.jvm.extract;

import com.tracemap.jvm.facts.FactFactory;
import com.tracemap.jvm.model.CodeFact;
import com.tracemap.jvm.model.EvidenceTiers;
import com.tracemap.jvm.model.FactTypes;
import com.tracemap.jvm.model.FileInventoryItem;
import com.tracemap.jvm.model.RuleIds;
import com.tracemap.jvm.model.ScanManifest;
import com.tracemap.jvm.model.ScannerVersions;
import com.tracemap.jvm.scan.AnalysisGapCollector;
import com.tracemap.jvm.util.Hashes;
import java.io.IOException;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

public final class SqlResourceExtractor {
    private SqlResourceExtractor() {
    }

    public static List<CodeFact> extract(ScanManifest manifest, List<FileInventoryItem> files, AnalysisGapCollector gaps) {
        List<CodeFact> facts = new ArrayList<>();
        for (FileInventoryItem file : files) {
            if (!"Sql".equals(file.kind()) || file.skipped()) {
                continue;
            }
            try {
                String sql = Files.readString(file.absolutePath());
                int lines = Math.max(1, sql.split("\\R", -1).length);
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.SQL_FILE_DECLARED,
                    RuleIds.SQL,
                    EvidenceTiers.TIER2_STRUCTURAL,
                    FactFactory.evidence(file.relativePath(), 1, lines, Hashes.sha256(sql, 32), "SqlResourceExtractor", ScannerVersions.INTEGRATION),
                    file.relativePath(),
                    null,
                    file.relativePath(),
                    file.relativePath(),
                    props("name", file.relativePath(), "fileKind", "Sql")));
                if (!SqlShapeExtractor.isSqlLike(sql)) {
                    continue;
                }
                Map<String, String> textProps = props("textHash", Hashes.sha256(sql, 32), "textLength", String.valueOf(sql.length()), "sqlSourceKind", "sql-file", "targetSymbol", file.relativePath());
                String operation = SqlShapeExtractor.operationName(sql);
                if (!operation.isBlank()) {
                    textProps.put("operationName", operation);
                }
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.SQL_TEXT_USED,
                    RuleIds.SQL,
                    EvidenceTiers.TIER2_STRUCTURAL,
                    FactFactory.evidence(file.relativePath(), 1, lines, Hashes.sha256(sql, 32), "SqlResourceExtractor", ScannerVersions.INTEGRATION),
                    file.relativePath(),
                    null,
                    file.relativePath(),
                    file.relativePath(),
                    textProps));
                Map<String, String> shapeProps = new LinkedHashMap<>(SqlShapeExtractor.queryShapeProperties(sql, "sql-file"));
                String target = shapeProps.getOrDefault("tableName", file.relativePath());
                shapeProps.put("targetSymbol", target);
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.QUERY_PATTERN_DETECTED,
                    RuleIds.SQL,
                    EvidenceTiers.TIER2_STRUCTURAL,
                    FactFactory.evidence(file.relativePath(), 1, lines, Hashes.sha256(sql, 32), "SqlResourceExtractor", ScannerVersions.INTEGRATION),
                    file.relativePath(),
                    null,
                    target,
                    target,
                    shapeProps));
            } catch (IOException exception) {
                gaps.add("SqlFileReadFailed: " + file.relativePath());
            }
        }
        return facts;
    }

    private static Map<String, String> props(String... values) {
        Map<String, String> props = new LinkedHashMap<>();
        for (int i = 0; i + 1 < values.length; i += 2) {
            props.put(values[i], values[i + 1]);
        }
        return props;
    }
}
