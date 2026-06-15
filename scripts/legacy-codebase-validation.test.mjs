import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { execFile } from "node:child_process";
import { promisify } from "node:util";
import test from "node:test";

import {
  assertNoTrackedLocalValidationFiles,
  assertPublicSafeText,
  createLegacyValidationSummary,
  detectUnsafePublicText,
  readLocalManifest,
  renderLegacyValidationMarkdown,
  sampleStatus,
  validateManifestPath,
  validateOutputRoot
} from "./legacy-codebase-validation.mjs";

const execFileAsync = promisify(execFile);

test("manifest and output paths must stay under ignored legacy validation tmp root", () => {
  const root = "/repo";
  assert.equal(validateManifestPath(".tmp/legacy-codebase-validation/repos.local.json", root), "/repo/.tmp/legacy-codebase-validation/repos.local.json");
  assert.equal(validateOutputRoot(".tmp/legacy-codebase-validation/out", root), "/repo/.tmp/legacy-codebase-validation/out");

  assert.throws(() => validateManifestPath("samples/repos.local.json", root), /Manifest path must be/);
  assert.throws(() => validateManifestPath(".tmp/legacy-codebase-validation/other.json", root), /Manifest path must be/);
  assert.throws(() => validateOutputRoot("reports/legacy", root), /Output root must be under/);
});

test("local manifest accepts neutral labels and default bounds without exposing paths", async () => {
  const root = await mkdtemp(join(tmpdir(), "legacy-validation-manifest-"));
  const manifestPath = join(root, ".tmp", "legacy-codebase-validation", "repos.local.json");
  await mkdir(join(root, ".tmp", "legacy-codebase-validation"), { recursive: true });
  await writeFile(manifestPath, JSON.stringify({
    samples: [
      {
        label: "legacy-winforms-app",
        path: join(root, "private-client-name"),
        kind: "legacy-ui"
      }
    ]
  }), "utf8");

  const manifest = await readLocalManifest(manifestPath, root);

  assert.equal(manifest.samples[0].label, "legacy-winforms-app");
  assert.equal(manifest.samples[0].timeoutSeconds, 1200);
  assert.equal(manifest.samples[0].maxArtifactBytes, 524288000);
});

test("local manifest rejects unsafe labels, relative paths, duplicate labels, and unknown kinds", async () => {
  await assertBadManifest({ label: "Private Client", path: "/tmp/private", kind: "legacy-ui" }, /neutral kebab-case/);
  await assertBadManifest({ label: "legacy-winforms-app", path: "relative/private", kind: "legacy-ui" }, /absolute operator-local path/);
  await assertBadManifest({ label: "legacy-winforms-app", path: "/tmp/private", kind: "surprise" }, /kind must be one of/);

  const root = await mkdtemp(join(tmpdir(), "legacy-validation-manifest-"));
  const manifestPath = join(root, ".tmp", "legacy-codebase-validation", "repos.local.json");
  await mkdir(join(root, ".tmp", "legacy-codebase-validation"), { recursive: true });
  await writeFile(manifestPath, JSON.stringify({
    samples: [
      { label: "legacy-winforms-app", path: "/tmp/private-one", kind: "legacy-ui" },
      { label: "legacy-winforms-app", path: "/tmp/private-two", kind: "unknown-legacy" }
    ]
  }), "utf8");
  await assert.rejects(readLocalManifest(manifestPath, root), /Duplicate sample label/);
});

test("redaction rejects unsafe public summary categories", () => {
  const cases = [
    [unixHomePath("example/private-client"), "local-absolute-path"],
    [`file://${unixHomePath("example/private-client")}`, "file-url"],
    ["git@github.com:private/client.git", "raw-remote"],
    ["select * from private_accounts where id = 1", "raw-sql"],
    ["Server=db;User ID=admin;Password=abc12345;", "connection-string"],
    ["apiKey=abcdefghi123", "secret"],
    ["endpoint: \"https://service.internal\"", "config-value"],
    ["public class PrivateController {", "snippet"],
    ["private-client-name", "private-name"]
  ];

  for (const [text, category] of cases) {
    assert.equal(detectUnsafePublicText(text, { privateFragments: ["private-client-name"] }), category, text);
    assert.throws(() => assertPublicSafeText(text, { privateFragments: ["private-client-name"] }), new RegExp(category));
  }
});

test("tracked local validation artifacts fail the safety gate", async () => {
  const root = await mkdtemp(join(tmpdir(), "legacy-validation-git-"));
  await execFileAsync("git", ["init"], { cwd: root });
  await execFileAsync("git", ["config", "user.email", "test@example.invalid"], { cwd: root });
  await execFileAsync("git", ["config", "user.name", "TraceMap Test"], { cwd: root });
  await mkdir(join(root, ".tmp", "legacy-codebase-validation"), { recursive: true });
  await writeFile(join(root, ".tmp", "legacy-codebase-validation", "repos.local.json"), "{}\n", "utf8");
  await execFileAsync("git", ["add", "-f", ".tmp/legacy-codebase-validation/repos.local.json"], { cwd: root });

  await assert.rejects(assertNoTrackedLocalValidationFiles(root), /must remain untracked/);
});

