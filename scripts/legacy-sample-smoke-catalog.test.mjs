import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, stat, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { spawnSync } from "node:child_process";
import test from "node:test";

import {
  CatalogValidationError,
  canonicalJson,
  catalogJsonHash,
  detectUnsafeText,
  main,
  renderCatalogMarkdown,
  renderCatalogObject,
  validateCatalogFiles,
  validateCatalogObject,
  validateSafeIdentity
} from "./legacy-sample-smoke-catalog.mjs";

const root = resolve(import.meta.dirname, "..");
const catalogPath = resolve(root, "docs/validation/legacy-sample-smoke-catalog/catalog.json");
const markdownPath = resolve(root, "docs/validation/legacy-sample-smoke-catalog/catalog.md");
const schemaPath = resolve(root, "docs/validation/legacy-sample-smoke-catalog/legacy-sample-smoke-catalog.v1.schema.json");

test("tracked catalog validates and generated Markdown sentinel matches canonical JSON", async () => {
  const result = await validateCatalogFiles({ catalogPath, markdownPath, root });
  assert.equal(result.catalogPath, catalogPath);

  const catalog = await readCatalog();
  const markdown = await readFile(markdownPath, "utf8");
  assert.equal(markdown, renderCatalogMarkdown(catalog));
  assert.match(markdown.split("\n")[0], /^<!-- catalog-json-sha256: [a-f0-9]{64} -->$/u);
  assert.ok(markdown.includes(catalogJsonHash(catalog)));
});

test("canonical JSON is byte-stable and schema excludes local-only commit kinds", async () => {
  const catalog = await readCatalog();
  assert.equal(await readFile(catalogPath, "utf8"), canonicalJson(catalog));

  const second = JSON.parse(canonicalJson(JSON.parse(canonicalJson(catalog))));
  assert.equal(canonicalJson(second), canonicalJson(catalog));

  const schema = await readFile(schemaPath, "utf8");
  assert.equal(schema.includes("redacted-sha256"), false);
  assert.equal(schema.includes("local-only"), false);
});

test("safe identity validation rejects label classes separately", () => {
  const cases = [
    ["foo/bar", "path-separator"],
    ["https://example", "uri-scheme"],
    ["repo.git", "git-suffix"],
    ["owner@repo", "at-identity"],
    ["C:\\repo", "windows-drive"],
    ["~/repo", "home-fragment"],
    ["example.com", "hostname"],
    ["owner/repo", "path-separator"],
    ["private-client", "private-token"],
    ["token-secret", "private-token"]
  ];

  for (const [value, expected] of cases) {
    assert.match(validateSafeIdentity(value, { field: "sampleLabel" }), new RegExp(expected), value);
  }
  assert.equal(validateSafeIdentity("legacy-wcf-public-fixture", { field: "sampleLabel" }), null);
});

test("source classification and commit identity enforce public-safe caps", async () => {
  const catalog = await readCatalog();
  const ruleIds = ruleIdsFor(catalog);

  const localSource = clone(catalog);
  localSource.entries[0].source.classification = "operator-local";
  localSource.entries[0].claimLevel = "demo-safe";
  assertHasDiagnostic(localSource, ruleIds, "source-claim-cap");

  const unreviewedSource = clone(catalog);
  unreviewedSource.entries[0].source.reviewed = false;
  assertHasDiagnostic(unreviewedSource, ruleIds, "source-claim-cap");

  const publicShaOnFixture = clone(catalog);
  publicShaOnFixture.entries[0].source.commitIdentity = {
    kind: "public-sha",
    value: "1111111111111111111111111111111111111111",
    shaPresent: true,
    limitations: []
  };
  assertHasDiagnostic(publicShaOnFixture, ruleIds, "public-sha-source");

  const unpinnedPublic = clone(catalog);
  unpinnedPublic.entries[0].source.commitIdentity = {
    kind: "category-only",
    shaPresent: true,
    limitations: []
  };
  assertHasDiagnostic(unpinnedPublic, ruleIds, "category-only-claim-cap");
  assertHasDiagnostic(unpinnedPublic, ruleIds, "public-safe-pinned-proof");

  const redactedCommit = clone(catalog);
  redactedCommit.entries[0].source.commitIdentity.kind = "redacted-sha256";
  assertHasDiagnostic(redactedCommit, ruleIds, "commit-identity-kind");
});

