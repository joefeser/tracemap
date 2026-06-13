package com.tracemap.jvm.reporting;

import com.tracemap.jvm.model.CodeFact;
import com.tracemap.jvm.model.ScanManifest;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Comparator;
import java.util.List;
import java.util.Map;
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
                .append(fact.evidence().filePath()).append(":").append(fact.evidence().startLine()).append("` |\n"));
        builder.append("\nJVM scans are static evidence only. They do not prove runtime dependency injection, reflection targets, generated source output, or route/serializer runtime configuration.\n");
        Files.writeString(path, builder.toString());
    }
}