test("legacy validation summary shape is deterministic and public-safe", () => {
  const sample = {
    label: "legacy-winforms-app",
    kind: "legacy-ui",
    status: "completed",
    exitCode: 0,
    durationSeconds: 3,
    artifactBytes: 2048,
    commitSha: "1111111111111111111111111111111111111111",
    repositoryIdentityHash: "repo-hash:0123456789abcdef01234567",
    artifactStatus: {
      "scan-manifest.json": true,
      "facts.ndjson": true,
      "index.sqlite": true,
      "report.md": true,
      "logs/analyzer.log": true
    },
    factCount: 42,
    coverage: "Reduced",
    analysisLevel: "Syntax",
    buildStatus: "ProjectLoadFailed",
    analyzerGapCount: 2,
    targetFrameworks: ["net48"],
    legacyIndicators: {
      oldTargetFrameworks: ["net48"],
      packagesConfigCount: 1,
      bindingRedirectCount: 0,
      oldStyleProjectCount: 1,
      toolsVersions: ["present"]
    },
    environmentGuidance: ["Project load or semantic analysis did not complete; inspect local analyzer logs for SDK, runtime, MSBuild, restore, or project-type requirements."],
    uiEventProbe: {
      classification: "MissingEvidence",
      runtimeCaveat: "Static handler wiring does not prove the handler executes at runtime.",
      handlerMethods: { count: 0, factTypes: [], ruleIds: [], evidenceTiers: [], examples: [] },
      handlerCalls: { count: 0, factTypes: [], ruleIds: [], evidenceTiers: [], examples: [] },
      eventWiring: { count: 0, factTypes: [], ruleIds: [], evidenceTiers: [], examples: [] },
      dependencySurfaces: { count: 0, factTypes: [], ruleIds: [], evidenceTiers: [], examples: [] },
      gaps: [
        {
          gapKind: "LegacyUiEventEvidenceUnavailable",
          ruleId: "legacy.validation.summary.v1",
          evidenceTier: "Tier4Unknown",
          followUpSpec: "legacy-ui-event-surfaces",
          message: "Current facts did not expose legacy UI event wiring evidence for this sample."
        }
      ]
    },
    limitations: ["Scan coverage is reduced; absence-of-evidence findings are coverage-relative."],
    outputArtifacts: [
      { name: "scan-manifest.json", present: true, publicSafe: false },
      { name: "facts.ndjson", present: true, publicSafe: false }
    ]
  };

  const first = createLegacyValidationSummary({ samples: [sample] });
  const second = createLegacyValidationSummary({ samples: [sample] });

  assert.deepEqual(first, second);
  assert.equal(first.ruleId, "legacy.validation.summary.v1");
  assert.deepEqual(first.followUps, ["legacy-ui-event-surfaces"]);
  const markdown = renderLegacyValidationMarkdown(first);
  assert.match(markdown, /Coverage: `Reduced`/);
  assert.match(markdown, /Repository identity: `repo-hash:0123456789abcdef01234567`/);
  assert.match(markdown, /Commit SHA: `1111111111111111111111111111111111111111`/);
  assert.match(markdown, /UI event rule IDs: `legacy\.validation\.summary\.v1`/);
  assert.doesNotThrow(() => assertPublicSafeText(JSON.stringify(first)));
  assert.doesNotThrow(() => assertPublicSafeText(markdown));
});

test("sampleStatus distinguishes clean, partial, failed, and truncated scans", () => {
  const clean = statusInput();
  assert.equal(sampleStatus(clean), "completed");
  assert.equal(sampleStatus(statusInput({ scanSummary: { coverage: "Reduced", buildStatus: "FailedOrPartial" } })), "partial");
  assert.equal(sampleStatus(statusInput({ run: { exitCode: 1, timedOut: false } })), "failed");
  assert.equal(sampleStatus(statusInput({ artifactStatus: { "scan-manifest.json": false } })), "failed");
  assert.equal(sampleStatus(statusInput({ run: { exitCode: 124, timedOut: true } })), "truncated");
  assert.equal(sampleStatus(statusInput({ artifactBytes: 12, maxArtifactBytes: 10 })), "truncated");
});

async function assertBadManifest(sample, pattern) {
  const root = await mkdtemp(join(tmpdir(), "legacy-validation-manifest-"));
  const manifestPath = join(root, ".tmp", "legacy-codebase-validation", "repos.local.json");
  await mkdir(join(root, ".tmp", "legacy-codebase-validation"), { recursive: true });
  await writeFile(manifestPath, JSON.stringify({ samples: [sample] }), "utf8");
  await assert.rejects(readLocalManifest(manifestPath, root), pattern);
}

function unixHomePath(path) {
  return ["", "Users", path].join("/");
}

function statusInput(overrides = {}) {
  return {
    run: { exitCode: 0, timedOut: false },
    artifactBytes: 1,
    maxArtifactBytes: 10,
    scanSummary: { coverage: "Full", buildStatus: "Succeeded" },
    artifactStatus: {
      "scan-manifest.json": true,
      "facts.ndjson": true,
      "index.sqlite": true,
      "report.md": true,
      "logs/analyzer.log": true
    },
    ...overrides
  };
}
