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
                    String relative = PathsUtil.relativeUnix(repo, path);
                    Path absolute = path.toAbsolutePath().normalize();
                    try {
                        long size = Files.size(path);
                        boolean skipped = size > options.maxFileByteSize();
                        String skipReason = skipped ? "FileSkippedMaxSize: " + relative : null;
                        items.add(new FileInventoryItem(relative, absolute, kind(path), size, skipped, skipReason));
                    } catch (IOException exception) {
                        items.add(new FileInventoryItem(
                            relative,
                            absolute,
                            kind(path),
                            -1,
                            true,
                            "FileUnreadable: " + relative + " (" + exception.getClass().getSimpleName() + ")"));
                    }
                });
        }
        items.sort(Comparator.comparing(FileInventoryItem::relativePath));
        return items;
    }

    private static boolean matchesScope(Path repo, Path path, ScanOptions options) {
        String relative = PathsUtil.relativeUnix(repo, path);
        if (!matchesProjectPaths(repo, path, options.projectPaths())) {
            return false;
        }
        if (!matchesLanguage(relative, options.language())) {
            return false;
        }
        if (!options.includeGlobs().isEmpty() && options.includeGlobs().stream().noneMatch(glob -> globMatches(glob, relative))) {
            return false;
        }
        return options.excludeGlobs().stream().noneMatch(glob -> globMatches(glob, relative));
    }

    private static boolean matchesProjectPaths(Path repo, Path path, List<Path> projectPaths) {
        if (projectPaths.isEmpty()) {
            return true;
        }
        Path absolute = path.toAbsolutePath().normalize();
        for (Path projectPath : projectPaths) {
            Path scope = projectPath.isAbsolute()
                ? projectPath.toAbsolutePath().normalize()
                : repo.resolve(projectPath).toAbsolutePath().normalize();
            Path scopeRoot = isProjectFile(scope) || Files.isRegularFile(scope) ? scope.getParent() : scope;
            if (absolute.equals(scope) || scopeRoot != null && absolute.startsWith(scopeRoot)) {
                return true;
            }
        }
        return false;
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
        for (Path part : root.toAbsolutePath().normalize().relativize(path.toAbsolutePath().normalize())) {
            if (EXCLUDED_DIRS.contains(part.toString())) {
                return true;
            }
        }
        String relative = PathsUtil.relativeUnix(root, path);
        return relative.contains("/generated/") || relative.contains("/generated-sources/");
    }

    private static boolean isUnder(Path parent, Path child) {
        return child.startsWith(parent);
    }

    private static boolean isProjectFile(Path path) {
        Path fileName = path.getFileName();
        return fileName != null && INCLUDED_NAMES.contains(fileName.toString());
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
