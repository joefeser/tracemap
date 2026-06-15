import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import test from "node:test";

import {
  collectPublicFiles,
  createDemoSummaryFixture,
  refreshDemoSummary,
  validateDemoSummaryFixture
} from "./refresh-demo-summary.mjs";

test("refreshDemoSummary writes a scrubbed fixture from demo-summary.json", async () => {
  const root = await createDemoOutput();
  const fixturePath = join(root, "fixture.json");

  await refreshDemoSummary({ demoRoot: root, fixturePath });

  const fixture = JSON.parse(await readFile(fixturePath, "utf8"));
  assert.equal(fixture.version, "1.0");
  assert.equal(fixture.publicClaimLevel, "demo");
  assert.equal(fixture.source.generator, "scripts/demo-public.sh");
  assert.equal(fixture.source.demoSummary.version, "1.0");
  assert.equal(fixture.source.demoSummary.outputRootHash, "path-hash:0123456789abcdef01234567");

  const sampleScans = section(fixture, "sample-scans");
  assert.deepEqual(sampleScans.artifacts, []);
  assert.deepEqual(sampleScans.localOnlyArtifactFamilies, ["scan-reports"]);

  const dependency = section(fixture, "combine-and-dependency-report");
  assert.equal(dependency.coverage, "PartialAnalysis");
  assert.deepEqual(dependency.artifacts, [
    "reports/dependency/endpoint-stack/dependency-report.json",
    "reports/dependency/endpoint-stack/dependency-report.md"
  ]);
});

test("createDemoSummaryFixture maps reportCoverage and artifactPaths fields", () => {
  const fixture = createDemoSummaryFixture(summaryFixture());
  const dependency = section(fixture, "combine-and-dependency-report");

  assert.equal(dependency.coverage, "PartialAnalysis");
  assert.deepEqual(dependency.artifacts, [
    "reports/dependency/endpoint-stack/dependency-report.json",
    "reports/dependency/endpoint-stack/dependency-report.md"
  ]);
  assert.equal(dependency.counts.endpointFindings, 14);
});

test("refreshDemoSummary rejects unsupported demo-summary.json versions", async () => {
  const root = await createDemoOutput({
    summary: {
      ...summaryFixture(),
      version: "2.0"
    }
  });

  await assert.rejects(refreshDemoSummary({ demoRoot: root, fixturePath: join(root, "fixture.json") }), /Unsupported demo-summary\.json version: 2\.0/);
});

test("refreshDemoSummary rejects unknown and duplicate section names", async () => {
  const unknownRoot = await createDemoOutput({
    summary: {
      ...summaryFixture(),
      sections: [...summaryFixture().sections, sectionRow("surprise")]
    }
  });
  await assert.rejects(
    refreshDemoSummary({ demoRoot: unknownRoot, fixturePath: join(unknownRoot, "fixture.json") }),
    /Unknown demo-summary\.json section name: surprise/
  );

  const duplicateRoot = await createDemoOutput({
    summary: {
      ...summaryFixture(),
      sections: [...summaryFixture().sections, sectionRow("toolchains")]
    }
  });
  await assert.rejects(
    refreshDemoSummary({ demoRoot: duplicateRoot, fixturePath: join(duplicateRoot, "fixture.json") }),
    /Duplicate demo-summary\.json section name: toolchains/
  );
});

test("refreshDemoSummary rejects missing rule IDs, evidence tiers, and required reasons", async () => {
  await assertBadSection({ ruleIds: [] }, /Section toolchains is missing ruleIds/);
  await assertBadSection({ evidenceTier: "" }, /Section toolchains is missing evidenceTier/);
  await assertBadSection(
    { name: "jvm", status: "unavailable", reason: "" },
    /Section jvm must include a reason for status unavailable/
  );
});

test("refreshDemoSummary rejects unsafe artifact paths and missing public artifacts", async () => {
  const cases = [
    ["absolute", unixHomePath("example/demo/report.md"), /local-absolute-path/],
    ["windows", "C:\\Users\\example\\report.md", /windows-absolute-path/],
    ["traversal", "../reports/demo.md", /must not contain empty, current, or parent path segments/],
    ["backslash", "reports\\demo.md", /POSIX path separators/],
    ["combined", "combined/endpoint-stack.sqlite", /raw artifact family/],
    ["logs", "logs/analyzer.log", /raw artifact family/],
    ["facts", "reports/dependency/facts.ndjson", /raw artifact family/],
    ["unknown", "exports/demo.json", /unknown artifact family/]
  ];

  for (const [label, artifactPath, pattern] of cases) {
    await assertBadSection({ artifactPaths: [artifactPath] }, pattern, label);
  }

  const root = await createDemoOutput({
    skipPublicArtifacts: ["reports/dependency/missing/dependency-report.md"],
    summary: withSection("combine-and-dependency-report", {
      artifactPaths: ["reports/dependency/missing/dependency-report.md"]
    })
  });
  await assert.rejects(
    refreshDemoSummary({ demoRoot: root, fixturePath: join(root, "fixture.json") }),
    /references missing public artifact: reports\/dependency\/missing\/dependency-report\.md/
  );
});

