package com.tracemap.jvm;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import com.tracemap.jvm.model.CodeFact;
import com.tracemap.jvm.model.FactTypes;
import com.tracemap.jvm.model.ScanOptions;
import com.tracemap.jvm.model.ScanResult;
import com.tracemap.jvm.scan.ScanEngine;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.concurrent.TimeUnit;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

final class GradleLockfileTest {
    @TempDir
    Path temp;

    @Test
    void parsesGradleLockfileResolvedVersionsWithLockfileIdentity() throws Exception {
        ScanResult result = scanLockfileRepo("""
            # This is a Gradle generated file for dependency locking.
            # Manual edits can break the build and are not advised.
            # This file is expected to be part of source control.
            org.springframework:spring-web:6.2.0=compileClasspath,runtimeClasspath
            com.example:fixture-lib:1.2.3=runtimeClasspath
            """);

        CodeFact springWeb = packageFact(result, "org.springframework:spring-web");
        assertEquals("6.2.0", springWeb.properties().get("resolvedVersion"));
        assertEquals("6.2.0", springWeb.properties().get("version"));
        assertEquals("gradle", springWeb.properties().get("ecosystem"));
        assertEquals("gradle.lockfile", springWeb.properties().get("manifestKind"));
        assertEquals("lockfile", springWeb.properties().get("sourceKind"));
        assertEquals("gradle.lockfile", springWeb.properties().get("lockfilePath"));
        assertEquals(32, springWeb.properties().get("lockfileHash").length());
        assertEquals("GradleLockfileExtractor", springWeb.evidence().extractorId());
        assertEquals("jvm-gradle-lockfile/0.1.0", springWeb.evidence().extractorVersion());
        assertEquals(4, springWeb.evidence().startLine());
        CodeFact fixtureLib = packageFact(result, "com.example:fixture-lib");
        assertEquals("1.2.3", fixtureLib.properties().get("resolvedVersion"));
        assertEquals(5, fixtureLib.evidence().startLine());
        assertEquals(springWeb.properties().get("lockfileHash"), fixtureLib.properties().get("lockfileHash"));
    }

    @Test
    void gradleLockfileRowsNeverCarryDigestsOrUnprovenRelations() throws Exception {
        ScanResult result = scanLockfileRepo("com.example:fixture-lib:1.2.3=runtimeClasspath\n");

        for (CodeFact fact : result.facts()) {
            if (FactTypes.PACKAGE_REFERENCED.equals(fact.factType())) {
                assertFalse(fact.properties().containsKey("artifactDigest"));
                assertFalse(fact.properties().containsKey("artifactDigestAlgorithm"));
                assertFalse(fact.properties().containsKey("dependencyRelation"));
            }
        }
        assertTrue(result.facts().stream().anyMatch(fact -> FactTypes.ANALYSIS_GAP.equals(fact.factType())
            && "LockfileDigestUnavailable".equals(fact.properties().get("gapKind"))));
        assertTrue(result.facts().stream().anyMatch(fact -> FactTypes.ANALYSIS_GAP.equals(fact.factType())
            && "DirectTransitiveUnavailable".equals(fact.properties().get("gapKind"))));
        assertTrue(result.manifest().knownGaps().contains("LockfileDigestUnavailable: gradle.lockfile"));
        assertTrue(result.manifest().knownGaps().contains("DirectTransitiveUnavailable: gradle.lockfile"));
    }

    @Test
    void malformedAndUnsupportedLockRowsEmitGapsWithoutRowFacts() throws Exception {
        ScanResult result = scanLockfileRepo("""
            # header comment
            com.example:fixture-lib:1.2.3=runtimeClasspath
            not-a-lockfile-row
            empty=annotationProcessor
            com.example:no-version:=compileClasspath
            ../etc/passwd:evil:1.0.0=runtimeClasspath
            com.example:empty-config:1.0.0=
            com.example:bad-config:1.0.0=runtimeClasspath=extra
            """);

        assertEquals(1, result.facts().stream()
            .filter(fact -> FactTypes.PACKAGE_REFERENCED.equals(fact.factType())
                && "com.example:fixture-lib".equals(fact.properties().get("packageName")))
            .count());
        List<String> gapKinds = result.facts().stream()
            .filter(fact -> FactTypes.ANALYSIS_GAP.equals(fact.factType()))
            .map(fact -> fact.properties().get("gapKind"))
            .toList();
        assertEquals(5, gapKinds.stream().filter("GradleLockRowMalformed"::equals).count());
        assertEquals(1, gapKinds.stream().filter("GradleLockRowUnsupported"::equals).count());
        assertTrue(gapKinds.contains("LockfileDigestUnavailable"));
        assertTrue(gapKinds.contains("DirectTransitiveUnavailable"));
    }