test("families require rules, tiers, coverage, extractors or gaps, limitations, and large-repo buckets", async () => {
  const catalog = await readCatalog();
  const ruleIds = ruleIdsFor(catalog);

  const unknownRule = clone(catalog);
  unknownRule.entries[0].families[0].expectedRuleIds = ["legacy.unknown.private-table.v1"];
  assertHasDiagnostic(unknownRule, ruleIds, "rule-id");

  const noExtractor = clone(catalog);
  delete noExtractor.entries[0].families[0].expectedExtractors;
  delete noExtractor.entries[0].families[0].extractorGapCodes;
  assertHasDiagnostic(noExtractor, ruleIds, "extractor-or-gap-required");

  const noLimitations = clone(catalog);
  noLimitations.entries[0].families[0].limitations = [];
  assertHasDiagnostic(noLimitations, ruleIds, "limitations");

  const syntaxUpgrade = clone(catalog);
  const fallback = syntaxUpgrade.entries.find((entry) => entry.sampleLabel === "large-public-dotnet-client").families.find((family) => family.familyId === "fallback-syntax-scan");
  fallback.expectedEvidenceTiers = ["Tier1Semantic"];
  assertHasDiagnostic(syntaxUpgrade, ruleIds, "syntax-fallback-tier");

  const missingBuckets = clone(catalog);
  const stress = missingBuckets.entries.find((entry) => entry.sampleLabel === "large-public-dotnet-client").families.find((family) => family.familyId === "large-repo-stress");
  delete stress.timeoutBucket;
  delete stress.artifactSizeBucket;
  assertHasDiagnostic(missingBuckets, ruleIds, "timeout-bucket");
  assertHasDiagnostic(missingBuckets, ruleIds, "artifact-size-bucket");
});

test("command template validation accepts closed-vocabulary literals and rejects identity-bearing literals", async () => {
  const catalog = await readCatalog();
  const ruleIds = ruleIdsFor(catalog);
  assert.deepEqual(validateCatalogObject(catalog, { catalogPath, ruleIds }), []);

  const cases = [
    [`tracemap scan --repo ${macHomePath("example/private")} --out <scan-output>`, "local-absolute-path"],
    ["tracemap scan --repo https://github.com/example/private.git --out <scan-output>", "raw-remote"],
    ["tracemap evidence-pack create --input <redacted-summary> --input-kind unknown-kind --label <sample-label> --claim-level <claim-level> --date <YYYY-MM> --out <pack-output>", "command-input-kind"],
    ["tracemap evidence-pack create --input <redacted-summary> --input-kind legacy-validation-summary --label my-internal-project --claim-level <claim-level> --date <YYYY-MM> --out <pack-output>", "identity-option-placeholder"],
    ["tracemap evidence-pack create --input <redacted-summary> --input-kind legacy-validation-summary --label <sample-label> --claim-level <claim-level> --date 2026-06 --out <pack-output>", "identity-option-placeholder"],
    ["tracemap scan --repo <sample-root> --branch feature-x --out <scan-output>", "unsupported-command-flag"]
  ];

  for (const [template, category] of cases) {
    const candidate = clone(catalog);
    candidate.entries[0].validation.commandTemplates[0].template = template;
    assertHasDiagnostic(candidate, ruleIds, category);
  }
});

test("relationships reject raw artifacts, ignored tmp paths, unknown artifact kinds, and unsafe IDs", async () => {
  const catalog = await readCatalog();
  const ruleIds = ruleIdsFor(catalog);

  const rawArtifact = clone(catalog);
  rawArtifact.entries[0].relationships[0].safeArtifactId = "scan-manifest-json";
  rawArtifact.entries[0].relationships[0].schemaVersion = "scan-manifest-json.v1";
  rawArtifact.entries[0].relationships[0].note = ".tmp/legacy-sample-smoke-catalog/scan-manifest.json";
  assertHasDiagnostic(rawArtifact, ruleIds, "raw-artifact-reference");

  const unknownKind = clone(catalog);
  unknownKind.entries[0].relationships[0].artifactKind = "raw-report";
  assertHasDiagnostic(unknownKind, ruleIds, "relationship-artifact-kind");

  const unsafeId = clone(catalog);
  unsafeId.entries[0].relationships[0].safeArtifactId = "private-client";
  assertHasDiagnostic(unsafeId, ruleIds, "safeArtifactId-private-token");

  const primitiveRelationship = clone(catalog);
  primitiveRelationship.entries[0].relationships[0] = null;
  assertHasDiagnostic(primitiveRelationship, ruleIds, "schema");
});

