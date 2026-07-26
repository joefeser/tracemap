import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeRenderedText,
  readSitemapLocSet,
  stripTagsQuoteAware
} from "./validate-utils.mjs";

export const databaseDesignReviewRoute = "/database/design-review/";
export const databaseDesignReviewProofRoute = "/database/design-review/proof-packet/";
export const databaseDesignReviewAsset = "/assets/database-design-review-proof-packet.json";
export const databaseDesignReviewStoryInboundRoutes = [
  "/manager-packet/",
  "/proof-paths/for-managers/",
  "/capabilities/",
  "/outputs/"
];
export const databaseDesignReviewProofInboundRoutes = [
  ...databaseDesignReviewStoryInboundRoutes,
  "/examples/",
  "/packets/examples/"
];

const storyText = [
  "Public claim level: demo",
  "database-design-review/1.0",
  "What database design, mappings, operations, and query relationships are visible in this repository?",
  "single index.sqlite",
  "combined combined.sqlite",
  "SingleIndexRoutePathUnavailable",
  "database.design-review.packet.v1",
  "database.design-review.gap.v1",
  "PostgreSQL",
  "EF mappings",
  "application database-operation candidates",
  "query surfaces",
  "runtime reachability",
  "DBA approval",
  "safe to run"
];

const proofText = [
  "Public claim level: demo",
  "database-design-review/1.0",
  "SingleIndexRoutePathUnavailable",
  "SourceCoverageReduced",
  "database.postgres.schema-migration.v1",
  "database.ef.v1",
  "database.operation.call-pattern.v1",
  "database.sql.shape.v1",
  "Tier1Semantic",
  "Tier2Structural",
  "Tier4Unknown",
  "supporting fact and edge IDs",
  "runtime reachability",
  "DBA approval",
  "safe to run"
];

const packetFields = new Set([
  "version", "ruleId", "claimLevel", "coverage", "sources", "summary", "tables",
  "globalObjects", "unlinkedQueries", "gaps", "limitations"
]);
const summaryFields = new Set([
  "sourceCount", "tableCount", "declarationCount", "operationCount", "queryReferenceCount",
  "routeReferenceCount", "globalObjectCount", "unlinkedQueryCount", "gapCount",
  "omittedObjectCount", "omittedEvidenceCount", "omittedRouteReferenceCount", "omittedGapCount"
]);
const evidenceFields = new Set([
  "ruleId", "evidenceTier", "sourceLabel", "commitSha", "filePath", "startLine", "endLine",
  "extractorId", "extractorVersion", "coverageLabel", "supportingFactIds", "supportingEdgeIds",
  "supportingRuleIds", "limitations"
]);
const metadataKeys = new Set([
  "operationKind", "configurationKind", "matchKind", "frameworkFamily", "objectKind",
  "routeReferenceCount", "coverageReason"
]);
const evidenceTiers = new Set(["Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"]);
const forbiddenKeys = new Set([
  "rawSql", "sql", "queryText", "commandText", "sourceSnippet", "snippetHash", "queryHash",
  "connectionString", "password", "credential", "scheduledCommandBody", "localPath",
  "serverName", "properties", "sqlite"
].map((key) => key.toLowerCase()));
const compatibleRuleTiers = new Map([
  ["database.postgres.schema-migration.v1", new Set(["Tier2Structural"])],
  ["database.ef.v1", new Set(["Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"])],
  ["database.operation.call-pattern.v1", new Set(["Tier1Semantic", "Tier4Unknown"])],
  ["database.sql.shape.v1", new Set(["Tier2Structural", "Tier3SyntaxOrTextual"])],
  ["database.design-review.packet.v1", evidenceTiers],
  ["database.design-review.gap.v1", new Set(["Tier4Unknown"])]
]);
const forbiddenPatterns = [
  /(?:\/Users\/|\/home\/|[A-Z]:\\Users\\)/i,
  /\b(?:Server|Password|User Id)\s*=/i,
  /\b(?:SELECT\s+.+?\s+FROM|INSERT\s+INTO|UPDATE\s+\w+\s+SET|DELETE\s+FROM|CREATE\s+TABLE)\b/i,
  /\b(?:private-host|private-server|private-repository|credential-leak|command-output-leak)\b/i
];

export async function validateDatabaseDesignReviewShowcaseDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const storyPath = resolve(dist, "database", "design-review", "index.html");
  const proofPath = resolve(dist, "database", "design-review", "proof-packet", "index.html");
  const assetPath = resolve(dist, "assets", "database-design-review-proof-packet.json");
  for (const [path, label] of [[storyPath, databaseDesignReviewRoute], [proofPath, databaseDesignReviewProofRoute], [assetPath, databaseDesignReviewAsset]]) {
    if (!(await fileExists(path))) errors.push(`Database design review showcase is missing: ${label}`);
  }
  if (errors.some((error) => error.startsWith("Database design review showcase is missing:"))) return;

  const storyHtml = await readFile(storyPath, "utf8");
  const proofHtml = await readFile(proofPath, "utf8");
  const assetText = await readFile(assetPath, "utf8");
  validatePage(storyHtml, storyText, databaseDesignReviewRoute, "story", errors);
  validatePage(proofHtml, proofText, databaseDesignReviewProofRoute, "proof packet", errors);

  for (const pattern of forbiddenPatterns) {
    const publicText = [storyHtml, proofHtml].map((html) => decodeHtmlEntities(stripTagsQuoteAware(html))).join(" ");
    if (pattern.test(`${publicText} ${assetText}`)) {
      errors.push(`Database design review showcase contains forbidden private or executable material: ${pattern}`);
    }
  }

  let projection;
  try {
    projection = JSON.parse(assetText);
  } catch (error) {
    errors.push(`Database design review proof packet asset is not valid JSON: ${error.message}`);
  }
  if (projection) validateProjection(projection, errors);

  await validateInboundLinks(dist, databaseDesignReviewStoryInboundRoutes, databaseDesignReviewRoute, "story", errors);
  await validateInboundLinks(dist, databaseDesignReviewProofInboundRoutes, databaseDesignReviewProofRoute, "proof packet", errors);

  const sitemapPath = resolve(dist, "sitemap.xml");
  if (await fileExists(sitemapPath)) {
    const urls = await readSitemapLocSet(sitemapPath);
    for (const route of [databaseDesignReviewRoute, databaseDesignReviewProofRoute]) {
      if (!urls.has(`${baseUrl}${route}`)) errors.push(`Sitemap is missing database design review URL: ${baseUrl}${route}`);
    }
  }

  const routesIndexPath = resolve(dist, "routes-index.json");
  if (await fileExists(routesIndexPath)) {
    try {
      const index = JSON.parse(await readFile(routesIndexPath, "utf8"));
      for (const route of [databaseDesignReviewRoute, databaseDesignReviewProofRoute]) {
        const entry = index?.entries?.find((candidate) => candidate?.path === route);
        if (!entry) errors.push(`routes-index.json is missing ${route}`);
        else if (entry.publicClaimLevel !== "demo" || entry.sourceType !== "site-page") {
          errors.push(`Database design review discovery metadata must remain demo-level site-page evidence: ${route}`);
        }
      }
    } catch (error) {
      errors.push(`Database design review could not parse routes-index.json: ${error.message}`);
    }
  }
}

