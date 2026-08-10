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

export const accessSafeEvidenceAcquisitionSlug = "reverse-engineering-access-without-running-it";
export const accessSafeEvidenceAcquisitionRoute = `/blog/${accessSafeEvidenceAcquisitionSlug}/`;
export const accessSafeEvidenceAcquisitionRequiredLinks = [
  "/evidence/",
  "/evidence/gaps/",
  "/proof-paths/for-managers/",
  "/static-vs-runtime/",
  "/capabilities/",
  "/use-cases/change-review/",
  "/limitations/"
];

const pageArtifact = `blog/${accessSafeEvidenceAcquisitionSlug}/index.html`;
const requiredBlocks = [
  "threat-boundary",
  "file-first",
  "provenance",
  "protected-design",
  "vba-lane",
  "evidence-contract",
  "review-handoff",
  "non-claims",
  "bottom-line"
];
const requiredText = [
  "Public claim level: demo",
  "local-file-snapshot",
  "legacy.access.database.inventory.v1",
  "legacy.access.design-input.v1",
  "legacy.access.coverage-gap.v1",
  "Tier 2",
  "Tier 4",
  "generic database name",
  "no-remote disposable repository",
  "startup behavior is suppressed",
  "force-disabled",
  "canaries",
  "source hashes",
  "fail",
  "unable to prove"
];
const forbiddenClaims = [
  /\bTraceMap\b[^.]{0,140}\b(?:ran|rendered|queried|reconstructed)\b[^.]{0,140}\b(?:application|database)\b/i,
  /\b(?:runtime execution|event firing|production behavior|complete coverage|data correctness|effective permissions)\b[^.]{0,100}\b(?:is|are|was|were)\s+(?:proved|proven|verified|validated|established)\b/i,
  /\b(?:safe to run|safe to release|approved for release|reconstruction succeeded|validation passed)\b/i
];
const rawMaterialPatterns = [
  /\bSELECT\s+.+\bFROM\b/i,
  /\b(?:CREATE|ALTER|DROP|GRANT|REVOKE)\s+(?:TABLE|VIEW|USER|ROLE|DATABASE|PROCEDURE|FUNCTION)\b/i,
  /\b(?:Sub|Function)\s+[A-Za-z_]\w*\s*\(/i,
  /\b(?:scheduled command|macro|VBA)\s+body\s*:/i,
  /\bServer\s*=/i,
  /\bUser Id\s*=/i,
  /\bPassword\s*=/i,
  /\bConnectionString\s*=/i
];
const hardPrivatePatterns = [
  /\/Users\//i,
  /\/private\//i,
  /\/home\//i,
  /\/tmp\//i,
  /\/var\/folders\//i,
  /~\//,
  /\bC:\\/i,
  /\bfile:\/\//i,
  /\bgit@/i,
  /\bsk-[A-Za-z0-9_-]{12,}\b/i
];

export async function validateAccessSafeEvidenceAcquisitionDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "blog", accessSafeEvidenceAcquisitionSlug, "index.html");

  if (!(await fileExists(pagePath))) {
    errors.push(withEvidence(`Access acquisition article is missing required route: ${accessSafeEvidenceAcquisitionRoute}`, pageArtifact));
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  await validateBlogIndex({ dist, errors: localErrors });
  await validateDiscovery({ dist, errors: localErrors });
  await validateArticle({ pagePath, errors: localErrors });
  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) return;
  const urls = await readSitemapLocSet(sitemapPath);
  if (!urls.has(`${baseUrl}${accessSafeEvidenceAcquisitionRoute}`)) {
    errors.push(withEvidence(`Access acquisition sitemap is missing required route: ${baseUrl}${accessSafeEvidenceAcquisitionRoute}`, "sitemap.xml"));
  }
}

async function validateBlogIndex({ dist, errors }) {
  const path = resolve(dist, "blog", "index.html");
  if (!(await fileExists(path))) {
    errors.push(withEvidence("Access acquisition blog index is missing.", "blog/index.html"));
    return;
  }
  const html = await readFile(path, "utf8");
  if (!hasHref(html, accessSafeEvidenceAcquisitionRoute)) {
    errors.push(withEvidence(`Access acquisition blog index is missing article link: ${accessSafeEvidenceAcquisitionRoute}`, "blog/index.html"));
  }
}

