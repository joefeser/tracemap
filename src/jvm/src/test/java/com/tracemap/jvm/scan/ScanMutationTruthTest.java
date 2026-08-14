package com.tracemap.jvm.scan;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assertions.assertNotEquals;

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

    @Test
    void artifactWriteFailurePreservesPriorCompleteOutput() throws Exception {
        Path repo = temp.resolve("transaction-repo");
        Files.createDirectories(repo.resolve("src"));
        Files.writeString(repo.resolve("src/Sample.java"), "final class Sample { int value = 1; }\n");
        git(repo, "init");
        git(repo, "add", ".");
        git(repo, "-c", "user.name=TraceMap", "-c", "user.email=fixture@example.invalid", "commit", "-m", "baseline");
        Path output = temp.resolve("transaction-output");
        ScanOptions options = new ScanOptions(repo, output, List.of(), List.of(), List.of(), 1024 * 1024, false, "all");
        new ScanEngine().scan(options);
        byte[] baselineManifest = Files.readAllBytes(output.resolve("scan-manifest.json"));

        assertThrows(IllegalStateException.class, () -> new ScanEngine().scan(
            options,
            () -> { },
            () -> { throw new IllegalStateException("SyntheticArtifactWriteFailure"); }));

        assertArrayEquals(baselineManifest, Files.readAllBytes(output.resolve("scan-manifest.json")));
        try (var children = Files.list(temp)) {
            assertFalse(children.anyMatch(path -> path.getFileName().toString().startsWith(".tracemap-transaction-output-")));
        }
    }

    @Test
    void rejectsArbitraryExistingOutputWithoutMovingIt() throws Exception {
        Path repo = initializedRepo("unsafe-repo");
        Path output = temp.resolve("existing-output");
        Files.createDirectories(output);
        Path sentinel = output.resolve("keep.txt");
        Files.writeString(sentinel, "important\n");

        IOException error = assertThrows(IOException.class, () -> new ScanEngine().scan(options(repo, output, List.of())));

        assertTrue(error.getMessage().contains("OutputArtifactSetNotReplaceable"));
        assertTrue(Files.exists(sentinel));
    }

    @Test
    void rejectsRepositoryRootAsOutput() throws Exception {
        Path repo = initializedRepo("same-output-repo");

        IOException error = assertThrows(IOException.class, () -> new ScanEngine().scan(options(repo, repo, List.of())));

        assertTrue(error.getMessage().contains("OutputArtifactSetNotReplaceable"));
        assertTrue(Files.exists(repo.resolve("src/Sample.java")));
    }

    @Test
    void optionListFramingPreventsDelimiterCollisions() throws Exception {
        Path repo = initializedRepo("option-repo");
        var oneValue = new ScanEngine().scan(options(repo, temp.resolve("one-value"), List.of("foo,bar")));
        var twoValues = new ScanEngine().scan(options(repo, temp.resolve("two-values"), List.of("foo", "bar")));

        assertNotEquals(oneValue.manifest().scanId(), twoValues.manifest().scanId());
    }

    private Path initializedRepo(String name) throws Exception {
        Path repo = temp.resolve(name);
        Files.createDirectories(repo.resolve("src"));
        Files.writeString(repo.resolve("src/Sample.java"), "final class Sample { int value = 1; }\n");
        git(repo, "init");
        git(repo, "add", ".");
        git(repo, "-c", "user.name=TraceMap", "-c", "user.email=fixture@example.invalid", "commit", "-m", "baseline");
        return repo;
    }

    private static ScanOptions options(Path repo, Path output, List<String> excludes) {
        return new ScanOptions(repo, output, List.of(), List.of(), excludes, 1024 * 1024, false, "all");
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
