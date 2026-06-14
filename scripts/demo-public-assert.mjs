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

function requireFile(file, label = file) {
  assert(fs.existsSync(file), `Missing ${label}: ${safeDisplayPath(file)}`);
}

function assertShaLike(value, label) {
  assert(/^[0-9a-f]{7,40}$/i.test(String(value ?? "")), `${label} is missing a SHA-like commit value.`);
}

function assertNonEmptyString(value, label) {
  assert(typeof value === "string" && value.trim().length > 0, `${label} is missing.`);
}

function sortedStrings(values) {
  return [...values].map(value => String(value)).sort((left, right) => left.localeCompare(right));
}

function assertExactLabels(actualLabels, expectedCsv, label) {
  const expected = sortedStrings(expectedCsv.split(",").map(item => item.trim()).filter(Boolean));
  const actual = sortedStrings(actualLabels);
  assert(expected.length > 0, `${label} requires at least one expected label.`);
  assert(
    actual.length === expected.length && actual.every((value, index) => value === expected[index]),
    `${label} expected labels ${expected.join(",")}, got ${actual.join(",")}`);
}

function hasVolatileKey(value) {
  if (Array.isArray(value)) {
    return value.some(hasVolatileKey);
  }

  if (value && typeof value === "object") {
    return Object.entries(value).some(([key, child]) =>
      /generatedAt|timestamp|scannedAt/i.test(key) || hasVolatileKey(child));
  }

  return false;
}

function assertNoVolatileKeys(value, label) {
  assert(!hasVolatileKey(value), `${label} contains a volatile generatedAt/timestamp/scannedAt field.`);
}

function publicReportFiles(reportDir, markdownName, jsonName) {
  const markdown = path.join(reportDir, markdownName);
  const json = path.join(reportDir, jsonName);
  requireFile(markdown, markdownName);
  requireFile(json, jsonName);
  return { markdown, json };
}

function safeDisplayPath(file) {
  return path.relative(process.cwd(), file) || path.basename(file);
}

function hashPath(value) {
  return `path-hash:${crypto.createHash("sha256").update(path.resolve(value)).digest("hex").slice(0, 24)}`;
}

function ruleIdsForSection(name) {
  assert(/^[a-z0-9][a-z0-9-]*$/i.test(name), `Invalid section name: ${name}`);
  return ["public.demo.summary.v1"];
}

