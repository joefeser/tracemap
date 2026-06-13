package com.tracemap.jvm.scan;

import com.tracemap.jvm.model.GitMetadata;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Duration;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.TimeUnit;

public final class GitMetadataProvider {
    private GitMetadataProvider() {
    }

    public static GitMetadata read(Path repoPath) throws IOException, InterruptedException {
        Path root = run(repoPath, "git", "rev-parse", "--show-toplevel").pathOrNull();
        if (root == null) {
            throw new IOException("TraceMap scans require a Git checkout with a concrete commit SHA.");
        }
        String commit = run(repoPath, "git", "rev-parse", "HEAD").stdoutOrBlank();
        if (commit.isBlank() || "unknown".equalsIgnoreCase(commit)) {
            throw new IOException("TraceMap scans require a concrete commit SHA.");
        }
        String repoName = root.getFileName() == null ? repoPath.getFileName().toString() : root.getFileName().toString();
        String remote = run(repoPath, "git", "config", "--get", "remote.origin.url").stdoutOrNull();
        String branch = run(repoPath, "git", "branch", "--show-current").stdoutOrNull();
        List<String> gaps = new ArrayList<>();
        String porcelain = run(repoPath, "git", "status", "--porcelain").stdoutOrBlank();
        if (!porcelain.isBlank()) {
            gaps.add("Working tree has uncommitted changes; scan is tied to commit SHA but local files may differ.");
        }
        return new GitMetadata(repoName, remote, branch, commit, gaps, root);
    }

    private static CommandResult run(Path cwd, String... args) throws IOException, InterruptedException {
        Process process = new ProcessBuilder(args)
            .directory(cwd.toFile())
            .redirectErrorStream(true)
            .start();
        boolean completed = process.waitFor(Duration.ofSeconds(10).toMillis(), TimeUnit.MILLISECONDS);
        if (!completed) {
            process.destroyForcibly();
            return new CommandResult(124, "");
        }
        String stdout = new String(process.getInputStream().readAllBytes(), StandardCharsets.UTF_8).trim();
        return new CommandResult(process.exitValue(), stdout);
    }

    private record CommandResult(int exitCode, String stdout) {
        String stdoutOrBlank() {
            return exitCode == 0 ? stdout : "";
        }

        String stdoutOrNull() {
            return exitCode == 0 && !stdout.isBlank() ? stdout : null;
        }

        Path pathOrNull() {
            if (exitCode != 0 || stdout.isBlank()) {
                return null;
            }
            Path path = Path.of(stdout).toAbsolutePath().normalize();
            return Files.isDirectory(path) ? path : null;
        }
    }
}
