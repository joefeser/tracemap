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

export const roadmapClaimLedgerRoute = "/roadmap/";
export const roadmapClaimLedgerRequiredLinks = [
  "/proof-paths/",
  "/capabilities/",
  "/demo/proof-upgrades/",
  "/limitations/"
];

const claimLevels = new Set(["shipped", "demo", "concept", "hidden"]);
const evidenceStatuses = new Set([
  "evidence-backed",
  "partial/reduced coverage",
  "gap-labeled demo evidence",
  "future-only",
  "hidden/internal",
  "not-yet-backed"
]);
const wordingStatuses = new Set([
  "live",
  "demo-only",
  "future-facing",
  "hidden-from-public-navigation",
  "forbidden"
]);

const requiredText = [
  "Public claim level: concept",
  "Claim ledger",
  "Public wording status",
  "Source-of-truth artifact family",
  "SQLite indexes, fact streams, reports, rule catalog entries, commit metadata, coverage labels, and documented limitations",
  "presentation and governance layer",
  "runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, AI impact analysis, LLM analysis, and complete product coverage wording is forbidden",
  "No public conclusion without evidence"
];

const forbiddenPrivateText = [
  "/Users/",
  "C:\\",
  "file://",
  "localhost",
  "127.0.0.1",
  "ConnectionString",
  "connection string",
  "Server=",
  "User Id=",
  "Password="
];

const forbiddenProofPathTargets = [
  "facts.ndjson",
  "index.sqlite",
  "logs/analyzer.log",
  "analyzer.log",
  "scan-manifest.json",
  "report.md"
];

export async function validateRoadmapClaimLedgerDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "roadmap", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence("Roadmap claim ledger page is missing required public route: /roadmap/", "roadmap/index.html"));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  await validateRoutesIndex({ dist, errors: localErrors });
  await validateRoadmapPage({ pagePath, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${roadmapClaimLedgerRoute}`)) {
    errors.push(withEvidence(`Roadmap claim ledger sitemap is missing required route: ${baseUrl}${roadmapClaimLedgerRoute}`, "sitemap.xml"));
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
    errors.push(withEvidence(`Roadmap claim ledger could not parse routes-index.json: ${error.message}`, "routes-index.json"));
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Roadmap claim ledger routes-index.json is invalid: expected entries array", "routes-index.json"));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === roadmapClaimLedgerRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Roadmap claim ledger routes-index.json is missing required route: ${roadmapClaimLedgerRoute}`, "routes-index.json"));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "concept",
    hintCategory: "roadmap",
    sourceType: "site-page",
    preferredProofPath: "/proof-paths/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Roadmap claim ledger routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, "routes-index.json"));
    }
  }
}

async function validateRoadmapPage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const lowerPageText = pageText.toLowerCase();

  for (const phrase of requiredText) {
    if (!lowerPageText.includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Roadmap claim ledger page is missing required text: ${phrase}`, "roadmap/index.html"));
    }
  }

  for (const link of roadmapClaimLedgerRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Roadmap claim ledger page is missing required link: ${link}`, "roadmap/index.html"));
    }
  }

  for (const text of forbiddenPrivateText) {
    if (html.includes(text) || decodedHtml.includes(text) || pageText.includes(text)) {
      errors.push(withEvidence(`Roadmap claim ledger page contains forbidden private text: ${text}`, "roadmap/index.html"));
    }
  }

  validateClaimRows(html, errors);
  validateMappingRows(html, errors);
  validateProofLinks(html, errors);
}

