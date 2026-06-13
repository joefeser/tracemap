package com.tracemap.jvm.model;

import java.nio.file.Path;
import java.util.List;

public record ScanOptions(
    Path repoPath,
    Path outputPath,
    List<Path> projectPaths,
    List<String> includeGlobs,
    List<String> excludeGlobs,
    long maxFileByteSize,
    boolean semantic,
    String language) {
}
