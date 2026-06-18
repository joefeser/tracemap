import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeBaseUrl,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const proofSourceCatalogRoute = "/proof-source-catalog/";
export const proofSourceCatalogRequiredLinks = [
  "/proof-paths/",
  "/roadmap/",
  "/capabilities/",
  "/docs/",
  "/validation/",
  "/limitations/"
];

const claimLevels = new Set(["shipped", "demo", "concept", "hidden"]);
const evidenceStatuses = new Set([
  "source-backed",
  "demo-evidence-backed",
  "partial-or-reduced",
  "gap-labeled-demo",
  "future-only",
  "hidden-or-internal",
  "not-yet-backed"
]);
const publishedEvidenceStatuses = new Set([...evidenceStatuses].filter((status) => status !== "not-yet-backed"));
const allowedPairs = new Map([
  ["shipped", new Set(["source-backed", "demo-evidence-backed", "partial-or-reduced"])],
  ["demo", new Set(["demo-evidence-backed", "partial-or-reduced", "gap-labeled-demo"])],
  ["concept", new Set(["future-only"])],
  ["hidden", new Set(["hidden-or-internal"])]
]);
const requiredRowFields = [
  "route",
  "claimLabel",
  "allowedPublicWording",
  "publicClaimLevel",
  "evidenceStatus",
  "proofPath",
  "sourceArtifactOrDoc",
  "ruleIdOrFamily",
  "evidenceTierOrCoverage",
  "limitation",
  "nonClaims"
];
const requiredClaimMapping = new Map([
  ["shipped", ["main", "shipped", "shipped navigation", "repository docs on main", "main with maturity caveats"]],
  [
    "demo",
    [
      "demo",
      "demo guidance",
      "main/demo",
      "public-demo",
      "checked-in public-safe demo summary",
      "route metadata publicClaimLevel: demo",
      "proof-path public status demo"
    ]
  ],
  [
    "concept",
    [
      "concept",
      "concept-only",
      "future",
      "future-only",
      "dev",
      "dev-only",
      "route metadata publicClaimLevel: concept",
      "proof-path public status future"
    ]
  ],
  [
    "hidden",
    [
      "hidden",
      "hidden pending validation",
      "no public capability row",
      "no public proof-path counterpart",
      "internal-only aggregate placeholder"
    ]
  ]
]);
const requiredEvidenceMapping = new Set(evidenceStatuses);
const allowedSentinelProofPaths = new Set(["future-only", "hidden"]);
const rowWordLimit = 55;
const hiddenAnchor = "proof-source-hidden-aggregate-placeholder";
const requiredText = [
  "Public claim level: demo",
  "Public claim level",
  "route-to-source map",
  "site-tracemap-tools-claim-ledger",
  "SQLite indexes, fact streams, reports, rule catalog entries, route metadata, source docs, coverage labels, and documented limitations remain authoritative",
  "Concept rows are not shipped capabilities",
  "No public conclusion without evidence"
];
const forbiddenPrivateText = [
  "/Users/",
  "/home/",
  "~/",
  "C:\\",
  "file://",
  "localhost",
  "127.0.0.1",
  "git@",
  ".git",
  "ConnectionString",
  "connection string",
  "Server=",
  "User Id=",
  "Password="
];
const forbiddenAffirmativeClaimPatterns = [
  /\bfacts\.ndjson\b/i,
  /\bindex\.sqlite\b/i,
  /\banalyzer\.log\b/i,
  /\blogs\/analyzer\.log\b/i,
  /\bscan-manifest\.json\b/i,
  /\breport\.md\b/i,
  /\bproven in production\b/i,
  /\bruntime-safe\b/i,
  /\bproduction-verified\b/i,
  /\btraffic-proven\b/i,
  /\brelease-safe\b/i,
  /\boperationally safe\b/i,
  /\boutage cause\b/i,
  /\brelease approval\b/i,
  /\bAI impact analysis\b/i,
  /\bLLM analysis\b/i,
  /\bembedding-backed\b/i,
  /\bprompt-classified\b/i,
  /\bcomplete coverage\b/i,
  /\bfull product coverage\b/i,
  /\ball endpoints proven\b/i,
  /\bimpacted\b/i,
  /(?<!public-)\bsafe\b/i,
  /\bclean\b/i,
  /\bproduction-proven\b/i
];

