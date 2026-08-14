package com.tracemap.jvm.scan;

import com.tracemap.jvm.extract.BuildFileExtractor;
import com.tracemap.jvm.extract.ConfigExtractor;
import com.tracemap.jvm.extract.JavaSemanticExtractor;
import com.tracemap.jvm.extract.JavaSyntaxExtractor;
import com.tracemap.jvm.extract.KotlinSyntaxExtractor;
import com.tracemap.jvm.extract.SqlResourceExtractor;
import com.tracemap.jvm.facts.FactFactory;
import com.tracemap.jvm.model.CodeFact;
import com.tracemap.jvm.model.EvidenceTiers;
import com.tracemap.jvm.model.FactTypes;
import com.tracemap.jvm.model.FileInventoryItem;
import com.tracemap.jvm.model.GitMetadata;
import com.tracemap.jvm.model.RuleIds;
import com.tracemap.jvm.model.ScanManifest;
import com.tracemap.jvm.model.ScanOptions;
import com.tracemap.jvm.model.ScanResult;
import com.tracemap.jvm.model.ScannerVersions;
import com.tracemap.jvm.reporting.MarkdownReportWriter;
import com.tracemap.jvm.storage.JsonlFactWriter;
import com.tracemap.jvm.storage.ManifestWriter;
import com.tracemap.jvm.storage.SqliteIndexWriter;
import com.tracemap.jvm.util.Hashes;
import com.tracemap.jvm.util.PathsUtil;
import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.security.MessageDigest;
import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.HexFormat;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

public final class ScanEngine {
    private static final List<String> REQUIRED_OUTPUT_ARTIFACTS = List.of(
        "scan-manifest.json",
        "facts.ndjson",
        "index.sqlite",
        "report.md",
        "logs/analyzer.log");

    public ScanResult scan(ScanOptions options) throws Exception {
        return scan(options, () -> { }, () -> { });
    }

    ScanResult scan(ScanOptions options, Runnable beforeSnapshotVerification) throws Exception {
        return scan(options, beforeSnapshotVerification, () -> { });
    }

    ScanResult scan(ScanOptions options, Runnable beforeSnapshotVerification, Runnable afterManifestWrite) throws Exception {
        Path repo = options.repoPath().toAbsolutePath().normalize();
        if (!Files.isDirectory(repo)) {
            throw new IOException("Repository path does not exist: " + repo);
        }
        Path out = options.outputPath().toAbsolutePath().normalize();
        validateOutputTarget(repo, out);

        GitMetadata git = GitMetadataProvider.read(repo);
        List<FileInventoryItem> inventory = FileInventory.collect(options);
        String sourceSnapshotDigest = sourceSnapshotDigest(inventory);
        AnalysisGapCollector gaps = new AnalysisGapCollector();
        git.knownGaps().forEach(gaps::add);
        for (FileInventoryItem item : inventory) {
            if (item.skipped()) {
                gaps.add(item.skipReason() == null ? "FileSkipped: " + item.relativePath() : item.skipReason());
            }
        }

        ScanManifest provisional = manifest(options, git, inventory, sourceSnapshotDigest, gaps.gaps(), "Level3SyntaxAnalysis", "NotRun");
        List<CodeFact> facts = new ArrayList<>();
        facts.addAll(fileFacts(provisional, inventory));
        facts.addAll(BuildFileExtractor.extract(provisional, inventory, gaps));
        facts.addAll(ConfigExtractor.extract(provisional, inventory, gaps));
        facts.addAll(SqlResourceExtractor.extract(provisional, inventory, gaps));
        facts.addAll(JavaSyntaxExtractor.extract(provisional, inventory));
        facts.addAll(KotlinSyntaxExtractor.extract(provisional, inventory));

        boolean semanticAttempted = options.semantic() && inventory.stream().anyMatch(file -> "Java".equals(file.kind()) && !file.skipped());
        boolean semanticProduced = false;
        if (semanticAttempted) {
            List<CodeFact> semanticFacts = JavaSemanticExtractor.extract(provisional, repo, inventory, gaps);
            semanticProduced = !semanticFacts.isEmpty();
            facts.addAll(semanticFacts);
        }
        boolean hasKotlin = inventory.stream().anyMatch(file -> ("Kotlin".equals(file.kind()) || "KotlinScript".equals(file.kind())) && !file.skipped());
        if (hasKotlin) {
            gaps.add("KotlinSemanticNotImplemented: Kotlin files receive syntax fallback only.");
        }

        String analysisLevel;
        String buildStatus;
        if (!semanticAttempted || !semanticProduced) {
            analysisLevel = "Level3SyntaxAnalysis";
            buildStatus = "NotRun";
        } else if (gaps.hasGaps()) {
            analysisLevel = "Level1SemanticAnalysisReduced";
            buildStatus = "FailedOrPartial";
        } else {
            analysisLevel = "Level1SemanticAnalysis";
            buildStatus = "Succeeded";
        }

        ScanManifest manifest = manifest(options, git, inventory, sourceSnapshotDigest, gaps.gaps(), analysisLevel, buildStatus);
        facts = rewriteManifest(facts, provisional, manifest);
        facts.add(repoScanned(manifest, git));
        facts.add(buildStatus(manifest));
        for (String gap : gaps.gaps()) {
            facts.add(analysisGap(manifest, gap));
        }
        facts = dedupeAndSort(facts);

        beforeSnapshotVerification.run();
        List<FileInventoryItem> verifiedInventory = FileInventory.collect(options);
        if (!sourceSnapshotDigest.equals(sourceSnapshotDigest(verifiedInventory))) {
            throw new IOException("SourceSnapshotChangedDuringScan");
        }

        writeOutputsTransaction(out, manifest, facts, gaps.gaps(), afterManifestWrite);
        return new ScanResult(manifest, facts, inventory);
    }

