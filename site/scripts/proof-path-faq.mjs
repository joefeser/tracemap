import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const proofPathFaqRoute = "/proof-paths/faq/";
export const proofPathFaqRequiredLinks = [
  "/questions/",
  "/proof-paths/",
  "/proof-paths/tour/",
  "/evidence/",
  "/limitations/",
  "/static-vs-runtime/",
  "/review-claim-checklist/",
  "/packets/assembly/"
];
export const proofPathFaqInboundRoutes = ["/proof-paths/"];

const pageArtifact = "proof-paths/faq/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "This FAQ is for readers who already know they need a proof path",
  "does not create a new proof source or approval flow"
];

const requiredQuestions = [
  ["what-is-a-proof-path", "What is a proof path?"],
  ["how-to-read", "How do I read a proof path?"],
  ["evidence-tiers", "What do evidence tiers mean?"],
  ["coverage-labels", "What do coverage labels mean?"],
  ["limitations", "Why do limitations matter?"],
  ["public-claim-levels", "What do public claim levels mean?"],
  ["missing-evidence", "What should I do when evidence is missing?"],
  ["review-packets", "How do proof paths relate to review packets?"],
  ["static-evidence-cannot-prove", "What can static evidence not prove?"],
  ["private-or-raw-artifacts", "Can a proof path use private or raw artifacts?"],
  ["agents-and-reviewers", "What should agents and reviewers preserve?"]
];

const requiredReadingOrder = [
  "claim",
  "public claim level",
  "proof path",
  "rule ID or rule family",
  "evidence tier",
  "coverage label",
  "commit or public-safe source context",
  "extractor version or schema family",
  "limitation",
  "non-claim",
  "next owner"
];

const requiredEvidenceTiers = ["Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"];
const requiredCoverageTerms = ["full", "partial", "reduced", "unknown", "unavailable", "future-only", "gap-labeled"];
const requiredSafeVerbs = [
  "inspect",
  "follow",
  "compare",
  "check",
  "record",
  "downgrade",
  "hold",
  "label the gap",
  "hand off",
  "escalate"
];
const requiredUnsafeVerbs = ["proves", "guarantees", "certifies", "approves", "replaces", "resolves"];

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
  "AI impact analysis",
  "LLM analysis",
  "embeddings",
  "vector databases",
  "prompt classification",
  "replacement for tests"
];

const rawMaterialPatterns = [
  /\bfacts\.ndjson\b/i,
  /\bindex\.sqlite\b/i,
  /\bscan-manifest\.json\b/i,
  /\breport\.md\b/i,
  /\blogs\/analyzer\.log\b/i,
  /\braw facts?\b/i,
  /\braw SQLite(?: content)?\b/i,
  /\banalyzer logs?\b/i,
  /\braw source snippets?\b/i,
  /\braw SQL\b/i,
  /\bconfig values?\b/i,
  /\bsecrets?\b/i,
  /\blocal paths?\b/i,
  /\braw remotes?\b/i,
  /\bgenerated scan directories\b/i,
  /\bprivate sample names?\b/i,
  /\bhidden validation details\b/i,
  /\braw command output\b/i,
  /\bcredential-like values\b/i
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
  /\b(?:TraceMap\s+)?(?:proves?|proven|proof of)\s+(?:runtime behavior|production traffic|endpoint performance|outage cause|release approval|release safety|operational safety|complete coverage|product behavior)\b/i,
  /\b(?:certifies?|guarantees?|verifies?)\s+(?:runtime behavior|production traffic|endpoint performance|release safety|operational safety|complete coverage|product behavior)\b/i,
  /\b(?:monitors?|knows?)\s+production traffic\b/i,
  /\bmeasures?\s+endpoint performance\b/i,
  /\bidentifies?\s+outage cause\b/i,
  /\bgrants?\s+release approval\b/i,
  /\bprovides?\s+operational safety\b/i,
  /\bperforms?\s+(?:AI impact analysis|LLM analysis)\b/i,
  /\buses?\s+(?:embeddings|vector databases|prompt classification)\b/i,
  /\bautonomously\s+approves?\b/i,
  /\breplaces?\s+(?:tests|code review|source review|runtime observability|human judgment|human review|telemetry|logs|traces|APM|release controls)\b/i
];