function validateClaimRows(html, errors) {
  const rows = extractRows(html, "data-claim-row");
  if (rows.length === 0) {
    errors.push(withEvidence("Roadmap claim ledger page has no data-claim-row entries.", "roadmap/index.html"));
    return;
  }

  const seenClaimLevels = new Set();
  const seenEvidenceStatuses = new Set();
  const seenWordingStatuses = new Set();
  const ids = new Set();

  for (const row of rows) {
    const id = getAttribute(row.attributes, "id");
    const claimLevel = getAttribute(row.attributes, "data-claim-level");
    const evidenceStatus = getAttribute(row.attributes, "data-evidence-status");
    const wordingStatus = getAttribute(row.attributes, "data-wording-status");

    if (!id) {
      errors.push(withEvidence("Roadmap claim ledger row is missing a stable id.", "roadmap/index.html"));
    } else if (ids.has(id)) {
      errors.push(withEvidence(`Roadmap claim ledger row id is duplicated: ${id}`, "roadmap/index.html"));
    } else {
      ids.add(id);
    }

    if (!claimLevels.has(claimLevel)) {
      errors.push(withEvidence(`Roadmap claim ledger row has unmapped claim level: ${String(claimLevel)}`, "roadmap/index.html"));
    } else {
      seenClaimLevels.add(claimLevel);
    }

    if (!evidenceStatuses.has(evidenceStatus)) {
      errors.push(withEvidence(`Roadmap claim ledger row has unmapped evidence status: ${String(evidenceStatus)}`, "roadmap/index.html"));
    } else {
      seenEvidenceStatuses.add(evidenceStatus);
    }

    if (!wordingStatuses.has(wordingStatus)) {
      errors.push(withEvidence(`Roadmap claim ledger row has unmapped public wording status: ${String(wordingStatus)}`, "roadmap/index.html"));
    } else {
      seenWordingStatuses.add(wordingStatus);
    }
  }

  requireSetCoverage({ errors, label: "claim level", required: claimLevels, seen: seenClaimLevels });
  requireSetCoverage({ errors, label: "evidence status", required: evidenceStatuses, seen: seenEvidenceStatuses });
  requireSetCoverage({ errors, label: "public wording status", required: wordingStatuses, seen: seenWordingStatuses });
}

function validateMappingRows(html, errors) {
  const rows = extractRows(html, "data-mapping-row");
  if (rows.length === 0) {
    errors.push(withEvidence("Roadmap claim ledger page has no data-mapping-row entries.", "roadmap/index.html"));
    return;
  }

  const mappedClaimLevels = new Set();
  const mappedEvidenceStatuses = new Set();

  for (const row of rows) {
    const axis = getAttribute(row.attributes, "data-mapping-axis");
    const label = getAttribute(row.attributes, "data-ledger-label");

    if (axis === "claim-level") {
      mappedClaimLevels.add(label);
    } else if (axis === "evidence-status") {
      mappedEvidenceStatuses.add(label);
    } else {
      errors.push(withEvidence(`Roadmap claim ledger mapping row has invalid axis: ${String(axis)}`, "roadmap/index.html"));
    }
  }

  requireSetCoverage({ errors, label: "mapped claim level", required: claimLevels, seen: mappedClaimLevels });
  requireSetCoverage({ errors, label: "mapped evidence status", required: evidenceStatuses, seen: mappedEvidenceStatuses });
}

function validateProofLinks(html, errors) {
  for (const href of extractHrefs(html)) {
    const normalizedHref = href.toLowerCase();
    for (const target of forbiddenProofPathTargets) {
      if (normalizedHref.includes(target.toLowerCase())) {
        errors.push(withEvidence(`Roadmap claim ledger links to forbidden raw proof artifact: ${href}`, "roadmap/index.html"));
      }
    }
  }
}

function requireSetCoverage({ errors, label, required, seen }) {
  for (const value of required) {
    if (!seen.has(value)) {
      errors.push(withEvidence(`Roadmap claim ledger is missing ${label}: ${value}`, "roadmap/index.html"));
    }
  }
}

function extractRows(html, marker) {
  const pattern = new RegExp(`<tr\\b(?=[^>]*\\b${escapeRegExp(marker)}\\b)([^>]*)>[\\s\\S]*?<\\/tr>`, "gi");
  return [...html.matchAll(pattern)].map((match) => ({ attributes: match[1] }));
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

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
