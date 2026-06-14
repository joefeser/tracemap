#!/usr/bin/env node
import crypto from "node:crypto";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

const [, , command, ...args] = process.argv;

function fail(message) {
  console.error(message);
  process.exit(1);
}

function assert(condition, message) {
  if (!condition) {
    fail(message);
  }
}

function readJson(file) {
  try {
    return JSON.parse(fs.readFileSync(file, "utf8"));
  } catch (error) {
    fail(`Failed to read JSON ${safeDisplayPath(file)}: ${error.message}`);
  }
}

function safeDisplayPath(file) {
  return path.relative(process.cwd(), file) || path.basename(file);
}

function hashPath(value) {
  return `path-hash:${crypto.createHash("sha256").update(path.resolve(value)).digest("hex").slice(0, 24)}`;
}

function appendSection() {
  const [jsonlPath, name, status, classification, reportCoverage, reason, artifactCsv = "", countsJson = "{}"] = args;
  assert(jsonlPath && name && status, "append-section requires jsonl path, name, and status.");
  const allowedStatuses = new Set(["available", "not_requested", "unavailable", "deferred", "failed"]);
  assert(allowedStatuses.has(status), `Invalid section status: ${status}`);

  let counts;
  try {
    counts = JSON.parse(countsJson);
  } catch {
    fail(`Invalid counts JSON for section ${name}.`);
  }

  const artifactPaths = artifactCsv
    .split(",")
    .map(item => item.trim())
    .filter(Boolean)
    .sort();

  if (["deferred", "unavailable", "failed"].includes(status)) {
    assert(reason && reason.trim().length > 0, `Section ${name} requires a reason for status ${status}.`);
  }

  const row = {
    name,
    status,
    classification: classification || "NoActionableEvidence",
    reportCoverage: reportCoverage || "not_requested",
    artifactPaths,
    counts,
    reason: reason || ""
  };

  fs.mkdirSync(path.dirname(jsonlPath), { recursive: true });
  fs.appendFileSync(jsonlPath, `${JSON.stringify(row)}\n`, "utf8");
}

function assertScanArtifacts() {
  const [label, scanDir] = args;
  assert(label && scanDir, "scan-artifacts requires label and scan directory.");
  for (const relative of [
    "scan-manifest.json",
    "facts.ndjson",
    "index.sqlite",
    "report.md",
    "logs/analyzer.log"
  ]) {
    const fullPath = path.join(scanDir, relative);
    assert(fs.existsSync(fullPath), `Missing ${label} scan artifact: ${relative}`);
  }

  const factsPath = path.join(scanDir, "facts.ndjson");
  const facts = fs.readFileSync(factsPath, "utf8").trim();
  assert(facts.length > 0, `${label} facts.ndjson is empty.`);
  try {
    JSON.parse(facts.split(/\r?\n/, 1)[0]);
  } catch (error) {
    fail(`${label} facts.ndjson first line is not valid JSON: ${error.message}`);
  }

  const manifest = readJson(path.join(scanDir, "scan-manifest.json"));
  const commitSha = manifest.commitSha ?? manifest.gitCommitSha ?? "";
  assert(/^[0-9a-f]{7,40}$/i.test(commitSha), `${label} scan manifest is missing a SHA-like commit value.`);
}

function scanSummary() {
  const counts = {
    scannedSources: 0,
    factsFiles: 0,
    fullCoverageScans: 0,
    reducedCoverageScans: 0,
    knownGaps: 0
  };

  for (const spec of args) {
    const separator = spec.indexOf("=");
    assert(separator > 0, "scan-summary entries must be label=scanDir.");
    const label = spec.slice(0, separator);
    const scanDir = spec.slice(separator + 1);
    assertScanArtifactShape(label, scanDir);
    const manifest = readJson(path.join(scanDir, "scan-manifest.json"));
    const analysisLevel = String(manifest.analysisLevel ?? "");
    const buildStatus = String(manifest.buildStatus ?? "");
    const gaps = Array.isArray(manifest.knownGaps) ? manifest.knownGaps.length : 0;
    counts.scannedSources += 1;
    counts.factsFiles += 1;
    counts.knownGaps += gaps;
    if (analysisLevel.includes("Reduced") || buildStatus === "FailedOrPartial" || gaps > 0) {
      counts.reducedCoverageScans += 1;
    } else {
      counts.fullCoverageScans += 1;
    }
  }

  process.stdout.write(JSON.stringify(counts));
}