const unsupportedVerbPatterns = [
  /\bproves?\b/i,
  /\bguarantees?\b/i,
  /\bcertifies?\b/i,
  /\bapproves?\b/i,
  /\breplaces?\b/i,
  /\bresolves?\b/i,
];

const blamePatterns = [/\bfailed\b/i, /\bfault\b/i, /\bto blame\b/i, /\bnegligent\b/i, /\bcareless\b/i];
const sanctionedBoundaryNames = new Set(["non-claims", "private-material", "unsafe-patterns"]);

export async function validateProofPathFaqDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "proof-paths", "faq", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence("Proof path FAQ page does not include required public route: /proof-paths/faq/", pageArtifact));
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }

  const routeContext = await readRouteContext({ dist, errors: localErrors });
  await validateProofPathFaqPage({ pagePath, routeContext, errors: localErrors });
  await validateInboundLinks({ dist, errors: localErrors });

  errors.push(...localErrors);
}

function normalizeBaseUrl(value, errors) {
  let url;

  try {
    url = new URL(value);
  } catch {
    errors.push(withEvidence(`Proof path FAQ baseUrl must be a valid absolute URL: ${String(value)}`, "baseUrl input"));
    return null;
  }

  if (url.protocol !== "https:" && url.protocol !== "http:") {
    errors.push(withEvidence(`Proof path FAQ baseUrl must use http or https: ${String(value)}`, "baseUrl input"));
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
  if (!sitemapUrls.has(`${baseUrl}${proofPathFaqRoute}`)) {
    errors.push(withEvidence(`Proof path FAQ sitemap does not include required route: ${baseUrl}${proofPathFaqRoute}`, sitemapArtifact));
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
    errors.push(withEvidence(`Proof path FAQ could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Proof path FAQ routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  for (const entry of parsed.entries) {
    if (typeof entry?.path === "string") {
      routes.add(normalizeRouteHref(entry.path));
    }
  }

  routeEntry = parsed.entries.find((entry) => normalizeRouteHref(entry?.path ?? "") === proofPathFaqRoute) ?? null;
  validateRouteEntry(routeEntry, errors);

  return { routeEntry, routes, sitemapRoutes };
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(withEvidence(`Proof path FAQ routes-index.json does not include required route: ${proofPathFaqRoute}`, routesIndexArtifact));
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
      errors.push(withEvidence(`Proof path FAQ routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  if (!Array.isArray(routeEntry.limitations) || routeEntry.limitations.length === 0) {
    errors.push(withEvidence("Proof path FAQ routes-index.json must include limitations metadata.", routesIndexArtifact));
  }

  if (!Array.isArray(routeEntry.nonClaims) || routeEntry.nonClaims.length === 0) {
    errors.push(withEvidence("Proof path FAQ routes-index.json must include nonClaims metadata.", routesIndexArtifact));
    return;
  }

  const normalizedNonClaims = normalizeScanText(routeEntry.nonClaims.join(" ")).toLowerCase();
  for (const term of metadataNonClaimTerms) {
    if (!normalizedNonClaims.includes(term.toLowerCase())) {
      errors.push(withEvidence(`Proof path FAQ routes-index.json nonClaims do not include required term: ${term}`, routesIndexArtifact));
    }
  }

  const publicMetadataText = [routeEntry.title, routeEntry.summary, ...(routeEntry.limitations ?? [])].join(" ");
  validateForbiddenClaims({ errors, text: publicMetadataText, label: "metadata", artifact: routesIndexArtifact });
  validateUnsupportedVerbs({ errors, text: publicMetadataText, label: "metadata", artifact: routesIndexArtifact });
  validateBlameLanguage({ errors, text: publicMetadataText, label: "metadata", artifact: routesIndexArtifact });
}

async function validateProofPathFaqPage({ pagePath, routeContext, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const mainHtml = extractMainHtml(html);
  const strippedHtml = stripSanctionedBoundaryRegions(html);
  const strippedMainHtml = extractMainHtml(strippedHtml);
  const pageText = normalizeRenderedText(mainHtml);
  const lowerPageText = pageText.toLowerCase();
  const strippedText = normalizeRenderedText(strippedMainHtml);
  const strippedTextTight = normalizeTightHtmlText(strippedMainHtml);
  const metadataText = collectMetadataText(html);
  const attributeText = collectDecodedAttributeText(strippedHtml);
  const allAttributeText = collectDecodedAttributeText(html);
  const allTextTight = normalizeTightHtmlText(html);
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Proof path FAQ page does not include required text: ${phrase}`, pageArtifact));
    }
  }

  for (const [anchor, question] of requiredQuestions) {
    if (!hasId(html, anchor)) {
      errors.push(withEvidence(`Proof path FAQ page does not include required anchor: #${anchor}`, pageArtifact));
    }
    if (!pageText.includes(question)) {
      errors.push(withEvidence(`Proof path FAQ page does not include required question: ${question}`, pageArtifact));
    }
  }

  for (const field of requiredReadingOrder) {
    if (!lowerPageText.includes(field.toLowerCase())) {
      errors.push(withEvidence(`Proof path FAQ reading order does not include field: ${field}`, pageArtifact));
    }
  }

  for (const tier of requiredEvidenceTiers) {
    if (!pageText.includes(tier)) {
      errors.push(withEvidence(`Proof path FAQ page does not include evidence tier: ${tier}`, pageArtifact));
    }
  }

  for (const term of requiredCoverageTerms) {
    if (!lowerPageText.includes(term)) {
      errors.push(withEvidence(`Proof path FAQ page does not include coverage label term: ${term}`, pageArtifact));
    }
  }

  for (const link of proofPathFaqRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Proof path FAQ page does not include required adjacent link: ${link}`, pageArtifact));
    }
  }

  validateSafeAndUnsafePatterns({ html, pageText, errors });
  validateIllustrativeExamples({ html, pageText, errors });
  validateDuplicateIds(html, errors);
  validatePageMetadata(html, errors);
  validateInternalRouteLinks(html, routeContext, errors);

  if (wordCount < 900 || wordCount > 2200) {
    errors.push(withEvidence(`Proof path FAQ page word count must be between 900 and 2200 words, got ${wordCount}`, pageArtifact));
  }

  validateForbiddenClaims({
    errors,
    text: `${strippedText} ${strippedTextTight} ${metadataText} ${attributeText}`,
    label: "page copy outside bounded non-claim, private-material, and unsafe-pattern sections",
    artifact: pageArtifact
  });
  validateUnsupportedVerbs({
    errors,
    text: `${strippedText} ${strippedTextTight} ${metadataText} ${attributeText}`,
    label: "page copy outside bounded non-claim, private-material, and unsafe-pattern sections",
    artifact: pageArtifact
  });
  validateRawMaterial({
    errors,
    text: `${normalizeRenderedText(strippedHtml)} ${normalizeTightHtmlText(strippedHtml)} ${metadataText} ${attributeText}`,
    label: "outside bounded private-material sections",
    artifact: pageArtifact
  });
  validateHardPrivateMaterial({
    errors,
    text: `${html} ${decodedHtml} ${pageText} ${allTextTight} ${metadataText} ${allAttributeText} ${JSON.stringify(routeContext.routeEntry ?? {})}`,
    label: "page, attributes, or metadata",
    artifact: pageArtifact
  });
  validateBlameLanguage({
    errors,
    text: `${pageText} ${metadataText} ${allAttributeText} ${JSON.stringify(routeContext.routeEntry ?? {})}`,
    label: "page copy, metadata, or attributes",
    artifact: pageArtifact
  });
}

function validateSafeAndUnsafePatterns({ html, pageText, errors }) {
  if (!hasId(html, "safe-answer-patterns")) {
    errors.push(withEvidence("Proof path FAQ page does not include safe answer patterns section.", pageArtifact));
  }

  if (!hasId(html, "unsafe-answer-patterns")) {
    errors.push(withEvidence("Proof path FAQ page does not include unsafe answer patterns section.", pageArtifact));
  }

  for (const verb of requiredSafeVerbs) {
    if (!pageText.toLowerCase().includes(verb.toLowerCase())) {
      errors.push(withEvidence(`Proof path FAQ safe patterns do not include bounded verb: ${verb}`, pageArtifact));
    }
  }

  for (const verb of requiredUnsafeVerbs) {
    if (!pageText.toLowerCase().includes(verb.toLowerCase())) {
      errors.push(withEvidence(`Proof path FAQ unsafe patterns do not include unsupported verb: ${verb}`, pageArtifact));
    }
  }

  const safePatternCount = (html.match(/\bdata-proof-faq-safe-pattern\b/g) ?? []).length;
  if (safePatternCount < 3) {
    errors.push(withEvidence(`Proof path FAQ page expected at least three marked safe answer patterns, got ${safePatternCount}`, pageArtifact));
  }

  const unsafeSection = html.match(/<section\b(?=[^>]*\bid\s*=\s*["']unsafe-answer-patterns["'])([\s\S]*?)<\/section>/i);
  if (!unsafeSection || !/\bdata-proof-faq-boundary\s*=\s*["']unsafe-patterns["']/i.test(unsafeSection[0])) {
    errors.push(withEvidence("Proof path FAQ unsafe patterns must be inside a bounded unsafe-patterns section.", pageArtifact));
  }
}

function validateIllustrativeExamples({ html, pageText, errors }) {
  if (!/illustrative pattern, not a real TraceMap finding/i.test(pageText)) {
    errors.push(withEvidence("Proof path FAQ illustrative examples must be visibly marked as not real TraceMap findings.", pageArtifact));
  }

  if (!/example\.rule\.family/.test(html)) {
    errors.push(withEvidence("Proof path FAQ does not include visibly illustrative placeholder rule family.", pageArtifact));
  }
}

function validateDuplicateIds(html, errors) {
  const ids = [...html.matchAll(/\bid\s*=\s*["']([^"']+)["']/gi)].map((match) => decodeHtmlEntities(match[1]));
  const seen = new Set();
  for (const id of ids) {
    if (seen.has(id)) {
      errors.push(withEvidence(`Proof path FAQ page has duplicate id: ${id}`, pageArtifact));
    }
    seen.add(id);
  }
}

function validatePageMetadata(html, errors) {
  const metaTags = findTagAttributes(html, "meta");
  const linkTags = findTagAttributes(html, "link");
  const checks = [
    [/<title>Proof Path FAQ \| TraceMap<\/title>/i.test(html), "title"],
    [hasMeta(metaTags, { name: "description", content: "non-empty" }), "description"],
    [
      linkTags.some(
        (attributes) =>
          hasRel(attributes, "canonical") &&
          getAttribute(attributes, "href") === "https://tracemap.tools/proof-paths/faq/"
      ),
      "canonical URL"
    ],
    [hasMeta(metaTags, { property: "og:type", content: "article" }), "Open Graph type"],
    [hasMeta(metaTags, { property: "og:title", content: "TraceMap Proof Path FAQ" }), "Open Graph title"],
    [hasMeta(metaTags, { property: "og:description", content: "non-empty" }), "Open Graph description"],
    [hasMeta(metaTags, { property: "og:url", content: "https://tracemap.tools/proof-paths/faq/" }), "Open Graph URL"]
  ];

  for (const [passed, label] of checks) {
    if (!passed) {
      errors.push(withEvidence(`Proof path FAQ page does not include required metadata: ${label}`, pageArtifact));
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
      errors.push(withEvidence(`Proof path FAQ page links to unresolved internal route: ${route}`, pageArtifact));
    }
  }
}

async function validateInboundLinks({ dist, errors }) {
  const missing = [];

  for (const route of proofPathFaqInboundRoutes) {
    const pagePath = resolve(dist, route.replace(/^\/|\/$/g, ""), "index.html");
    if (!(await fileExists(pagePath))) {
      continue;
    }

    const html = await readFile(pagePath, "utf8");
    if (!hasHref(html, proofPathFaqRoute)) {
      missing.push(route);
    }
  }

  if (missing.length > 0) {
    errors.push(withEvidence(`Proof path FAQ does not include inbound links from live adjacent routes: ${missing.join(", ")}`, "adjacent route HTML"));
  }
}

function validateForbiddenClaims({ errors, text, label, artifact = pageArtifact }) {
  for (const pattern of forbiddenClaimPatterns) {
    const flags = pattern.flags.includes("g") ? pattern.flags : `${pattern.flags}g`;
    const globalPattern = new RegExp(pattern.source, flags);
    for (const match of text.matchAll(globalPattern)) {
      if (!hasNegatedContext(text, match.index)) {
        errors.push(withEvidence(`Proof path FAQ contains forbidden public claim in ${label}: ${match[0]}`, artifact));
        break;
      }
    }
  }
}

function validateUnsupportedVerbs({ errors, text, label, artifact }) {
  for (const pattern of unsupportedVerbPatterns) {
    const flags = pattern.flags.includes("g") ? pattern.flags : `${pattern.flags}g`;
    const globalPattern = new RegExp(pattern.source, flags);
    for (const match of text.matchAll(globalPattern)) {
      if (!hasNegatedContext(text, match.index)) {
        errors.push(withEvidence(`Proof path FAQ contains unsupported conclusion verb in ${label}: ${match[0]}`, artifact));
        break;
      }
    }
  }
}

function validateRawMaterial({ errors, text, label, artifact }) {
  for (const pattern of rawMaterialPatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Proof path FAQ contains forbidden raw/private material ${label}: ${pattern.source}`, artifact));
    }
  }
}

