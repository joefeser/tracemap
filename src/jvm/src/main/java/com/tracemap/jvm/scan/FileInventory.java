package com.tracemap.jvm.scan;

import com.tracemap.jvm.model.FileInventoryItem;
import com.tracemap.jvm.model.ScanOptions;
import com.tracemap.jvm.util.PathsUtil;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.FileSystems;
import java.nio.file.PathMatcher;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.stream.Stream;

public final class FileInventory {
    private static final Set<String> EXCLUDED_DIRS = Set.of(
        ".git", "target", "build", ".gradle", "node_modules", "dist", "out", ".idea", ".vscode", ".tracemap");
    private static final Set<String> INCLUDED_NAMES = Set.of(
        "pom.xml", "build.gradle", "build.gradle.kts", "settings.gradle", "settings.gradle.kts", "gradle.properties");
    private static final Set<String> INCLUDED_EXTENSIONS = Set.of(
        ".java", ".kt", ".kts", ".properties", ".yml", ".yaml", ".json", ".xml", ".sql");

    private FileInventory() {
    }

    public static List<FileInventoryItem> collect(ScanOptions options) throws IOException {
        Path repo = options.repoPath().toAbsolutePath().normalize();
        Path output = options.outputPath().toAbsolutePath().normalize();
        List<FileInventoryItem> items = new ArrayList<>();
        try (Stream<Path> stream = Files.walk(repo)) {
            stream.filter(Files::isRegularFile)
                .filter(path -> !isUnder(output, path.toAbsolutePath().normalize()))
                .filter(path -> !hasExcludedDir(repo, path))
                .filter(FileInventory::isIncluded)
                .filter(path -> matchesScope(repo, path, options))
                .forEach(path -> {
                    try {
                        String relative = PathsUtil.relativeUnix(repo, path);
                        boolean skipped = Files.size(path) > options.maxFileByteSize();
                        items.add(new FileInventoryItem(relative, path.toAbsolutePath().normalize(), kind(path), Files.size(path), skipped));
                    } catch (IOException exception) {
                        // Unreadable files are represented later as gaps by the scan engine.
                    }
                });
        }
        items.sort(Comparator.comparing(FileInventoryItem::relativePath));
        return items;
    }

    private static boolean matchesScope(Path repo, Path path, ScanOptions options) {
        String relative = PathsUtil.relativeUnix(repo, path);
        if (!matchesLanguage(relative, options.language())) {
            return false;
        }
        if (!options.includeGlobs().isEmpty() && options.includeGlobs().stream().noneMatch(glob -> globMatches(glob, relative))) {
            return false;
        }
        return options.excludeGlobs().stream().noneMatch(glob -> globMatches(glob, relative));
    }

    private static boolean matchesLanguage(String relative, String language) {
        String lowerLanguage = language == null ? "all" : language.toLowerCase();
        if ("all".equals(lowerLanguage)) {
            return true;
        }
        if ("java".equals(lowerLanguage)) {
            return relative.endsWith(".java") || relative.endsWith("pom.xml") || relative.endsWith(".gradle") || relative.endsWith(".gradle.kts") || isConfig(relative);
        }
        if ("kotlin".equals(lowerLanguage)) {
            return relative.endsWith(".kt") || relative.endsWith(".kts") || relative.endsWith(".gradle") || relative.endsWith(".gradle.kts") || isConfig(relative);
        }
        return true;
    }

    private static boolean isConfig(String relative) {
        String lower = relative.toLowerCase();
        return lower.endsWith(".properties") || lower.endsWith(".json") || lower.endsWith(".yml") || lower.endsWith(".yaml") || lower.endsWith(".xml") || lower.endsWith(".sql");
    }

    private static boolean globMatches(String glob, String relative) {
        String normalized = glob.contains("/") ? glob : "**/" + glob;
        PathMatcher matcher = FileSystems.getDefault().getPathMatcher("glob:" + normalized);
        return matcher.matches(Path.of(relative));
    }

    private static boolean isIncluded(Path path) {
        String name = path.getFileName().toString();
        if (INCLUDED_NAMES.contains(name)) {
            return true;
        }
        String lower = name.toLowerCase();
        return INCLUDED_EXTENSIONS.stream().anyMatch(lower::endsWith);
    }

    private static boolean hasExcludedDir(Path root, Path path) {
        Set<String> parts = new HashSet<>();
        for (Path part : root.toAbsolutePath().normalize().relativize(path.toAbsolutePath().normalize())) {
            parts.add(part.toString());
        }
        for (String excluded : EXCLUDED_DIRS) {
            if (parts.contains(excluded)) {
                return true;
            }
        }
        String relative = PathsUtil.relativeUnix(root, path);
        return relative.contains("/generated/") || relative.contains("/generated-sources/");
    }

    private static boolean isUnder(Path parent, Path child) {
        return child.startsWith(parent);
    }

    private static String kind(Path path) {
        String name = path.getFileName().toString();
        String lower = name.toLowerCase();
        if (lower.endsWith(".java")) return "Java";
        if (lower.endsWith(".kt")) return "Kotlin";
        if (lower.endsWith(".kts")) return "KotlinScript";
        if (lower.equals("pom.xml")) return "MavenProject";
        if (lower.startsWith("build.gradle")) return "GradleBuild";
        if (lower.startsWith("settings.gradle")) return "GradleSettings";
        if (lower.endsWith(".properties")) return "PropertiesConfig";
        if (lower.endsWith(".yml") || lower.endsWith(".yaml")) return "YamlConfig";
        if (lower.endsWith(".json")) return "JsonConfig";
        if (lower.endsWith(".xml")) return "XmlConfig";
        if (lower.endsWith(".sql")) return "Sql";
        return "Other";
    }
}