test("refreshDemoSummary rejects unsafe values before committing fixture data", async () => {
  const cases = [
    ["unix path", unixTempPath("tracemap-demo/private"), /local-absolute-path/],
    ["windows path", "C:\\Users\\example\\private", /windows-absolute-path/],
    ["file url", `file://${unixHomePath("example/private")}`, /file-url/],
    ["raw remote", "git@github.com:private/repo.git", /unsafe value \(raw-repository-remote\)/],
    ["git path", "repo/.git/config", /unsafe value \(git-path\)/],
    ["secret", "token=abcdefghi", /secret-looking-value/],
    ["sql", "select * from private.accounts", /sql-sentinel/]
  ];

  for (const [label, reason, pattern] of cases) {
    const root = await createDemoOutput({
      summary: withSection("jvm", { status: "unavailable", reason })
    });
    await assert.rejects(
      refreshDemoSummary({ demoRoot: root, fixturePath: join(root, "fixture.json") }),
      pattern,
      label
    );
  }
});

test("refreshDemoSummary ignores unsafe files under raw generated folders", async () => {
  const root = await createDemoOutput();
  await mkdir(join(root, "scans", "sample"), { recursive: true });
  await mkdir(join(root, "combined"), { recursive: true });
  await mkdir(join(root, "logs"), { recursive: true });
  await writeFile(join(root, "scans", "sample", "report.md"), `leak ${unixHomePath("example/private")}\n`, "utf8");
  await writeFile(join(root, "combined", "combined.json"), `leak ${unixHomePath("example/private")}\n`, "utf8");
  await writeFile(join(root, "logs", "analyzer.log"), "token=abcdefghi\n", "utf8");

  await refreshDemoSummary({ demoRoot: root, fixturePath: join(root, "fixture.json") });
});

test("refreshDemoSummary rejects unsafe portfolio manifest indexPath values", async () => {
  const root = await createDemoOutput({
    portfolioManifest: { inputs: [{ label: "endpoint-stack", indexPath: unixHomePath("example/index.sqlite") }] }
  });

  await assert.rejects(
    refreshDemoSummary({ demoRoot: root, fixturePath: join(root, "fixture.json") }),
    /portfolio-manifest\.json inputs\[0\]\.indexPath must be relative/
  );
});

test("refreshDemoSummary reports invalid portfolio manifest JSON with context", async () => {
  const root = await createDemoOutput();
  await writeFile(join(root, "portfolio-manifest.json"), "{not-json}\n", "utf8");

  await assert.rejects(
    refreshDemoSummary({ demoRoot: root, fixturePath: join(root, "fixture.json") }),
    /Failed to parse or validate portfolio-manifest\.json/
  );
});

test("collectPublicFiles only skips raw generated folders at the output root", async () => {
  const root = await mkdtemp(join(tmpdir(), "tracemap-demo-summary-test-"));
  await mkdir(join(root, "reports", "combined"), { recursive: true });
  await mkdir(join(root, "combined"), { recursive: true });
  await writeFile(join(root, "reports", "combined", "report.md"), "safe nested report\n", "utf8");
  await writeFile(join(root, "combined", "report.md"), "raw root report\n", "utf8");

  const files = (await collectPublicFiles(root)).map((file) => file.slice(root.length + 1));

  assert.deepEqual(files, ["reports/combined/report.md"]);
});

test("validateDemoSummaryFixture rejects raw remotes and raw artifacts in committed fixture data", () => {
  const fixture = createDemoSummaryFixture(summaryFixture());
  section(fixture, "toolchains").reason = "git@github.com:private/repo.git";
  assert.match(validateDemoSummaryFixture(fixture).join("\n"), /unsafe value \(raw-repository-remote\)/);

  const unsafeArtifactFixture = createDemoSummaryFixture(summaryFixture());
  section(unsafeArtifactFixture, "combine-and-dependency-report").artifacts = ["scans/demo/report.md"];
  assert.match(validateDemoSummaryFixture(unsafeArtifactFixture).join("\n"), /unsafe artifact path/);
});

async function assertBadSection(overrides, pattern, label = "bad section") {
  const root = await createDemoOutput({
    summary: withSection(overrides.name ?? "toolchains", overrides)
  });

  await assert.rejects(
    refreshDemoSummary({ demoRoot: root, fixturePath: join(root, "fixture.json") }),
    pattern,
    label
  );
}

