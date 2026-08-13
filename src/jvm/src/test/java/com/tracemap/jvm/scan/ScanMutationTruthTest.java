package com.tracemap.jvm.scan;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import com.tracemap.jvm.model.ScanOptions;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

final class ScanMutationTruthTest {
    @TempDir
    Path temp;

    @Test
    void sourceMutationBeforeVerificationFailsWithoutPublishing() throws Exception {
        Path repo = temp.resolve("repo");
        Files.createDirectories(repo.resolve("src"));
        Path source = repo.resolve("src/Sample.java");
        Files.writeString(source, "final class Sample { int value = 1; }\n");
        git(repo, "init");
        git(repo, "add", ".");
        git(repo, "-c", "user.name=TraceMap", "-c", "user.email=fixture@example.invalid", "commit", "-m", "baseline");
        Path output = temp.resolve("output");

        IOException error = assertThrows(IOException.class, () -> new ScanEngine().scan(
            new ScanOptions(repo, output, List.of(), List.of(), List.of(), 1024 * 1024, false, "all"),
            () -> {
                try {
                    Files.writeString(source, "final class Sample { int value = 2; }\n");
                } catch (IOException exception) {
                    throw new IllegalStateException(exception);
                }
            }));

        assertTrue(error.getMessage().contains("SourceSnapshotChangedDuringScan"));
        assertFalse(Files.exists(output.resolve("scan-manifest.json")));
    }

    private static void git(Path repo, String... args) throws Exception {
        List<String> command = new java.util.ArrayList<>();
        command.add("git");
        command.add("-C");
        command.add(repo.toString());
        command.addAll(List.of(args));
        Process process = new ProcessBuilder(command).redirectErrorStream(true).start();
        String output = new String(process.getInputStream().readAllBytes());
        if (process.waitFor() != 0) {
            throw new IOException(output);
        }
    }
}
