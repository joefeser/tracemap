package com.tracemap.jvm.model;

import java.nio.file.Path;
import java.util.List;

public record GitMetadata(
    String repoName,
    String remoteUrl,
    String branch,
    String commitSha,
    List<String> knownGaps,
    Path gitRootPath) {
}