async function createDemoOutput({
  portfolioManifest = { inputs: [{ label: "endpoint-stack", indexPath: "combined/endpoint-stack.sqlite" }] },
  skipPublicArtifacts = [],
  summary = summaryFixture()
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-demo-summary-test-"));
  await mkdir(join(root, "reports", "dependency", "endpoint-stack"), { recursive: true });
  await mkdir(join(root, "reports", "paths", "endpoint-to-sql"), { recursive: true });
  await mkdir(join(root, "reports", "reverse", "sql-to-endpoints"), { recursive: true });
  await mkdir(join(root, "reports", "portfolio"), { recursive: true });
  await mkdir(join(root, "reports", "diff", "public-demo"), { recursive: true });
  await mkdir(join(root, "reports", "impact", "public-demo"), { recursive: true });
  await mkdir(join(root, "reports", "release-review", "public-demo"), { recursive: true });

  await writeFile(join(root, "demo-summary.md"), "safe summary\n", "utf8");
  await writeFile(join(root, "demo-summary.json"), `${JSON.stringify(summary, null, 2)}\n`, "utf8");
  await writeFile(join(root, "portfolio-manifest.json"), `${JSON.stringify(portfolioManifest)}\n`, "utf8");

  const skippedArtifacts = new Set(skipPublicArtifacts);
  for (const artifact of allPublicArtifacts(summary)) {
    if (artifact === "portfolio-manifest.json" || skippedArtifacts.has(artifact)) {
      continue;
    }
    await mkdir(dirname(join(root, artifact)), { recursive: true });
    await writeFile(join(root, artifact), "safe report\n", "utf8");
  }

  return root;
}

function summaryFixture() {
  return {
    version: "1.0",
    outputRootHash: "path-hash:0123456789abcdef01234567",
    sections: [
      sectionRow("toolchains", { counts: { requiredTools: 4 } }),
      sectionRow("python", { status: "not_requested", coverage: "not_requested", counts: { requested: 0 } }),
      sectionRow("jvm", {
        status: "unavailable",
        classification: "PartialAnalysis",
        evidenceTier: "Tier4Unknown",
        coverage: "unavailable",
        counts: { java21Available: 0 },
        reason: "Java 21 was not detected; JVM demo scan is optional in this slice."
      }),
      sectionRow("build", { counts: { dotnet: 1, typescript: 1 } }),
      sectionRow("sample-scans", {
        classification: "PartialAnalysis",
        coverage: "PartialAnalysis",
        artifactPaths: ["scans/dotnet-modern/report.md"],
        counts: { scannedSources: 6, factsFiles: 6 }
      }),
      sectionRow("combine-and-dependency-report", {
        classification: "PartialAnalysis",
        coverage: "PartialAnalysis",
        artifactPaths: [
          "reports/dependency/endpoint-stack/dependency-report.md",
          "reports/dependency/endpoint-stack/dependency-report.json"
        ],
        counts: { sources: 6, endpointFindings: 14 }
      }),
      sectionRow("paths-and-reverse", {
        classification: "PartialAnalysis",
        coverage: "PartialAnalysis",
        artifactPaths: [
          "reports/paths/endpoint-to-sql/paths-report.md",
          "reports/reverse/sql-to-endpoints/reverse-report.md"
        ],
        counts: { paths: 12, reversePaths: 29 }
      }),
      sectionRow("portfolio", {
        classification: "PartialAnalysis",
        coverage: "PartialAnalysis",
        artifactPaths: ["portfolio-manifest.json", "reports/portfolio/portfolio-report.md"],
        counts: { portfolioInputs: 2, portfolioSources: 6 }
      }),
      sectionRow("diff", {
        classification: "PartialAnalysis",
        coverage: "PartialAnalysis",
        artifactPaths: ["reports/diff/public-demo/diff-report.md"],
        counts: { diffRows: 14, surfaceDiffs: 12 }
      }),
      sectionRow("impact", {
        classification: "PartialAnalysis",
        coverage: "PartialAnalysis",
        artifactPaths: ["reports/impact/public-demo/impact-report.md"],
        counts: { impactItems: 12, surfaceImpacts: 12 }
      }),
      sectionRow("release-review", {
        classification: "PartialAnalysis",
        coverage: "PartialAnalysis",
        artifactPaths: ["reports/release-review/public-demo/release-review.md"],
        counts: { findings: 50, checklistItems: 53 }
      })
    ]
  };
}

function sectionRow(name, overrides = {}) {
  return {
    name,
    status: overrides.status ?? "available",
    classification: overrides.classification ?? "NoActionableEvidence",
    evidenceTier: overrides.evidenceTier ?? "Tier2Structural",
    ruleIds: overrides.ruleIds ?? ["public.demo.summary.v1"],
    reportCoverage: overrides.coverage ?? "FullEvidenceAvailable",
    artifactPaths: overrides.artifactPaths ?? [],
    counts: overrides.counts ?? {},
    reason: overrides.reason ?? ""
  };
}

function withSection(name, overrides) {
  return {
    ...summaryFixture(),
    sections: summaryFixture().sections.map((item) => (item.name === name ? { ...item, ...overrides, name } : item))
  };
}

function allPublicArtifacts(summary) {
  return summary.sections
    .flatMap((item) => item.artifactPaths)
    .filter((artifact) => artifact === "portfolio-manifest.json" || artifact.startsWith("reports/"));
}

function section(fixture, id) {
  return fixture.sections.find((item) => item.id === id);
}

function unixHomePath(path) {
  return `${String.fromCharCode(47)}Users/${path}`;
}

function unixTempPath(path) {
  return `${String.fromCharCode(47)}tmp/${path}`;
}