test("redaction and claim wording diagnostics are sanitized and point to JSON locations", async () => {
  const catalog = await readCatalog();
  const ruleIds = ruleIdsFor(catalog);
  const planted = "token=do-not-echo-this-value";
  const unsafe = clone(catalog);
  unsafe.entries[0].displayName = planted;

  const diagnostics = validateCatalogObject(unsafe, { catalogPath, ruleIds });
  const rendered = new CatalogValidationError(diagnostics).message;
  assert.match(rendered, /\/entries\/0\/displayName/u);
  assert.match(rendered, /secret/u);
  assert.equal(rendered.includes(planted), false);

  const claim = clone(catalog);
  claim.entries[0].displayName = "Proves runtime execution";
  assertHasDiagnostic(claim, ruleIds, "runtime-claim");

  assert.equal(detectUnsafeText("select * from unsafe_table"), "raw-sql");
  assert.equal(detectUnsafeText("Server=db;Password=abc12345;"), "connection-string");
  assert.equal(detectUnsafeText("TraceMap does not prove execution."), null);
  assert.equal(detectUnsafeText("TraceMap does not prove execution. This proves runtime."), "runtime-claim");
});

test("classification floor, hidden entries, duplicates, empty entries, and empty families fail", async () => {
  const catalog = await readCatalog();
  const ruleIds = ruleIdsFor(catalog);

  const hidden = clone(catalog);
  const hiddenEntry = clone(hidden.entries[0]);
  hiddenEntry.sampleLabel = "hidden-operator-sample";
  hiddenEntry.claimLevel = "hidden";
  hiddenEntry.source.classification = "operator-local";
  hiddenEntry.source.commitIdentity = { kind: "category-only", shaPresent: true, limitations: [] };
  hidden.entries.push(hiddenEntry);
  assertHasDiagnostic(hidden, ruleIds, "claim-level-floor");
  assertHasDiagnostic(hidden, ruleIds, "hidden-entry-in-tracked-output");

  const duplicate = clone(catalog);
  duplicate.entries[1].sampleLabel = duplicate.entries[0].sampleLabel;
  assertHasDiagnostic(duplicate, ruleIds, "duplicate-sample-label");

  const empty = clone(catalog);
  empty.entries = [];
  assertHasDiagnostic(empty, ruleIds, "entries");

  const noFamilies = clone(catalog);
  noFamilies.entries[0].families = [];
  assertHasDiagnostic(noFamilies, ruleIds, "families-empty");
});

test("render filtering recomputes top-level classification and fails when no entries remain", async () => {
  const catalog = await readCatalog();
  const mixed = clone(catalog);
  mixed.entries[0].claimLevel = "demo-safe";
  mixed.entries[1].claimLevel = "hidden";
  mixed.entries[1].source.classification = "operator-local";
  mixed.entries[1].source.commitIdentity = { kind: "category-only", shaPresent: true, limitations: [] };

  const demo = renderCatalogObject(mixed, { date: "2026-07", minimumEntryClaimLevel: "demo-safe" });
  assert.equal(demo.generatedFrom.generatedAt, "2026-07");
  assert.equal(demo.safety.classification, "demo-safe");
  assert.equal(demo.entries.some((entry) => entry.claimLevel === "hidden"), false);
  assert.ok(demo.entries.some((entry) => entry.claimLevel === "public-safe"));

  const publicOnly = renderCatalogObject(mixed, { date: "2026-07", minimumEntryClaimLevel: "public-safe" });
  assert.equal(publicOnly.safety.classification, "public-safe");
  assert.equal(publicOnly.entries.some((entry) => entry.claimLevel !== "public-safe"), false);

  const hiddenOnly = clone(catalog);
  hiddenOnly.entries = [mixed.entries[1]];
  assert.throws(() => renderCatalogObject(hiddenOnly, { date: "2026-07", minimumEntryClaimLevel: "demo-safe" }), /removed every catalog entry/);

  const empty = clone(catalog);
  empty.entries = [];
  assert.throws(() => renderCatalogObject(empty, { date: "2026-07" }), /at least one entry/);

  const invalidClaim = clone(catalog);
  delete invalidClaim.entries[0].claimLevel;
  assert.throws(() => renderCatalogObject(invalidClaim, { date: "2026-07" }), /valid claimLevel/);
});

test("render requires explicit date and dry-run writes no files", async () => {
  const code = await main([
    "render",
    "--catalog",
    "docs/validation/legacy-sample-smoke-catalog/catalog.json",
    "--out",
    "docs/validation/legacy-sample-smoke-catalog/dry-run-output",
    "--dry-run"
  ], { root });
  assert.equal(code, 1);

  const dryRunOut = resolve(root, "docs/validation/legacy-sample-smoke-catalog/dry-run-output");
  const dryRunCode = await main([
    "render",
    "--catalog",
    "docs/validation/legacy-sample-smoke-catalog/catalog.json",
    "--out",
    "docs/validation/legacy-sample-smoke-catalog/dry-run-output",
    "--date",
    "2026-06",
    "--dry-run"
  ], { root });
  assert.equal(dryRunCode, 0);
  await assert.rejects(stat(dryRunOut), /ENOENT/u);
});

