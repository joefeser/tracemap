package com.tracemap.jvm.model;

import java.util.Map;

public record CodeFact(
    String factId,
    String scanId,
    String repo,
    String commitSha,
    String projectPath,
    String factType,
    String ruleId,
    String evidenceTier,
    String sourceSymbol,
    String targetSymbol,
    String contractElement,
    EvidenceSpan evidence,
    Map<String, String> properties) {
}