function assertScanArtifactShape(label, scanDir) {
  for (const relative of [
    "scan-manifest.json",
    "facts.ndjson",
    "index.sqlite",
    "report.md",
    "logs/analyzer.log"
  ]) {
    const fullPath = path.join(scanDir, relative);
    assert(fs.existsSync(fullPath), `Missing ${label} scan artifact: ${relative}`);
  }
  const facts = fs.readFileSync(path.join(scanDir, "facts.ndjson"), "utf8").trim();
  assert(facts.length > 0, `${label} facts.ndjson is empty.`);
}

function writeSummary() {
  const [outRoot, sectionsJsonl, summaryJson, summaryMd] = args;
  assert(outRoot && sectionsJsonl && summaryJson && summaryMd, "write-summary requires output root, sections jsonl, summary json, and summary md.");
  const sections = fs.existsSync(sectionsJsonl)
    ? fs.readFileSync(sectionsJsonl, "utf8")
      .split(/\r?\n/)
      .filter(Boolean)
      .map(line => JSON.parse(line))
    : [];

  const summary = {
    version: "1.0",
    outputRootHash: hashPath(outRoot),
    sections
  };

  fs.writeFileSync(summaryJson, `${JSON.stringify(summary, null, 2)}\n`, "utf8");

  const rows = [
    "# TraceMap Public Demo Summary",
    "",
    `Output root: \`${summary.outputRootHash}\``,
    "",
    "| Section | Status | Coverage | Counts | Reason |",
    "| --- | --- | --- | --- | --- |",
    ...sections.map(section => {
      const counts = Object.entries(section.counts ?? {})
        .sort(([left], [right]) => left.localeCompare(right))
        .map(([key, value]) => `${key}=${value}`)
        .join(", ");
      return `| ${escapeCell(section.name)} | \`${escapeCell(section.status)}\` | \`${escapeCell(section.reportCoverage)}\` | ${escapeCell(counts || "n/a")} | ${escapeCell(section.reason || "")} |`;
    }),
    "",
    "Static analysis demo output is evidence context only. It does not prove runtime execution, deployment, production traffic, package restore, vulnerability, compatibility, or release approval.",
    ""
  ];

  fs.writeFileSync(summaryMd, rows.join("\n"), "utf8");
}

function escapeCell(value) {
  return String(value)
    .replaceAll("|", "\\|")
    .replaceAll("\n", " ")
    .replaceAll("\r", " ")
    .replaceAll("`", "\\`");
}

function sentinelScan() {
  const [outRoot] = args;
  assert(outRoot, "sentinel-scan requires output root.");
  const root = path.resolve(outRoot);
  const failures = findSentinelFailures(root);

  if (failures.length > 0) {
    console.error("public-report-sentinel-scan failed:");
    for (const failure of failures.sort()) {
      console.error(`- ${failure}`);
    }
    process.exit(1);
  }
}

function findSentinelFailures(root) {
  const files = [];
  collectPublicFiles(root, files);
  assert(files.length > 0, "public-report-sentinel-scan found no public demo files to inspect.");

  const checks = [
    ["local-absolute-path", /(?:^|[^A-Za-z0-9_])(?:\/Users\/[^/\s`"']+|\/home\/[^/\s`"']+|\/private\/tmp\/[^/\s`"']+|\/tmp\/tracemap-[^/\s`"']+)/],
    ["windows-absolute-path", /[A-Za-z]:\\Users\\/],
    ["url-credential", /https?:\/\/[^/\s:@]+:[^/\s@]+@/i],
    ["connection-string", /\b(?:Password|Pwd|User Id|AccountKey|SharedAccessKey|ConnectionString)\s*=/i],
    ["secret-looking-value", /\b(?:token|secret|password|apikey|api_key|credential)\b\s*[:=]\s*["']?[A-Za-z0-9_./+=-]{8,}/i],
    ["sql-sentinel", /TRACEMAP_SQL_SENTINEL|select\s+\*\s+from\s+private/i]
  ];

  const failures = [];
  for (const file of files) {
    const text = fs.readFileSync(file, "utf8");
    for (const [category, pattern] of checks) {
      if (pattern.test(text)) {
        failures.push(`${path.relative(root, file)} (${category})`);
      }
    }
  }

  return failures.sort();
}

function collectPublicFiles(dir, files) {
  if (!fs.existsSync(dir)) {
    return;
  }

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === "scans" || entry.name === "combined" || entry.name === "logs") {
        continue;
      }
      collectPublicFiles(fullPath, files);
      continue;
    }

    const relative = path.relative(dir, fullPath);
    const isSummary = entry.name === "demo-summary.md" || entry.name === "demo-summary.json";
    const isReport = fullPath.includes(`${path.sep}reports${path.sep}`) && /\.(md|json)$/i.test(entry.name);
    if (isSummary || isReport || relative === "demo-summary.md" || relative === "demo-summary.json") {
      files.push(fullPath);
    }
  }
}

