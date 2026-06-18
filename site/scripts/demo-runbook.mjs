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

export const demoRunbookRoute = "/demo/runbook/";
export const demoRunbookRequiredLinks = [
  "/demo/start-here/",
  "/demo/result/",
  "/demo/evidence-trail/",
  "/demo/proof-upgrades/",
  "/proof-paths/",
  "/validation/",
  "/limitations/"
];
export const demoRunbookInboundLinkRoutes = [
  "/demo/",
  "/demo/start-here/",
  "/demo/result/",
  "/demo/evidence-trail/",
  "/demo/proof-upgrades/",
  "/proof-paths/",
  "/validation/",
  "/limitations/"
];

const pageArtifact = "demo/runbook/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

const requiredText = [
  "Public claim level: demo",
  "No public conclusion without evidence",
  "operator checklist",
  "Follow the evidence",
  "scripts/demo-public.sh",
  "<ignored-output-dir>",
  "./scripts/check-private-paths.sh",
  "public.demo.summary.v1",
  "Tier1Semantic",
  "Tier2Structural",
  "Tier3SyntaxOrTextual",
  "Tier4Unknown",
  "PartialAnalysis",
  "not_requested",
  "unavailable",
  "gap-labeled row: partial coverage, no clean reducer conclusion"
];

const artifactFamilyPatterns = [
  /\bfacts\.ndjson\b/i,
  /\bindex\.sqlite\b/i,
  /\blogs\/analyzer\.log\b/i,
  /\banalyzer\.log\b/i,
  /\breport\.md\b/i,
  /\bscan-manifest\.json\b/i,
  /\b[a-z0-9._/-]+\.ndjson\b/i,
  /\b[a-z0-9._/-]+\.sqlite\b/i
];

const rawCategoryPatterns = [
  /\braw SQL\b/i,
  /\bconfig values\b/i,
  /\bsecrets\b/i,
  /\bgenerated scan directories\b/i,
  /\bprivate sample names\b/i,
  /\braw source snippets\b/i,
  /\braw repository remotes\b/i,
  /\blocal absolute paths\b/i
];