function validateHardPrivateMaterial({ errors, text, label, artifact }) {
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Proof path FAQ contains hard private material in ${label}: ${pattern.source}`, artifact));
    }
  }
}

function validateBlameLanguage({ errors, text, label, artifact }) {
  for (const pattern of blamePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Proof path FAQ contains blame-language indicator in ${label}: ${pattern.source}`, artifact));
    }
  }
}

function hasNegatedContext(value, index) {
  const prefix = value.slice(Math.max(0, index - 64), index).toLowerCase();
  return /(?:not|no|without|cannot|can't|does not|do not|never)\s+(?:a\s+)?(?:real\s+)?(?:new\s+)?$/.test(prefix);
}

function hasId(html, id) {
  const escaped = escapeRegExp(id);
  return new RegExp(`\\bid\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function stripSanctionedBoundaryRegions(html) {
  let result = "";
  let index = 0;

  while (index < html.length) {
    const start = findNextSectionStart(html, index);
    if (!start) {
      result += html.slice(index);
      break;
    }

    result += html.slice(index, start.start);

    const boundaryName = getAttribute(start.attributes, "data-proof-faq-boundary");
    if (!sanctionedBoundaryNames.has(boundaryName ?? "")) {
      result += html.slice(start.start, start.end);
      index = start.end;
      continue;
    }

    const end = findMatchingSectionEnd(html, start.end);
    if (end === -1) {
      result += html.slice(start.start, start.end);
      index = start.end;
      continue;
    }

    result += " ";
    index = end;
  }

  return result;
}

function findNextSectionStart(html, from) {
  const pattern = /<section\b/gi;
  pattern.lastIndex = from;
  const match = pattern.exec(html);
  if (!match) {
    return null;
  }

  const endIndex = findTagEnd(html, match.index);
  if (endIndex === -1) {
    return null;
  }

  return {
    attributes: html.slice(match.index + match[0].length, endIndex),
    end: endIndex + 1,
    start: match.index
  };
}

function findMatchingSectionEnd(html, from) {
  const pattern = /<\/?section\b[^>]*>/gi;
  pattern.lastIndex = from;
  let depth = 1;

  for (let match = pattern.exec(html); match; match = pattern.exec(html)) {
    const tagStart = match.index;
    const tagEnd = findTagEnd(html, tagStart);
    if (tagEnd === -1) {
      return -1;
    }

    const rawTag = html.slice(tagStart, tagEnd + 1);
    if (/^<\/section\b/i.test(rawTag)) {
      depth -= 1;
      if (depth === 0) {
        return tagEnd + 1;
      }
    } else {
      depth += 1;
    }

    pattern.lastIndex = tagEnd + 1;
  }

  return -1;
}

function collectMetadataText(html) {
  const values = [];
  for (const attributes of findTagAttributes(html, "meta")) {
    const content = getAttribute(attributes, "content");
    if (content) {
      values.push(content);
    }
  }

  const title = html.match(/<title>([\s\S]*?)<\/title>/i);
  if (title) {
    values.push(title[1]);
  }

  return decodeHtmlEntities(values.join(" "));
}

function collectDecodedAttributeText(html) {
  return decodeHtmlEntities(
    findAllTagAttributes(html)
      .flatMap((attributes) => [...attributes.matchAll(/\s[a-z:-]+\s*=\s*("[^"]*"|'[^']*')/gi)])
      .map((match) => unquoteAttributeValue(match[1]))
      .join(" ")
  );
}

function normalizeTightHtmlText(html) {
  return normalizeScanText(decodeHtmlEntities(stripTagsTight(html)));
}

function normalizeScanText(value) {
  return String(value)
    .normalize("NFKC")
    .replace(/\p{Cf}/gu, "")
    .replace(/\s+/g, " ")
    .trim();
}

function stripTagsTight(html) {
  let text = "";
  let insideTag = false;
  let quote = "";

  for (let index = 0; index < html.length; index += 1) {
    const char = html[index];

    if (insideTag) {
      if (quote) {
        if (char === quote) {
          quote = "";
        }
        continue;
      }

      if (char === "\"" || char === "'") {
        quote = char;
        continue;
      }

      if (char === ">") {
        insideTag = false;
      }
      continue;
    }

    if (char === "<") {
      if (html.startsWith("<!--", index)) {
        const end = html.indexOf("-->", index + 4);
        index = end === -1 ? html.length : end + 2;
        continue;
      }

      insideTag = true;
      quote = "";
      continue;
    }

    text += char;
  }

  return text;
}

function extractMainHtml(html) {
  const main = html.match(/<main\b[^>]*>([\s\S]*?)<\/main>/i);
  return main ? main[1] : html;
}

function extractHrefs(html) {
  return findTagAttributes(html, "a")
    .map((attributes) => getAttribute(attributes, "href"))
    .filter((href) => typeof href === "string");
}

function normalizeRouteHref(href) {
  const path = href.split("#", 1)[0].split("?", 1)[0];
  if (path === "") {
    return "/";
  }

  if (/\.[a-z0-9]+$/i.test(path)) {
    return path;
  }

  return path.endsWith("/") ? path : `${path}/`;
}

function hasHref(html, href) {
  const normalizedHref = normalizeRouteHref(href);
  return extractHrefs(html).some((candidate) => normalizeRouteHref(candidate) === normalizedHref);
}

function getAttribute(attributes, name) {
  const match = attributes.match(new RegExp(`(?:^|\\s)${escapeRegExp(name)}\\s*=\\s*("[^"]*"|'[^']*')`, "i"));
  return match ? decodeHtmlEntities(unquoteAttributeValue(match[1])) : null;
}

function findTagAttributes(html, tagName) {
  const tags = [];
  let from = 0;

  while (from < html.length) {
    const tag = findNextTag(html, tagName, from);
    if (!tag) {
      break;
    }

    tags.push(tag.attributes);
    from = tag.end;
  }

  return tags;
}

function findAllTagAttributes(html) {
  const tags = [];
  let from = 0;

  while (from < html.length) {
    const start = html.indexOf("<", from);
    if (start === -1) {
      break;
    }

    const end = findTagEnd(html, start);
    if (end === -1) {
      break;
    }

    const raw = html.slice(start + 1, end);
    if (!raw.startsWith("/") && !raw.startsWith("!") && !raw.startsWith("?")) {
      const name = raw.match(/^([a-z][a-z0-9:-]*)\b/i);
      if (name) {
        tags.push(raw.slice(name[0].length));
      }
    }

    from = end + 1;
  }

  return tags;
}

function findNextTag(html, tagName, from) {
  const pattern = new RegExp(`<${escapeRegExp(tagName)}\\b`, "gi");
  pattern.lastIndex = from;
  const match = pattern.exec(html);
  if (!match) {
    return null;
  }

  const end = findTagEnd(html, match.index);
  if (end === -1) {
    return null;
  }

  return {
    attributes: html.slice(match.index + match[0].length, end),
    end: end + 1,
    start: match.index
  };
}

function findTagEnd(html, start) {
  let quote = null;

  for (let index = start + 1; index < html.length; index += 1) {
    const char = html[index];
    if (quote) {
      if (char === quote) {
        quote = null;
      }
      continue;
    }

    if (char === "\"" || char === "'") {
      quote = char;
      continue;
    }

    if (char === ">") {
      return index;
    }
  }

  return -1;
}

function unquoteAttributeValue(value) {
  return value.slice(1, -1);
}

function countWords(value) {
  return (value.match(/\b[\w'-]+\b/g) ?? []).length;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
