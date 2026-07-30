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

export const sqlProjectRefactorArticleRoute = "/blog/sql-project-refactor-intent-evidence/";
export const sqlProjectRefactorProofRoute = "/sql/project-refactor-intent/";
export const sqlProjectRefactorAsset = "/assets/sql-project-refactor-intent-proof-packet.json";
export const sqlProjectRefactorInboundRoutes = [
  "/",
  "/capabilities/",
  "/database/design-review/",
  "/sql/operator-handoff/",
  "/outputs/",
  "/evidence/",
  "/limitations/"
];

const expectedCommit = "26189d1dd8d97d5741005ae7cbd033840099f216";
const expectedScanId = "scan-bc301542ac1a5995396c";
const requiredProofLinks = [
  sqlProjectRefactorArticleRoute,
  sqlProjectRefactorAsset,
  "/database/design-review/",
  "/sql/operator-handoff/",
  "/outputs/",
  "/evidence/",
  "/limitations/"
];
const requiredArticleText = [
  "Public claim level: demo",
  "What a database rename can prove before deployment",
  "schema diff can hide rename intent",
  ".sqlproj",
  ".refactorlog",
  "database.sql-project.refactor-intent.v1",
  "database.sql-project.refactor-intent.gap.v1",
  "Tier2Structural",
  "Tier4Unknown",
  "table rename",
  "column rename",
  "schema-move",
  "database design review",
  "release review",
  "Questions for the reviewer",
  "not affiliated with or endorsed by Microsoft"
];
const requiredProofText = [
  "Public claim level: demo",
  "database.sql-project.refactor-intent.v1",
  "database.sql-project.refactor-intent.gap.v1",
  "SqlProjectRefactorExtractor",
  "sql-project-refactor/0.1.0",
  "Tier2Structural",
  "Tier4Unknown",
  "bounded-static-evidence",
  "rename-table",
  "rename-column",
  "move-schema",
  "fixture evidence",
  "illustrative supported category",
  "RefactorOperationUnsupported",
  "RefactorLogReferenceMissing",
  "RefactorOperationLimitExceeded",
  "ReviewRecommended",
  "intended-by-project-refactor-log",
  "End with ownership, not approval"
];
const requiredRuleIds = new Set([
  "database.sql-project.refactor-intent.v1",
  "database.sql-project.refactor-intent.gap.v1"
]);
const expectedOperationCategories = [
  {
    operationKind: "rename-table",
    objectKind: "table",
    exampleKind: "fixture-evidence",
    safeSource: "dbo.InventoryItem",
    safeTarget: "dbo.CatalogItem"
  },
  {
    operationKind: "rename-column",
    objectKind: "column",
    exampleKind: "illustrative-supported-category",
    safeSource: "dbo.InventoryItem.DisplayName",
    safeTarget: "dbo.InventoryItem.ItemName"
  },
  {
    operationKind: "move-schema",
    objectKind: "table",
    exampleKind: "fixture-evidence",
    safeSource: "dbo.CatalogItem",
    safeTarget: "catalog.CatalogItem"
  }
];
const expectedGapShapes = [
  {
    id: "unsupported-or-unsafe-shape",
    classification: "RefactorOperationUnsupported",
    coverageLabel: "reduced"
  },
  {
    id: "missing-project-link",
    classification: "RefactorLogReferenceMissing",
    coverageLabel: "reduced"
  },
  {
    id: "bounded-operation-cap",
    classification: "RefactorOperationLimitExceeded",
    coverageLabel: "partial"
  }
];
const expectedDownstreamSurfaces = [
  {
    surface: "database-design-review",
    classification: "ReviewRecommended",
    state: undefined
  },
  {
    surface: "release-review",
    classification: "ReviewRecommended",
    state: undefined
  },
  {
    surface: "sql-runbook",
    classification: undefined,
    state: "intended-by-project-refactor-log"
  }
];
const expectedFixtureEvidence = [
  {
    id: "project-log-link",
    sourceFactId: "fact-37e5fc9f9eabe1c68db6",
    factType: "SqlProjectRefactorLogDeclared",
    operationKind: undefined,
    projectPath: "samples/sql-project-refactor/Inventory.sqlproj",
    safeSource: "Inventory.sqlproj",
    safeTarget: "Inventory.refactorlog",
    span: {
      filePath: "samples/sql-project-refactor/Inventory.sqlproj",
      startLine: 3,
      endLine: 3
    }
  },
  {
    id: "table-rename-intent",
    sourceFactId: "fact-58e4a0e819b64afe7430",
    factType: "SqlProjectRefactorOperation",
    operationKind: "rename-table",
    projectPath: "samples/sql-project-refactor/Inventory.sqlproj",
    safeSource: "dbo.InventoryItem",
    safeTarget: "dbo.CatalogItem",
    span: {
      filePath: "samples/sql-project-refactor/Inventory.refactorlog",
      startLine: 2,
      endLine: 2
    }
  },
  {
    id: "table-schema-move-intent",
    sourceFactId: "fact-b4dea22f53654c5c9cfe",
    factType: "SqlProjectRefactorOperation",
    operationKind: "move-schema",
    projectPath: "samples/sql-project-refactor/Inventory.sqlproj",
    safeSource: "dbo.CatalogItem",
    safeTarget: "catalog.CatalogItem",
    span: {
      filePath: "samples/sql-project-refactor/Inventory.refactorlog",
      startLine: 7,
      endLine: 7
    }
  }
];

