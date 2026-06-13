package com.tracemap.jvm;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import com.tracemap.jvm.model.EvidenceTiers;
import com.tracemap.jvm.model.FactTypes;
import com.tracemap.jvm.model.ScanOptions;
import com.tracemap.jvm.model.ScanResult;
import com.tracemap.jvm.scan.ScanEngine;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.concurrent.TimeUnit;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

final class ScanEngineIntegrationTest {
    @TempDir
    Path temp;

    @Test
    void scansJavaSampleAndReducerClassifiesSemanticPropertyImpact() throws Exception {
        Path repo = temp.resolve("repo");
        copyDirectory(repoRoot().resolve("samples/jvm-modern-sample"), repo);
        initGit(repo);
        Path out = temp.resolve("out");

        ScanResult result = new ScanEngine().scan(new ScanOptions(repo, out, List.of(), List.of(), List.of(), 1024 * 1024, true, "all"));

        assertEquals("Level1SemanticAnalysis", result.manifest().analysisLevel());
        assertTrue(Files.exists(out.resolve("scan-manifest.json")));
        assertTrue(Files.exists(out.resolve("facts.ndjson")));
        assertTrue(Files.exists(out.resolve("index.sqlite")));
        assertTrue(Files.exists(out.resolve("report.md")));
        assertTrue(Files.exists(out.resolve("logs/analyzer.log")));
        assertTrue(result.facts().stream().anyMatch(fact ->
            FactTypes.PROPERTY_ACCESSED.equals(fact.factType())
                && EvidenceTiers.TIER1_SEMANTIC.equals(fact.evidenceTier())
                && "status".equals(fact.properties().get("propertyName"))));

        Path report = temp.resolve("impact.md");
        Process process = new ProcessBuilder(
            "dotnet",
            "run",
            "--project",
            repoRoot().resolve("src/dotnet/TraceMap.Cli").toString(),
            "--",
            "reduce",
            "--index",
            out.resolve("index.sqlite").toString(),
            "--contract-delta",
            repoRoot().resolve("samples/contract-deltas/jvm-modern.order-status.json").toString(),
            "--out",
            report.toString())
            .directory(repoRoot().toFile())
            .redirectErrorStream(true)
            .start();
        assertTrue(process.waitFor(60, TimeUnit.SECONDS), "dotnet reduce timed out");
        String output = new String(process.getInputStream().readAllBytes());
        assertEquals(0, process.exitValue(), output);
        assertTrue(Files.readString(report).contains("DefiniteImpact"));
    }

    @Test
    void kotlinOnlySampleIsSyntaxCoverage() throws Exception {
        Path repo = temp.resolve("kotlin-repo");
        copyDirectory(repoRoot().resolve("samples/jvm-kotlin-sample"), repo);
        initGit(repo);
        Path out = temp.resolve("kotlin-out");

        ScanResult result = new ScanEngine().scan(new ScanOptions(repo, out, List.of(), List.of(), List.of(), 1024 * 1024, true, "all"));

        assertEquals("Level3SyntaxAnalysis", result.manifest().analysisLevel());
        assertTrue(result.manifest().knownGaps().stream().anyMatch(gap -> gap.startsWith("KotlinSemanticNotImplemented")));
        assertTrue(result.facts().stream().anyMatch(fact -> FactTypes.TYPE_DECLARED.equals(fact.factType()) && "kotlin".equals(fact.properties().get("language"))));
    }

    private static Path repoRoot() {
        return Path.of(System.getProperty("user.dir")).toAbsolutePath().normalize().getParent().getParent();
    }

    private static void copyDirectory(Path source, Path target) throws IOException {
        try (var stream = Files.walk(source)) {
            for (Path path : stream.toList()) {
                Path relative = source.relativize(path);
                Path destination = target.resolve(relative);
                if (Files.isDirectory(path)) {
                    Files.createDirectories(destination);
                } else {
                    Files.createDirectories(destination.getParent());
                    Files.copy(path, destination);
                }
            }
        }
    }

    private static void initGit(Path repo) throws Exception {
        run(repo, "git", "init");
        run(repo, "git", "config", "user.email", "test@example.invalid");
        run(repo, "git", "config", "user.name", "TraceMap Test");
        run(repo, "git", "add", ".");
        run(repo, "git", "commit", "-m", "initial");
    }

    private static void run(Path cwd, String... command) throws Exception {
        Process process = new ProcessBuilder(command)
            .directory(cwd.toFile())
            .redirectErrorStream(true)
            .start();
        assertTrue(process.waitFor(30, TimeUnit.SECONDS), "command timed out: " + String.join(" ", command));
        String output = new String(process.getInputStream().readAllBytes());
        assertEquals(0, process.exitValue(), output);
    }
}
