package com.tracemap.jvm.model;

import java.util.List;

public record ScanManifest(
    String scanId,
    String repoName,
    String remoteUrl,
    String branch,
    String commitSha,
    String scannerVersion,
    String scannedAt,
    String analysisLevel,
    String buildStatus,
    List<String> solutions,
    List<String> projects,
    List<String> targetFrameworks,
    List<String> knownGaps,
    String scanRootRelativePath,
    String scanRootPathHash,
    String gitRootHash) {
}