const hardLeakPatterns = [
  /(?:\/Users\/|\/home\/|\/private\/|[A-Z]:\\Users\\)/i,
  /\b(?:Server|Password|User Id)\s*=/i,
  /\bP\s*a\s*s\s*s\s*w\s*o\s*r\s*d\s*=/i,
  /\b(?:ConnectionString|connection string|api[_-]?key|secret\s*=|sk-[A-Za-z0-9_-]{12,})\b/i,
  /\b(?:private-infrastructure|private-host|internal-ticket|raw-analyzer-output|operation-key-sentinel)\b/i,
  /\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b/i,
  /\bSqlPackage(?:\.exe)?\s+(?:\/|--)[A-Za-z]/i
];
const rawMaterialPatterns = [
  /\b(?:SELECT\s+.+?\s+FROM|INSERT\s+INTO|UPDATE\s+\w+\s+SET|DELETE\s+FROM|CREATE\s+TABLE|ALTER\s+TABLE|DROP\s+TABLE)\b/i,
  /\bTRUNCATE\s+TABLE\s+(?:\[[^\]]+\]|[A-Za-z_][A-Za-z0-9_.$]*)\b/i,
  /\b(?:EXEC\s+(?:\[[^\]]+\]|[A-Za-z_][A-Za-z0-9_.$]*)|EXECUTE\s+(?:\[[^\]]+\](?:\.\[[^\]]+\])?|[A-Za-z_][A-Za-z0-9_$]*\.[A-Za-z_][A-Za-z0-9_$]*))\b/i,
  /\b(?:GRANT|DENY|REVOKE)\s+[A-Za-z][A-Za-z0-9_ ]{0,40}\s+(?:ON\s+\S+\s+)?(?:TO|FROM)\s+\S+/i,
  /<(?:Project|Operation|Property|RefactorLog)\b/i
];
const positiveOverclaimPatterns = [
  /\b(?:deployment|refactor|rename|schema move)\s+(?:succeeded|completed successfully|is safe|was approved)\b/i,
  /\b(?:database\s+)?(?:change|deployment|refactor|rename|schema move)\s+(?:is|was)\s+compatible\b/i,
  /\b(?:change|deployment|refactor|rename|schema move)\s+(?:is|was|has been)\s+applied(?:\s+successfully)?\b/i,
  /\b(?:proves|confirms|guarantees|certifies)\b[^.]{0,100}\b(?:deployed|applied|compatible|successful|reversible|approved|safe to run)\b/i,
  /\b(?:it|this|the (?:change|deployment|refactor|release|rename|schema move|script))\s+(?:is|was)\s+(?:ready|approved|safe)\s+(?:for|to)\s+(?:deploy|deployment|release|run)\b/i
];