async function validateDiscovery({ dist, errors }) {
  const path = resolve(dist, "routes-index.json");
  if (!(await fileExists(path))) {
    errors.push(withEvidence("Access acquisition routes discovery output is missing.", "routes-index.json"));
    return;
  }
  const entries = JSON.parse(await readFile(path, "utf8")).entries ?? [];
  const entry = entries.find((candidate) => candidate.path === accessSafeEvidenceAcquisitionRoute);
  if (!entry) {
    errors.push(withEvidence("Access acquisition discovery entry is missing.", "routes-index.json"));
    return;
  }
  if (entry.publicClaimLevel !== "demo") errors.push(withEvidence("Access acquisition discovery claim level must be demo.", "routes-index.json"));
  if (entry.preferredProofPath !== "/evidence/") errors.push(withEvidence("Access acquisition preferred proof path must remain /evidence/.", "routes-index.json"));
  if (!Array.isArray(entry.limitations) || entry.limitations.length < 2) errors.push(withEvidence("Access acquisition discovery must include at least two limitations.", "routes-index.json"));
  if (!Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) errors.push(withEvidence("Access acquisition discovery must include at least two non-claims.", "routes-index.json"));
}

async function validateArticle({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decoded = decodeHtmlEntities(html);
  const rendered = normalizeRenderedText(html);
  const bounded = stripNonClaimBoundary(decoded);
  const boundedText = normalizeRenderedText(bounded);

  if (!html.includes("<title>Reverse Engineering Access Without Running It | TraceMap</title>")) {
    errors.push(withEvidence("Access acquisition article is missing expected title.", pageArtifact));
  }
  if (!new RegExp(`<link\\b[^>]*rel=["']canonical["'][^>]*href=["']https://tracemap\\.tools${escapeRegExp(accessSafeEvidenceAcquisitionRoute)}["']`, "i").test(html)) {
    errors.push(withEvidence("Access acquisition article canonical URL is missing or incorrect.", pageArtifact));
  }
  for (const block of requiredBlocks) {
    if (!new RegExp(`<section\\b[^>]*data-access-acquisition-block=["']${escapeRegExp(block)}["']`, "i").test(html)) {
      errors.push(withEvidence(`Access acquisition article is missing required block: ${block}`, pageArtifact));
    }
  }
  for (const phrase of requiredText) {
    if (!rendered.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Access acquisition article is missing required text: ${phrase}`, pageArtifact));
    }
  }
  for (const link of accessSafeEvidenceAcquisitionRequiredLinks) {
    if (!hasHref(html, link)) errors.push(withEvidence(`Access acquisition article is missing required link: ${link}`, pageArtifact));
  }
  const words = rendered.split(/\s+/).filter(Boolean).length;
  if (words < 900 || words > 1800) errors.push(withEvidence(`Access acquisition article word count must be between 900 and 1800 words, got ${words}`, pageArtifact));
  for (const pattern of forbiddenClaims) {
    if (pattern.test(boundedText)) errors.push(withEvidence(`Access acquisition article contains unsupported positive claim: ${pattern}`, pageArtifact));
  }
  for (const pattern of rawMaterialPatterns) {
    if (pattern.test(boundedText)) errors.push(withEvidence(`Access acquisition article contains raw or executable material: ${pattern}`, pageArtifact));
  }
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(decoded)) errors.push(withEvidence(`Access acquisition article contains hard private material: ${pattern}`, pageArtifact));
  }
}

function stripNonClaimBoundary(html) {
  return html.replace(/<section\b[^>]*data-access-acquisition-boundary=["']non-claims["'][^>]*>[\s\S]*?<\/section>/gi, "");
}

function hasHref(html, href) {
  return new RegExp(`<a\\b[^>]*href\\s*=\\s*["']${escapeRegExp(href)}["'][^>]*>`, "i").test(html);
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
