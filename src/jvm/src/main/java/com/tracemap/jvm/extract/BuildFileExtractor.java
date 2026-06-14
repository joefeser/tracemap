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
import com.tracemap.jvm.util.Hashes;
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
import org.w3c.dom.Node;
import org.w3c.dom.NodeList;

public final class BuildFileExtractor {
    private static final Pattern GRADLE_COORDINATE = Pattern.compile("\\b([A-Za-z][A-Za-z0-9_]*)\\s*\\(?\\s*['\"]([A-Za-z0-9_.-]+):([A-Za-z0-9_.-]+):([^'\"]+)['\"]");
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
            String groupId = firstDirectText(project, "groupId");
            String artifactId = firstDirectText(project, "artifactId");
            String version = firstDirectText(project, "version");
            Element parent = firstDirectChild(project, "parent");
            if (parent != null) {
                if (groupId == null) groupId = firstDirectText(parent, "groupId");
                if (version == null) version = firstDirectText(parent, "version");
                String relativePath = firstDirectText(parent, "relativePath");
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
                props("buildTool", "maven", "ecosystem", "maven", "groupId", safe(groupId), "artifactId", safe(artifactId), "version", safe(version), "name", packageName, "manifestKind", "pom.xml", "packageManager", "maven", "sourceKind", "build-file")));

            NodeList dependencies = project.getElementsByTagName("dependency");
            for (int i = 0; i < dependencies.getLength(); i++) {
                if (!(dependencies.item(i) instanceof Element dep)) {
                    continue;
                }
                String depGroup = firstDirectText(dep, "groupId");
                String depArtifact = firstDirectText(dep, "artifactId");
                String depVersion = firstDirectText(dep, "version");
                String depScope = firstDirectText(dep, "scope");
                if (depGroup == null || depArtifact == null) {
                    continue;
                }
                if (depVersion != null && depVersion.contains("${")) {
                    gaps.add("DynamicBuildValue: " + file.relativePath() + " dependency " + depGroup + ":" + depArtifact);
                }
                String dependencyName = depGroup + ":" + depArtifact;
                Map<String, String> depProps = props("artifactId", depArtifact, "buildTool", "maven", "dependencyGroup", safe(depScope), "dependencyScope", mavenScope(depScope), "ecosystem", "maven", "groupId", depGroup, "manifestKind", "pom.xml", "name", dependencyName, "packageManager", "maven", "packageName", dependencyName, "sourceKind", "build-file", "surfaceKind", "package-config");
                putVersion(depProps, depVersion);
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
                    depProps));
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
                props("buildTool", "gradle", "ecosystem", "gradle", "groupId", safe(group), "artifactId", projectName, "version", safe(version), "name", projectName, "manifestKind", "gradle", "packageManager", "gradle", "sourceKind", "build-file")));
            Matcher dependencyMatcher = GRADLE_COORDINATE.matcher(text);
            while (dependencyMatcher.find()) {
                String configuration = dependencyMatcher.group(1);
                String dependencyName = dependencyMatcher.group(2) + ":" + dependencyMatcher.group(3);
                int line = lineOf(text, dependencyMatcher.start());
                Map<String, String> depProps = props("artifactId", dependencyMatcher.group(3), "buildTool", "gradle", "dependencyGroup", configuration, "dependencyScope", gradleScope(configuration), "ecosystem", "gradle", "groupId", dependencyMatcher.group(2), "manifestKind", "gradle", "name", dependencyName, "packageManager", "gradle", "packageName", dependencyName, "sourceKind", "build-file", "surfaceKind", "package-config");
                putVersion(depProps, dependencyMatcher.group(4));
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
                    depProps));
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

    private static String firstDirectText(Element parent, String tag) {
        Element child = firstDirectChild(parent, tag);
        if (child == null) {
            return null;
        }
        String value = child.getTextContent();
        return value == null || value.isBlank() ? null : value.trim();
    }

    private static Element firstDirectChild(Element parent, String tag) {
        NodeList children = parent.getChildNodes();
        for (int i = 0; i < children.getLength(); i++) {
            Node child = children.item(i);
            if (child instanceof Element element && tag.equals(element.getTagName())) {
                return element;
            }
        }
        return null;
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

    private static String mavenScope(String scope) {
        if (scope == null || scope.isBlank() || "compile".equals(scope) || "runtime".equals(scope)) {
            return "runtime";
        }
        if ("test".equals(scope)) {
            return "test";
        }
        if ("provided".equals(scope)) {
            return "build";
        }
        if ("system".equals(scope)) {
            return "runtime";
        }
        if ("import".equals(scope)) {
            return "dependencyManagement";
        }
        return "unknown";
    }

    private static String gradleScope(String configuration) {
        if (configuration == null || configuration.isBlank()) {
            return "unknown";
        }
        String lower = configuration.toLowerCase();
        if (lower.contains("test")) {
            return "test";
        }
        if (lower.contains("compileonly") || lower.contains("annotationprocessor")) {
            return "build";
        }
        if (lower.contains("runtime") || lower.contains("implementation") || lower.equals("api") || lower.equals("compile")) {
            return "runtime";
        }
        return "unknown";
    }

    private static void putVersion(Map<String, String> props, String version) {
        if (version == null || version.isBlank()) {
            props.put("version", "");
            return;
        }
        String trimmed = version.trim();
        if (unsafePackageVersion(trimmed)) {
            props.put("versionHash", Hashes.sha256(trimmed, 32));
            props.put("redactionReason", "unsafe-package-version");
        } else {
            props.put("version", trimmed);
        }
    }

    private static boolean unsafePackageVersion(String value) {
        String lower = value.toLowerCase();
        return value.contains("://")
            || value.contains("\\")
            || value.startsWith("/")
            || value.startsWith("./")
            || value.startsWith("../")
            || lower.startsWith("file:")
            || lower.startsWith("git+")
            || value.contains("${")
            || value.contains("$(")
            || value.contains("%");
    }

    private static Map<String, String> props(String... values) {
        Map<String, String> props = new LinkedHashMap<>();
        for (int i = 0; i + 1 < values.length; i += 2) {
            props.put(values[i], values[i + 1]);
        }
        return props;
    }
}