    @Test
    void unsafeLockfileVersionsAreHashedNotEmitted() throws Exception {
        ScanResult result = scanLockfileRepo("com.example:fixture-lib:${fixtureLibVersion}=runtimeClasspath\n");

        CodeFact fixtureLib = packageFact(result, "com.example:fixture-lib");
        assertFalse(fixtureLib.properties().containsKey("resolvedVersion"));
        assertFalse(fixtureLib.properties().containsKey("version"));
        assertEquals("unsafe-package-version", fixtureLib.properties().get("redactionReason"));
        assertEquals(32, fixtureLib.properties().get("versionHash").length());
        assertFalse(result.facts().toString().contains("fixtureLibVersion"));
    }

    @Test
    void duplicateAndConflictingCoordinatesStayDeterministic() throws Exception {
        Path repo = lockfileRepo("""
            com.example:fixture-lib:1.2.3=compileClasspath
            com.example:fixture-lib:1.2.3=runtimeClasspath
            com.example:fixture-lib:2.0.0=runtimeClasspath
            """);
        initGit(repo);

        ScanResult first = new ScanEngine().scan(new ScanOptions(repo, temp.resolve("duplicate-one"), List.of(), List.of(), List.of(), 1024 * 1024, false, "all"));
        ScanResult second = new ScanEngine().scan(new ScanOptions(repo, temp.resolve("duplicate-two"), List.of(), List.of(), List.of(), 1024 * 1024, false, "all"));

        List<String> firstRows = resolvedRows(first);
        assertEquals(List.of("1.2.3", "1.2.3", "2.0.0"), firstRows);
        assertEquals(firstRows, resolvedRows(second));
        assertEquals(first.facts().stream().map(CodeFact::factId).toList(),
            second.facts().stream().map(CodeFact::factId).toList());
    }

