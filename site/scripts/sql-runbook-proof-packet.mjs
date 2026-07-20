import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import { decodeHtmlEntities, escapeRegExp, fileExists, normalizeRenderedText, readSitemapLocSet } from "./validate-utils.mjs";

export const sqlRunbookProofPacketRoute = "/sql/operator-handoff/proof-packet/";
export const sqlRunbookProofPacketAsset = "/assets/sql-operator-runbook-proof-packet.json";
export const sqlRunbookProofPacketInboundRoutes = [
  "/sql/operator-handoff/",
  "/manager-packet/",
  "/outputs/",
  "/limitations/",
  "/proof-paths/for-managers/",
  "/packets/",
  "/packets/examples/",
  "/examples/",
  "/demo/"
];

const requiredPageText = [
  "Public claim level: demo",
  "sql-operator-runbook-packet/v2",
  "wrong-tab or wrong-database",
  "postgres_fdw",
  "dblink",
  "Logical replication",
  "pg_cron",
  "present-in-scripts",
  "missing-evidence",
  "conflicting-evidence",
  "unknown",
  "needs-owner-review",
  "user-mapping",
  "connection-material",
  "remote-query-input",
  "scheduled-command-body",
  "intended-by-script",
  "validation-step-present",
  "validation-evidence-not-provided",
  "database.sql.context.declaration.v1",
  "database.sql.secret-bearing-step.v1",
  "database.postgres.permission.coverage.v1",
  "database.postgres.archive-link.v1",
  "Tier2Structural",
  "Tier4Unknown"
];

const requiredTopLevelFields = [
  "schemaVersion",
  "derivedFromContract",
  "publicClaimLevel",
  "purpose",
  "source",
  "coverage",
  "contextGroups",
  "surfaceCoverage",
  "permissionStatusVocabulary",
  "prerequisites",
  "protectedSteps",
  "milestones",
  "stopConditions",
  "gaps",
  "ownerQuestions",
  "evidence",
  "limitations"
];

const permissionStatuses = new Set([
  "present-in-scripts",
  "missing-evidence",
  "conflicting-evidence",
  "unknown",
  "needs-owner-review"
]);

const evidenceTiers = new Set(["Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"]);
const requiredSurfaces = new Set(["postgres_fdw", "dblink", "logical-replication", "pg_cron"]);
const requiredProtectedCategories = new Set(["user-mapping", "connection-material", "remote-query-input", "scheduled-command-body"]);
const forbiddenPatterns = [
  /(?:\/Users\/|\/home\/|[A-Z]:\\Users\\)/i,
  /\b(?:Server|Password|User Id)\s*=/i,
  /\b(?:SELECT\s+.+?\s+FROM|INSERT\s+INTO|UPDATE\s+\w+\s+SET|DELETE\s+FROM|CREATE\s+(?:SERVER|USER\s+MAPPING|SUBSCRIPTION)|ALTER\s+SUBSCRIPTION|DROP\s+SERVER)\b/i,
  /\b(?:safe to run|setup succeeded|permissions are effective|replication is healthy|validation passed|rollback worked)\b/i,
  /\b(?:private-host|private-password|private-infrastructure|raw-scheduled-command|validation-output|ticket-[0-9]+)\b/i,
  /\b(?:host|hostname|password|connectionString|scheduledCommandBody|rawSql|validationOutput)\b\s*:/i
];

export async function validateSqlRunbookProofPacketDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const pagePath = resolve(dist, "sql", "operator-handoff", "proof-packet", "index.html");
  const assetPath = resolve(dist, "assets", "sql-operator-runbook-proof-packet.json");

  if (!(await fileExists(pagePath))) {
    errors.push(`SQL runbook proof packet page is missing required route: ${sqlRunbookProofPacketRoute}`);
    return;
  }
  if (!(await fileExists(assetPath))) {
    errors.push(`SQL runbook proof packet asset is missing: ${sqlRunbookProofPacketAsset}`);
    return;
  }

  const html = await readFile(pagePath, "utf8");
  const text = normalizeRenderedText(html);
  const decoded = decodeHtmlEntities(html);
  const tagCollapsedText = decoded.replace(/<[^>]*>/g, "");
  const assetText = await readFile(assetPath, "utf8");

  for (const phrase of requiredPageText) {
    if (!text.includes(phrase) && !decoded.includes(phrase)) {
      errors.push(`SQL runbook proof packet page is missing required text: ${phrase}`);
    }
  }
  for (const pattern of forbiddenPatterns) {
    if (pattern.test(`${decoded} ${text} ${tagCollapsedText} ${assetText}`)) {
      errors.push(`SQL runbook proof packet contains forbidden private, executable, protected, or overclaim text: ${pattern}`);
    }
  }

  if (!/<meta\b(?=[^>]*\bproperty\s*=\s*["']og:type["'])(?=[^>]*\bcontent\s*=\s*["']article["'])[^>]*>/i.test(html)) {
    errors.push("SQL runbook proof packet page must use article social metadata.");
  }
  if (!/<link\b(?=[^>]*\brel\s*=\s*["']canonical["'])(?=[^>]*\bhref\s*=\s*["']https:\/\/tracemap\.tools\/sql\/operator-handoff\/proof-packet\/["'])[^>]*>/i.test(html)) {
    errors.push("SQL runbook proof packet canonical URL is missing or incorrect.");
  }

  let packet;
  try {
    packet = JSON.parse(assetText);
  } catch (error) {
    errors.push(`SQL runbook proof packet asset is not valid JSON: ${error.message}`);
  }
  if (packet) validatePacketShape(packet, errors);

  const routeLinkRegex = new RegExp(`href\\s*=\\s*["']${escapeRegExp(sqlRunbookProofPacketRoute)}["']`, "i");
  for (const route of sqlRunbookProofPacketInboundRoutes) {
    const inboundPath = resolve(dist, route.slice(1), "index.html");
    if (!(await fileExists(inboundPath))) {
      errors.push(`SQL runbook proof packet inbound route is missing: ${route}`);
      continue;
    }
    const inbound = await readFile(inboundPath, "utf8");
    if (!routeLinkRegex.test(inbound)) {
      errors.push(`SQL runbook proof packet inbound route does not link to ${sqlRunbookProofPacketRoute}: ${route}`);
    }
  }

  const sitemapPath = resolve(dist, "sitemap.xml");
  if (await fileExists(sitemapPath)) {
    const sitemapUrls = await readSitemapLocSet(sitemapPath);
    if (!sitemapUrls.has(`${baseUrl}${sqlRunbookProofPacketRoute}`)) {
      errors.push(`Sitemap is missing SQL runbook proof packet URL: ${baseUrl}${sqlRunbookProofPacketRoute}`);
    }
  }

  const routesIndexPath = resolve(dist, "routes-index.json");
  if (await fileExists(routesIndexPath)) {
    try {
      const routesIndex = JSON.parse(await readFile(routesIndexPath, "utf8"));
      const routeEntries = Array.isArray(routesIndex?.entries) ? routesIndex.entries : [];
      const entry = routeEntries.find((candidate) => candidate?.path === sqlRunbookProofPacketRoute);
      if (!entry) {
        errors.push(`routes-index.json is missing ${sqlRunbookProofPacketRoute}`);
      } else if (entry.publicClaimLevel !== "demo" || entry.sourceType !== "site-page") {
        errors.push("SQL runbook proof packet discovery metadata must remain demo-level site-page evidence.");
      }
    } catch (error) {
      errors.push(`SQL runbook proof packet could not parse routes-index.json: ${error.message}`);
    }
  }
}

function validatePacketShape(packet, errors) {
  if (!isPlainObject(packet)) {
    errors.push("SQL runbook proof packet asset must contain an object.");
    return;
  }
  for (const field of requiredTopLevelFields) {
    if (!Object.hasOwn(packet, field)) errors.push(`SQL runbook proof packet is missing required field: ${field}`);
  }
  if (packet.schemaVersion !== "tracemap-public-sql-proof-packet/v1") {
    errors.push("SQL runbook proof packet schemaVersion must be tracemap-public-sql-proof-packet/v1.");
  }
  if (packet.derivedFromContract !== "sql-operator-runbook-packet/v2") {
    errors.push("SQL runbook proof packet must identify sql-operator-runbook-packet/v2 as its source contract.");
  }
  if (packet.publicClaimLevel !== "demo") errors.push("SQL runbook proof packet publicClaimLevel must remain demo.");

  const source = packet.source;
  if (!isPlainObject(source) || source.repository !== "joefeser/tracemap" || !/^[0-9a-f]{40}$/.test(source.commitSha ?? "")) {
    errors.push("SQL runbook proof packet source must include the public repository and a full commit SHA.");
  }
  if (source?.fixturePath !== "samples/sql-operator-runbook/setup.sql" || !source?.scanId) {
    errors.push("SQL runbook proof packet source must include public-safe fixture and scan provenance.");
  }

  if (!Array.isArray(packet.contextGroups) || packet.contextGroups.length < 5) {
    errors.push("SQL runbook proof packet must include ordered categorical context groups.");
  } else {
    for (const [index, group] of packet.contextGroups.entries()) {
      if (group?.order !== index + 1 || !group.serverRole || !group.databaseRole || !group.schemaRole || !group.executionMode || !group.checkpoint) {
        errors.push(`SQL runbook proof packet context group ${index + 1} is missing deterministic order or categorical context.`);
      }
    }
    if (!packet.contextGroups.some((group) => group?.transition === true && /wrong-tab|verify/.test(group?.checkpoint ?? ""))) {
      errors.push("SQL runbook proof packet must include a manual-client transition verification checkpoint.");
    }
  }

  const surfaces = new Set(Array.isArray(packet.surfaceCoverage) ? packet.surfaceCoverage.map((row) => row?.surface) : []);
  for (const surface of requiredSurfaces) {
    if (!surfaces.has(surface)) errors.push(`SQL runbook proof packet is missing PostgreSQL surface: ${surface}`);
  }

  if (!Array.isArray(packet.permissionStatusVocabulary) || packet.permissionStatusVocabulary.length !== permissionStatuses.size) {
    errors.push("SQL runbook proof packet must include the complete permission status vocabulary.");
  } else {
    for (const status of permissionStatuses) {
      if (!packet.permissionStatusVocabulary.includes(status)) errors.push(`SQL runbook proof packet is missing permission status: ${status}`);
    }
  }
  for (const row of Array.isArray(packet.prerequisites) ? packet.prerequisites : []) {
    if (!permissionStatuses.has(row?.status)) errors.push(`SQL runbook proof packet contains invalid permission status: ${row?.status}`);
  }

  const protectedSteps = Array.isArray(packet.protectedSteps) ? packet.protectedSteps : [];
  if (!Array.isArray(packet.protectedSteps)) {
    errors.push("SQL runbook proof packet protectedSteps must be an array.");
  }
  const protectedCategories = new Set(protectedSteps.flatMap((row) => row?.protectedCategories ?? []));
  for (const category of requiredProtectedCategories) {
    if (!protectedCategories.has(category)) errors.push(`SQL runbook proof packet is missing protected category: ${category}`);
  }
  if (protectedSteps.some((row) => row?.valuesOmitted !== true)) {
    errors.push("Every SQL runbook proof packet protected step must explicitly omit values.");
  }

  const milestoneStates = JSON.stringify(packet.milestones ?? []);
  for (const state of ["intended-by-script", "validation-step-present", "validation-evidence-not-provided"]) {
    if (!milestoneStates.includes(state)) errors.push(`SQL runbook proof packet milestones are missing state: ${state}`);
  }

  const evidenceIds = new Set();
  for (const [index, evidence] of (Array.isArray(packet.evidence) ? packet.evidence : []).entries()) {
    if (!evidence?.id || evidenceIds.has(evidence.id)) errors.push(`SQL runbook proof packet evidence row ${index + 1} has a missing or duplicate id.`);
    evidenceIds.add(evidence?.id);
    if (!evidence?.ruleId || !evidenceTiers.has(evidence?.evidenceTier) || !evidence?.extractorId || !evidence?.extractorVersion) {
      errors.push(`SQL runbook proof packet evidence row ${index + 1} is missing rule, tier, or extractor provenance.`);
    }
    if (evidence?.commitSha !== source?.commitSha) errors.push(`SQL runbook proof packet evidence row ${index + 1} does not match source commit provenance.`);
    if (!/^samples\/sql-operator-runbook\/[a-z0-9._/-]+$/i.test(evidence?.filePath ?? "") || evidence?.filePath?.includes("..")) {
      errors.push(`SQL runbook proof packet evidence row ${index + 1} must use a public repo-relative fixture path.`);
    }
    if (!Number.isInteger(evidence?.lineSpan?.startLine) || !Number.isInteger(evidence?.lineSpan?.endLine)
      || evidence.lineSpan.startLine < 1 || evidence.lineSpan.endLine < evidence.lineSpan.startLine) {
      errors.push(`SQL runbook proof packet evidence row ${index + 1} has an invalid line span.`);
    }
  }

  for (const collection of ["prerequisites", "protectedSteps", "milestones", "gaps"]) {
    for (const row of Array.isArray(packet[collection]) ? packet[collection] : []) {
      if (!evidenceIds.has(row?.evidenceRef)) errors.push(`SQL runbook proof packet ${collection} row references missing evidence: ${row?.evidenceRef}`);
    }
  }
  for (const field of ["stopConditions", "gaps", "ownerQuestions", "limitations"]) {
    if (!Array.isArray(packet[field]) || packet[field].length === 0) errors.push(`SQL runbook proof packet must include non-empty ${field}.`);
  }
}

function isPlainObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}