export async function validateProofSourceCatalogDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "proof-source-catalog", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence("Proof source catalog page is missing required public route: /proof-source-catalog/", "proof-source-catalog/index.html"));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  await validateRoutesIndex({ dist, errors: localErrors });
  await validateCatalogPage({ pagePath, errors: localErrors });
  await validateSurfaceStatusVocabulary({ catalogPagePath: pagePath, dist, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${proofSourceCatalogRoute}`)) {
    errors.push(withEvidence(`Proof source catalog sitemap is missing required route: ${baseUrl}${proofSourceCatalogRoute}`, "sitemap.xml"));
  }
}

async function validateRoutesIndex({ dist, errors }) {
  const indexPath = resolve(dist, "routes-index.json");
  if (!(await fileExists(indexPath))) {
    return;
  }

  let parsed;
  try {
    parsed = JSON.parse(await readFile(indexPath, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Proof source catalog could not parse routes-index.json: ${error.message}`, "routes-index.json"));
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Proof source catalog routes-index.json is invalid: expected entries array", "routes-index.json"));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === proofSourceCatalogRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Proof source catalog routes-index.json is missing required route: ${proofSourceCatalogRoute}`, "routes-index.json"));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "site-page",
    preferredProofPath: "/proof-paths/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Proof source catalog routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, "routes-index.json"));
    }
  }
}

async function validateCatalogPage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const lowerDecodedHtml = decodedHtml.toLowerCase();
  const lowerPageText = pageText.toLowerCase();

  for (const phrase of requiredText) {
    if (!lowerPageText.includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Proof source catalog page is missing required text: ${phrase}`, "proof-source-catalog/index.html"));
    }
  }

  for (const link of proofSourceCatalogRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Proof source catalog page is missing required link: ${link}`, "proof-source-catalog/index.html"));
    }
  }

  for (const text of forbiddenPrivateText) {
    const lowerText = text.toLowerCase();
    if (lowerDecodedHtml.includes(lowerText) || lowerPageText.includes(lowerText)) {
      errors.push(withEvidence(`Proof source catalog page contains forbidden private text: ${text}`, "proof-source-catalog/index.html"));
    }
  }

  validateRows(html, errors);
  validateClaimLevelMapping(html, errors);
  validateEvidenceStatusMapping(html, errors);
  validateForbiddenProofLinks(html, errors);
}

function validateRows(html, errors) {
  const rows = extractRows(html, "data-proof-source-row");
  const ids = new Set();
  let hiddenRows = 0;

  if (rows.length === 0) {
    errors.push(withEvidence("Proof source catalog page has no data-proof-source-row entries.", "proof-source-catalog/index.html"));
    return;
  }

  for (const row of rows) {
    const id = getAttribute(row.attributes, "id");
    const fields = extractRowFields(row.html);

    if (!id) {
      errors.push(withEvidence("Proof source catalog row is missing a stable id.", "proof-source-catalog/index.html"));
    } else if (ids.has(id)) {
      errors.push(withEvidence(`Proof source catalog row id is duplicated: ${id}`, "proof-source-catalog/index.html"));
    } else {
      ids.add(id);
    }

    for (const field of requiredRowFields) {
      if (!fields[field] || fields[field].trim() === "") {
        errors.push(withEvidence(`Proof source catalog row ${id ?? "(missing id)"} is missing required field: ${field}`, "proof-source-catalog/index.html"));
      }
    }

    if (fields.limitation && countWords(fields.limitation) > rowWordLimit) {
      errors.push(withEvidence(`Proof source catalog row ${id} limitation exceeds ${rowWordLimit} words.`, "proof-source-catalog/index.html"));
    }

    if (fields.allowedPublicWording && countWords(fields.allowedPublicWording) > rowWordLimit) {
      errors.push(withEvidence(`Proof source catalog row ${id} allowed public wording exceeds ${rowWordLimit} words.`, "proof-source-catalog/index.html"));
    }

    const claimLevel = normalizeInlineValue(fields.publicClaimLevel);
    const evidenceStatus = normalizeInlineValue(fields.evidenceStatus);
    const route = normalizeInlineValue(fields.route);
    const proofPath = normalizeInlineValue(fields.proofPath);

    if (!claimLevels.has(claimLevel)) {
      errors.push(withEvidence(`Proof source catalog row ${id} has invalid Public claim level: ${String(claimLevel)}`, "proof-source-catalog/index.html"));
    }

    if (!publishedEvidenceStatuses.has(evidenceStatus)) {
      errors.push(withEvidence(`Proof source catalog row ${id} has invalid published evidence status: ${String(evidenceStatus)}`, "proof-source-catalog/index.html"));
    }

    if (claimLevels.has(claimLevel) && publishedEvidenceStatuses.has(evidenceStatus)) {
      const allowed = allowedPairs.get(claimLevel);
      if (!allowed?.has(evidenceStatus)) {
        errors.push(withEvidence(`Proof source catalog row ${id} has disallowed claim/evidence pair: ${claimLevel}/${evidenceStatus}`, "proof-source-catalog/index.html"));
      }
    }

    const isHiddenRowCandidate =
      id === hiddenAnchor || route === "hidden" || claimLevel === "hidden" || evidenceStatus === "hidden-or-internal";

    if (isHiddenRowCandidate && id !== hiddenAnchor) {
      errors.push(withEvidence(`Proof source catalog row ${id} uses hidden route or evidence outside the reserved placeholder.`, "proof-source-catalog/index.html"));
    }

    if (isHiddenRowCandidate) {
      hiddenRows += 1;
      validateHiddenRow({ fields, id, errors });
    }

    validateProofPath({ fields, id, rowHtml: row.html, errors });
    validateProofTrailDeferral({ fields, id, rowHtml: row.html, errors });
    validateAnchor({ fields, id, errors });
    validateAffirmativeClaimText({ fields, id, errors });
  }

  if (hiddenRows > 1) {
    errors.push(withEvidence(`Proof source catalog has more than one hidden aggregate row: ${hiddenRows}`, "proof-source-catalog/index.html"));
  }
}

