import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const proofPathsForManagersRoute = "/proof-paths/for-managers/";
export const proofPathsForManagersRequiredLinks = [
  "/manager-brief/",
  "/manager-faq/",
  "/manager-packet/",
  "/packets/",
  "/packets/assembly/",
  "/proof-paths/",
  "/proof-paths/faq/",
  "/proof-paths/tour/",
  "/proof-source-catalog/",
  "/questions/",
  "/limitations/",
  "/static-vs-runtime/",
  "/review-claim-checklist/"
];
export const proofPathsForManagersInboundRoutes = [
  "/proof-paths/",
  "/proof-paths/faq/",
  "/proof-paths/tour/",
  "/manager-packet/",
  "/manager-faq/"
];

const pageArtifact = "proof-paths/for-managers/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "does not automate management, product, runtime, release, or service-owner decisions",
  "This is the manager lens inside the proof-path family"
];

const matrixHeaders = [
  "Manager or reviewer question",
  "Evidence packet to inspect",
  "What static evidence can support",
  "What it does not prove",
  "Coverage-label consequence",
  "Stop condition",
  "Next owner",
  "Supporting public route"
];

const requiredQuestions = [
  ["question-code-path-change", "What changed in the code path we are reviewing?"],
  ["question-repeat-claim", "What evidence supports repeating this claim?"],
  ["question-coverage-meaning", "What does reduced or partial coverage mean for this decision?"],
  ["question-next-runtime-product-owner", "Who should answer runtime or product behavior next?"],
  ["question-release-decision", "Can this evidence approve, block, or certify a release?"],
  ["question-runtime-incident-performance", "Can this evidence explain production traffic, endpoint performance, or outage cause?"],
  ["question-public-sharing", "Can this evidence be shared publicly?"],
  ["question-missing-evidence", "What should happen when evidence is missing, private-only, syntax-only, or unknown?"]
];

const anatomyFields = [
  "claim/question",
  "claim level",
  "proof path/packet link",
  "rule ID/family",
  "tier",
  "coverage label",
  "commit/source context",
  "extractor/schema family",
  "public-safe file path/span",
  "snippet hash or summary",
  "artifact family",
  "limitation",
  "non-claim",
  "validation evidence",
  "unresolved gaps",
  "next owner"
];

const requiredEvidenceTiers = ["Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"];
const requiredCoverageLabels = [
  "reduced",
  "partial",
  "unknown",
  "unavailable",
  "syntax-only",
  "private-only",
  "future-only",
  "gap-labeled"
];

const ownerTerms = [
  "manager",
  "reviewer",
  "service owner",
  "code owner",
  "architect",
  "runtime observability owner",
  "release owner",
  "test owner",
  "product owner",
  "security owner",
  "repository owner",
  "build/tooling owner",
  "incident owner",
  "TraceMap owner"
];

const metadataNonClaimTerms = [
  "runtime behavior",
  "production traffic",
  "endpoint performance",
  "outage cause",
  "release safety",
  "operational safety",
  "release approval",
  "complete coverage",
  "autonomous approval",
  "automated management decision",
  "AI impact analysis",
  "LLM analysis",
  "embeddings",
  "vector databases",
  "prompt classification",
  "replacement for tests"
];

const hardPrivatePatterns = [
  /\/Users\//i,
  /\/home\//i,
  /~\//,
  /\bC:\\/i,
  /\bfile:\/\//i,
  /\blocalhost\b/i,
  /\b127\.0\.0\.1\b/i,
  /\bgit@/i,
  /\bConnectionString\b/i,
  /\bconnection string\b/i,
  /\bServer\s*=/i,
  /\bUser Id\s*=/i,
  /\bPassword\s*=/i,
  /\bapi[_-]?key\b/i,
  /\bsecret\s*=/i,
  /\bsk-[A-Za-z0-9_-]{12,}\b/i
];

