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
import java.io.IOException;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import javax.xml.parsers.DocumentBuilderFactory;
import org.w3c.dom.Document;
import org.w3c.dom.Element;
import org.w3c.dom.NodeList;

public final class BuildFileExtractor {
    private static final Pattern GRADLE_COORDINATE = Pattern.compile("['\"]([A-Za-z0-9_.-]+):([A-Za-z0-9_.-]+):([^'\"]+)['\"]");
    private static final Pattern GRADLE_GROUP = Pattern.compile("\\bgroup\\s*=\\s*['\"]([^'\"]+)['\"]");
    private static final Pattern GRADLE_VERSION = Pattern.compile("\\bversion\\s*=\\s*['\"]([^'\"]+)['\"]");
    private static final Pattern GRADLE_INCLUDE = Pattern.compile("\\binclude\\s*\\(?\\s*['\"]([^'\"]+)['\"]");

    private BuildFileExtractor() {
    }

    public static List<CodeFact> extract(ScanManifest manifest, List<FileInventoryItem> files, AnalysisGapCollector gaps) {
        List<CodeFact> facts = new ArrayList<>();
        for (FileInventoryItem file : files) {
            if (file.skipped()) {
                continue;
            }
            switch (file.kind()) {
                case "MavenProject" -> extractMaven(manifest, file, facts, gaps);
                case "GradleBuild", "GradleSettings" -> extractGradle(manifest, file, facts, gaps);
                default -> {
                }
            }
        }
        return facts;
    }