function validatePage(html, phrases, route, label, errors) {
  const text = normalizeRenderedText(html);
  const decoded = decodeHtmlEntities(html);
  for (const phrase of phrases) {
    if (!text.includes(phrase) && !decoded.includes(phrase)) {
      errors.push(`Database design review ${label} is missing required text: ${phrase}`);
    }
  }
  if (!/<meta\b(?=[^>]*\bproperty\s*=\s*["']og:type["'])(?=[^>]*\bcontent\s*=\s*["']article["'])[^>]*>/i.test(html)) {
    errors.push(`Database design review ${label} must use article social metadata.`);
  }
  const canonical = new RegExp(`<link\\b(?=[^>]*\\brel\\s*=\\s*["']canonical["'])(?=[^>]*\\bhref\\s*=\\s*["']https:\\/\\/tracemap\\.tools${escapeRegExp(route)}["'])[^>]*>`, "i");
  if (!canonical.test(html)) errors.push(`Database design review ${label} canonical URL is missing or incorrect.`);
}

function validateProjection(projection, errors) {
  const topFields = new Set(["schemaVersion", "derivedFromContract", "publicClaimLevel", "purpose", "source", "modes", "limitations"]);
  if (!requireExactFields(projection, topFields, "public projection", errors)) return;
  if (projection.schemaVersion !== "tracemap-public-database-design-review-proof-packet/v1") {
    errors.push("Database design review projection schemaVersion is incorrect.");
  }
  if (projection.derivedFromContract !== "database-design-review/1.0") {
    errors.push("Database design review projection must identify database-design-review/1.0.");
  }
  if (projection.publicClaimLevel !== "demo") errors.push("Database design review publicClaimLevel must remain demo.");
  requireExactFields(
    projection.source,
    new Set(["repositoryId", "commitSha", "sourceKind"]),
    "public projection source",
    errors);
  if (!/^[a-z0-9_.-]+\/[a-z0-9_.-]+$/i.test(projection.source?.repositoryId ?? "")) {
    errors.push("Database design review projection must include a public repository identifier.");
  }
  if (!/^[0-9a-f]{40}$/.test(projection.source?.commitSha ?? "")) {
    errors.push("Database design review projection must include a full public commit SHA.");
  }
  if (!Array.isArray(projection.limitations) || projection.limitations.length < 3) {
    errors.push("Database design review projection must include public limitations.");
  }

  const modes = Array.isArray(projection.modes) ? projection.modes : [];
  if (modes.length !== 2) errors.push("Database design review projection must include exactly two input modes.");
  const single = modes.find((mode) => mode?.mode === "single-index");
  const combined = modes.find((mode) => mode?.mode === "combined-index");
  if (!single || single.inputKind !== "index.sqlite") errors.push("Database design review projection is missing the single-index mode.");
  if (!combined || combined.inputKind !== "combined.sqlite") errors.push("Database design review projection is missing the combined-index mode.");
  for (const mode of modes) {
    if (!requireExactFields(mode, new Set(["mode", "inputKind", "routeCoverage", "packet"]), `mode ${mode?.mode ?? "unknown"}`, errors)) continue;
    validatePacket(mode?.packet, mode?.mode, projection.source?.commitSha, errors);
  }
  if (single?.packet?.summary?.routeReferenceCount !== 0
      || single?.packet?.tables?.some((table) => table?.routeReferences?.length !== 0)
      || !single?.packet?.gaps?.some((gap) => gap?.gapKind === "SingleIndexRoutePathUnavailable")) {
    errors.push("Single-index public packet must retain zero route references and SingleIndexRoutePathUnavailable.");
  }
  if (!(combined?.packet?.summary?.routeReferenceCount > 0)
      || !combined?.packet?.tables?.some((table) => table?.routeReferences?.length > 0)) {
    errors.push("Combined-index public packet must include bounded route-reference evidence.");
  }

  walk(projection, (key) => {
    if (forbiddenKeys.has(key.toLowerCase())) errors.push(`Database design review projection contains forbidden arbitrary or protected field: ${key}`);
  });
}

function validatePacket(packet, mode, commitSha, errors) {
  if (!isPlainObject(packet)) {
    errors.push(`Database design review ${mode} packet must be an object.`);
    return;
  }
  requireExactFields(packet, packetFields, `${mode} packet`, errors);
  if (packet.version !== "1.0" || packet.ruleId !== "database.design-review.packet.v1" || packet.claimLevel !== "static-evidence") {
    errors.push(`Database design review ${mode} packet identity does not match the shipped contract.`);
  }
  if (requireExactFields(packet.summary, summaryFields, `${mode} summary`, errors)) {
    for (const field of summaryFields) {
      if (!Number.isInteger(packet.summary[field]) || packet.summary[field] < 0) {
        errors.push(`Database design review ${mode} summary has invalid count: ${field}`);
      }
    }
  }
  if (!Array.isArray(packet.limitations) || packet.limitations.length < 5) {
    errors.push(`Database design review ${mode} packet must retain substantive limitations.`);
  }
  for (const source of asArray(packet.sources)) {
    if (!requireExactFields(source, new Set(["sourceLabel", "commitSha", "language", "analysisLevel", "buildStatus", "identityVerified", "coverageWarnings"]), `${mode} source`, errors)) continue;
    if (source.commitSha !== commitSha) errors.push(`Database design review ${mode} source commit does not match projection provenance.`);
    if (source.identityVerified !== false || !source.coverageWarnings?.includes("source-identity-synthetic")) {
      errors.push(`Database design review ${mode} public source must retain its synthetic identity boundary.`);
    }
  }
  for (const table of asArray(packet.tables)) {
    if (!requireExactFields(table, new Set(["groupId", "sourceLabel", "schemaName", "tableName", "schemaResolution", "coverage", "declarations", "operations", "queryReferences", "routeReferences", "limitations"]), `${mode} table`, errors)) continue;
    for (const collection of ["declarations", "operations", "queryReferences"]) {
      for (const item of asArray(table[collection])) validateItem(item, mode, commitSha, errors);
    }
    for (const route of asArray(table.routeReferences)) {
      if (!requireExactFields(route, new Set(["routeReferenceId", "entryKind", "method", "normalizedPathKey", "pathClassification", "tableMatchKind", "evidence"]), `${mode} route reference`, errors)) continue;
      validateEvidence(route.evidence, mode, commitSha, errors);
    }
  }
  for (const collection of ["globalObjects", "unlinkedQueries"]) {
    for (const item of asArray(packet[collection])) validateItem(item, mode, commitSha, errors);
  }
  for (const gap of asArray(packet.gaps)) {
    if (!requireExactFields(gap, new Set(["gapId", "gapKind", "classification", "message", "sourceLabel", "ruleId", "evidenceTier", "coverage", "commitSha", "filePath", "startLine", "endLine", "extractorId", "extractorVersion", "supportingFactIds", "supportingEdgeIds", "supportingRuleIds", "metadata", "limitations"]), `${mode} gap`, errors)) continue;
    if (gap.ruleId !== "database.design-review.gap.v1" || gap.evidenceTier !== "Tier4Unknown") {
      errors.push(`Database design review ${mode} gap must use the shipped gap rule and Tier4Unknown.`);
    }
    validateMetadata(gap.metadata, mode, errors);
  }
}

function validateItem(item, mode, commitSha, errors) {
  if (!requireExactFields(item, new Set(["itemId", "evidenceKind", "displayName", "classification", "metadata", "evidence"]), `${mode} evidence item`, errors)) return;
  validateMetadata(item.metadata, mode, errors);
  validateEvidence(item.evidence, mode, commitSha, errors);
}

function validateEvidence(evidence, mode, commitSha, errors) {
  if (!requireExactFields(evidence, evidenceFields, `${mode} evidence reference`, errors)) return;
  if (!evidence?.ruleId || !evidenceTiers.has(evidence?.evidenceTier) || evidence?.commitSha !== commitSha) {
    errors.push(`Database design review ${mode} evidence reference is missing compatible rule, tier, or commit provenance.`);
  }
  if (!compatibleRuleTiers.get(evidence.ruleId)?.has(evidence.evidenceTier)) {
    errors.push(`Database design review ${mode} evidence reference uses an incompatible rule and tier: ${evidence.ruleId} / ${evidence.evidenceTier}.`);
  }
  if (!evidence?.extractorId || !evidence?.extractorVersion || !evidence?.coverageLabel) {
    errors.push(`Database design review ${mode} evidence reference is missing extractor or coverage provenance.`);
  }
  if (!/^(?:db|Data|Controllers)\/[a-z0-9._/{}-]+$/i.test(evidence?.filePath ?? "") || evidence?.filePath?.includes("..")) {
    errors.push(`Database design review ${mode} evidence reference must use a public repo-relative synthetic fixture path.`);
  }
  if (!Number.isInteger(evidence?.startLine) || !Number.isInteger(evidence?.endLine)
      || evidence.startLine < 1 || evidence.endLine < evidence.startLine) {
    errors.push(`Database design review ${mode} evidence reference has an invalid line span.`);
  }
  for (const field of ["supportingFactIds", "supportingEdgeIds", "supportingRuleIds", "limitations"]) {
    if (!Array.isArray(evidence?.[field])) errors.push(`Database design review ${mode} evidence reference must include ${field}.`);
  }
  for (const id of [...(evidence?.supportingFactIds ?? []), ...(evidence?.supportingEdgeIds ?? [])]) {
    if (!/^(?:fact|edge)-public-[a-z0-9-]+$/.test(id)) errors.push(`Database design review ${mode} uses a non-public supporting ID.`);
  }
}

function validateMetadata(metadata, mode, errors) {
  if (!Array.isArray(metadata)) {
    errors.push(`Database design review ${mode} metadata must be an array.`);
    return;
  }
  for (const row of metadata) {
    if (!requireExactFields(row, new Set(["key", "value"]), `${mode} metadata row`, errors)) continue;
    if (!metadataKeys.has(row?.key)) errors.push(`Database design review ${mode} metadata contains non-allowlisted key: ${row?.key}`);
  }
}

async function validateInboundLinks(dist, routes, destination, label, errors) {
  const pattern = new RegExp(`href\\s*=\\s*["']${escapeRegExp(destination)}["']`, "i");
  for (const route of routes) {
    const path = resolve(dist, route.slice(1), "index.html");
    if (!(await fileExists(path))) {
      errors.push(`Database design review ${label} inbound route is missing: ${route}`);
      continue;
    }
    if (!pattern.test(await readFile(path, "utf8"))) {
      errors.push(`Database design review ${label} inbound route does not link to ${destination}: ${route}`);
    }
  }
}

function requireExactFields(value, allowed, label, errors) {
  if (!isPlainObject(value)) {
    errors.push(`Database design review ${label} must be an object.`);
    return false;
  }
  for (const field of allowed) {
    if (!Object.hasOwn(value, field)) errors.push(`Database design review ${label} is missing field: ${field}`);
  }
  for (const field of Object.keys(value)) {
    if (!allowed.has(field)) errors.push(`Database design review ${label} contains non-contract field: ${field}`);
  }
  return true;
}

function asArray(value) {
  return Array.isArray(value) ? value : [];
}

function walk(value, visit) {
  if (Array.isArray(value)) {
    for (const item of value) walk(item, visit);
  } else if (isPlainObject(value)) {
    for (const [key, child] of Object.entries(value)) {
      visit(key, child);
      walk(child, visit);
    }
  }
}

function isPlainObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}