function validateHiddenRow({ fields, id, errors }) {
  const expected = {
    route: "hidden",
    claimLabel: "Internal-only aggregate placeholder",
    allowedPublicWording: "none",
    publicClaimLevel: "hidden",
    evidenceStatus: "hidden-or-internal",
    proofPath: "hidden",
    sourceArtifactOrDoc: "hidden",
    ruleIdOrFamily: "none",
    evidenceTierOrCoverage: "hidden"
  };

  for (const [field, value] of Object.entries(expected)) {
    if (normalizeInlineValue(fields[field]) !== value) {
      errors.push(withEvidence(`Proof source catalog hidden row ${id} expected ${field} ${value}, got ${fields[field]}`, "proof-source-catalog/index.html"));
    }
  }

  const hiddenText = `${fields.limitation ?? ""} ${fields.nonClaims ?? ""}`.toLowerCase();
  if (/\b\d+\s+(hidden|internal|private|in-flight)\b/.test(hiddenText)) {
    errors.push(withEvidence("Proof source catalog hidden row discloses hidden/internal counts.", "proof-source-catalog/index.html"));
  }

  if (/\b(weekly|monthly|sprint|next|upcoming|phase\s+\d+)\b/.test(hiddenText)) {
    errors.push(withEvidence("Proof source catalog hidden row discloses cadence, sequencing, or in-flight status.", "proof-source-catalog/index.html"));
  }
}

function validateProofPath({ fields, id, rowHtml, errors }) {
  const proofPath = normalizeInlineValue(fields.proofPath);
  const proofPathHtml = extractRowFieldHtml(rowHtml, "proofPath");

  if (proofPath === "blocked-pending-validation" || proofPath === "not available") {
    errors.push(withEvidence(`Proof source catalog row ${id} uses forbidden proofPath sentinel: ${proofPath}`, "proof-source-catalog/index.html"));
    return;
  }

  if (allowedSentinelProofPaths.has(proofPath)) {
    return;
  }

  if (!/<a\b[^>]*\bhref\s*=/.test(proofPathHtml)) {
    errors.push(withEvidence(`Proof source catalog row ${id} proofPath must be a public-safe link or allowed sentinel.`, "proof-source-catalog/index.html"));
  }
}

function validateProofTrailDeferral({ fields, id, rowHtml, errors }) {
  const trailLikeText = `${fields.ruleIdOrFamily ?? ""} ${fields.evidenceTierOrCoverage ?? ""}`;
  if (!/(Tier[1-4]|FullEvidenceAvailable|PartialAnalysis|ReducedCoverage|not_requested|unavailable)/i.test(trailLikeText)) {
    return;
  }

  if (rowHtml.includes('href="/proof-paths/"') || /deferred to \/proof-paths\/|proof-path detail/i.test(fields.limitation ?? "")) {
    return;
  }

  errors.push(withEvidence(`Proof source catalog row ${id} includes evidence-trail detail without linking to /proof-paths/ or deferring trail detail.`, "proof-source-catalog/index.html"));
}

