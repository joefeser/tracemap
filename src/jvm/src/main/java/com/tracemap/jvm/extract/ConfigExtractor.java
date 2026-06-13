package com.tracemap.jvm.extract;

import com.fasterxml.jackson.databind.JsonNode;
import com.tracemap.jvm.facts.FactFactory;
import com.tracemap.jvm.model.CodeFact;
import com.tracemap.jvm.model.EvidenceTiers;
import com.tracemap.jvm.model.FactTypes;
import com.tracemap.jvm.model.FileInventoryItem;
import com.tracemap.jvm.model.RuleIds;
import com.tracemap.jvm.model.ScanManifest;
import com.tracemap.jvm.model.ScannerVersions;
import com.tracemap.jvm.scan.AnalysisGapCollector;
import com.tracemap.jvm.storage.JsonSupport;
import com.tracemap.jvm.util.Hashes;
import java.io.IOException;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

public final class ConfigExtractor {
    private ConfigExtractor() {
    }

    public static List<CodeFact> extract(ScanManifest manifest, List<FileInventoryItem> files, AnalysisGapCollector gaps) {
        List<CodeFact> facts = new ArrayList<>();
        for (FileInventoryItem file : files) {
            if (file.skipped()) {
                continue;
            }
            switch (file.kind()) {
                case "PropertiesConfig" -> extractProperties(manifest, file, facts, gaps);
                case "JsonConfig" -> extractJson(manifest, file, facts, gaps);
                case "YamlConfig" -> extractYaml(manifest, file, facts, gaps);
                case "XmlConfig" -> facts.add(configFile(manifest, file, "xml"));
                default -> {
                }
            }
        }
        return facts;
    }

    private static void extractProperties(ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts, AnalysisGapCollector gaps) {
        facts.add(configFile(manifest, file, "properties"));
        try {
            List<String> lines = Files.readAllLines(file.absolutePath());
            for (int i = 0; i < lines.size(); i++) {
                String line = lines.get(i).trim();
                if (line.isBlank() || line.startsWith("#") || !line.contains("=")) {
                    continue;
                }
                String key = line.substring(0, line.indexOf('=')).trim();
                String value = line.substring(line.indexOf('=') + 1).trim();
                facts.add(configKey(manifest, file, key, value, i + 1, "properties"));
            }
        } catch (IOException exception) {
            gaps.add("ConfigParseFailed: " + file.relativePath());
        }
    }

    private static void extractJson(ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts, AnalysisGapCollector gaps) {
        facts.add(configFile(manifest, file, "json"));
        try {
            JsonNode root = JsonSupport.JSON.readTree(file.absolutePath().toFile());
            walkJson(manifest, file, root, "", facts);
        } catch (Exception exception) {
            gaps.add("ConfigParseFailed: " + file.relativePath() + " (" + exception.getClass().getSimpleName() + ")");
        }
    }

    private static void extractYaml(ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts, AnalysisGapCollector gaps) {
        facts.add(configFile(manifest, file, "yaml"));
        try {
            JsonNode root = JsonSupport.YAML.readTree(file.absolutePath().toFile());
            if (root != null) {
                walkJson(manifest, file, root, "", facts);
            }
        } catch (Exception exception) {
            gaps.add("ConfigParseFailed: " + file.relativePath() + " (" + exception.getClass().getSimpleName() + ")");
        }
    }

    private static void walkJson(ScanManifest manifest, FileInventoryItem file, JsonNode node, String path, List<CodeFact> facts) {
        if (node == null) {
            return;
        }
        if (node.isObject()) {
            Iterator<Map.Entry<String, JsonNode>> fields = node.fields();
            while (fields.hasNext()) {
                Map.Entry<String, JsonNode> entry = fields.next();
                String next = path.isBlank() ? entry.getKey() : path + "." + entry.getKey();
                if (entry.getValue().isValueNode()) {
                    facts.add(configKey(manifest, file, next, entry.getValue().asText(), 1, "json"));
                }
                walkJson(manifest, file, entry.getValue(), next, facts);
            }
        } else if (node.isArray()) {
            for (int i = 0; i < node.size(); i++) {
                String next = path.isBlank() ? "[" + i + "]" : path + "[" + i + "]";
                JsonNode child = node.get(i);
                if (child != null && child.isValueNode()) {
                    facts.add(configKey(manifest, file, next, child.asText(), 1, "json"));
                }
                walkJson(manifest, file, child, next, facts);
            }
        }
    }

    private static CodeFact configFile(ScanManifest manifest, FileInventoryItem file, String kind) {
        return FactFactory.create(
            manifest,
            FactTypes.CONFIG_FILE_DECLARED,
            RuleIds.CONFIG,
            EvidenceTiers.TIER2_STRUCTURAL,
            FactFactory.evidence(file.relativePath(), 1, 1, "ConfigExtractor", ScannerVersions.CONFIG),
            file.relativePath(),
            null,
            file.relativePath(),
            null,
            props("configKind", kind, "name", file.relativePath()));
    }

    private static CodeFact configKey(ScanManifest manifest, FileInventoryItem file, String key, String value, int line, String kind) {
        return FactFactory.create(
            manifest,
            FactTypes.CONFIG_KEY_DECLARED,
            RuleIds.CONFIG,
            EvidenceTiers.TIER2_STRUCTURAL,
            FactFactory.evidence(file.relativePath(), line, line, "ConfigExtractor", ScannerVersions.CONFIG),
            file.relativePath(),
            null,
            key,
            key,
            props("configKind", kind, "keyPath", key, "name", key, "valueKind", classify(value), "valueHash", Hashes.sha256(value, 32)));
    }

    private static String classify(String value) {
        if (value == null) return "null";
        if (value.matches("-?\\d+(\\.\\d+)?")) return "number";
        if ("true".equalsIgnoreCase(value) || "false".equalsIgnoreCase(value)) return "boolean";
        return "string";
    }

    private static Map<String, String> props(String... values) {
        Map<String, String> props = new LinkedHashMap<>();
        for (int i = 0; i + 1 < values.length; i += 2) {
            props.put(values[i], values[i + 1]);
        }
        return props;
    }
}
