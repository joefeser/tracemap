package com.tracemap.jvm;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
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
            FactTypes.BUILD_STATUS.equals(fact.factType())
                && result.manifest().buildStatus().equals(fact.targetSymbol())
                && result.manifest().analysisLevel().equals(fact.properties().get("analysisLevel"))));
        assertTrue(result.facts().stream().anyMatch(fact ->
            FactTypes.HTTP_ROUTE_BINDING.equals(fact.factType())
                && "GET".equals(fact.properties().get("httpMethod"))
                && "/api/orders/{id}".equals(fact.properties().get("normalizedPathTemplate"))
                && "getOrder".equals(fact.properties().get("methodName"))));
        assertFalse(result.facts().stream().anyMatch(fact ->
            FactTypes.CALCULATION_EXPRESSION.equals(fact.factType())
                && fact.evidence().filePath().endsWith("OrderController.java")));
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

    @Test
    void javaSyntaxHandlesMultilineRoutesAndSpringBootAnnotations() throws Exception {
        Path repo = temp.resolve("java-routes");
        Files.createDirectories(repo.resolve("src/main/java/example"));
        Files.writeString(repo.resolve("src/main/java/example/SampleApplication.java"), """
            package example;

            import org.springframework.boot.autoconfigure.SpringBootApplication;

            @SpringBootApplication
            public class SampleApplication {
            }
            """);
        Files.writeString(repo.resolve("src/main/java/example/SampleController.java"), """
            package example;

            import org.springframework.web.bind.annotation.GetMapping;
            import org.springframework.web.bind.annotation.RequestMapping;

            @RequestMapping("/api")
            public class SampleController {
                @GetMapping(
                    value = "/multi/{id}",
                    produces = "application/json"
                )
                @SuppressWarnings("unused")
                public String show(String id) {
                    return id;
                }
            }
            """);
        initGit(repo);

        ScanResult result = new ScanEngine().scan(new ScanOptions(repo, temp.resolve("java-routes-out"), List.of(), List.of(), List.of(), 1024 * 1024, false, "all"));

        assertTrue(result.facts().stream().anyMatch(fact ->
            FactTypes.HTTP_ROUTE_BINDING.equals(fact.factType())
                && "GET".equals(fact.properties().get("httpMethod"))
                && "/api/multi/{id}".equals(fact.properties().get("normalizedPathTemplate"))
                && "show".equals(fact.properties().get("methodName"))));
        assertTrue(result.facts().stream().anyMatch(fact ->
            FactTypes.INFRASTRUCTURE_BOILERPLATE.equals(fact.factType())
                && "SpringBootEntrypoint".equals(fact.properties().get("boilerplateKind"))));
        assertFalse(result.facts().stream().anyMatch(fact ->
            FactTypes.CALCULATION_EXPRESSION.equals(fact.factType())
                && fact.evidence().filePath().endsWith("SampleController.java")));
    }

    @Test
    void projectOptionLimitsInventoryToRequestedModule() throws Exception {
        Path repo = temp.resolve("multi-module");
        Files.createDirectories(repo.resolve("module-a/src/main/java/example"));
        Files.createDirectories(repo.resolve("module-b/src/main/java/example"));
        Files.writeString(repo.resolve("module-a/pom.xml"), """
            <project>
              <modelVersion>4.0.0</modelVersion>
              <groupId>example</groupId>
              <artifactId>module-a</artifactId>
              <version>1.0.0</version>
            </project>
            """);
        Files.writeString(repo.resolve("module-b/pom.xml"), """
            <project>
              <modelVersion>4.0.0</modelVersion>
              <groupId>example</groupId>
              <artifactId>module-b</artifactId>
              <version>1.0.0</version>
            </project>
            """);
        Files.writeString(repo.resolve("module-a/src/main/java/example/A.java"), "package example; class A {}\n");
        Files.writeString(repo.resolve("module-b/src/main/java/example/B.java"), "package example; class B {}\n");
        initGit(repo);

        ScanResult result = new ScanEngine().scan(new ScanOptions(
            repo,
            temp.resolve("module-out"),
            List.of(Path.of("module-a/pom.xml")),
            List.of(),
            List.of(),
            1024 * 1024,
            false,
            "all"));

        assertTrue(result.inventory().stream().anyMatch(item -> item.relativePath().startsWith("module-a/")));
        assertFalse(result.inventory().stream().anyMatch(item -> item.relativePath().startsWith("module-b/")));
    }

    @Test
    void extractsJsonArrayConfigKeys() throws Exception {
        Path repo = temp.resolve("json-config");
        Files.createDirectories(repo);
        Files.writeString(repo.resolve("application.json"), """
            {
              "servers": [
                { "url": "https://example.invalid", "enabled": true }
              ]
            }
            """);
        initGit(repo);

        ScanResult result = new ScanEngine().scan(new ScanOptions(repo, temp.resolve("json-out"), List.of(), List.of(), List.of(), 1024 * 1024, false, "all"));

        assertTrue(result.facts().stream().anyMatch(fact ->
            FactTypes.CONFIG_KEY_DECLARED.equals(fact.factType())
                && "servers[0].url".equals(fact.properties().get("keyPath"))));
        assertTrue(result.facts().stream().anyMatch(fact ->
            FactTypes.CONFIG_KEY_DECLARED.equals(fact.factType())
                && "servers[0].enabled".equals(fact.properties().get("keyPath"))));

        ScanResult differentOptions = new ScanEngine().scan(new ScanOptions(repo, temp.resolve("json-out-small"), List.of(), List.of(), List.of(), 16, false, "all"));
        assertFalse(result.manifest().scanId().equals(differentOptions.manifest().scanId()));
    }

    @Test
    void mavenProjectCoordinatesPreferDirectProjectChildren() throws Exception {
        Path repo = temp.resolve("maven-parent");
        Files.createDirectories(repo);
        Files.writeString(repo.resolve("pom.xml"), """
            <project>
              <modelVersion>4.0.0</modelVersion>
              <parent>
                <groupId>example.parent</groupId>
                <artifactId>parent-artifact</artifactId>
                <version>9.9.9</version>
                <relativePath>../missing-parent.xml</relativePath>
              </parent>
              <artifactId>child-artifact</artifactId>
            </project>
            """);
        initGit(repo);

        ScanResult result = new ScanEngine().scan(new ScanOptions(repo, temp.resolve("maven-out"), List.of(), List.of(), List.of(), 1024 * 1024, false, "all"));

        assertTrue(result.facts().stream().anyMatch(fact ->
            FactTypes.PROJECT_DECLARED.equals(fact.factType())
                && "example.parent:child-artifact".equals(fact.targetSymbol())
                && "child-artifact".equals(fact.properties().get("artifactId"))));
        assertFalse(result.facts().stream().anyMatch(fact ->
            FactTypes.PROJECT_DECLARED.equals(fact.factType())
                && "example.parent:parent-artifact".equals(fact.targetSymbol())));
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