    private static void writeOutputsTransaction(
        Path out,
        ScanManifest manifest,
        List<CodeFact> facts,
        List<String> gaps,
        Runnable afterManifestWrite) throws Exception {
        Path parent = out.getParent();
        if (parent == null) {
            throw new IOException("OutputParentUnavailable");
        }
        Files.createDirectories(parent);
        Path staging = Files.createTempDirectory(parent, ".tracemap-" + out.getFileName() + "-");
        Path previous = staging.resolveSibling(staging.getFileName() + ".previous");
        boolean previousMoved = false;
        try {
            Files.createDirectories(staging.resolve("logs"));
            ManifestWriter.write(staging.resolve("scan-manifest.json"), manifest);
            afterManifestWrite.run();
            JsonlFactWriter.write(staging.resolve("facts.ndjson"), facts);
            SqliteIndexWriter.write(staging.resolve("index.sqlite"), manifest, facts);
            MarkdownReportWriter.write(staging.resolve("report.md"), manifest, facts);
            Files.writeString(staging.resolve("logs/analyzer.log"), String.join(System.lineSeparator(), gaps) + System.lineSeparator());
            if (Files.exists(out)) {
                moveDirectory(out, previous);
                previousMoved = true;
            }
            try {
                moveDirectory(staging, out);
            } catch (Exception exception) {
                if (previousMoved && !Files.exists(out)) {
                    moveDirectory(previous, out);
                    previousMoved = false;
                }
                throw exception;
            }
            if (previousMoved) {
                deleteTree(previous);
                previousMoved = false;
            }
        } finally {
            deleteTree(staging);
            if (previousMoved && Files.exists(previous) && !Files.exists(out)) {
                moveDirectory(previous, out);
            }
        }
    }

    private static void validateOutputTarget(Path repo, Path out) throws IOException {
        if (repo.startsWith(out)) {
            throw new IOException("OutputArtifactSetNotReplaceable");
        }
        if (!Files.exists(out)) {
            return;
        }
        if (!Files.isDirectory(out)) {
            throw new IOException("OutputArtifactSetNotReplaceable");
        }
        try (var entries = Files.list(out)) {
            if (entries.findAny().isEmpty()) {
                return;
            }
        }
        if (REQUIRED_OUTPUT_ARTIFACTS.stream().allMatch(relative -> Files.isRegularFile(out.resolve(relative)))) {
            return;
        }
        throw new IOException("OutputArtifactSetNotReplaceable");
    }

    private static void moveDirectory(Path source, Path target) throws IOException {
        try {
            Files.move(source, target, StandardCopyOption.ATOMIC_MOVE);
        } catch (java.nio.file.AtomicMoveNotSupportedException exception) {
            Files.move(source, target);
        }
    }

    private static void deleteTree(Path root) throws IOException {
        if (!Files.exists(root)) {
            return;
        }
        try (var paths = Files.walk(root)) {
            for (Path path : paths.sorted(Comparator.reverseOrder()).toList()) {
                Files.deleteIfExists(path);
            }
        }
    }

