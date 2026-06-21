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

export const managerDemoScriptRoute = "/demo/manager-script/";

export const managerDemoScriptRequiredLinks = [
  "/",
  "/capabilities/",
  "/proof-paths/",
  "/proof-source-catalog/",
  "/demo/result/",
  "/demo/runbook/",
  "/questions/",
  "/limitations/",
  "/validation/",
  "/static-vs-runtime/"
];

export const managerDemoScriptInboundLinkRoutes = ["/demo/"];

const requiredRouteLevels = new Map([
  ["/", "demo"],
  ["/capabilities/", "demo"],
  ["/proof-paths/", "demo"],
  ["/proof-source-catalog/", "demo"],
  ["/demo/result/", "demo"],
  ["/demo/runbook/", "demo"],
  ["/questions/", "concept"],
  ["/limitations/", "demo"],
  ["/validation/", "demo"],
  ["/static-vs-runtime/", "concept"]
]);

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "bounded demo script, not a product capability proof",
  "Opening context",
  "2-minute tour",
  "5-minute proof walkthrough",
  "Manager questions and safe answer shapes",
  "Engineer questions and proof routes",
  "Stop conditions",
  "Follow-up handoff",
  "Non-claims",
  "rule ID or rule family",
  "evidence tier",
  "coverage label",
  "proof path",
  "limitation",
  "raw facts, SQLite content, analyzer logs"
];

const managerQuestionFamilies = [
  "value",
  "trust",
  "completeness",
  "release-decision",
  "production-behavior",
  "incident-use",
  "team-handoff",
  "next"
];

const engineerQuestionText = [
  "Where are the rule IDs and evidence tiers?",
  "How does source mapping stay public-safe?",
  "What does the demo result status mean?",
  "Where do validation and static-versus-runtime boundaries live?",
  "What stays out of public copy?"
];

const privateRawPatterns = [
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
  {
    label: "raw SQL statement",
    pattern: /\bSELECT\s+(?:\*|[A-Z_][\w."]*(?:\s*,\s*[A-Z_][\w."]*)*)\s+FROM\s+[A-Z_][\w."]*\b/i
  },
  { label: "raw SQL statement", pattern: /\b(?:INSERT\s+INTO|UPDATE\s+\w+\s+SET|DELETE\s+FROM)\b/i },
  { label: "raw git remote", pattern: /\bgit@[\w.-]+:/i },
  { label: "raw ssh remote", pattern: /\bssh:\/\/[^\s<>"']+/i },
  { label: "raw https git remote", pattern: /\bhttps:\/\/[^/\s<>"']+\/[^/\s<>"']+\/[^/\s<>"']+\.git\b/i }
];

const forbiddenPositioningPattern =
  /\b(AI[- ]?powered|LLM[- ]?powered|machine learning impact analysis|artificial intelligence impact analysis|intelligent analysis|smart impact|autonomous approval)\b/i;

const forbiddenProofClaimPattern =
  /\b(?:TraceMap\s+)?(?:proves?|guarantees?|certif(?:y|ies|ied)|approves?|diagnoses?|resolves?|replaces?)\s+(?:runtime behavior|production traffic|production behavior|endpoint performance|outage cause|release safety|operational safety|release approval|complete coverage|complete dependency understanding|incident(?:s)?|human judgment)\b/i;

const unsupportedConclusionPattern =
  /\b(?:TraceMap|demo|script|result|route|claim|evidence)\b[^.]{0,80}\b(?:is|are|was|were|be|being|becomes|marked|classified|called)\s+(?:impacted|safe|unsafe|approved|blocked|root cause|validated for release|production proven)\b/i;

const pageArtifact = "demo/manager-script/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

export async function validateManagerDemoScriptDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeManagerDemoScriptBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "demo", "manager-script", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence("Manager demo script page is missing required public route: /demo/manager-script/", pageArtifact));
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }

  await validateRoutesIndex({ dist, errors: localErrors });
  await validateInboundLinks({ dist, errors: localErrors });
  await validateTargetRoutes({ dist, errors: localErrors });
  await validateManagerDemoScriptPage({ pagePath, errors: localErrors });

  errors.push(...localErrors);
}