const forbiddenClaimPatterns = [
  /\b(?:TraceMap\s+)?(?:proves?|proven|proof of)\s+(?:runtime behavior|production traffic|endpoint performance|outage cause|release approval|release safety|operational safety|complete coverage|product behavior|customer impact)\b/i,
  /\b(?:certifies?|guarantees?|verifies?|confirms?)\s+(?:runtime behavior|production traffic|endpoint performance|release safety|operational safety|complete coverage|product behavior|customer impact)\b/i,
  /\b(?:monitors?|knows?)\s+production traffic\b/i,
  /\bmeasures?\s+endpoint performance\b/i,
  /\bidentifies?\s+outage cause\b/i,
  /\bgrants?\s+release approval\b/i,
  /\bprovides?\s+operational safety\b/i,
  /\bperforms?\s+(?:AI impact analysis|LLM analysis)\b/i,
  /\buses?\s+(?:embeddings|vector databases|prompt classification)\b/i,
  /\bautonomously\s+approves?\b/i,
  /\breplaces?\s+(?:tests|code review|source review|runtime observability|human judgment|human review|telemetry|logs|traces|release controls|manager judgment)\b/i
];

const blamePatterns = [/\bfailed\b/i, /\bfault\b/i, /\bto blame\b/i, /\bnegligent\b/i, /\bcareless\b/i];

export async function validateProofPathsForManagersDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "proof-paths", "for-managers", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Proof paths for managers page is missing required route: ${proofPathsForManagersRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }

  const routeContext = await readRouteContext({ dist, errors: localErrors });
  await validatePage({ pagePath, routeContext, errors: localErrors });
  await validateInboundLinks({ dist, errors: localErrors });

  errors.push(...localErrors);
}