    private static ScanManifest manifest(ScanOptions options, GitMetadata git, List<FileInventoryItem> inventory, String sourceSnapshotDigest, List<String> knownGaps, String analysisLevel, String buildStatus) {
        String repoIdentity = git.remoteUrl() == null ? git.repoName() : git.remoteUrl();
        String signature = String.join("|",
            framedValues(sortedPaths(options.projectPaths())),
            framedValues(options.includeGlobs()),
            framedValues(options.excludeGlobs()),
            options.language(),
            "semantic=" + options.semantic(),
            "maxFileByteSize=" + options.maxFileByteSize());
        String scanId = "scan-" + Hashes.sha256(repoIdentity + "|" + git.commitSha() + "|" + sourceSnapshotDigest + "|" + signature + "|" + ScannerVersions.TRACEMAP_JVM, 20);
        List<String> projects = inventory.stream()
            .filter(file -> file.kind().contains("Project") || file.kind().contains("Gradle"))
            .map(FileInventoryItem::relativePath)
            .distinct()
            .sorted()
            .toList();
        Path repo = options.repoPath().toAbsolutePath().normalize();
        String gitRootHash = git.gitRootPath() == null ? null : Hashes.sha256(git.gitRootPath().toAbsolutePath().normalize().toString(), 32);
        return new ScanManifest(
            scanId,
            git.repoName(),
            git.remoteUrl(),
            git.branch(),
            git.commitSha(),
            ScannerVersions.TRACEMAP_JVM,
            OffsetDateTime.now(ZoneOffset.UTC).toString(),
            analysisLevel,
            buildStatus,
            List.of(),
            projects,
            List.of("jvm"),
            knownGaps.stream().distinct().sorted().toList(),
            git.gitRootPath() == null ? null : PathsUtil.relativeUnix(git.gitRootPath(), repo),
            Hashes.sha256(repo.toString(), 32),
            gitRootHash,
            sourceSnapshotDigest);
    }

    private static String framedValues(List<String> values) {
        return values.stream()
            .sorted()
            .map(value -> value.getBytes(StandardCharsets.UTF_8).length + ":" + value)
            .collect(java.util.stream.Collectors.joining("", values.size() + ":", ""));
    }

    private static String sourceSnapshotDigest(List<FileInventoryItem> inventory) throws Exception {
        MessageDigest snapshot = MessageDigest.getInstance("SHA-256");
        snapshot.update("scan-truth-source-snapshot/v1\0".getBytes(StandardCharsets.UTF_8));
        for (FileInventoryItem item : inventory.stream().sorted(Comparator.comparing(FileInventoryItem::relativePath)).toList()) {
            if (item.skipped()) {
                updateSnapshotSegment(snapshot, item.relativePath());
                updateSnapshotSegment(snapshot, item.kind());
                updateSnapshotSegment(snapshot, Long.toString(item.sizeBytes()));
                updateSnapshotSegment(snapshot, "skipped-before-analysis");
                continue;
            }
            MessageDigest content = MessageDigest.getInstance("SHA-256");
            long length = 0;
            try (InputStream stream = Files.newInputStream(item.absolutePath())) {
                byte[] buffer = new byte[1024 * 1024];
                int count;
                while ((count = stream.read(buffer)) >= 0) {
                    if (count == 0) continue;
                    length += count;
                    content.update(buffer, 0, count);
                }
            }
            updateSnapshotSegment(snapshot, item.relativePath());
            updateSnapshotSegment(snapshot, item.kind());
            updateSnapshotSegment(snapshot, Long.toString(length));
            updateSnapshotSegment(snapshot, HexFormat.of().formatHex(content.digest()));
        }
        return HexFormat.of().formatHex(snapshot.digest());
    }

    private static void updateSnapshotSegment(MessageDigest digest, String value) {
        digest.update(value.getBytes(StandardCharsets.UTF_8));
        digest.update((byte) 0);
    }

    private static CodeFact repoScanned(ScanManifest manifest, GitMetadata git) {
        return FactFactory.create(
            manifest,
            FactTypes.REPO_SCANNED,
            RuleIds.REPO_MANIFEST,
            EvidenceTiers.TIER2_STRUCTURAL,
            FactFactory.evidence("scan-manifest.json", 1, 1, "RepoManifestExtractor", ScannerVersions.REPO_MANIFEST),
            null,
            null,
            manifest.repoName(),
            null,
            props("repoName", manifest.repoName(), "remoteUrl", git.remoteUrl(), "branch", git.branch(), "commitSha", manifest.commitSha()));
    }

    private static CodeFact buildStatus(ScanManifest manifest) {
        return FactFactory.create(
            manifest,
            FactTypes.BUILD_STATUS,
            RuleIds.REPO_MANIFEST,
            EvidenceTiers.TIER2_STRUCTURAL,
            FactFactory.evidence("scan-manifest.json", 1, 1, "RepoManifestExtractor", ScannerVersions.REPO_MANIFEST),
            null,
            null,
            manifest.buildStatus(),
            null,
            props("analysisLevel", manifest.analysisLevel(), "buildStatus", manifest.buildStatus()));
    }

