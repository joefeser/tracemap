package com.tracemap.jvm.model;

public record EvidenceSpan(
    String filePath,
    int startLine,
    int endLine,
    String snippetHash,
    String extractorId,
    String extractorVersion) {
}
