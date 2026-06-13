package com.tracemap.jvm.model;

import java.util.List;

public record ScanResult(
    ScanManifest manifest,
    List<CodeFact> facts,
    List<FileInventoryItem> inventory) {
}