export async function validateSqlProjectRefactorIntentStoryDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const articlePath = resolve(dist, "blog", "sql-project-refactor-intent-evidence", "index.html");
  const proofPath = resolve(dist, "sql", "project-refactor-intent", "index.html");
  const assetPath = resolve(dist, "assets", "sql-project-refactor-intent-proof-packet.json");

  for (const [path, label] of [
    [articlePath, sqlProjectRefactorArticleRoute],
    [proofPath, sqlProjectRefactorProofRoute],
    [assetPath, sqlProjectRefactorAsset]
  ]) {
    if (!(await fileExists(path))) errors.push(`SQL project refactor-intent story is missing required artifact: ${label}`);
  }
  if (!(await fileExists(articlePath)) || !(await fileExists(proofPath)) || !(await fileExists(assetPath))) return;

  const articleHtml = await readFile(articlePath, "utf8");
  const proofHtml = await readFile(proofPath, "utf8");
  const assetText = await readFile(assetPath, "utf8");

  validatePage({
    canonical: `${baseUrl}${sqlProjectRefactorArticleRoute}`,
    errors,
    html: articleHtml,
    label: "article",
    requiredText: requiredArticleText
  });
  validatePage({
    canonical: `${baseUrl}${sqlProjectRefactorProofRoute}`,
    errors,
    html: proofHtml,
    label: "proof page",
    requiredText: requiredProofText
  });

  const renderedStoryText = [articleHtml, proofHtml]
    .map((html) => decodeHtmlEntities(stripTagsQuoteAware(html)))
    .join(" ");
  const leakScanText = [
    decodeHtmlEntities(articleHtml),
    decodeHtmlEntities(proofHtml),
    renderedStoryText,
    collapseTagSplitTextTight(articleHtml),
    collapseTagSplitTextTight(proofHtml),
    assetText
  ].join(" ");
  for (const pattern of [...hardLeakPatterns, ...rawMaterialPatterns]) {
    if (pattern.test(leakScanText)) {
      errors.push(`SQL project refactor-intent story contains forbidden private, raw, key, command, SQL, or XML material: ${pattern}`);
    }
  }
  const claimScan = [
    normalizeRenderedText(articleHtml),
    normalizeRenderedText(proofHtml),
    assetText
  ].join(" ");
  for (const pattern of positiveOverclaimPatterns) {
    if (pattern.test(claimScan)) {
      errors.push(`SQL project refactor-intent story contains a forbidden positive deployment, approval, or safety claim: ${pattern}`);
    }
  }

  if (!hasHref(articleHtml, "https://devblogs.microsoft.com/azure-sql/refactor-your-database-with-sql-projects-in-vs-code/")) {
    errors.push("SQL project refactor-intent article must link the attributed Microsoft workflow source.");
  }
  if (!hasHref(articleHtml, sqlProjectRefactorProofRoute) || !hasHref(proofHtml, sqlProjectRefactorArticleRoute)) {
    errors.push("SQL project refactor-intent article and proof page must link bidirectionally.");
  }
  for (const link of requiredProofLinks) {
    if (!hasHref(proofHtml, link)) errors.push(`SQL project refactor-intent proof page is missing required link: ${link}`);
  }

  let packet;
  try {
    packet = JSON.parse(assetText);
  } catch (error) {
    errors.push(`SQL project refactor-intent proof asset is not valid JSON: ${error.message}`);
  }
  if (packet) validatePacket(packet, errors);

  const inboundRegex = new RegExp(`href\\s*=\\s*["']${escapeRegExp(sqlProjectRefactorProofRoute)}["']`, "i");
  for (const route of sqlProjectRefactorInboundRoutes) {
    const path = route === "/" ? resolve(dist, "index.html") : resolve(dist, route.slice(1), "index.html");
    if (!(await fileExists(path))) {
      errors.push(`SQL project refactor-intent inbound route is missing: ${route}`);
      continue;
    }
    if (!inboundRegex.test(await readFile(path, "utf8"))) {
      errors.push(`SQL project refactor-intent inbound route does not link to ${sqlProjectRefactorProofRoute}: ${route}`);
    }
  }

  const sitemapPath = resolve(dist, "sitemap.xml");
  if (await fileExists(sitemapPath)) {
    const sitemapUrls = await readSitemapLocSet(sitemapPath);
    for (const route of [sqlProjectRefactorArticleRoute, sqlProjectRefactorProofRoute]) {
      if (!sitemapUrls.has(`${baseUrl}${route}`)) errors.push(`Sitemap is missing SQL project refactor-intent URL: ${baseUrl}${route}`);
    }
  }

  const routesIndexPath = resolve(dist, "routes-index.json");
  if (await fileExists(routesIndexPath)) {
    try {
      const routes = JSON.parse(await readFile(routesIndexPath, "utf8"));
      const entries = Array.isArray(routes?.entries) ? routes.entries : [];
      for (const route of [sqlProjectRefactorArticleRoute, sqlProjectRefactorProofRoute]) {
        const entry = entries.find((candidate) => candidate?.path === route);
        if (!entry) errors.push(`routes-index.json is missing ${route}`);
        else if (entry.publicClaimLevel !== "demo" || entry.sourceType !== "site-page") {
          errors.push(`SQL project refactor-intent discovery entry must remain demo-level site-page evidence: ${route}`);
        }
      }
    } catch (error) {
      errors.push(`SQL project refactor-intent validator could not parse routes-index.json: ${error.message}`);
    }
  }
}