    private static void extractMaven(ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts, AnalysisGapCollector gaps) {
        try {
            DocumentBuilderFactory factory = DocumentBuilderFactory.newInstance();
            factory.setFeature("http://apache.org/xml/features/disallow-doctype-decl", true);
            Document document = factory.newDocumentBuilder().parse(file.absolutePath().toFile());
            Element project = document.getDocumentElement();
            String groupId = firstText(project, "groupId");
            String artifactId = firstText(project, "artifactId");
            String version = firstText(project, "version");
            NodeList parentNodes = project.getElementsByTagName("parent");
            if (parentNodes.getLength() > 0 && parentNodes.item(0) instanceof Element parent) {
                if (groupId == null) groupId = firstText(parent, "groupId");
                if (version == null) version = firstText(parent, "version");
                String relativePath = firstText(parent, "relativePath");
                if (relativePath != null && !relativePath.isBlank()) {
                    var parentPath = file.absolutePath().getParent().resolve(relativePath).normalize();
                    if (!Files.exists(parentPath)) {
                        gaps.add("ParentPomNotLocal: " + file.relativePath());
                    }
                }
            }
            String packageName = safe(groupId) + ":" + safe(artifactId);
            facts.add(FactFactory.create(
                manifest,
                FactTypes.PROJECT_DECLARED,
                RuleIds.BUILD_FILE,
                EvidenceTiers.TIER2_STRUCTURAL,
                FactFactory.evidence(file.relativePath(), 1, 1, "BuildFileExtractor", ScannerVersions.BUILD_FILE),
                file.relativePath(),
                null,
                packageName,
                null,
                props("buildTool", "maven", "groupId", safe(groupId), "artifactId", safe(artifactId), "version", safe(version), "name", packageName)));

            NodeList dependencies = project.getElementsByTagName("dependency");
            for (int i = 0; i < dependencies.getLength(); i++) {
                if (!(dependencies.item(i) instanceof Element dep)) {
                    continue;
                }
                String depGroup = firstText(dep, "groupId");
                String depArtifact = firstText(dep, "artifactId");
                String depVersion = firstText(dep, "version");
                if (depGroup == null || depArtifact == null) {
                    continue;
                }
                if (depVersion != null && depVersion.contains("${")) {
                    gaps.add("DynamicBuildValue: " + file.relativePath() + " dependency " + depGroup + ":" + depArtifact);
                }
                String dependencyName = depGroup + ":" + depArtifact;
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.PACKAGE_REFERENCED,
                    RuleIds.BUILD_FILE,
                    EvidenceTiers.TIER2_STRUCTURAL,
                    FactFactory.evidence(file.relativePath(), 1, 1, "BuildFileExtractor", ScannerVersions.BUILD_FILE),
                    file.relativePath(),
                    packageName,
                    dependencyName,
                    null,
                    props("buildTool", "maven", "groupId", depGroup, "artifactId", depArtifact, "version", safe(depVersion), "name", dependencyName)));
            }
        } catch (Exception exception) {
            gaps.add("MavenParseFailed: " + file.relativePath() + " (" + exception.getClass().getSimpleName() + ")");
        }
    }

    private static void extractGradle(ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts, AnalysisGapCollector gaps) {
        try {
            String text = Files.readString(file.absolutePath());
            if (text.contains("libs.") || text.contains("versionCatalog") || text.contains("buildSrc") || text.contains("${")) {
                gaps.add("DynamicBuildValue: " + file.relativePath());
            }
            String group = firstMatch(GRADLE_GROUP, text, 1);
            String version = firstMatch(GRADLE_VERSION, text, 1);
            String projectName = file.relativePath();
            facts.add(FactFactory.create(
                manifest,
                FactTypes.PROJECT_DECLARED,
                RuleIds.BUILD_FILE,
                EvidenceTiers.TIER2_STRUCTURAL,
                FactFactory.evidence(file.relativePath(), 1, 1, "BuildFileExtractor", ScannerVersions.BUILD_FILE),
                file.relativePath(),
                null,
                projectName,
                null,
                props("buildTool", "gradle", "groupId", safe(group), "artifactId", projectName, "version", safe(version), "name", projectName)));
            Matcher dependencyMatcher = GRADLE_COORDINATE.matcher(text);
            while (dependencyMatcher.find()) {
                String dependencyName = dependencyMatcher.group(1) + ":" + dependencyMatcher.group(2);
                int line = lineOf(text, dependencyMatcher.start());
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.PACKAGE_REFERENCED,
                    RuleIds.BUILD_FILE,
                    EvidenceTiers.TIER2_STRUCTURAL,
                    FactFactory.evidence(file.relativePath(), line, line, "BuildFileExtractor", ScannerVersions.BUILD_FILE),
                    file.relativePath(),
                    projectName,
                    dependencyName,
                    null,
                    props("buildTool", "gradle", "groupId", dependencyMatcher.group(1), "artifactId", dependencyMatcher.group(2), "version", dependencyMatcher.group(3), "name", dependencyName)));
            }
            Matcher includeMatcher = GRADLE_INCLUDE.matcher(text);
            while (includeMatcher.find()) {
                int line = lineOf(text, includeMatcher.start());
                facts.add(FactFactory.create(
                    manifest,
                    FactTypes.PROJECT_DECLARED,
                    RuleIds.BUILD_FILE,
                    EvidenceTiers.TIER2_STRUCTURAL,
                    FactFactory.evidence(file.relativePath(), line, line, "BuildFileExtractor", ScannerVersions.BUILD_FILE),
                    file.relativePath(),
                    null,
                    includeMatcher.group(1),
                    null,
                    props("buildTool", "gradle", "includedProject", includeMatcher.group(1), "name", includeMatcher.group(1))));
            }
        } catch (IOException exception) {
            gaps.add("GradleParseFailed: " + file.relativePath());
        }
    }

    private static String firstText(Element parent, String tag) {
        NodeList nodes = parent.getElementsByTagName(tag);
        if (nodes.getLength() == 0 || nodes.item(0) == null) {
            return null;
        }
        String value = nodes.item(0).getTextContent();
        return value == null || value.isBlank() ? null : value.trim();
    }

    private static String firstMatch(Pattern pattern, String text, int group) {
        Matcher matcher = pattern.matcher(text);
        return matcher.find() ? matcher.group(group) : null;
    }

    private static int lineOf(String text, int offset) {
        int line = 1;
        for (int i = 0; i < Math.min(offset, text.length()); i++) {
            if (text.charAt(i) == '\n') line++;
        }
        return line;
    }

    private static String safe(String value) {
        return value == null ? "" : value;
    }

    private static Map<String, String> props(String... values) {
        Map<String, String> props = new LinkedHashMap<>();
        for (int i = 0; i + 1 < values.length; i += 2) {
            props.put(values[i], values[i + 1]);
        }
        return props;
    }
}