    private static List<CodeFact> fileFacts(ScanManifest manifest, List<FileInventoryItem> inventory) {
        List<CodeFact> facts = new ArrayList<>();
        for (FileInventoryItem item : inventory) {
            facts.add(FactFactory.create(
                manifest,
                FactTypes.FILE_INVENTORIED,
                RuleIds.FILE_INVENTORY,
                EvidenceTiers.TIER2_STRUCTURAL,
                FactFactory.evidence(item.relativePath(), 1, 1, "FileInventory", ScannerVersions.FILE_INVENTORY),
                null,
                null,
                item.relativePath(),
                null,
                props(
                    "path", item.relativePath(),
                    "kind", item.kind(),
                    "sizeBytes", String.valueOf(item.sizeBytes()),
                    "skipped", String.valueOf(item.skipped()),
                    "skipReason", item.skipReason(),
                    "name", item.relativePath())));
        }
        return facts;
    }

    private static CodeFact analysisGap(ScanManifest manifest, String gap) {
        return FactFactory.create(
            manifest,
            FactTypes.ANALYSIS_GAP,
            RuleIds.REPO_MANIFEST,
            EvidenceTiers.TIER4_UNKNOWN,
            FactFactory.evidence("logs/analyzer.log", 1, 1, "AnalysisGapCollector", ScannerVersions.REPO_MANIFEST),
            null,
            null,
            gap,
            null,
            props("gapKind", gap.contains(":") ? gap.substring(0, gap.indexOf(':')) : gap, "messageHash", Hashes.sha256(gap, 32), "name", gap));
    }

    private static List<CodeFact> rewriteManifest(List<CodeFact> facts, ScanManifest oldManifest, ScanManifest manifest) {
        if (oldManifest.scanId().equals(manifest.scanId()) && oldManifest.analysisLevel().equals(manifest.analysisLevel()) && oldManifest.buildStatus().equals(manifest.buildStatus())) {
            return facts;
        }
        List<CodeFact> rewritten = new ArrayList<>();
        for (CodeFact fact : facts) {
            rewritten.add(FactFactory.create(
                manifest,
                fact.factType(),
                fact.ruleId(),
                fact.evidenceTier(),
                fact.evidence(),
                fact.projectPath(),
                fact.sourceSymbol(),
                fact.targetSymbol(),
                fact.contractElement(),
                fact.properties()));
        }
        return rewritten;
    }

    private static List<CodeFact> dedupeAndSort(List<CodeFact> facts) {
        Map<String, CodeFact> bySite = new LinkedHashMap<>();
        for (CodeFact fact : facts) {
            String key = fact.factType() + "|" + fact.evidence().filePath() + "|" + fact.evidence().startLine() + "|" + fact.evidence().endLine() + "|" + fact.targetSymbol() + "|" + fact.properties().getOrDefault("methodName", "") + "|" + fact.properties().getOrDefault("memberName", "") + "|" + fact.properties().getOrDefault("typeName", "");
            CodeFact existing = bySite.get(key);
            if (existing == null || tierRank(fact.evidenceTier()) < tierRank(existing.evidenceTier())) {
                bySite.put(key, fact);
            }
        }
        Set<String> seenIds = new LinkedHashSet<>();
        List<CodeFact> result = new ArrayList<>();
        for (CodeFact fact : bySite.values()) {
            if (seenIds.add(fact.factId())) {
                result.add(fact);
            }
        }
        result.sort(Comparator
            .comparing((CodeFact fact) -> fact.evidence().filePath())
            .thenComparing(fact -> fact.evidence().startLine())
            .thenComparing(CodeFact::factType)
            .thenComparing(CodeFact::factId));
        return result;
    }

    private static int tierRank(String tier) {
        return switch (tier) {
            case EvidenceTiers.TIER1_SEMANTIC -> 1;
            case EvidenceTiers.TIER2_STRUCTURAL -> 2;
            case EvidenceTiers.TIER3_SYNTAX_OR_TEXTUAL -> 3;
            default -> 4;
        };
    }

    private static List<String> sortedPaths(List<Path> paths) {
        return paths.stream()
            .map(Path::toString)
            .sorted()
            .toList();
    }

    private static Map<String, String> props(String... values) {
        Map<String, String> props = new LinkedHashMap<>();
        for (int i = 0; i + 1 < values.length; i += 2) {
            if (values[i + 1] != null) {
                props.put(values[i], values[i + 1]);
            }
        }
        return props;
    }
}