async function validateManagerDemoScriptPage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase) && !decodedHtml.includes(phrase) && !html.includes(phrase)) {
      errors.push(withEvidence(`Manager demo script page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const link of managerDemoScriptRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Manager demo script page is missing required link: ${link}`, pageArtifact));
    }
  }

  for (const family of managerQuestionFamilies) {
    if (!new RegExp(`data-question-family\\s*=\\s*["']${escapeRegExp(family)}["']`, "i").test(html)) {
      errors.push(withEvidence(`Manager demo script page is missing manager question family: ${family}`, pageArtifact));
    }
  }

  for (const phrase of engineerQuestionText) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Manager demo script page is missing engineer question: ${phrase}`, pageArtifact));
    }
  }

  if (!hasOgTypeArticle(html)) {
    errors.push(withEvidence('Manager demo script page must include <meta property="og:type" content="article">.', pageArtifact));
  }

  if (wordCount < 900 || wordCount > 2400) {
    errors.push(withEvidence(`Manager demo script page word count must be between 900 and 2400 words, got ${wordCount}`, pageArtifact));
  }

  validateMetadataLength({ html, errors });
  validateForbiddenPrivateText({ errors, text: `${html} ${decodedHtml} ${pageText}` });
  validatePositioning({ errors, html: decodedHtml });
}

function validateMetadataLength({ html, errors }) {
  const ogTitle = readMetaContent(html, "property", "og:title");
  const ogDescription = readMetaContent(html, "property", "og:description");

  if (!ogTitle || ogTitle.length > 70) {
    errors.push(withEvidence(`Manager demo script social title must be present and at most 70 characters, got ${ogTitle?.length ?? 0}`, pageArtifact));
  }

  if (!ogDescription || ogDescription.length > 160) {
    errors.push(
      withEvidence(
        `Manager demo script social description must be present and at most 160 characters, got ${ogDescription?.length ?? 0}`,
        pageArtifact
      )
    );
  }
}

function validateForbiddenPrivateText({ errors, text }) {
  for (const { label, pattern } of privateRawPatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Manager demo script page contains forbidden private/raw text: ${label}`, pageArtifact));
    }
  }
}

function validatePositioning({ errors, html }) {
  const unsanctionedHtml = stripSanctionedSections(html);
  const unsanctionedText = normalizeRenderedText(unsanctionedHtml);
  const unsanctioned = normalizeOverclaimText(`${unsanctionedHtml} ${unsanctionedText}`);

  if (forbiddenPositioningPattern.test(unsanctioned)) {
    errors.push(withEvidence("Manager demo script page contains forbidden AI/LLM positioning outside non-claim sections.", pageArtifact));
  }

  const proofMatch = unsanctioned.match(forbiddenProofClaimPattern);
  if (proofMatch) {
    errors.push(withEvidence(`Manager demo script page contains forbidden proof claim outside non-claim sections: ${proofMatch[0]}`, pageArtifact));
  }

  const conclusionMatch = unsanctioned.match(unsupportedConclusionPattern);
  if (conclusionMatch) {
    errors.push(withEvidence(`Manager demo script page contains unsupported conclusion outside stop/non-claim sections: ${conclusionMatch[0]}`, pageArtifact));
  }
}