function normalizeBaseUrl(value, errors) {
  let url;

  try {
    url = new URL(value);
  } catch {
    errors.push(withEvidence(`Proof paths for managers baseUrl must be a valid absolute URL: ${String(value)}`, "baseUrl input"));
    return null;
  }

  if (url.protocol !== "https:" && url.protocol !== "http:") {
    errors.push(withEvidence(`Proof paths for managers baseUrl must use http or https: ${String(value)}`, "baseUrl input"));
    return null;
  }

  return url.origin;
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${proofPathsForManagersRoute}`)) {
    errors.push(withEvidence(`Proof paths for managers sitemap is missing required route: ${baseUrl}${proofPathsForManagersRoute}`, sitemapArtifact));
  }
}

async function readRouteContext({ dist, errors }) {
  const indexPath = resolve(dist, "routes-index.json");
  const sitemapPath = resolve(dist, "sitemap.xml");
  const routes = new Set();
  const sitemapRoutes = new Set();
  let routeEntry = null;

  if (await fileExists(sitemapPath)) {
    for (const loc of await readSitemapLocSet(sitemapPath)) {
      try {
        sitemapRoutes.add(normalizeRouteHref(new URL(loc).pathname));
      } catch {
        // The aggregate validator reports malformed sitemap URLs separately.
      }
    }
  }

  if (!(await fileExists(indexPath))) {
    return { routeEntry, routes, sitemapRoutes };
  }

  let parsed;
  try {
    parsed = JSON.parse(await readFile(indexPath, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Proof paths for managers could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Proof paths for managers routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  for (const entry of parsed.entries) {
    if (typeof entry?.path === "string") {
      routes.add(normalizeRouteHref(entry.path));
    }
  }

  routeEntry = parsed.entries.find((entry) => normalizeRouteHref(entry?.path ?? "") === proofPathsForManagersRoute) ?? null;
  validateRouteEntry(routeEntry, errors);

  return { routeEntry, routes, sitemapRoutes };
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(withEvidence(`Proof paths for managers routes-index.json does not include required route: ${proofPathsForManagersRoute}`, routesIndexArtifact));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "concept",
    hintCategory: "evidence",
    sourceType: "site-page",
    preferredProofPath: "/proof-paths/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Proof paths for managers routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  if (!Array.isArray(routeEntry.limitations) || routeEntry.limitations.length === 0) {
    errors.push(withEvidence("Proof paths for managers routes-index.json must include limitations metadata.", routesIndexArtifact));
  }

  if (!Array.isArray(routeEntry.nonClaims) || routeEntry.nonClaims.length === 0) {
    errors.push(withEvidence("Proof paths for managers routes-index.json must include nonClaims metadata.", routesIndexArtifact));
    return;
  }

  const normalizedNonClaims = normalizeScanText(routeEntry.nonClaims.join(" ")).toLowerCase();
  for (const term of metadataNonClaimTerms) {
    if (!normalizedNonClaims.includes(term.toLowerCase())) {
      errors.push(withEvidence(`Proof paths for managers routes-index.json nonClaims do not include required term: ${term}`, routesIndexArtifact));
    }
  }

  const publicMetadataText = [routeEntry.title, routeEntry.summary, ...(routeEntry.limitations ?? [])].join(" ");
  validateForbiddenClaims({ errors, text: publicMetadataText, label: "metadata", artifact: routesIndexArtifact });
  validateBlameLanguage({ errors, text: publicMetadataText, label: "metadata", artifact: routesIndexArtifact });
}

async function validatePage({ pagePath, routeContext, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const mainHtml = extractMainHtml(html);
  const pageText = normalizeRenderedText(mainHtml);
  const lowerPageText = pageText.toLowerCase();
  const metadataText = collectMetadataText(html);
  const attributeText = collectDecodedAttributeText(html);
  const allTextTight = normalizeTightHtmlText(html);

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Proof paths for managers page does not include required text: ${phrase}`, pageArtifact));
    }
  }

  for (const header of matrixHeaders) {
    if (!pageText.includes(header)) {
      errors.push(withEvidence(`Proof paths for managers matrix is missing field header: ${header}`, pageArtifact));
    }
  }

  for (const [anchor, question] of requiredQuestions) {
    if (!hasId(html, anchor)) {
      errors.push(withEvidence(`Proof paths for managers page is missing required matrix anchor: #${anchor}`, pageArtifact));
    }
    if (!pageText.includes(question)) {
      errors.push(withEvidence(`Proof paths for managers page is missing required matrix question: ${question}`, pageArtifact));
    }
    if (!hasDataAttribute(html, "data-proof-manager-question", question)) {
      errors.push(withEvidence(`Proof paths for managers page is missing matrix data marker for: ${question}`, pageArtifact));
    }
  }

  for (const field of anatomyFields) {
    if (!hasDataAttribute(html, "data-proof-manager-anatomy", field)) {
      errors.push(withEvidence(`Proof paths for managers anatomy is missing field marker: ${field}`, pageArtifact));
    }
  }

  for (const tier of requiredEvidenceTiers) {
    if (!pageText.includes(tier)) {
      errors.push(withEvidence(`Proof paths for managers page does not include evidence tier: ${tier}`, pageArtifact));
    }
  }

  for (const match of pageText.matchAll(/\bTier\d[A-Za-z]+\b/g)) {
    if (!requiredEvidenceTiers.includes(match[0])) {
      errors.push(withEvidence(`Proof paths for managers page includes unsupported evidence tier: ${match[0]}`, pageArtifact));
    }
  }

  for (const label of requiredCoverageLabels) {
    if (!lowerPageText.includes(label)) {
      errors.push(withEvidence(`Proof paths for managers page does not preserve coverage label term: ${label}`, pageArtifact));
    }
  }

  for (const owner of ownerTerms) {
    if (!lowerPageText.includes(owner.toLowerCase())) {
      errors.push(withEvidence(`Proof paths for managers owner routing is missing public role category: ${owner}`, pageArtifact));
    }
  }

  for (const link of proofPathsForManagersRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Proof paths for managers page does not include required adjacent link: ${link}`, pageArtifact));
    }
  }

  validatePageMetadata(html, errors);
  validateInternalRouteLinks(html, routeContext, errors);
  validateDuplicateIds(html, errors);
  validateIllustrativeExamples(pageText, errors);
  validateForbiddenClaims({
    errors,
    text: `${pageText} ${metadataText} ${attributeText}`,
    label: "page copy, metadata, and attributes",
    artifact: pageArtifact
  });
  validateHardPrivateMaterial({
    errors,
    text: `${html} ${decodedHtml} ${pageText} ${allTextTight} ${metadataText} ${attributeText} ${JSON.stringify(routeContext.routeEntry ?? {})}`,
    label: "page, attributes, or metadata",
    artifact: pageArtifact
  });
  validateBlameLanguage({
    errors,
    text: `${pageText} ${metadataText} ${attributeText} ${JSON.stringify(routeContext.routeEntry ?? {})}`,
    label: "page copy, metadata, or attributes",
    artifact: pageArtifact
  });
}

function validatePageMetadata(html, errors) {
  const metaTags = findTagAttributes(html, "meta");
  const linkTags = findTagAttributes(html, "link");
  const checks = [
    [/<title>Proof Paths for Managers \| TraceMap<\/title>/i.test(html), "title"],
    [hasMeta(metaTags, { name: "description", content: "non-empty" }), "description"],
    [
      linkTags.some(
        (attributes) =>
          hasRel(attributes, "canonical") &&
          getAttribute(attributes, "href") === "https://tracemap.tools/proof-paths/for-managers/"
      ),
      "canonical URL"
    ],
    [hasMeta(metaTags, { property: "og:type", content: "article" }), "Open Graph type"],
    [hasMeta(metaTags, { property: "og:title", content: "TraceMap Proof Paths for Managers" }), "Open Graph title"],
    [hasMeta(metaTags, { property: "og:description", content: "non-empty" }), "Open Graph description"],
    [hasMeta(metaTags, { property: "og:url", content: "https://tracemap.tools/proof-paths/for-managers/" }), "Open Graph URL"]
  ];

  for (const [passed, label] of checks) {
    if (!passed) {
      errors.push(withEvidence(`Proof paths for managers page does not include required metadata: ${label}`, pageArtifact));
    }
  }
}

function hasMeta(metaTags, expected) {
  return metaTags.some((attributes) => {
    if (expected.name && getAttribute(attributes, "name") !== expected.name) {
      return false;
    }

    if (expected.property && getAttribute(attributes, "property") !== expected.property) {
      return false;
    }

    const content = getAttribute(attributes, "content");
    return expected.content === "non-empty" ? Boolean(content?.trim()) : content === expected.content;
  });
}

function hasRel(attributes, expectedRel) {
  return (getAttribute(attributes, "rel") ?? "")
    .split(/\s+/)
    .some((rel) => rel.toLowerCase() === expectedRel.toLowerCase());
}

function validateInternalRouteLinks(html, { routes, sitemapRoutes }, errors) {
  if (routes.size === 0 && sitemapRoutes.size === 0) {
    return;
  }

  for (const href of extractHrefs(extractMainHtml(html))) {
    if (!href.startsWith("/") || href.startsWith("//")) {
      continue;
    }

    const route = normalizeRouteHref(href);
    if (!routes.has(route) && !sitemapRoutes.has(route)) {
      errors.push(withEvidence(`Proof paths for managers page links to unresolved internal route: ${route}`, pageArtifact));
    }
  }
}

async function validateInboundLinks({ dist, errors }) {
  const missing = [];

  for (const route of proofPathsForManagersInboundRoutes) {
    const pagePath = resolve(dist, route.replace(/^\/|\/$/g, ""), "index.html");
    if (!(await fileExists(pagePath))) {
      continue;
    }

    const html = await readFile(pagePath, "utf8");
    if (!hasHref(html, proofPathsForManagersRoute)) {
      missing.push(route);
    }
  }

  if (missing.length > 0) {
    errors.push(withEvidence(`Proof paths for managers does not include inbound links from live adjacent routes: ${missing.join(", ")}`, "adjacent route HTML"));
  }
}

function validateIllustrativeExamples(pageText, errors) {
  if (!/illustrative only, not a real TraceMap finding/i.test(pageText)) {
    errors.push(withEvidence("Proof paths for managers illustrative examples must be marked as not real TraceMap findings.", pageArtifact));
  }

  if (!/example\.manager\.proof-path\.v1/.test(pageText)) {
    errors.push(withEvidence("Proof paths for managers does not include visibly illustrative placeholder rule family.", pageArtifact));
  }
}

function validateDuplicateIds(html, errors) {
  const ids = [...html.matchAll(/\bid\s*=\s*["']([^"']+)["']/gi)].map((match) => decodeHtmlEntities(match[1]));
  const seen = new Set();
  for (const id of ids) {
    if (seen.has(id)) {
      errors.push(withEvidence(`Proof paths for managers page has duplicate id: ${id}`, pageArtifact));
    }
    seen.add(id);
  }
}

function validateForbiddenClaims({ errors, text, label, artifact = pageArtifact }) {
  for (const pattern of forbiddenClaimPatterns) {
    const flags = pattern.flags.includes("g") ? pattern.flags : `${pattern.flags}g`;
    const globalPattern = new RegExp(pattern.source, flags);
    for (const match of text.matchAll(globalPattern)) {
      if (!hasNegatedContext(text, match.index)) {
        errors.push(withEvidence(`Proof paths for managers contains forbidden public claim in ${label}: ${match[0]}`, artifact));
        break;
      }
    }
  }
}

function validateHardPrivateMaterial({ errors, text, label, artifact }) {
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Proof paths for managers contains hard private material in ${label}: ${pattern.source}`, artifact));
    }
  }
}

