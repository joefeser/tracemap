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
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Pattern;

/**
 * Deterministic offline parsing of checked-in Gradle dependency lockfiles.
 *
 * <p>gradle.lockfile rows carry exact resolved versions only: the format has no artifact digests
 * and its configuration names do not prove a direct versus transitive relation, so those
 * capabilities are reported as explicit analysis gaps instead of guesses. Gradle dependency
 * verification metadata ({@code gradle/verification-metadata.xml}) is deliberately not consumed:
 * its per-artifact checksums cannot be correlated to a package-decision record's digest because
 * the record contract does not identify the artifact form (module jar, sources, POM), so exact
 * artifact identity stays deferred rather than guessed.
 */
public final class GradleLockfileExtractor {
    private static final Pattern COORDINATE = Pattern.compile("^([^:=]+):([^:=]+):([^:=]+)$");
    private static final Pattern SAFE_COORDINATE_PART = Pattern.compile("[A-Za-z0-9][A-Za-z0-9_.-]*");
    private static final String DIGEST_GAP_MESSAGE =
        "gradle.lockfile provides resolved versions only; artifact digests are not available from this format";
    private static final String RELATION_GAP_MESSAGE =
        "gradle.lockfile configuration names do not prove a direct versus transitive dependency relation";

    private GradleLockfileExtractor() {
    }

    public static List<CodeFact> extract(ScanManifest manifest, List<FileInventoryItem> files, AnalysisGapCollector gaps) {
        List<CodeFact> facts = new ArrayList<>();
        for (FileInventoryItem file : files) {
            if (!"GradleLockfile".equals(file.kind()) || file.skipped()) {
                continue;
            }
            extractLockfile(manifest, file, facts, gaps);
        }
        return facts;
    }

    private static void extractLockfile(ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts, AnalysisGapCollector gaps) {
        byte[] raw;
        String text;
        try {
            raw = Files.readAllBytes(file.absolutePath());
            text = new String(raw, StandardCharsets.UTF_8);
        } catch (IOException exception) {
            gaps.add("GradleLockParseFailed: " + file.relativePath());
            return;
        }
        String lockfileHash = Hashes.sha256(raw, 32);
        String[] lines = text.split("\n", -1);
        boolean emittedRow = false;
        for (int i = 0; i < lines.length; i++) {
            String line = lines[i].trim();
            if (line.isEmpty() || line.startsWith("#")) {
                continue;
            }
            int separator = line.indexOf('=');
            if (separator < 0) {
                gaps.add("GradleLockRowMalformed: " + file.relativePath() + ":" + (i + 1));
                continue;
            }
            String coordinate = line.substring(0, separator).trim();
            if ("empty".equals(coordinate)) {
                gaps.add("GradleLockRowUnsupported: " + file.relativePath() + ":" + (i + 1));
                continue;
            }
            var matcher = COORDINATE.matcher(coordinate);
            if (!matcher.matches()) {
                gaps.add("GradleLockRowMalformed: " + file.relativePath() + ":" + (i + 1));
                continue;
            }
            String group = matcher.group(1);
            String artifact = matcher.group(2);
            String version = matcher.group(3).trim();
            if (!SAFE_COORDINATE_PART.matcher(group).matches() || !SAFE_COORDINATE_PART.matcher(artifact).matches() || version.isEmpty()) {
                gaps.add("GradleLockRowMalformed: " + file.relativePath() + ":" + (i + 1));
                continue;
            }
            String dependencyName = group + ":" + artifact;
            Map<String, String> props = props(
                "artifactId", artifact,
                "buildTool", "gradle",
                "dependencyGroup", "lockfile",
                "ecosystem", "gradle",
                "groupId", group,
                "lockfileHash", lockfileHash,
                "lockfilePath", file.relativePath(),
                "manifestKind", "gradle.lockfile",
                "name", dependencyName,
                "packageManager", "gradle",
                "packageName", dependencyName,
                "sourceKind", "lockfile",
                "surfaceKind", "package-config");
            putVersion(props, version);
            facts.add(FactFactory.create(
                manifest,
                FactTypes.PACKAGE_REFERENCED,
                RuleIds.BUILD_FILE,
                EvidenceTiers.TIER2_STRUCTURAL,
                FactFactory.evidence(file.relativePath(), i + 1, i + 1, "GradleLockfileExtractor", ScannerVersions.GRADLE_LOCKFILE),
                file.relativePath(),
                null,
                dependencyName,
                null,
                props));
            emittedRow = true;
        }
        if (emittedRow) {
            facts.add(capabilityGap(manifest, file, "LockfileDigestUnavailable", DIGEST_GAP_MESSAGE));
            facts.add(capabilityGap(manifest, file, "DirectTransitiveUnavailable", RELATION_GAP_MESSAGE));
        }
    }

    private static CodeFact capabilityGap(ScanManifest manifest, FileInventoryItem file, String kind, String message) {
        return FactFactory.create(
            manifest,
            FactTypes.ANALYSIS_GAP,
            RuleIds.BUILD_FILE,
            EvidenceTiers.TIER4_UNKNOWN,
            FactFactory.evidence(file.relativePath(), 1, 1, "GradleLockfileExtractor", ScannerVersions.GRADLE_LOCKFILE),
            file.relativePath(),
            null,
            kind + ":" + file.relativePath(),
            null,
            props("gapKind", kind, "message", message, "messageHash", Hashes.sha256(message, 32)));
    }

    static void putVersion(Map<String, String> props, String version) {
        if (version == null || version.isBlank()) {
            props.put("version", "");
            return;
        }
        String trimmed = version.trim();
        if (BuildFileExtractor.unsafePackageVersion(trimmed)) {
            props.put("versionHash", Hashes.sha256(trimmed, 32));
            props.put("redactionReason", "unsafe-package-version");
        } else {
            props.put("version", trimmed);
            props.put("resolvedVersion", trimmed);
        }
    }

    private static Map<String, String> props(String... values) {
        Map<String, String> props = new LinkedHashMap<>();
        for (int i = 0; i + 1 < values.length; i += 2) {
            props.put(values[i], values[i + 1]);
        }
        return props;
    }
}
