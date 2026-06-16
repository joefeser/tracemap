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

function scrubPublicJsonValue(value) {
  if (Array.isArray(value)) {
    for (const item of value) {
      scrubPublicJsonValue(item);
    }
    return value;
  }

  if (!value || typeof value !== "object") {
    return value;
  }

  for (const key of Object.keys(value)) {
    if (/^(?:remoteUrl|repositoryRemote|repoRemote|gitRemote)$/i.test(key)) {
      delete value[key];
      continue;
    }

    scrubPublicJsonValue(value[key]);
  }

  return value;
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

function assertSnapshotSources(snapshot, expectedLabelsCsv, label) {
  assert(snapshot, `${label} snapshot is missing.`);
  assertExactLabels((snapshot.sources ?? []).map(source => source.sourceLabel), expectedLabelsCsv, `${label} snapshot sources`);
  for (const source of snapshot.sources ?? []) {
    assertShaLike(source.commitSha, `${label} source ${source.sourceLabel} commitSha`);
    assertNonEmptyString(source.coverage, `${label} source ${source.sourceLabel} coverage`);
  }
}

function assertDiffEvidence(evidence, label) {
  assert(evidence, `${label} evidence is missing.`);
  assertNonEmptyString(evidence.sourceLabel, `${label} sourceLabel`);
  assertShaLike(evidence.commitSha, `${label} commitSha`);
  assertNonEmptyString(evidence.ruleId, `${label} ruleId`);
  assertNonEmptyString(evidence.evidenceTier, `${label} evidenceTier`);
  assertNonEmptyString(evidence.evidenceKind, `${label} evidenceKind`);
}

function assertGapShape(gap, label) {
  assertNonEmptyString(gap.ruleId, `${label} ruleId`);
  assertNonEmptyString(gap.evidenceTier, `${label} evidenceTier`);
  assertNonEmptyString(gap.classification, `${label} classification`);
}

function diffReport() {
  const [reportDir, expectedLabelsCsv] = args;
  assert(reportDir && expectedLabelsCsv, "diff-report requires report dir and expected labels.");
  const { json } = publicReportFiles(reportDir, "diff-report.md", "diff-report.json");
  const report = readJson(json) ?? {};
  assert(report.reportType === "combined-dependency-diff", `Unexpected diff report type: ${report.reportType}`);
  assertNoVolatileKeys(report, "diff-report.json");
  assertSnapshotSources(report.beforeSnapshot, expectedLabelsCsv, "diff before");
  assertSnapshotSources(report.afterSnapshot, expectedLabelsCsv, "diff after");

  const endpointDiffs = report.endpointDiffs ?? [];
  const surfaceDiffs = report.surfaceDiffs ?? [];
  const edgeDiffs = report.edgeDiffs ?? [];
  const gaps = report.gaps ?? [];
  assert((report.summary?.surfaceDiffCount ?? surfaceDiffs.length) > 0, "Expected public demo surface diff evidence.");

  const allRows = [...endpointDiffs, ...surfaceDiffs, ...edgeDiffs];
  for (const row of allRows) {
    assertNonEmptyString(row.diffId, "diff row diffId");
    assertNonEmptyString(row.changeType, `diff row ${row.diffId} changeType`);
    assert(["Added", "Removed", "Changed"].includes(row.changeType), `Unexpected diff changeType ${row.changeType}.`);
    assertNonEmptyString(row.classification, `diff row ${row.diffId} classification`);
    assertNonEmptyString(row.diffRuleId, `diff row ${row.diffId} diffRuleId`);
    assert(row.before || row.after, `diff row ${row.diffId} must include before or after evidence.`);
    if (row.before) {
      assertDiffEvidence(row.before, `diff row ${row.diffId} before`);
    }
    if (row.after) {
      assertDiffEvidence(row.after, `diff row ${row.diffId} after`);
    }
    const supportingFactIds = [
      ...(row.before?.supportingFactIds ?? []),
      ...(row.after?.supportingFactIds ?? [])
    ];
    const supportingEdgeIds = [
      ...(row.before?.supportingEdgeIds ?? []),
      ...(row.after?.supportingEdgeIds ?? [])
    ];
    if ((row.before?.evidenceKind ?? row.after?.evidenceKind) !== "source") {
      assert(supportingFactIds.length > 0 || supportingEdgeIds.length > 0,
        `diff row ${row.diffId} is missing supporting fact or edge IDs.`);
    }
  }

  assert(surfaceDiffs.some(row =>
    (row.after?.displayName ?? "").includes("/api/public/orders/{}/cancel")
      && (row.after?.safeMetadata ?? []).some(pair => pair.key === "surfaceKind" && pair.value === "http-route")),
    "Expected added public demo cancel route surface diff.");
  assert(surfaceDiffs.some(row => (row.after?.safeMetadata ?? row.before?.safeMetadata ?? [])
    .some(pair => pair.key === "surfaceKind" && pair.value === "sql-query")),
    "Expected public demo SQL surface diff.");

  for (const gap of gaps) {
    assertGapShape(gap, `diff gap ${gap.gapId}`);
  }

  process.stdout.write(JSON.stringify({
    diffRows: (report.summary?.endpointDiffCount ?? endpointDiffs.length)
      + (report.summary?.surfaceDiffCount ?? surfaceDiffs.length)
      + (report.summary?.edgeDiffCount ?? edgeDiffs.length)
      + (report.summary?.sourceDiffCount ?? 0)
      + (report.summary?.coverageDiffCount ?? 0)
      + (report.summary?.pathDiffCount ?? 0),
    endpointDiffs: report.summary?.endpointDiffCount ?? endpointDiffs.length,
    surfaceDiffs: report.summary?.surfaceDiffCount ?? surfaceDiffs.length,
    edgeDiffs: report.summary?.edgeDiffCount ?? edgeDiffs.length,
    gaps: report.summary?.gapCount ?? gaps.length
  }));
}

function impactReport() {
  const [reportDir, expectedLabelsCsv] = args;
  assert(reportDir && expectedLabelsCsv, "impact-report requires report dir and expected labels.");
  const { json } = publicReportFiles(reportDir, "impact-report.md", "impact-report.json");
  const report = readJson(json) ?? {};
  assert(report.reportType === "combined-change-impact", `Unexpected impact report type: ${report.reportType}`);
  assertNoVolatileKeys(report, "impact-report.json");
  assertSnapshotSources(report.beforeSnapshot, expectedLabelsCsv, "impact before");
  assertSnapshotSources(report.afterSnapshot, expectedLabelsCsv, "impact after");

  const items = report.impactItems ?? [];
  const gaps = report.gaps ?? [];
  assert((report.summary?.impactItemCount ?? items.length) > 0, "Expected public demo impact items.");
  assert(items.some(item => item.evidenceKind === "surface"), "Expected surface impact evidence.");

  for (const item of items) {
    assertNonEmptyString(item.impactId, "impact item impactId");
    assertNonEmptyString(item.changeType, `impact item ${item.impactId} changeType`);
    assertNonEmptyString(item.classification, `impact item ${item.impactId} classification`);
    assertNonEmptyString(item.diffRuleId, `impact item ${item.impactId} diffRuleId`);
    assertNonEmptyString(item.impactRuleId, `impact item ${item.impactId} impactRuleId`);
    assertNonEmptyString(item.evidenceTier, `impact item ${item.impactId} evidenceTier`);
    assertNonEmptyString(item.sourceLabel, `impact item ${item.impactId} sourceLabel`);
    assertShaLike((item.after ?? item.before)?.commitSha, `impact item ${item.impactId} commitSha`);
    if (item.evidenceKind !== "source" && item.evidenceKind !== "coverage") {
      assert((item.supportingFactIds ?? []).length > 0 || (item.supportingEdgeIds ?? []).length > 0,
        `impact item ${item.impactId} is missing supporting fact or edge IDs.`);
    }
    assert(item.classification !== "StaticImpactEvidence",
      `impact item ${item.impactId} overclaims static evidence as runtime impact.`);
  }

  for (const gap of gaps) {
    assertGapShape(gap, `impact gap ${gap.gapId}`);
  }

  process.stdout.write(JSON.stringify({
    diffRows: report.summary?.diffCount ?? 0,
    impactItems: report.summary?.impactItemCount ?? items.length,
    endpointImpacts: report.summary?.endpointImpactCount ?? items.filter(item => item.evidenceKind === "endpoint").length,
    surfaceImpacts: report.summary?.surfaceImpactCount ?? items.filter(item => item.evidenceKind === "surface").length,
    edgeImpacts: report.summary?.edgeImpactCount ?? items.filter(item => item.evidenceKind === "edge").length,
    gaps: report.summary?.gapCount ?? gaps.length
  }));
}

function releaseReview() {
  const [reportDir, expectedLabelsCsv] = args;
  assert(reportDir && expectedLabelsCsv, "release-review requires report dir and expected labels.");
  const { json } = publicReportFiles(reportDir, "release-review.md", "release-review.json");
  const report = readJson(json) ?? {};
  assert(report.reportType === "release-review", `Unexpected release-review report type: ${report.reportType}`);
  assert(report.mode === "ReleaseReviewCombinedV1", `Unexpected release-review mode: ${report.mode}`);
  assertNoVolatileKeys(report, "release-review.json");
  assertSnapshotSources(report.beforeSnapshot, expectedLabelsCsv, "release-review before");
  assertSnapshotSources(report.afterSnapshot, expectedLabelsCsv, "release-review after");
  assertNonEmptyString(report.summary?.ruleId, "release-review summary ruleId");
  assert((report.summary?.topChangedSurfaceCount ?? 0) > 0, "Expected release-review top changed surface findings.");
  assert(["ActionableStaticEvidence", "ReviewRecommended", "PartialAnalysis"].includes(report.summary?.rollupClassification),
    `Unexpected release-review rollup ${report.summary?.rollupClassification}.`);

  for (const row of report.sourceCoverage ?? []) {
    assertNonEmptyString(row.ruleId, `release-review source coverage ${row.sourceLabel} ruleId`);
    assertNonEmptyString(row.evidenceTier, `release-review source coverage ${row.sourceLabel} evidenceTier`);
    assertShaLike(row.beforeCommitSha, `release-review source coverage ${row.sourceLabel} beforeCommitSha`);
    assertShaLike(row.afterCommitSha, `release-review source coverage ${row.sourceLabel} afterCommitSha`);
  }

  const topChanged = report.topChangedSurfaces ?? {};
  assert(["available", "truncated"].includes(topChanged.status),
    `Unexpected topChangedSurfaces status ${topChanged.status}.`);
  const findings = topChanged.findings ?? [];
  assert(findings.length > 0, "Expected release-review findings.");
  assert(findings.some(finding => finding.section === "topChangedSurfaces" && finding.sourceLabel === "public-demo-api"),
    "Expected release-review public-demo-api finding.");
  for (const finding of findings) {
    assertNonEmptyString(finding.ruleId, `release-review finding ${finding.findingId} ruleId`);
    assertNonEmptyString(finding.evidenceTier, `release-review finding ${finding.findingId} evidenceTier`);
    assertShaLike(finding.commitSha, `release-review finding ${finding.findingId} commitSha`);
    if ((finding.metadata ?? []).some(pair => pair.key === "evidenceKind" && pair.value !== "source" && pair.value !== "coverage")) {
      assert((finding.supportingFactIds ?? []).length > 0 || (finding.supportingEdgeIds ?? []).length > 0,
        `release-review finding ${finding.findingId} is missing supporting fact or edge IDs.`);
    }
  }

  assert((report.contractImpact?.status ?? "") === "not_requested",
    "release-review contractImpact should stay not_requested without a contract delta.");
  for (const gap of report.gaps ?? []) {
    assertGapShape(gap, `release-review gap ${gap.gapId}`);
  }
  for (const item of report.reviewerChecklist ?? []) {
    assertNonEmptyString(item.ruleId, `release-review checklist ${item.checklistId} ruleId`);
  }

  process.stdout.write(JSON.stringify({
    findings: (report.topChangedSurfaces?.findings ?? []).length
      + (report.contractImpact?.findings ?? []).length
      + (report.apiDtoChanges?.findings ?? []).length
      + (report.sqlSchemaImpact?.findings ?? []).length
      + (report.packageImpact?.findings ?? []).length
      + (report.pathContext?.findings ?? []).length
      + (report.reverseContext?.findings ?? []).length,
    topChangedSurfaces: report.summary?.topChangedSurfaceCount ?? (report.topChangedSurfaces?.findings ?? []).length,
    contractFindings: report.summary?.contractFindingCount ?? 0,
    gaps: report.summary?.gapCount ?? (report.gaps ?? []).length,
    checklistItems: (report.reviewerChecklist ?? []).length
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
    ["raw-repository-remote", /\b(?:git@|https?:\/\/[^/\s]+\/[^/\s]+\/[^/\s]+\.git\b)/i],
    ["git-path", /(?:^|[\/\\])\.git(?:[\/\\]|$)/i],
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
    const isPortfolioManifest = relative === "portfolio-manifest.json";
    if (isSummary || isReport || isPortfolioManifest || relative === "demo-summary.md" || relative === "demo-summary.json") {
      files.push(fullPath);
    }
  }
}

function scrubPublicReportJson() {
  const [outRoot] = args;
  assert(outRoot, "scrub-public-report-json requires output root.");
  const root = path.resolve(outRoot);
  const files = [];
  collectPublicFiles(root, files);

  for (const file of files) {
    const relative = path.relative(root, file).split(path.sep).join("/");
    if (!relative.startsWith("reports/") || !relative.endsWith(".json")) {
      continue;
    }

    const json = readJson(file);
    scrubPublicJsonValue(json);
    fs.writeFileSync(file, `${JSON.stringify(json, null, 2)}\n`, "utf8");
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
    fs.writeFileSync(path.join(tempRoot, "portfolio-manifest.json"), "{\"inputs\":[{\"indexPath\":\"combined/endpoint-stack.sqlite\"}]}\n", "utf8");
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

    fs.writeFileSync(path.join(tempRoot, "reports", "sample", "report.md"), "safe report\n", "utf8");
    fs.writeFileSync(path.join(tempRoot, "portfolio-manifest.json"), `{"inputs":[{"indexPath":"${plantedHomePath}"}]}\n`, "utf8");
    failures = findSentinelFailures(tempRoot);
    assert(failures.some(failure => failure.includes("portfolio-manifest.json") && failure.includes("local-absolute-path")),
      "sentinel fixture should catch portfolio manifest path leak.");
    fs.writeFileSync(path.join(tempRoot, "portfolio-manifest.json"), "{\"inputs\":[{\"indexPath\":\"combined/endpoint-stack.sqlite\"}]}\n", "utf8");

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

    const diffDir = path.join(tempRoot, "reports", "diff");
    fs.mkdirSync(diffDir, { recursive: true });
    fs.writeFileSync(path.join(diffDir, "diff-report.md"), "safe diff report\n", "utf8");
    fs.writeFileSync(path.join(diffDir, "diff-report.json"), JSON.stringify({
      reportType: "combined-dependency-diff",
      beforeSnapshot: { sources: [{ sourceLabel: "public-demo-api", commitSha: "abcdef1", coverage: "Level1SemanticAnalysis/Succeeded" }] },
      afterSnapshot: { sources: [{ sourceLabel: "public-demo-api", commitSha: "abcdef2", coverage: "Level1SemanticAnalysis/Succeeded" }] },
      summary: { endpointDiffCount: 1, surfaceDiffCount: 2, edgeDiffCount: 0, sourceDiffCount: 0, coverageDiffCount: 0, pathDiffCount: 0, gapCount: 0 },
      endpointDiffs: [{
        diffId: "diff:endpoint",
        changeType: "Added",
        classification: "Added",
        diffRuleId: "combined.diff.endpoint.v1",
        after: {
          sourceLabel: "public-demo-api",
          commitSha: "abcdef2",
          ruleId: "csharp.syntax.aspnet.route.v1",
          evidenceTier: "Tier3SyntaxOrTextual",
          evidenceKind: "endpoint",
          displayName: "POST /api/public/orders/{}/cancel",
          supportingFactIds: ["fact:1"]
        }
      }],
      surfaceDiffs: [{
        diffId: "diff:route-surface",
        changeType: "Added",
        classification: "Added",
        diffRuleId: "combined.diff.surface.v1",
        after: {
          sourceLabel: "public-demo-api",
          commitSha: "abcdef2",
          ruleId: "csharp.syntax.aspnetroute.v1",
          evidenceTier: "Tier3SyntaxOrTextual",
          evidenceKind: "surface",
          displayName: "/api/public/orders/{}/cancel",
          safeMetadata: [{ key: "surfaceKind", value: "http-route" }],
          supportingFactIds: ["fact:3"]
        }
      }, {
        diffId: "diff:surface",
        changeType: "Added",
        classification: "NeedsReviewDiff",
        diffRuleId: "combined.diff.surface.v1",
        after: {
          sourceLabel: "public-demo-api",
          commitSha: "abcdef2",
          ruleId: "csharp.syntax.query-pattern.v1",
          evidenceTier: "Tier3SyntaxOrTextual",
          evidenceKind: "surface",
          displayName: "sql-query",
          safeMetadata: [{ key: "surfaceKind", value: "sql-query" }],
          supportingFactIds: ["fact:2"]
        }
      }],
      edgeDiffs: [],
      gaps: []
    }), "utf8");
    const diffResult = spawnSync(process.execPath, [process.argv[1], "diff-report", diffDir, "public-demo-api"], { encoding: "utf8" });
    assert(diffResult.status === 0, `diff-report should pass: ${diffResult.stderr}`);

    const impactDir = path.join(tempRoot, "reports", "impact");
    fs.mkdirSync(impactDir, { recursive: true });
    fs.writeFileSync(path.join(impactDir, "impact-report.md"), "safe impact report\n", "utf8");
    fs.writeFileSync(path.join(impactDir, "impact-report.json"), JSON.stringify({
      reportType: "combined-change-impact",
      beforeSnapshot: { sources: [{ sourceLabel: "public-demo-api", commitSha: "abcdef1", coverage: "Level1SemanticAnalysis/Succeeded" }] },
      afterSnapshot: { sources: [{ sourceLabel: "public-demo-api", commitSha: "abcdef2", coverage: "Level1SemanticAnalysis/Succeeded" }] },
      summary: { diffCount: 1, impactItemCount: 1, endpointImpactCount: 0, surfaceImpactCount: 1, edgeImpactCount: 0, gapCount: 0 },
      impactItems: [{
        impactId: "impact:surface",
        changeType: "Added",
        classification: "ProbableStaticImpact",
        evidenceKind: "surface",
        sourceLabel: "public-demo-api",
        diffRuleId: "combined.diff.surface.v1",
        impactRuleId: "combined.impact.surface.v1",
        evidenceTier: "Tier3SyntaxOrTextual",
        supportingFactIds: ["fact:2"],
        supportingEdgeIds: [],
        after: {
          sourceLabel: "public-demo-api",
          commitSha: "abcdef2",
          ruleId: "csharp.syntax.query-pattern.v1",
          evidenceTier: "Tier3SyntaxOrTextual",
          evidenceKind: "surface",
          supportingFactIds: ["fact:2"]
        }
      }],
      gaps: []
    }), "utf8");
    const impactResult = spawnSync(process.execPath, [process.argv[1], "impact-report", impactDir, "public-demo-api"], { encoding: "utf8" });
    assert(impactResult.status === 0, `impact-report should pass: ${impactResult.stderr}`);

    const releaseDir = path.join(tempRoot, "reports", "release-review");
    fs.mkdirSync(releaseDir, { recursive: true });
    fs.writeFileSync(path.join(releaseDir, "release-review.md"), "safe release review report\n", "utf8");
    fs.writeFileSync(path.join(releaseDir, "release-review.json"), JSON.stringify({
      reportType: "release-review",
      mode: "ReleaseReviewCombinedV1",
      beforeSnapshot: { sources: [{ sourceLabel: "public-demo-api", commitSha: "abcdef1", coverage: "Level1SemanticAnalysis/Succeeded" }] },
      afterSnapshot: { sources: [{ sourceLabel: "public-demo-api", commitSha: "abcdef2", coverage: "Level1SemanticAnalysis/Succeeded" }] },
      summary: {
        ruleId: "release.review.rollup.v1",
        rollupClassification: "ActionableStaticEvidence",
        topChangedSurfaceCount: 1,
        contractFindingCount: 0,
        gapCount: 0
      },
      sourceCoverage: [{
        sourceLabel: "public-demo-api",
        beforeCommitSha: "abcdef1",
        afterCommitSha: "abcdef2",
        ruleId: "release.review.source.v1",
        evidenceTier: "Tier2Structural"
      }],
      topChangedSurfaces: {
        status: "available",
        findings: [{
          findingId: "finding:surface",
          section: "topChangedSurfaces",
          sourceLabel: "public-demo-api",
          classification: "ProbableStaticImpact",
          ruleId: "combined.impact.surface.v1",
          evidenceTier: "Tier3SyntaxOrTextual",
          commitSha: "abcdef2",
          metadata: [{ key: "evidenceKind", value: "surface" }],
          supportingFactIds: ["fact:2"],
          supportingEdgeIds: []
        }]
      },
      contractImpact: { status: "not_requested", findings: [] },
      apiDtoChanges: { status: "not_requested", findings: [] },
      sqlSchemaImpact: { status: "not_requested", findings: [] },
      packageImpact: { status: "not_requested", findings: [] },
      pathContext: { status: "not_requested", findings: [] },
      reverseContext: { status: "not_requested", findings: [] },
      gaps: [],
      reviewerChecklist: [{ checklistId: "checklist:surface", ruleId: "release.review.checklist.v1" }]
    }), "utf8");
    const releaseResult = spawnSync(process.execPath, [process.argv[1], "release-review", releaseDir, "public-demo-api"], { encoding: "utf8" });
    assert(releaseResult.status === 0, `release-review should pass: ${releaseResult.stderr}`);
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
  case "diff-report":
    diffReport();
    break;
  case "impact-report":
    impactReport();
    break;
  case "release-review":
    releaseReview();
    break;
  case "write-summary":
    writeSummary();
    break;
  case "scrub-public-report-json":
    scrubPublicReportJson();
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