function validateBlameLanguage({ errors, text, label, artifact }) {
  for (const pattern of blamePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Proof paths for managers contains blame-language indicator in ${label}: ${pattern.source}`, artifact));
    }
  }
}

function hasNegatedContext(value, index) {
  const prefix = value.slice(Math.max(0, index - 96), index).toLowerCase();
  return /(?:not|no|without|cannot|can't|does not|do not|never|what it does not)\s+(?:a\s+)?(?:real\s+)?(?:new\s+)?$/.test(prefix);
}

function extractMainHtml(html) {
  return html.match(/<main\b[^>]*>([\s\S]*?)<\/main>/i)?.[1] ?? html;
}

function collectMetadataText(html) {
  const title = html.match(/<title[^>]*>([\s\S]*?)<\/title>/i)?.[1] ?? "";
  const metaText = findTagAttributes(html, "meta")
    .map((attributes) => getAttribute(attributes, "content") ?? "")
    .filter(Boolean)
    .join(" ");
  return normalizeScanText(`${title} ${metaText}`);
}

function collectDecodedAttributeText(html) {
  return [...html.matchAll(/\s(?:href|alt|title|aria-label|content|data-[a-z0-9-]+)\s*=\s*(["'])(.*?)\1/gis)]
    .map((match) => decodeHtmlEntities(match[2]))
    .join(" ");
}

function normalizeTightHtmlText(html) {
  return decodeHtmlEntities(html)
    .replace(/<[^>]+>/g, "")
    .replace(/\s+/g, " ")
    .trim();
}

function normalizeScanText(value) {
  return decodeHtmlEntities(value).replace(/\s+/g, " ").trim();
}

function hasId(html, id) {
  const escaped = escapeRegExp(id);
  return new RegExp(`\\bid\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`\\bhref\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasDataAttribute(html, name, value) {
  const escapedName = escapeRegExp(name);
  const escapedValue = escapeRegExp(value);
  return new RegExp(`\\b${escapedName}\\s*=\\s*["']${escapedValue}["']`, "i").test(html);
}

function extractHrefs(html) {
  return [...html.matchAll(/\bhref\s*=\s*(["'])(.*?)\1/gi)]
    .map((match) => decodeHtmlEntities(match[2]))
    .filter((href) => href && !href.startsWith("#"));
}

function normalizeRouteHref(href) {
  const path = href.split("#", 1)[0].split("?", 1)[0];
  if (path === "") {
    return "/";
  }

  return path.endsWith("/") ? path : `${path}/`;
}

function findTagAttributes(html, tagName) {
  const pattern = new RegExp(`<${tagName}\\b([^>]*)>`, "gi");
  return [...html.matchAll(pattern)].map((match) => match[1] ?? "");
}

function getAttribute(attributes, name) {
  const escaped = escapeRegExp(name);
  const match = attributes.match(new RegExp(`\\b${escaped}\\s*=\\s*(["'])(.*?)\\1`, "i"));
  return match ? decodeHtmlEntities(match[2]) : null;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