function validateAnchor({ fields, id, errors }) {
  if (!id) {
    return;
  }

  if (id === hiddenAnchor) {
    return;
  }

  const expected = `proof-source-${routeSlug(fields.route)}-${claimSlug(fields.claimLabel)}`;
  if (id !== expected) {
    errors.push(withEvidence(`Proof source catalog row anchor mismatch: expected ${expected}, got ${id}`, "proof-source-catalog/index.html"));
  }
}

function validateAffirmativeClaimText({ fields, id, errors }) {
  const claimText = `${fields.claimLabel ?? ""} ${fields.allowedPublicWording ?? ""}`;

  for (const pattern of forbiddenAffirmativeClaimPatterns) {
    if (pattern.test(claimText)) {
      errors.push(withEvidence(`Proof source catalog row ${id} contains forbidden affirmative claim wording: ${pattern}`, "proof-source-catalog/index.html"));
    }
  }

  if (/(^|[\s"'])\/Users\//i.test(claimText) || /(^|[\s"'])\/home\//i.test(claimText) || /[A-Za-z]:\\/.test(claimText) || /\\\\/.test(claimText)) {
    errors.push(withEvidence(`Proof source catalog row ${id} contains a private path indicator in affirmative claim text.`, "proof-source-catalog/index.html"));
  }
}

async function validateSurfaceStatusVocabulary({ catalogPagePath, dist, errors }) {
  const catalogHtml = await readFile(catalogPagePath, "utf8");
  const mappedVocabulary = collectClaimLevelVocabulary(catalogHtml);
  const statusSources = [
    ["capabilities", resolve(dist, "capabilities", "index.html")],
    ["roadmap", resolve(dist, "roadmap", "index.html")],
    ["proof-paths", resolve(dist, "proof-paths", "index.html")]
  ];
  const encountered = new Set();

  for (const [label, path] of statusSources) {
    if (!(await fileExists(path))) {
      continue;
    }

    const text = normalizeRenderedText(await readFile(path, "utf8"));
    for (const token of extractStatusTokens(text, label, errors)) {
      encountered.add(token);
    }
  }

  for (const token of encountered) {
    if (!mappedVocabulary.has(token)) {
      errors.push(withEvidence(`Proof source catalog status vocabulary is unmapped: ${token}`, "proof-source-catalog/index.html"));
    }
  }
}

function collectClaimLevelVocabulary(html) {
  const vocabulary = new Set();
  for (const row of extractRows(html, "data-proof-source-claim-level-map")) {
    const values = (getAttribute(row.attributes, "data-source-vocabulary") ?? "")
      .split("|")
      .map((value) => value.trim())
      .filter(Boolean);

    for (const value of values) {
      vocabulary.add(value);
    }
  }

  return vocabulary;
}

function extractStatusTokens(text, label, errors) {
  const tokens = new Set();
  const knownTokens = [
    "main with maturity caveats",
    "shipped navigation",
    "demo guidance",
    "main/demo",
    "future-only",
    "concept-only",
    "dev-only",
    "public-demo",
    "shipped",
    "concept",
    "future",
    "hidden",
    "demo",
    "main",
    "dev"
  ];

  for (const match of text.matchAll(/\b(?:Status|Public status):\s*([^.;]+)/gi)) {
    const phrase = match[1].trim().toLowerCase();
    let matched = false;

    if (phrase.includes("demo or future")) {
      tokens.add("demo");
      tokens.add("future");
      matched = true;
    }

    for (const token of knownTokens) {
      if (phrase.includes(token)) {
        tokens.add(token);
        matched = true;
      }
    }

    if (!matched) {
      errors.push(withEvidence(`Proof source catalog encountered unmapped ${label} status phrase: ${match[1].trim()}`, "proof-source-catalog/index.html"));
    }
  }

  if (/Rows use main, demo, dev, or future status language/i.test(text)) {
    tokens.add("main");
    tokens.add("demo");
    tokens.add("dev");
    tokens.add("future");
  }

  return tokens;
}

function validateClaimLevelMapping(html, errors) {
  const rows = extractRows(html, "data-proof-source-claim-level-map");
  const mappings = new Map();

  for (const row of rows) {
    const level = getAttribute(row.attributes, "data-catalog-level");
    const vocabulary = (getAttribute(row.attributes, "data-source-vocabulary") ?? "")
      .split("|")
      .map((value) => value.trim())
      .filter(Boolean);

    if (!claimLevels.has(level)) {
      errors.push(withEvidence(`Proof source catalog claim-level mapping has invalid level: ${String(level)}`, "proof-source-catalog/index.html"));
      continue;
    }

    mappings.set(level, new Set(vocabulary));
  }

  for (const [level, values] of requiredClaimMapping.entries()) {
    const mapped = mappings.get(level);
    if (!mapped) {
      errors.push(withEvidence(`Proof source catalog is missing claim-level mapping for ${level}`, "proof-source-catalog/index.html"));
      continue;
    }

    for (const value of values) {
      if (!mapped.has(value)) {
        errors.push(withEvidence(`Proof source catalog claim-level mapping for ${level} is missing source vocabulary: ${value}`, "proof-source-catalog/index.html"));
      }
    }
  }
}

function validateEvidenceStatusMapping(html, errors) {
  const rows = extractRows(html, "data-proof-source-evidence-status-map");
  const mapped = new Set();

  for (const row of rows) {
    const status = getAttribute(row.attributes, "data-evidence-status");
    if (!evidenceStatuses.has(status)) {
      errors.push(withEvidence(`Proof source catalog evidence-status mapping has invalid status: ${String(status)}`, "proof-source-catalog/index.html"));
      continue;
    }

    mapped.add(status);
  }

  for (const status of requiredEvidenceMapping) {
    if (!mapped.has(status)) {
      errors.push(withEvidence(`Proof source catalog evidence-status mapping is missing status: ${status}`, "proof-source-catalog/index.html"));
    }
  }
}

function validateForbiddenProofLinks(html, errors) {
  for (const href of extractHrefs(html)) {
    const lowerHref = href.toLowerCase();
    if (/\bfacts\.ndjson\b|\bindex\.sqlite\b|\blogs\/analyzer\.log\b|\banalyzer\.log\b|\bscan-manifest\.json\b|\breport\.md\b/.test(lowerHref)) {
      errors.push(withEvidence(`Proof source catalog links to forbidden raw proof artifact: ${href}`, "proof-source-catalog/index.html"));
    }
  }
}

function extractRows(html, marker) {
  const pattern = new RegExp(`<tr\\b(?=[^>]*\\b${escapeRegExp(marker)}\\b)([^>]*)>([\\s\\S]*?)<\\/tr>`, "gi");
  return [...html.matchAll(pattern)].map((match) => ({ attributes: match[1], html: match[2] }));
}

function extractRowFields(rowHtml) {
  const fields = {};
  const pattern = /<td\b(?=[^>]*\bdata-field\s*=\s*["']([^"']+)["'])[^>]*>([\s\S]*?)<\/td>/gi;

  for (const match of rowHtml.matchAll(pattern)) {
    fields[match[1]] = normalizeRenderedText(match[2]);
  }

  return fields;
}

function extractRowFieldHtml(rowHtml, field) {
  const escapedField = escapeRegExp(field);
  const pattern = new RegExp(`<td\\b(?=[^>]*\\bdata-field\\s*=\\s*["']${escapedField}["'])[^>]*>([\\s\\S]*?)<\\/td>`, "i");
  return rowHtml.match(pattern)?.[1] ?? "";
}

function extractHrefs(html) {
  return [...html.matchAll(/<a\b[^>]*\bhref\s*=\s*["']([^"']+)["'][^>]*>/gi)].map((match) => decodeHtmlEntities(match[1]));
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escaped}(?:#[^"']*)?["']`, "i").test(html);
}

function getAttribute(attributes, name) {
  const match = attributes.match(new RegExp(`\\b${escapeRegExp(name)}\\s*=\\s*["']([^"']*)["']`, "i"));
  return match ? decodeHtmlEntities(match[1]) : null;
}

function normalizeInlineValue(value) {
  return String(value ?? "").replace(/\s+/g, " ").trim();
}

function routeSlug(route) {
  let value = normalizeInlineValue(route)
    .replace(/^https?:\/\/[^/]+/i, "")
    .replace(/[?#].*$/, "")
    .replace(/\/index\/?$/, "")
    .replace(/^\/+|\/+$/g, "");

  if (value === "") {
    value = "home";
  }

  value = value.replace(/\/+/g, "-").toLowerCase();
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(value)) {
    throw new Error(`Proof source catalog route slug is invalid for route: ${route}`);
  }

  return value;
}

function claimSlug(label) {
  const value = normalizeInlineValue(label)
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .replace(/-+/g, "-");

  if (value === "") {
    throw new Error(`Proof source catalog claim slug is empty for label: ${label}`);
  }

  return value;
}

function countWords(value) {
  return (String(value).match(/\b[\w'-]+\b/g) ?? []).length;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
