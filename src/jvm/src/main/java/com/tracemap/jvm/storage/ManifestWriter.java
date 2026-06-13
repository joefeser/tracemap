package com.tracemap.jvm.storage;

import com.tracemap.jvm.model.ScanManifest;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;

public final class ManifestWriter {
    private ManifestWriter() {
    }

    public static void write(Path path, ScanManifest manifest) throws IOException {
        Files.createDirectories(path.getParent());
        JsonSupport.JSON.writerWithDefaultPrettyPrinter().writeValue(path.toFile(), manifest);
    }
}
