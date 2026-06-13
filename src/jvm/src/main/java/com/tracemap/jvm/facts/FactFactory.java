package com.tracemap.jvm.facts;

import com.tracemap.jvm.model.CodeFact;
import com.tracemap.jvm.model.EvidenceSpan;
import com.tracemap.jvm.model.ScanManifest;
import com.tracemap.jvm.util.Hashes;
import java.util.Map;
import java.util.TreeMap;

public final class FactFactory {
    private FactFactory() {
    }

    public static EvidenceSpan evidence(String filePath, int startLine, int endLine, String extractorId, String extractorVersion) {
        int safeStart = Math.max(1, startLine);
        int safeEnd = Math.max(safeStart, endLine);
        return new EvidenceSpan(filePath, safeStart, safeEnd, null, extractorId, extractorVersion);
    }

    public static EvidenceSpan evidence(String filePath, int startLine, int endLine, String snippetHash, String extractorId, String extractorVersion) {
        int safeStart = Math.max(1, startLine);
        int safeEnd = Math.max(safeStart, endLine);
        return new EvidenceSpan(filePath, safeStart, safeEnd, snippetHash, extractorId, extractorVersion);
    }

    public static CodeFact create(
        ScanManifest manifest,
        String factType,
        String ruleId,
        String evidenceTier,
        EvidenceSpan evidence,
        String projectPath,
        String sourceSymbol,
        String targetSymbol,
        String contractElement,
        Map<String, String> properties) {
        TreeMap<String, String> stableProperties = new TreeMap<>();
        if (properties != null) {
            for (Map.Entry<String, String> entry : properties.entrySet()) {
                if (entry.getValue() != null) {
                    stableProperties.put(entry.getKey(), entry.getValue());
                }
            }
        }
        String factId = createFactId(
            manifest.scanId(),
            factType,
            ruleId,
            evidence.filePath(),
            evidence.startLine(),
            evidence.endLine(),
            projectPath,
            sourceSymbol,
            targetSymbol,
            contractElement,
            stableProperties);
        return new CodeFact(
            factId,
            manifest.scanId(),
            manifest.repoName(),
            manifest.commitSha(),
            projectPath,
            factType,
            ruleId,
            evidenceTier,
            sourceSymbol,
            targetSymbol,
            contractElement,
            evidence,
            Map.copyOf(stableProperties));
    }

    public static String createFactId(
        String scanId,
        String factType,
        String ruleId,
        String filePath,
        int startLine,
        int endLine,
        String projectPath,
        String sourceSymbol,
        String targetSymbol,
        String contractElement,
        Map<String, String> properties) {
        StringBuilder builder = new StringBuilder();
        builder.append(scanId).append('|')
            .append(factType).append('|')
            .append(ruleId).append('|')
            .append(filePath).append('|')
            .append(startLine).append('|')
            .append(endLine).append('|')
            .append(projectPath == null ? "" : projectPath).append('|')
            .append(sourceSymbol == null ? "" : sourceSymbol).append('|')
            .append(targetSymbol == null ? "" : targetSymbol).append('|')
            .append(contractElement == null ? "" : contractElement);
        properties.entrySet().stream()
            .sorted(Map.Entry.comparingByKey())
            .forEach(entry -> builder.append('|').append(entry.getKey()).append('=').append(entry.getValue()));
        return "fact-" + Hashes.sha256(builder.toString(), 20);
    }
}
