package com.tracemap.jvm.model;

import java.nio.file.Path;

public record FileInventoryItem(
    String relativePath,
    Path absolutePath,
    String kind,
    long sizeBytes,
    boolean skipped,
    String skipReason) {
}