function validatePage({ canonical, errors, html, label, requiredText }) {
  const text = normalizeRenderedText(html);
  const decoded = decodeHtmlEntities(html);
  for (const phrase of requiredText) {
    if (!text.includes(phrase) && !decoded.includes(phrase)) {
      errors.push(`SQL project refactor-intent ${label} is missing required text: ${phrase}`);
    }
  }
  if (!/<meta\b(?=[^>]*\bproperty\s*=\s*["']og:type["'])(?=[^>]*\bcontent\s*=\s*["']article["'])[^>]*>/i.test(html)) {
    errors.push(`SQL project refactor-intent ${label} must use article social metadata.`);
  }
  const canonicalRegex = new RegExp(`<link\\b(?=[^>]*\\brel\\s*=\\s*["']canonical["'])(?=[^>]*\\bhref\\s*=\\s*["']${escapeRegExp(canonical)}["'])[^>]*>`, "i");
  if (!canonicalRegex.test(html)) errors.push(`SQL project refactor-intent ${label} canonical URL is missing or incorrect.`);
}

function validatePacket(packet, errors) {
  if (!isPlainObject(packet)) {
    errors.push("SQL project refactor-intent proof asset must contain an object.");
    return;
  }
  for (const field of [
    "schemaVersion",
    "publicClaimLevel",
    "purpose",
    "source",
    "extractor",
    "ruleIds",
    "supportedOperationCategories",
    "evidence",
    "gaps",
    "downstreamReviewSurfaces",
    "reviewerQuestions",
    "limitations"
  ]) {
    if (!Object.hasOwn(packet, field)) errors.push(`SQL project refactor-intent proof asset is missing required field: ${field}`);
  }
  if (packet.schemaVersion !== "tracemap-public-sql-project-refactor-intent/v1") {
    errors.push("SQL project refactor-intent proof asset has an invalid schemaVersion.");
  }
  if (packet.publicClaimLevel !== "demo") errors.push("SQL project refactor-intent proof asset must remain demo-level.");
  if (packet.source?.repository !== "joefeser/tracemap" || packet.source?.commitSha !== expectedCommit
    || packet.source?.fixtureRoot !== "samples/sql-project-refactor" || packet.source?.scanId !== expectedScanId) {
    errors.push("SQL project refactor-intent proof asset has invalid public source provenance.");
  }
  if (packet.extractor?.family !== "SqlProjectRefactorExtractor" || packet.extractor?.version !== "sql-project-refactor/0.1.0") {
    errors.push("SQL project refactor-intent proof asset has invalid extractor provenance.");
  }

  const rules = new Set(Array.isArray(packet.ruleIds) ? packet.ruleIds : []);
  for (const ruleId of requiredRuleIds) if (!rules.has(ruleId)) errors.push(`SQL project refactor-intent proof asset is missing rule ID: ${ruleId}`);

  const categories = Array.isArray(packet.supportedOperationCategories) ? packet.supportedOperationCategories : [];
  if (categories.length !== expectedOperationCategories.length) {
    errors.push("SQL project refactor-intent proof asset must contain exactly the three pinned supported operation categories.");
  }
  for (const expected of expectedOperationCategories) {
    const row = categories.find((candidate) => candidate?.operationKind === expected.operationKind);
    if (!row || !operationCategoryMatches(row, expected)) {
      errors.push(`SQL project refactor-intent proof asset does not match pinned supported category: ${expected.operationKind}`);
    }
  }

  const evidence = Array.isArray(packet.evidence) ? packet.evidence : [];
  if (evidence.length !== expectedFixtureEvidence.length) {
    errors.push("SQL project refactor-intent proof asset must contain exactly the three pinned fixture-backed evidence rows.");
  }
  const evidenceIds = new Set();
  for (const [index, row] of evidence.entries()) {
    if (!row?.id || evidenceIds.has(row.id)) errors.push(`SQL project refactor-intent evidence row ${index + 1} has a missing or duplicate ID.`);
    evidenceIds.add(row?.id);
    if (row?.ruleId !== "database.sql-project.refactor-intent.v1" || row?.evidenceTier !== "Tier2Structural"
      || row?.coverageLabel !== "bounded-static-evidence" || row?.commitSha !== expectedCommit
      || row?.extractorFamily !== "SqlProjectRefactorExtractor" || row?.extractorVersion !== "sql-project-refactor/0.1.0"
      || !row?.sourceFactId) {
      errors.push(`SQL project refactor-intent evidence row ${index + 1} has incomplete rule, tier, coverage, fact, commit, or extractor provenance.`);
    }
    for (const path of [row?.projectPath, row?.span?.filePath]) {
      if (!/^samples\/sql-project-refactor\/[A-Za-z0-9._/-]+$/.test(path ?? "") || path?.includes("..")) {
        errors.push(`SQL project refactor-intent evidence row ${index + 1} must use public repository-relative sample paths.`);
      }
    }
    if (!Number.isInteger(row?.span?.startLine) || !Number.isInteger(row?.span?.endLine)
      || row.span.startLine < 1 || row.span.endLine < row.span.startLine) {
      errors.push(`SQL project refactor-intent evidence row ${index + 1} has an invalid line span.`);
    }
  }
  for (const expected of expectedFixtureEvidence) {
    const row = evidence.find((candidate) => candidate?.id === expected.id);
    if (!row || !fixtureEvidenceMatches(row, expected)) {
      errors.push(`SQL project refactor-intent proof asset does not match pinned fixture fact: ${expected.id}`);
    }
  }

  const fixtureKinds = new Set(evidence.map((row) => row?.operationKind).filter(Boolean));
  for (const operationKind of ["rename-table", "move-schema"]) {
    if (!fixtureKinds.has(operationKind)) errors.push(`SQL project refactor-intent proof asset is missing fixture operation: ${operationKind}`);
  }
  if (fixtureKinds.has("rename-column")) errors.push("SQL project refactor-intent proof asset must not present column rename as fixture evidence.");

  const gaps = Array.isArray(packet.gaps) ? packet.gaps : [];
  if (gaps.length !== expectedGapShapes.length) {
    errors.push("SQL project refactor-intent proof asset must contain exactly the three pinned representative gap shapes.");
  }
  for (const [index, gap] of gaps.entries()) {
    if (gap?.exampleKind !== "illustrative-gap-shape"
      || gap?.ruleId !== "database.sql-project.refactor-intent.gap.v1"
      || gap?.evidenceTier !== "Tier4Unknown"
      || !["reduced", "partial"].includes(gap?.coverageLabel)) {
      errors.push(`SQL project refactor-intent gap row ${index + 1} must remain an illustrative Tier 4 reduced/partial shape.`);
    }
  }
  for (const expected of expectedGapShapes) {
    const gap = gaps.find((candidate) => candidate?.id === expected.id);
    if (!gap || gap.classification !== expected.classification || gap.coverageLabel !== expected.coverageLabel) {
      errors.push(`SQL project refactor-intent proof asset does not match pinned gap shape: ${expected.id}`);
    }
  }

  const downstream = Array.isArray(packet.downstreamReviewSurfaces) ? packet.downstreamReviewSurfaces : [];
  if (downstream.length !== expectedDownstreamSurfaces.length) {
    errors.push("SQL project refactor-intent proof asset must contain exactly the three pinned downstream review surfaces.");
  }
  for (const expected of expectedDownstreamSurfaces) {
    const row = downstream.find((candidate) => candidate?.surface === expected.surface);
    if (!row || row.classification !== expected.classification || row.state !== expected.state) {
      errors.push(`SQL project refactor-intent proof asset does not match pinned downstream review surface: ${expected.surface}`);
    }
  }
  for (const field of ["reviewerQuestions", "limitations"]) {
    if (!Array.isArray(packet[field]) || packet[field].length === 0) errors.push(`SQL project refactor-intent proof asset must include non-empty ${field}.`);
  }
}

function collapseTagSplitTextTight(html) {
  return decodeHtmlEntities(stripTagsQuoteAware(String(html))).replace(/\s+/g, "");
}

function fixtureEvidenceMatches(row, expected) {
  return row.sourceFactId === expected.sourceFactId
    && row.factType === expected.factType
    && row.operationKind === expected.operationKind
    && row.projectPath === expected.projectPath
    && row.safeSource === expected.safeSource
    && row.safeTarget === expected.safeTarget
    && row.span?.filePath === expected.span.filePath
    && row.span?.startLine === expected.span.startLine
    && row.span?.endLine === expected.span.endLine;
}

function operationCategoryMatches(row, expected) {
  return row.objectKind === expected.objectKind
    && row.exampleKind === expected.exampleKind
    && row.safeSource === expected.safeSource
    && row.safeTarget === expected.safeTarget;
}

function hasHref(html, href) {
  return new RegExp(`href\\s*=\\s*["']${escapeRegExp(href)}["']`, "i").test(html);
}

function isPlainObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}