function evidenceTierForSection(status) {
  if (["deferred", "unavailable", "failed"].includes(status)) {
    return "Tier4Unknown";
  }

  return "Tier2Structural";
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
    evidenceTier: evidenceTierForSection(status),
    ruleIds: ruleIdsForSection(name),
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

  const manifest = readJson(path.join(scanDir, "scan-manifest.json")) ?? {};
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
    const manifest = readJson(path.join(scanDir, "scan-manifest.json")) ?? {};
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

function dependencyReport() {
  const [reportDir, expectedLabelsCsv, targetPathKey] = args;
  assert(reportDir && expectedLabelsCsv && targetPathKey, "dependency-report requires report dir, expected labels, and target path key.");
  const { json } = publicReportFiles(reportDir, "dependency-report.md", "dependency-report.json");
  const report = readJson(json) ?? {};
  assertNoVolatileKeys(report, "dependency-report.json");
  assertExactLabels((report.sources ?? []).map(source => source.label), expectedLabelsCsv, "dependency report sources");
  for (const source of report.sources ?? []) {
    assertNonEmptyString(source.sourceIndexId, `source ${source.label} sourceIndexId`);
    assertShaLike(source.commitSha, `source ${source.label} commitSha`);
  }

  const findings = report.endpointFindings ?? [];
  const targetFinding = findings.find(finding =>
    finding.normalizedPathKey === targetPathKey
      && finding.clientSourceLabel
      && finding.serverSourceLabel);
  assert(targetFinding, `Expected endpoint finding for ${targetPathKey}.`);
  assert(["MatchedEndpoint", "AmbiguousMatch", "OptionalSegmentMatch"].includes(targetFinding.classification),
    `Unexpected endpoint classification for ${targetPathKey}: ${targetFinding.classification}`);
  assertNonEmptyString(targetFinding.clientRuleId, "target endpoint client ruleId");
  assertNonEmptyString(targetFinding.clientEvidenceTier, "target endpoint client evidenceTier");
  assertNonEmptyString(targetFinding.serverRuleId, "target endpoint server ruleId");
  assertNonEmptyString(targetFinding.serverEvidenceTier, "target endpoint server evidenceTier");
  assertShaLike(targetFinding.clientCommitSha, "target endpoint clientCommitSha");
  assertShaLike(targetFinding.serverCommitSha, "target endpoint serverCommitSha");

  for (const surface of report.dependencySurfaces ?? []) {
    assertNonEmptyString(surface.ruleId, `surface ${surface.displayName} ruleId`);
    assertNonEmptyString(surface.evidenceTier, `surface ${surface.displayName} evidenceTier`);
    assertShaLike(surface.commitSha, `surface ${surface.displayName} commitSha`);
  }

  for (const edge of report.dependencyEdges ?? []) {
    assertNonEmptyString(edge.ruleId, `edge ${edge.edgeId} ruleId`);
    assertNonEmptyString(edge.evidenceTier, `edge ${edge.edgeId} evidenceTier`);
  }

  process.stdout.write(JSON.stringify({
    sources: report.summary?.sourceCount ?? (report.sources ?? []).length,
    endpointFindings: report.summary?.endpointFindingCount ?? findings.length,
    dependencySurfaces: (report.dependencySurfaces ?? []).length,
    dependencyEdges: report.summary?.dependencyEdgeCount ?? (report.dependencyEdges ?? []).length,
    gaps: (report.knownGaps ?? []).length
  }));
}

function pathsReport() {
  const [reportDir, expectedLabelsCsv] = args;
  assert(reportDir && expectedLabelsCsv, "paths-report requires report dir and expected labels.");
  const { json } = publicReportFiles(reportDir, "paths-report.md", "paths-report.json");
  const report = readJson(json) ?? {};
  assertNoVolatileKeys(report, "paths-report.json");
  assertExactLabels((report.sources ?? []).map(source => source.label), expectedLabelsCsv, "paths report sources");

  const paths = report.paths ?? [];
  const gaps = report.gaps ?? [];
  assert((report.summary?.pathCount ?? paths.length) > 0, "Expected at least one default public demo path.");

  for (const row of paths) {
    assertNonEmptyString(row.pathId, "path pathId");
    assert((row.nodes ?? []).length > 0, `Path ${row.pathId} has no nodes.`);
    assert((row.edges ?? []).length > 0, `Path ${row.pathId} has no edges.`);
    assert((row.supportingFactIds ?? []).length > 0 || (row.supportingEdgeIds ?? []).length > 0,
      `Path ${row.pathId} is missing supporting fact or edge IDs.`);
    for (const node of row.nodes ?? []) {
      assertNonEmptyString(node.sourceLabel, `path ${row.pathId} node sourceLabel`);
      if (node.ruleId || node.evidenceTier) {
        assertNonEmptyString(node.ruleId, `path ${row.pathId} node ruleId`);
        assertNonEmptyString(node.evidenceTier, `path ${row.pathId} node evidenceTier`);
      }
    }
    for (const edge of row.edges ?? []) {
      assertNonEmptyString(edge.ruleId, `path ${row.pathId} edge ${edge.edgeId} ruleId`);
      assertNonEmptyString(edge.evidenceTier, `path ${row.pathId} edge ${edge.edgeId} evidenceTier`);
    }
  }

  const usefulClassifications = new Set(["StrongStaticPath", "ProbableStaticPath", "NeedsReviewPath"]);
  const connectedSqlPath = paths.find(row => {
    const sources = new Set((row.nodes ?? []).map(node => node.sourceLabel));
    const edgeKinds = new Set((row.edges ?? []).map(edge => edge.edgeKind));
    const terminal = row.nodes?.[row.nodes.length - 1];
    return sources.has("public-ts-client")
      && sources.has("public-dotnet-server")
      && terminal?.surfaceKind === "sql-query"
      && edgeKinds.has("endpoint-match")
      && edgeKinds.has("calls")
      && edgeKinds.has("symbol-reconciliation")
      && edgeKinds.has("surface-evidence")
      && usefulClassifications.has(row.classification);
  });
  assert(connectedSqlPath, "Expected a connected public-ts-client -> public-dotnet-server -> sql-query path.");

  for (const gap of gaps) {
    assertNonEmptyString(gap.ruleId, `path gap ${gap.gapId} ruleId`);
    assertNonEmptyString(gap.evidenceTier, `path gap ${gap.gapId} evidenceTier`);
  }

  process.stdout.write(JSON.stringify({
    paths: report.summary?.pathCount ?? paths.length,
    gaps: report.summary?.gapCount ?? gaps.length,
    sources: report.summary?.sourceCount ?? (report.sources ?? []).length
  }));
}

function reverseReport() {
  const [reportDir] = args;
  assert(reportDir, "reverse-report requires report dir.");
  const { json } = publicReportFiles(reportDir, "reverse-report.md", "reverse-report.json");
  const report = readJson(json) ?? {};
  assert(report.reportType === "combined-reverse-query", `Unexpected reverse report type: ${report.reportType}`);
  assertNoVolatileKeys(report, "reverse-report.json");
  assert((report.summary?.selectedSurfaceCount ?? 0) > 0, "Expected reverse query to select at least one surface.");
  assert((report.summary?.pathCount ?? 0) > 0, "Expected reverse query to produce at least one path.");
  assert((report.reverseRoots ?? []).some(root => root.rootKind === "EndpointClient" || root.rootKind === "EndpointRoute"),
    "Expected reverse query to include an endpoint root.");

  for (const source of report.snapshot?.sources ?? []) {
    assertShaLike(source.commitSha, `reverse source ${source.sourceLabel} commitSha`);
  }
  for (const surface of report.selectedSurfaces ?? []) {
    assertNonEmptyString(surface.ruleId, `reverse surface ${surface.surfaceId} ruleId`);
    assertNonEmptyString(surface.evidenceTier, `reverse surface ${surface.surfaceId} evidenceTier`);
    assert((surface.supportingFactIds ?? []).length > 0, `reverse surface ${surface.surfaceId} missing supportingFactIds`);
  }
  for (const root of report.reverseRoots ?? []) {
    assert((root.ruleIds ?? []).length > 0, `reverse root ${root.rootId} missing ruleIds`);
    assert((root.evidenceTiers ?? []).length > 0, `reverse root ${root.rootId} missing evidenceTiers`);
  }
  for (const row of report.paths ?? []) {
    assert((row.nodes ?? []).length > 0, `Reverse path ${row.pathId} has no nodes.`);
    assert((row.edges ?? []).length > 0, `Reverse path ${row.pathId} has no edges.`);
    assert((row.ruleIds ?? []).length > 0, `Reverse path ${row.pathId} is missing ruleIds.`);
    assert((row.evidenceTiers ?? []).length > 0, `Reverse path ${row.pathId} is missing evidenceTiers.`);
    assert((row.supportingFactIds ?? []).length > 0 || (row.supportingEdgeIds ?? []).length > 0,
      `Reverse path ${row.pathId} is missing supporting fact or edge IDs.`);
  }
  for (const gap of report.gaps ?? []) {
    assertNonEmptyString(gap.ruleId, `reverse gap ${gap.gapId} ruleId`);
    assertNonEmptyString(gap.evidenceTier, `reverse gap ${gap.gapId} evidenceTier`);
  }

  process.stdout.write(JSON.stringify({
    reverseRoots: report.summary?.reverseRootCount ?? (report.reverseRoots ?? []).length,
    paths: report.summary?.pathCount ?? (report.paths ?? []).length,
    gaps: report.summary?.gapCount ?? (report.gaps ?? []).length,
    selectedSurfaces: report.summary?.selectedSurfaceCount ?? (report.selectedSurfaces ?? []).length
  }));
}

function portfolioManifest() {
  const [outRoot, manifestPath, ...inputSpecs] = args;
  assert(outRoot && manifestPath && inputSpecs.length > 0, "portfolio-manifest requires output root, manifest path, and label=relative-index inputs.");
  const root = path.resolve(outRoot);
  const inputs = inputSpecs.map(spec => {
    const separator = spec.indexOf("=");
    assert(separator > 0, "portfolio-manifest inputs must be label=relative-index.");
    const label = spec.slice(0, separator);
    const indexPath = spec.slice(separator + 1);
    assert(/^[a-z0-9][a-z0-9-]*$/i.test(label), `Invalid portfolio label: ${label}`);
    assert(!path.isAbsolute(indexPath), `portfolio manifest index path must be relative: ${label}`);
    requireFile(path.join(root, indexPath), `portfolio input ${label}`);
    return {
      label,
      indexPath,
      group: label.includes("endpoint") ? "endpoint-stack" : "mixed-stack",
      roleTags: label.includes("endpoint") ? ["endpoint-demo"] : ["mixed-demo"]
    };
  });

  fs.writeFileSync(manifestPath, `${JSON.stringify({
    version: "1.0",
    portfolioId: "public-demo",
    snapshotId: "generated-current",
    inputs
  }, null, 2)}\n`, "utf8");
}

function portfolioReport() {
  const [reportDir, expectedInputLabelsCsv] = args;
  assert(reportDir && expectedInputLabelsCsv, "portfolio-report requires report dir and expected input labels.");
  const { json } = publicReportFiles(reportDir, "portfolio-report.md", "portfolio-report.json");
  const report = readJson(json) ?? {};
  assert(report.reportType === "multi-index-portfolio-report", `Unexpected portfolio report type: ${report.reportType}`);
  assertNoVolatileKeys(report, "portfolio-report.json");
  assertExactLabels((report.inputs ?? []).map(input => input.label), expectedInputLabelsCsv, "portfolio inputs");
  assert((report.summary?.sourceCount ?? 0) > 0, "Expected portfolio source coverage.");
  assert((report.summary?.surfaceCount ?? 0) > 0, "Expected portfolio dependency surfaces.");

  for (const source of report.sources ?? []) {
    assertNonEmptyString(source.label, "portfolio source label");
    assertShaLike(source.commitSha, `portfolio source ${source.label} commitSha`);
  }

  const sourceCoverageRows = report.sourceCoverage?.rows ?? [];
  assert(sourceCoverageRows.length > 0, "Expected portfolio source coverage rows.");
  for (const row of sourceCoverageRows) {
    assertNonEmptyString(row.ruleId, `portfolio source coverage ${row.sourceLabel} ruleId`);
    assertNonEmptyString(row.evidenceTier, `portfolio source coverage ${row.sourceLabel} evidenceTier`);
    assertShaLike(row.commitSha, `portfolio source coverage ${row.sourceLabel} commitSha`);
  }

  for (const sectionName of ["endpointAlignment", "dependencySurfaces", "dependencyEdges", "sharedSurfaces"]) {
    const section = report[sectionName];
    assert(section, `portfolio section ${sectionName} is missing.`);
    for (const gap of section.gaps ?? []) {
      assertNonEmptyString(gap.ruleId, `portfolio ${sectionName} gap ruleId`);
      assertNonEmptyString(gap.evidenceTier, `portfolio ${sectionName} gap evidenceTier`);
    }
  }

  for (const row of report.dependencySurfaces?.rows ?? []) {
    assertNonEmptyString(row.ruleId, `portfolio surface ${row.surfaceId} ruleId`);
    assertNonEmptyString(row.evidenceTier, `portfolio surface ${row.surfaceId} evidenceTier`);
    assertShaLike(row.commitSha, `portfolio surface ${row.surfaceId} commitSha`);
    assert((row.supportingFactIds ?? []).length > 0, `portfolio surface ${row.surfaceId} missing supportingFactIds`);
  }

  process.stdout.write(JSON.stringify({
    portfolioInputs: report.summary?.inputCount ?? (report.inputs ?? []).length,
    portfolioSources: report.summary?.sourceCount ?? (report.sources ?? []).length,
    dependencySurfaces: report.summary?.surfaceCount ?? (report.dependencySurfaces?.rows ?? []).length,
    dependencyEdges: report.summary?.edgeCount ?? (report.dependencyEdges?.rows ?? []).length,
    endpointFindings: report.summary?.endpointFindingCount ?? (report.endpointAlignment?.rows ?? []).length,
    sharedSurfaces: report.summary?.sharedSurfaceCount ?? (report.sharedSurfaces?.rows ?? []).length,
    gaps: report.summary?.gapCount ?? (report.gaps ?? []).length
  }));
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
    "| Section | Status | Evidence Tier | Rule IDs | Coverage | Counts | Reason |",
    "| --- | --- | --- | --- | --- | --- | --- |",
    ...sections.map(section => {
      const counts = Object.entries(section.counts ?? {})
        .sort(([left], [right]) => left.localeCompare(right))
        .map(([key, value]) => `${key}=${value}`)
        .join(", ");
      const ruleIds = Array.isArray(section.ruleIds) ? section.ruleIds.join(", ") : "";
      return `| ${escapeCell(section.name)} | \`${escapeCell(section.status)}\` | \`${escapeCell(section.evidenceTier || "")}\` | ${escapeCell(ruleIds || "n/a")} | \`${escapeCell(section.reportCoverage)}\` | ${escapeCell(counts || "n/a")} | ${escapeCell(section.reason || "")} |`;
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
    ...localAbsolutePathChecks(root),
    ["windows-absolute-path", /[A-Za-z]:\\(?:Users|home|workspace|workspaces|tmp|temp|repo|agent|a)\\/i],
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

function localAbsolutePathChecks(root) {
  const exactRoots = new Set([
    path.resolve(root),
    process.cwd(),
    os.tmpdir()
  ].filter(Boolean));
  const checks = [];
  for (const exactRoot of [...exactRoots].sort()) {
    checks.push(["local-absolute-path", new RegExp(`${escapeRegExp(exactRoot)}(?:[/\\\\]|$)`)]);
  }

  checks.push([
    "local-absolute-path",
    /(?<![:/])\/(?:Users|home|private\/tmp|tmp|var\/folders|workspace|workspaces|github\/workspace|runner\/work|__w|mnt|opt\/hostedtoolcache|builds)\/[^\s`"'<>|)]+/i
  ]);
  return checks;
}

function escapeRegExp(value) {
  return String(value).replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
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
  const summary = readJson(summaryJson) ?? {};
  assert(summary.version === "1.0", "demo-summary.json version must be 1.0.");
  assert(/^path-hash:[0-9a-f]{24}$/i.test(summary.outputRootHash ?? ""), "demo-summary.json must include outputRootHash and not an absolute root.");
  assert(Array.isArray(summary.sections), "demo-summary.json sections must be an array.");
  assert(summary.sections.length > 0, "demo-summary.json must include at least one section.");
  for (const section of summary.sections) {
    assert(section.name, "summary section is missing name.");
    assert(section.status, `summary section ${section.name} is missing status.`);
    assert(section.evidenceTier, `summary section ${section.name} is missing evidenceTier.`);
    assert(Array.isArray(section.ruleIds) && section.ruleIds.length > 0, `summary section ${section.name} is missing ruleIds.`);
    if (["deferred", "unavailable", "failed"].includes(section.status)) {
      assert(section.reason, `summary section ${section.name} must include a reason.`);
    }
  }
}

function selfTest() {
  const tempRoot = fs.mkdtempSync(path.join(os.tmpdir(), "tracemap-demo-assert-"));
  try {
    fs.mkdirSync(path.join(tempRoot, "reports", "sample"), { recursive: true });
    fs.writeFileSync(path.join(tempRoot, "demo-summary.md"), "safe summary /api/public\n", "utf8");
    fs.writeFileSync(path.join(tempRoot, "demo-summary.json"), "{\"version\":\"1.0\"}\n", "utf8");
    assert(findSentinelFailures(tempRoot).length === 0, "clean sentinel fixture should pass.");

    const plantedHomePath = `${String.fromCharCode(47)}Users/example/private`;
    fs.writeFileSync(path.join(tempRoot, "reports", "sample", "report.md"), `leak ${plantedHomePath}\n`, "utf8");
    let failures = findSentinelFailures(tempRoot);
    assert(failures.some(failure => failure.includes("local-absolute-path")), "sentinel fixture should catch home-path leak.");

    fs.writeFileSync(path.join(tempRoot, "reports", "sample", "report.md"), "leak /workspace/tracemap/out/demo-summary.md\n", "utf8");
    failures = findSentinelFailures(tempRoot);
    assert(failures.some(failure => failure.includes("local-absolute-path")), "sentinel fixture should catch workspace-path leak.");

    fs.writeFileSync(path.join(tempRoot, "reports", "sample", "report.md"), "leak /tmp/tmp.public-demo/report.json\n", "utf8");
    failures = findSentinelFailures(tempRoot);
    assert(failures.some(failure => failure.includes("local-absolute-path")), "sentinel fixture should catch temp-path leak.");

    const sectionsJsonl = path.join(tempRoot, "sections.jsonl");
    const appendSectionResult = spawnSync(process.execPath, [
      process.argv[1],
      "append-section",
      sectionsJsonl,
      "sample-scans",
      "available",
      "ActionableStaticEvidence",
      "FullEvidenceAvailable",
      "",
      "reports/sample/report.md",
      "{\"scannedSources\":1}"
    ], { encoding: "utf8" });
    assert(appendSectionResult.status === 0, `append-section should pass: ${appendSectionResult.stderr}`);
    const sectionRow = JSON.parse(fs.readFileSync(sectionsJsonl, "utf8").trim());
    assert(sectionRow.evidenceTier === "Tier2Structural", "append-section should include evidenceTier.");
    assert((sectionRow.ruleIds ?? []).includes("public.demo.summary.v1"), "append-section should include ruleIds.");

    const missingScan = path.join(tempRoot, "missing-scan");
    fs.mkdirSync(missingScan);
    const missingScanResult = spawnSync(process.execPath, [process.argv[1], "scan-artifacts", "missing", missingScan], { encoding: "utf8" });
    assert(missingScanResult.status !== 0, "scan-artifacts should fail when required artifacts are missing.");

    const invalidSummary = path.join(tempRoot, "invalid-summary.json");
    fs.writeFileSync(invalidSummary, "{\"version\":\"1.0\",\"sections\":[]}\n", "utf8");
    const invalidSummaryResult = spawnSync(process.execPath, [process.argv[1], "validate-summary", invalidSummary], { encoding: "utf8" });
    assert(invalidSummaryResult.status !== 0, "validate-summary should fail when outputRootHash is missing.");

    const dependencyDir = path.join(tempRoot, "reports", "dependency");
    fs.mkdirSync(dependencyDir, { recursive: true });
    fs.writeFileSync(path.join(dependencyDir, "dependency-report.md"), "safe dependency report\n", "utf8");
    fs.writeFileSync(path.join(dependencyDir, "dependency-report.json"), JSON.stringify({
      sources: [
        { label: "public-dotnet-server", sourceIndexId: "src-server", commitSha: "abcdef1" },
        { label: "public-ts-client", sourceIndexId: "src-client", commitSha: "abcdef2" }
      ],
      summary: { sourceCount: 2, endpointFindingCount: 1, dependencyEdgeCount: 1 },
      endpointFindings: [{
        classification: "MatchedEndpoint",
        normalizedPathKey: "/api/demo/{}",
        clientSourceLabel: "public-ts-client",
        serverSourceLabel: "public-dotnet-server",
        clientRuleId: "ts.http.client.v1",
        clientEvidenceTier: "Tier1Semantic",
        serverRuleId: "aspnet.route.v1",
        serverEvidenceTier: "Tier1Semantic",
        clientCommitSha: "abcdef2",
        serverCommitSha: "abcdef1"
      }],
      dependencySurfaces: [{
        displayName: "shape:demo",
        ruleId: "sql.query.shape.v1",
        evidenceTier: "Tier2Structural",
        commitSha: "abcdef1"
      }],
      dependencyEdges: [{ edgeId: "edge:1", ruleId: "call.edge.v1", evidenceTier: "Tier1Semantic" }],
      knownGaps: []
    }), "utf8");
    const dependencyResult = spawnSync(process.execPath, [
      process.argv[1],
      "dependency-report",
      dependencyDir,
      "public-dotnet-server,public-ts-client",
      "/api/demo/{}"
    ], { encoding: "utf8" });
    assert(dependencyResult.status === 0, `dependency-report should pass: ${dependencyResult.stderr}`);
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
  case "dependency-report":
    dependencyReport();
    break;
  case "paths-report":
    pathsReport();
    break;
  case "reverse-report":
    reverseReport();
    break;
  case "portfolio-manifest":
    portfolioManifest();
    break;
  case "portfolio-report":
    portfolioReport();
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