function normalizeOverclaimText(value) {
  return value.replace(/\bpublic-safe\b/gi, "public evidence");
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${managerDemoScriptRoute}`)) {
    errors.push(withEvidence(`Manager demo script sitemap is missing required route: ${baseUrl}${managerDemoScriptRoute}`, sitemapArtifact));
  }
}

async function validateRoutesIndex({ dist, errors }) {
  const routeEntry = await readRouteEntry({ dist, errors, route: managerDemoScriptRoute });
  if (!routeEntry) {
    return;
  }

  const expectedFields = {
    publicClaimLevel: "concept",
    hintCategory: "demo",
    sourceType: "site-page",
    preferredProofPath: "/proof-paths/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Manager demo script routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  for (const field of ["title", "summary"]) {
    if (typeof routeEntry[field] !== "string" || routeEntry[field].trim() === "") {
      errors.push(withEvidence(`Manager demo script routes-index.json is missing non-empty ${field}.`, routesIndexArtifact));
    }
  }

  for (const field of ["limitations", "nonClaims"]) {
    if (!Array.isArray(routeEntry[field]) || routeEntry[field].length === 0) {
      errors.push(withEvidence(`Manager demo script routes-index.json is missing non-empty ${field}.`, routesIndexArtifact));
    }
  }
}

async function validateTargetRoutes({ dist, errors }) {
  for (const [route, expectedClaimLevel] of requiredRouteLevels.entries()) {
    const targetPath = route === "/" ? resolve(dist, "index.html") : resolve(dist, route.replace(/^\/|\/$/g, ""), "index.html");
    if (!(await fileExists(targetPath))) {
      errors.push(withEvidence(`Manager demo script required target route is missing from generated output: ${route}`, pageArtifact));
      continue;
    }

    const routeEntry = await readRouteEntry({ dist, errors, route });
    if (!routeEntry) {
      errors.push(withEvidence(`Manager demo script required target route is missing from routes-index.json: ${route}`, routesIndexArtifact));
      continue;
    }

    if (routeEntry.publicClaimLevel !== expectedClaimLevel) {
      errors.push(
        withEvidence(
          `Manager demo script expected target ${route} publicClaimLevel ${expectedClaimLevel}, got ${String(routeEntry.publicClaimLevel)}`,
          routesIndexArtifact
        )
      );
    }
  }
}

async function validateInboundLinks({ dist, errors }) {
  for (const route of managerDemoScriptInboundLinkRoutes) {
    const pagePath = route === "/" ? resolve(dist, "index.html") : resolve(dist, route.replace(/^\/|\/$/g, ""), "index.html");

    if (!(await fileExists(pagePath))) {
      errors.push(withEvidence(`Manager demo script inbound-link source route is missing: ${route}`, route));
      continue;
    }

    const html = await readFile(pagePath, "utf8");
    if (!hasHref(html, managerDemoScriptRoute)) {
      errors.push(withEvidence(`Required inbound route ${route} does not link to ${managerDemoScriptRoute}`, route));
    }
  }
}

async function readRouteEntry({ dist, errors, route }) {
  const indexPath = resolve(dist, "routes-index.json");
  if (!(await fileExists(indexPath))) {
    return null;
  }

  let parsed;
  try {
    parsed = JSON.parse(await readFile(indexPath, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Manager demo script could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return null;
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Manager demo script routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return null;
  }

  return parsed.entries.find((entry) => entry?.path === route) ?? null;
}

function stripSanctionedSections(html) {
  return html.replace(
    /<([a-z][a-z0-9:-]*)\b(?=[^>]*\bdata-manager-script-section\s*=\s*["'](?:raw-boundary|stop|non-claims)["'])[^>]*>[\s\S]*?<\/\1>/gi,
    " "
  );
}

function normalizeManagerDemoScriptBaseUrl(value, errors) {
  try {
    return normalizeBaseUrl(new URL(value).origin);
  } catch {
    errors.push(withEvidence(`Manager demo script baseUrl must be a valid absolute URL: ${String(value)}`, "baseUrl input"));
    return null;
  }
}

function readMetaContent(html, keyName, keyValue) {
  const escaped = escapeRegExp(keyValue);
  const pattern = new RegExp(`<meta\\b(?=[^>]*\\b${keyName}\\s*=\\s*["']${escaped}["'])(?=[^>]*\\bcontent\\s*=\\s*["']([^"']+)["'])[^>]*>`, "i");
  const match = html.match(pattern);
  return match?.[1] ? decodeHtmlEntities(match[1]) : null;
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasOgTypeArticle(html) {
  return /<meta\b(?=[^>]*\bproperty\s*=\s*["']og:type["'])(?=[^>]*\bcontent\s*=\s*["']article["'])[^>]*>/i.test(html);
}

function countWords(value) {
  return (value.match(/\b[\w'-]+\b/g) ?? []).length;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
