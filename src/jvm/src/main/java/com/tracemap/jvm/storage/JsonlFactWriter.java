package com.tracemap.jvm.storage;

import com.tracemap.jvm.model.CodeFact;
import java.io.BufferedWriter;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;

public final class JsonlFactWriter {
    private JsonlFactWriter() {
    }

    public static void write(Path path, List<CodeFact> facts) throws IOException {
        Files.createDirectories(path.getParent());
        try (BufferedWriter writer = Files.newBufferedWriter(path, StandardCharsets.UTF_8)) {
            for (CodeFact fact : facts) {
                writer.write(JsonSupport.JSON.writeValueAsString(fact));
                writer.newLine();
            }
        }
    }
}