test("CLI entrypoint runs when invoked through node", () => {
  const result = spawnSync(process.execPath, ["scripts/legacy-sample-smoke-catalog.mjs", "--help"], {
    cwd: root,
    encoding: "utf8"
  });

  assert.equal(result.status, 0);
  assert.match(result.stdout, /legacy-sample-smoke-catalog\.mjs validate/u);
});

test("Markdown validation fails when sentinel is missing, stale, or hand edited", async () => {
  const catalog = await readCatalog();
  const tempRoot = await mkdtemp(join(tmpdir(), "legacy-smoke-catalog-"));
  const tempCatalog = join(tempRoot, "catalog.json");
  const tempMarkdown = join(tempRoot, "catalog.md");
  await writeFile(tempCatalog, canonicalJson(catalog), "utf8");

  await writeFile(tempMarkdown, "# Missing sentinel\n", "utf8");
  await assert.rejects(validateCatalogFiles({ catalogPath: tempCatalog, markdownPath: tempMarkdown, root }), /markdown-sentinel-missing/u);

  await writeFile(tempMarkdown, renderCatalogMarkdown(catalog).replace(/[a-f0-9]{64}/u, "0".repeat(64)), "utf8");
  await assert.rejects(validateCatalogFiles({ catalogPath: tempCatalog, markdownPath: tempMarkdown, root }), /markdown-sentinel-stale/u);

  await writeFile(tempMarkdown, `${renderCatalogMarkdown(catalog)}\nHand edit\n`, "utf8");
  await assert.rejects(validateCatalogFiles({ catalogPath: tempCatalog, markdownPath: tempMarkdown, root }), /markdown-stale/u);
});

test("promotion rejects ignored or out-of-root destinations and check-ignore proves local root", async () => {
  const ignored = spawnSync("git", ["check-ignore", ".tmp/legacy-sample-smoke-catalog/example"], {
    cwd: root,
    encoding: "utf8"
  });
  assert.equal(ignored.status, 0);
  assert.match(ignored.stdout, /\.tmp\/legacy-sample-smoke-catalog\/example/u);

  const outCode = await main([
    "promote",
    "--catalog",
    "docs/validation/legacy-sample-smoke-catalog/catalog.json",
    "--markdown",
    "docs/validation/legacy-sample-smoke-catalog/catalog.md",
    "--out",
    ".tmp/legacy-sample-smoke-catalog/promoted",
    "--dry-run"
  ], { root });
  assert.equal(outCode, 1);
});

test("force does not bypass stale Markdown validation", async () => {
  const catalog = await readCatalog();
  const tempRoot = await mkdtemp(join(tmpdir(), "legacy-smoke-catalog-"));
  const sourceDir = join(tempRoot, "source");
  const outDir = resolve(root, "docs/validation/legacy-sample-smoke-catalog/force-stale-output");
  await mkdir(sourceDir, { recursive: true });
  const tempCatalog = join(sourceDir, "catalog.json");
  const tempMarkdown = join(sourceDir, "catalog.md");
  await writeFile(tempCatalog, canonicalJson(catalog), "utf8");
  await writeFile(tempMarkdown, "# stale\n", "utf8");

  const code = await main([
    "promote",
    "--catalog",
    tempCatalog,
    "--markdown",
    tempMarkdown,
    "--out",
    outDir,
    "--force",
    "--dry-run"
  ], { root });
  assert.equal(code, 1);
});

async function readCatalog() {
  return JSON.parse(await readFile(catalogPath, "utf8"));
}

function clone(value) {
  return JSON.parse(JSON.stringify(value));
}

function ruleIdsFor(catalog) {
  const ids = new Set([
    "legacy.sample-smoke-catalog.entry.v1",
    "legacy.sample-smoke-catalog.source-identity.v1",
    "legacy.sample-smoke-catalog.family-expectation.v1",
    "legacy.sample-smoke-catalog.validation-command.v1",
    "legacy.sample-smoke-catalog.relationship.v1",
    "legacy.sample-smoke-catalog.safety-validation.v1"
  ]);
  for (const entry of catalog.entries) {
    for (const family of entry.families) {
      for (const ruleId of family.expectedRuleIds) {
        ids.add(ruleId);
      }
    }
  }
  return ids;
}

function assertHasDiagnostic(catalog, ruleIds, category) {
  const diagnostics = validateCatalogObject(catalog, { catalogPath, ruleIds });
  assert.ok(diagnostics.some((diagnostic) => diagnostic.category === category), `${category}\n${JSON.stringify(diagnostics, null, 2)}`);
}

function macHomePath(path) {
  return ["", "Users", path].join("/");
}