    @Test
    void verificationMetadataIsInventoriedButNeverEmitsDigestEvidence() throws Exception {
        Path repo = temp.resolve("verification-repo");
        Files.createDirectories(repo.resolve("gradle"));
        Files.writeString(repo.resolve("settings.gradle"), "rootProject.name = 'verification-repo'\n");
        Files.writeString(repo.resolve("gradle.lockfile"), "com.example:fixture-lib:1.2.3=runtimeClasspath\n");
        Files.writeString(repo.resolve("gradle/verification-metadata.xml"), """
            <?xml version="1.0" encoding="UTF-8"?>
            <verification-metadata xmlns="https://schema.gradle.org/dependency-verification" modelVersion="1.1">
              <components>
                <component group="com.example" name="fixture-lib" version="1.2.3">
                  <artifact name="fixture-lib-1.2.3.jar">
                    <sha256 value="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"/>
                  </artifact>
                  <artifact name="fixture-lib-1.2.3.pom">
                    <sha256 value="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"/>
                  </artifact>
                </component>
              </components>
            </verification-metadata>
            """);
        initGit(repo);

        ScanResult result = new ScanEngine().scan(new ScanOptions(repo, temp.resolve("verification-out"), List.of(), List.of(), List.of(), 1024 * 1024, false, "all"));

        assertTrue(result.facts().stream().anyMatch(fact -> FactTypes.FILE_INVENTORIED.equals(fact.factType())
            && "gradle/verification-metadata.xml".equals(fact.properties().get("path"))));
        for (CodeFact fact : result.facts()) {
            assertFalse(fact.properties().containsKey("artifactDigest"), fact.toString());
            assertFalse(fact.properties().containsKey("artifactDigestAlgorithm"), fact.toString());
        }
        assertFalse(result.facts().toString().contains("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
    }

    @Test
    void mavenScansEmitMavenLockfileCapabilityGapWithoutLosingBuildEvidence() throws Exception {
        Path repo = temp.resolve("maven-repo");
        Files.createDirectories(repo);
        Files.writeString(repo.resolve("pom.xml"), """
            <project xmlns="http://maven.apache.org/POM/4.0.0">
              <modelVersion>4.0.0</modelVersion>
              <groupId>com.example</groupId>
              <artifactId>maven-gap-sample</artifactId>
              <version>1.0.0</version>
              <dependencies>
                <dependency>
                  <groupId>org.springframework</groupId>
                  <artifactId>spring-web</artifactId>
                  <version>6.2.0</version>
                </dependency>
              </dependencies>
            </project>
            """);
        initGit(repo);

        ScanResult result = new ScanEngine().scan(new ScanOptions(repo, temp.resolve("maven-gap-out"), List.of(), List.of(), List.of(), 1024 * 1024, false, "all"));

        assertTrue(result.facts().stream().anyMatch(fact -> FactTypes.ANALYSIS_GAP.equals(fact.factType())
            && "MavenLockfileUnavailable".equals(fact.properties().get("gapKind"))
            && "pom.xml".equals(fact.evidence().filePath())));
        assertTrue(result.facts().stream().anyMatch(fact -> FactTypes.PACKAGE_REFERENCED.equals(fact.factType())
            && "org.springframework:spring-web".equals(fact.properties().get("packageName"))));
        assertEquals("Level3SyntaxAnalysis", result.manifest().analysisLevel());
        assertEquals("NotRun", result.manifest().buildStatus());
    }

    @Test
    void repeatedLockfileScansAreByteIdentical() throws Exception {
        String lockfile = """
            org.springframework:spring-web:6.2.0=runtimeClasspath
            com.example:fixture-lib:1.2.3=compileClasspath,runtimeClasspath
            """;
        Path repo = lockfileRepo(lockfile);
        initGit(repo);

        new ScanEngine().scan(new ScanOptions(repo, temp.resolve("repeat-one"), List.of(), List.of(), List.of(), 1024 * 1024, false, "all"));
        new ScanEngine().scan(new ScanOptions(repo, temp.resolve("repeat-two"), List.of(), List.of(), List.of(), 1024 * 1024, false, "all"));

        assertEquals(
            Files.readString(temp.resolve("repeat-one/facts.ndjson")),
            Files.readString(temp.resolve("repeat-two/facts.ndjson")));
    }

    private ScanResult scanLockfileRepo(String lockfile) throws Exception {
        Path repo = lockfileRepo(lockfile);
        initGit(repo);
        return new ScanEngine().scan(new ScanOptions(repo, temp.resolve("lock-out-" + System.nanoTime()), List.of(), List.of(), List.of(), 1024 * 1024, false, "all"));
    }

    private Path lockfileRepo(String lockfile) throws Exception {
        Path repo = temp.resolve("gradle-lockfile-repo-" + System.nanoTime());
        Files.createDirectories(repo);
        Files.writeString(repo.resolve("settings.gradle"), "rootProject.name = 'lockfile-sample'\n");
        Files.writeString(repo.resolve("gradle.lockfile"), lockfile);
        return repo;
    }

    private static CodeFact packageFact(ScanResult result, String packageName) {
        return result.facts().stream()
            .filter(fact -> FactTypes.PACKAGE_REFERENCED.equals(fact.factType())
                && packageName.equals(fact.properties().get("packageName")))
            .findFirst()
            .orElseThrow();
    }

    private static List<String> resolvedRows(ScanResult result) {
        return result.facts().stream()
            .filter(fact -> FactTypes.PACKAGE_REFERENCED.equals(fact.factType())
                && "gradle.lockfile".equals(fact.properties().get("manifestKind")))
            .map(fact -> fact.properties().get("resolvedVersion"))
            .toList();
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
        assertEquals(0, process.exitValue(), new String(process.getInputStream().readAllBytes()));
    }
}