const forbiddenPrivatePatterns = [
  { label: "home directory path", pattern: /(?:^|[\s"'(])(?:\/Users\/|\/home\/|~\/)[^\s<>"']*/i },
  { label: "Windows user directory path", pattern: /[A-Z]:\\Users\\/i },
  { label: "file URL", pattern: /\bfile:\/\//i },
  { label: "localhost", pattern: /\blocalhost\b/i },
  { label: "loopback address", pattern: /\b127\.0\.0\.1\b/ },
  { label: "generated scan root", pattern: /(?:^|[\s"'(])\.tracemap(?:[/?\s"'<)]|$)/i },
  { label: "connection string Server fragment", pattern: /\bServer\s*=/i },
  { label: "connection string Password fragment", pattern: /\bPassword\s*=/i },
  { label: "connection string User Id fragment", pattern: /\bUser\s+Id\s*=/i },
  { label: "connection string keyword", pattern: /\bConnectionString\b/i },
  { label: "raw SQL statement", pattern: /\bSELECT\s+.+\s+FROM\b/i },
  { label: "raw SQL statement", pattern: /\b(?:INSERT\s+INTO|UPDATE\s+\w+\s+SET|DELETE\s+FROM)\b/i },
  { label: "raw git remote", pattern: /\bgit@[\w.-]+:/i },
  { label: "raw ssh remote", pattern: /\bssh:\/\/[^\s<>"']+/i },
  { label: "raw https git remote", pattern: /\bhttps:\/\/[^/\s<>"']+\/[^/\s<>"']+\/[^/\s<>"']+\.git\b/i }
];

export const demoRunbookForbiddenPositioning =
  /\b(AI[- ]?powered|AI impact analysis|LLM[- ]?powered|LLM analysis|machine learning impact analysis|artificial intelligence impact analysis|intelligent analysis|smart impact)\b/i;

const forbiddenOperationalOverclaims = [
  /\bproves?\s+(?:runtime behavior|production traffic|endpoint performance|outage cause|release safety|operational safety|complete product coverage)\b/i,
  /\bcertif(?:y|ies|ied)\s+operational safety\b/i,
  /\bapproves?\s+a?\s*release\b/i,
  /\bproduction[- ]verified\b/i,
  /\bruntime[- ]safe\b/i,
  /\brelease[- ]safe\b/i,
  /\bcomplete product coverage\b/i
];

const impactedAssertionPattern = /\b(?:demo row|row|surface|endpoint|route)\b[^.]{0,80}\bimpacted\b/i;

export async function validateDemoRunbookDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeDemoRunbookBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "demo", "runbook", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence("Demo runbook page is missing required public route: /demo/runbook/", pageArtifact));
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }
  await validateRoutesIndex({ dist, errors: localErrors });
  await validateInboundLinks({ dist, errors: localErrors });

  const html = await readFile(pagePath, "utf8");
  validateRunbookPage({ html, errors: localErrors });

  errors.push(...localErrors);
}

function validateRunbookPage({ html, errors }) {
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const fullText = `${html} ${decodedHtml} ${pageText}`;
  const unsanctionedHtml = stripSanctionedSections(decodedHtml);
  const unsanctionedText = normalizeRenderedText(unsanctionedHtml);
  const fullUnsanctionedText = `${unsanctionedHtml} ${unsanctionedText}`;

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase) && !decodedHtml.includes(phrase) && !html.includes(phrase)) {
      errors.push(withEvidence(`Demo runbook page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const link of demoRunbookRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Demo runbook page is missing required link: ${link}`, pageArtifact));
    }
  }

  if (!hasHref(html, "https://github.com/joefeser/tracemap/blob/main/scripts/demo-public.sh")) {
    errors.push(withEvidence("Demo runbook page is missing the public demo script source link.", pageArtifact));
  }

  if (!hasOgTypeArticle(html)) {
    errors.push(withEvidence('Demo runbook page must include <meta property="og:type" content="article">.', pageArtifact));
  }

  validateArtifactVocabulary({ errors, html: decodedHtml });
  validateForbiddenPrivateText({ errors, fullText });
  validatePositioning({ errors, text: fullUnsanctionedText });
}

function validateArtifactVocabulary({ errors, html }) {
  const unsanctionedHtml = stripSanctionedSections(html);

  for (const pattern of [...artifactFamilyPatterns, ...rawCategoryPatterns]) {
    const match = unsanctionedHtml.match(pattern);
    if (match) {
      errors.push(withEvidence(`Demo runbook page contains artifact-boundary vocabulary outside sanctioned sections: ${match[0]}`, pageArtifact));
    }
  }
}

function validateForbiddenPrivateText({ errors, fullText }) {
  for (const { label, pattern } of forbiddenPrivatePatterns) {
    if (pattern.test(fullText)) {
      errors.push(withEvidence(`Demo runbook page contains forbidden private/raw text: ${label}`, pageArtifact));
    }
  }
}

function validatePositioning({ errors, text }) {
  if (demoRunbookForbiddenPositioning.test(text)) {
    errors.push(withEvidence("Demo runbook page contains forbidden AI/LLM positioning outside sanctioned red-flag or non-claim sections.", pageArtifact));
  }

  for (const pattern of forbiddenOperationalOverclaims) {
    const match = text.match(pattern);
    if (match) {
      errors.push(withEvidence(`Demo runbook page contains forbidden operational overclaim outside sanctioned sections: ${match[0]}`, pageArtifact));
    }
  }

  const impactedMatch = text.match(impactedAssertionPattern);
  if (impactedMatch) {
    errors.push(withEvidence(`Demo runbook page contains unsupported impacted assertion outside sanctioned guidance: ${impactedMatch[0]}`, pageArtifact));
  }
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${demoRunbookRoute}`)) {
    errors.push(withEvidence(`Demo runbook sitemap is missing required route: ${baseUrl}${demoRunbookRoute}`, sitemapArtifact));
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
    errors.push(withEvidence(`Demo runbook could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Demo runbook routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return;
  }

  const routeEntry = parsed.entries.find((entry) => entry?.path === demoRunbookRoute);
  if (!routeEntry) {
    errors.push(withEvidence(`Demo runbook routes-index.json is missing required route: ${demoRunbookRoute}`, routesIndexArtifact));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "demo",
    hintCategory: "demo",
    sourceType: "site-page",
    preferredProofPath: "/proof-paths/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Demo runbook routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  for (const field of ["title", "summary"]) {
    if (typeof routeEntry[field] !== "string" || routeEntry[field].trim() === "") {
      errors.push(withEvidence(`Demo runbook routes-index.json is missing non-empty ${field}.`, routesIndexArtifact));
    }
  }

  for (const field of ["limitations", "nonClaims"]) {
    if (!Array.isArray(routeEntry[field]) || routeEntry[field].length === 0) {
      errors.push(withEvidence(`Demo runbook routes-index.json is missing non-empty ${field}.`, routesIndexArtifact));
    }
  }
}

async function validateInboundLinks({ dist, errors }) {
  for (const route of demoRunbookInboundLinkRoutes) {
    const pagePath = route === "/" ? resolve(dist, "index.html") : resolve(dist, route.replace(/^\/|\/$/g, ""), "index.html");

    if (!(await fileExists(pagePath))) {
      errors.push(withEvidence(`Demo runbook inbound-link source route is missing: ${route}`, route));
      continue;
    }

    const html = await readFile(pagePath, "utf8");
    if (!hasHref(html, demoRunbookRoute)) {
      errors.push(withEvidence(`Required inbound route ${route} does not link to ${demoRunbookRoute}`, route));
    }
  }
}

function stripSanctionedSections(html) {
  return html.replace(
    /<section\b(?=[^>]*\bdata-runbook-section\s*=\s*["'](?:artifact-boundary|sharing-guidance|red-flag)["'])[^>]*>[\s\S]*?<\/section>/gi,
    " "
  );
}

function normalizeDemoRunbookBaseUrl(value, errors) {
  try {
    return normalizeBaseUrl(new URL(value).origin);
  } catch {
    errors.push(withEvidence(`Demo runbook baseUrl must be a valid absolute URL: ${String(value)}`, "baseUrl input"));
    return null;
  }
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasOgTypeArticle(html) {
  return /<meta\b(?=[^>]*\bproperty\s*=\s*["']og:type["'])(?=[^>]*\bcontent\s*=\s*["']article["'])[^>]*>/i.test(html);
}