function validateSummary() {
  const [summaryJson] = args;
  const summary = readJson(summaryJson);
  assert(summary.version === "1.0", "demo-summary.json version must be 1.0.");
  assert(/^path-hash:[0-9a-f]{24}$/i.test(summary.outputRootHash ?? ""), "demo-summary.json must include outputRootHash and not an absolute root.");
  assert(Array.isArray(summary.sections), "demo-summary.json sections must be an array.");
  assert(summary.sections.length > 0, "demo-summary.json must include at least one section.");
  for (const section of summary.sections) {
    assert(section.name, "summary section is missing name.");
    assert(section.status, `summary section ${section.name} is missing status.`);
    if (["deferred", "unavailable", "failed"].includes(section.status)) {
      assert(section.reason, `summary section ${section.name} must include a reason.`);
    }
  }
}

function selfTest() {
  const tempRoot = fs.mkdtempSync(path.join(os.tmpdir(), "tracemap-demo-assert-"));
  try {
    fs.mkdirSync(path.join(tempRoot, "reports", "sample"), { recursive: true });
    fs.writeFileSync(path.join(tempRoot, "demo-summary.md"), "safe summary\n", "utf8");
    fs.writeFileSync(path.join(tempRoot, "demo-summary.json"), "{\"version\":\"1.0\"}\n", "utf8");
    assert(findSentinelFailures(tempRoot).length === 0, "clean sentinel fixture should pass.");

    fs.writeFileSync(path.join(tempRoot, "reports", "sample", "report.md"), "leak /Users/example/private\n", "utf8");
    const failures = findSentinelFailures(tempRoot);
    assert(failures.some(failure => failure.includes("local-absolute-path")), "sentinel fixture should catch home-path leak.");

    const missingScan = path.join(tempRoot, "missing-scan");
    fs.mkdirSync(missingScan);
    const missingScanResult = spawnSync(process.execPath, [process.argv[1], "scan-artifacts", "missing", missingScan], { encoding: "utf8" });
    assert(missingScanResult.status !== 0, "scan-artifacts should fail when required artifacts are missing.");

    const invalidSummary = path.join(tempRoot, "invalid-summary.json");
    fs.writeFileSync(invalidSummary, "{\"version\":\"1.0\",\"sections\":[]}\n", "utf8");
    const invalidSummaryResult = spawnSync(process.execPath, [process.argv[1], "validate-summary", invalidSummary], { encoding: "utf8" });
    assert(invalidSummaryResult.status !== 0, "validate-summary should fail when outputRootHash is missing.");
  } finally {
    fs.rmSync(tempRoot, { recursive: true, force: true });
  }
}

switch (command) {
  case "append-section":
    appendSection();
    break;
  case "scan-artifacts":
    assertScanArtifacts();
    break;
  case "scan-summary":
    scanSummary();
    break;
  case "write-summary":
    writeSummary();
    break;
  case "sentinel-scan":
    sentinelScan();
    break;
  case "validate-summary":
    validateSummary();
    break;
  case "self-test":
    selfTest();
    break;
  default:
    fail(`Unknown demo-public-assert command: ${command ?? "<missing>"}`);
}
