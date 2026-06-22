import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const reviewerQuickstartRoute = "/reviewer-quickstart/";
export const reviewerQuickstartRequiredLinks = [
  "/review-room/",
  "/packets/assembly/",
  "/review-claim-checklist/",
  "/proof-paths/tour/",
  "/proof-paths/",
  "/questions/",
  "/demo/runbook/",
  "/demo/manager-script/",
  "/limitations/",
  "/validation/"
];
export const reviewerQuickstartInboundRoutes = ["/review-room/", "/packets/assembly/"];

const pageArtifact = "reviewer-quickstart/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "five minutes",
  "Missing evidence creates a follow-up owner question, not a clean conclusion"
];

const requiredSections = [
  "Start Here",
  "Five-Minute Review",
  "Evidence Fields",
  "Stop Conditions",
  "Safe Review Language",
  "Escalation Owners",
  "Non-Claims"
];

const requiredSteps = [
  "identify the claim",
  "find the proof path",
  "check public claim level",
  "read rule ID/family",
  "inspect evidence tier and coverage label",
  "check commit/extractor context",
  "read limitations/non-claims",
  "name next owner",
  "stop on missing evidence"
];

const requiredEvidenceFields = [
  "claim",
  "proof path",
  "public claim level",
  "rule ID or rule family",
  "evidence tier",
  "coverage label",
  "commit SHA or source revision context",
  "extractor version or extractor family",
  "file path and line span when public-safe",
  "limitation",
  "non-claim",
  "validation evidence",
  "unresolved gap",
  "next owner"
];

const requiredStopConditions = [
  "missing proof path",
  "missing rule ID or rule family",
  "missing evidence tier",
  "missing coverage label",
  "missing limitation",
  "missing public claim level",
  "missing commit or extractor context without explicit limitation",
  "no validation evidence",
  "no next owner",
  "private-only support presented as public proof",
  "raw artifact leakage",
  "unsupported wording"
];

const requiredOwnerCategories = [
  "reviewer owner",
  "source review owner",
  "code owner",
  "service owner",
  "database owner",
  "test owner",
  "validation owner",
  "telemetry or runtime owner",
  "release owner",
  "manager or decision owner"
];

const safeLanguageTerms = [
  "inspect",
  "check",
  "follow",
  "review",
  "compare",
  "label",
  "record",
  "route",
  "escalate",
  "cannot conclude from this packet"
];

const metadataNonClaimTerms = [
  "runtime behavior",
  "production traffic",
  "endpoint performance",
  "outage cause",
  "release approval",
  "release safety",
  "operational safety",
  "complete coverage",
  "AI impact analysis",
  "LLM analysis",
  "embeddings",
  "vector database analysis",
  "prompt classification",
  "autonomous approval",
  "replacement of tests"
];

const rawMaterialPatterns = [
  /\braw facts?\b/i,
  /\braw SQLite(?: content)?\b/i,
  /\banalyzer logs?\b/i,
  /\braw source snippets?\b/i,
  /\braw SQL\b/i,
  /\bconfig values?\b/i,
  /\bsecrets?\b/i,
  /\braw remotes?\b/i,
  /\bgenerated scan directories\b/i,
  /\bprivate sample names?\b/i,
  /\braw command output\b/i,
  /\bhidden validation details\b/i,
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
  /\b(?:TraceMap\s+)?(?:proves?|proven|proof of)\s+(?:runtime behavior|production traffic|endpoint performance|outage cause|release approval|release safety|operational safety|complete coverage)\b/i,
  /\b(?:certifies?|guarantees?|verifies?)\s+(?:runtime behavior|production traffic|endpoint performance|release safety|operational safety|complete coverage)\b/i,
  /\b(?:monitors?|knows?)\s+production traffic\b/i,
  /\bmeasures?\s+endpoint performance\b/i,
  /\bidentifies?\s+outage cause\b/i,
  /\bgrants?\s+release approval\b/i,
  /\bprovides?\s+operational safety\b/i,
  /\bperforms?\s+(?:AI impact analysis|LLM analysis)\b/i,
  /\buses?\s+(?:embeddings|vector databases|prompt classification)\b/i,
  /\bautonomously\s+approves?\b/i,
  /\breplaces?\s+(?:tests|code review|source review|runtime observability|human judgment|telemetry|release controls)\b/i
];

const sanctionedBoundarySectionPattern =
  /<section\b(?=[^>]*\bdata-reviewer-boundary\s*=\s*["'][^"']+["'])[^>]*>[\s\S]*?<\/section>/gi;

export async function validateReviewerQuickstartDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "reviewer-quickstart", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence("Reviewer quickstart page is missing required public route: /reviewer-quickstart/", pageArtifact));
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }

  const routeContext = await readRouteContext({ dist, errors: localErrors });
  await validateReviewerQuickstartPage({ pagePath, routeContext, errors: localErrors });
  await validateInboundLinks({ dist, errors: localErrors });

  errors.push(...localErrors);
}

function normalizeBaseUrl(value, errors) {
  let url;

  try {
    url = new URL(value);
  } catch {
    errors.push(withEvidence(`Reviewer quickstart baseUrl must be a valid absolute URL: ${String(value)}`, "baseUrl input"));
    return null;
  }

  if (url.protocol !== "https:" && url.protocol !== "http:") {
    errors.push(withEvidence(`Reviewer quickstart baseUrl must use http or https: ${String(value)}`, "baseUrl input"));
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
  if (!sitemapUrls.has(`${baseUrl}${reviewerQuickstartRoute}`)) {
    errors.push(withEvidence(`Reviewer quickstart sitemap is missing required route: ${baseUrl}${reviewerQuickstartRoute}`, sitemapArtifact));
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
    errors.push(withEvidence(`Reviewer quickstart could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Reviewer quickstart routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  for (const entry of parsed.entries) {
    if (typeof entry?.path === "string") {
      routes.add(normalizeRouteHref(entry.path));
    }
  }

  routeEntry = parsed.entries.find((entry) => normalizeRouteHref(entry?.path ?? "") === reviewerQuickstartRoute) ?? null;
  validateRouteEntry(routeEntry, errors);

  return { routeEntry, routes, sitemapRoutes };
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(withEvidence(`Reviewer quickstart routes-index.json is missing required route: ${reviewerQuickstartRoute}`, routesIndexArtifact));
    return;
  }

  const expectedFields = {
    publicClaimLevel: "concept",
    hintCategory: "use-case",
    sourceType: "site-page",
    preferredProofPath: "/proof-paths/"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (routeEntry[field] !== expected) {
      errors.push(withEvidence(`Reviewer quickstart routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  if (!Array.isArray(routeEntry.limitations) || routeEntry.limitations.length === 0) {
    errors.push(withEvidence("Reviewer quickstart routes-index.json must include limitations metadata.", routesIndexArtifact));
  }

  if (!Array.isArray(routeEntry.nonClaims) || routeEntry.nonClaims.length === 0) {
    errors.push(withEvidence("Reviewer quickstart routes-index.json must include nonClaims metadata.", routesIndexArtifact));
    return;
  }

  const normalizedNonClaims = normalizeScanText(routeEntry.nonClaims.join(" ")).toLowerCase();
  for (const term of metadataNonClaimTerms) {
    if (!normalizedNonClaims.includes(term.toLowerCase())) {
      errors.push(withEvidence(`Reviewer quickstart routes-index.json nonClaims are missing required term: ${term}`, routesIndexArtifact));
    }
  }

  const limitations = Array.isArray(routeEntry.limitations) ? routeEntry.limitations : [];
  const publicMetadataText = [routeEntry.title, routeEntry.summary, ...limitations].join(" ");
  validateForbiddenClaims({ errors, text: publicMetadataText, label: "metadata", artifact: routesIndexArtifact });
}

async function validateReviewerQuickstartPage({ pagePath, routeContext, errors }) {
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
      errors.push(withEvidence(`Reviewer quickstart page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const section of requiredSections) {
    if (!pageText.includes(section)) {
      errors.push(withEvidence(`Reviewer quickstart page is missing required section: ${section}`, pageArtifact));
    }
  }

  for (const step of requiredSteps) {
    if (!lowerPageText.includes(step.toLowerCase())) {
      errors.push(withEvidence(`Reviewer quickstart page is missing quickstart step: ${step}`, pageArtifact));
    }
    if (!hasDataRow(html, "data-quickstart-step", step)) {
      errors.push(withEvidence(`Reviewer quickstart page is missing quickstart step marker: ${step}`, pageArtifact));
    }
  }

  for (const field of requiredEvidenceFields) {
    if (!lowerPageText.includes(field.toLowerCase())) {
      errors.push(withEvidence(`Reviewer quickstart page is missing evidence field: ${field}`, pageArtifact));
    }
    if (!hasDataRow(html, "data-evidence-field", field)) {
      errors.push(withEvidence(`Reviewer quickstart page is missing evidence field row: ${field}`, pageArtifact));
    }
  }

  for (const condition of requiredStopConditions) {
    if (!lowerPageText.includes(condition.toLowerCase())) {
      errors.push(withEvidence(`Reviewer quickstart page is missing stop condition: ${condition}`, pageArtifact));
    }
  }

  for (const owner of requiredOwnerCategories) {
    if (!lowerPageText.includes(owner)) {
      errors.push(withEvidence(`Reviewer quickstart page is missing owner category: ${owner}`, pageArtifact));
    }
  }

  for (const term of safeLanguageTerms) {
    if (!lowerPageText.includes(term.toLowerCase())) {
      errors.push(withEvidence(`Reviewer quickstart page is missing safe language term: ${term}`, pageArtifact));
    }
  }

  for (const link of reviewerQuickstartRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Reviewer quickstart page is missing required link: ${link}`, pageArtifact));
    }
  }

  validatePageMetadata(html, errors);
  validateInternalRouteLinks(html, routeContext, errors);

  if (wordCount < 500 || wordCount > 1400) {
    errors.push(withEvidence(`Reviewer quickstart page word count must be between 500 and 1400 words, got ${wordCount}`, pageArtifact));
  }

  validateForbiddenClaims({
    errors,
    text: `${strippedText} ${strippedTextTight} ${metadataText} ${attributeText}`,
    label: "page copy outside sanctioned boundary regions"
  });
  validateRawMaterial({
    errors,
    text: `${normalizeRenderedText(strippedHtml)} ${normalizeTightHtmlText(strippedHtml)} ${metadataText} ${attributeText}`,
    label: "outside sanctioned boundary regions"
  });
  validateHardPrivateMaterial({
    errors,
    text: `${html} ${decodedHtml} ${pageText} ${allTextTight} ${metadataText} ${allAttributeText} ${JSON.stringify(routeContext.routeEntry ?? {})}`,
    label: "page, attributes, or metadata"
  });
}

function validatePageMetadata(html, errors) {
  const metaTags = findTagAttributes(html, "meta");
  const linkTags = findTagAttributes(html, "link");
  const checks = [
    [/<title>[^<]+<\/title>/i.test(html), "title"],
    [hasMeta(metaTags, { name: "description", content: "non-empty" }), "description"],
    [
      linkTags.some(
        (attributes) =>
          hasRel(attributes, "canonical") &&
          getAttribute(attributes, "href") === "https://tracemap.tools/reviewer-quickstart/"
      ),
      "canonical URL"
    ],
    [hasMeta(metaTags, { property: "og:type", content: "article" }), "Open Graph type"],
    [hasMeta(metaTags, { property: "og:title", content: "non-empty" }), "Open Graph title"],
    [hasMeta(metaTags, { property: "og:description", content: "non-empty" }), "Open Graph description"],
    [
      hasMeta(metaTags, {
        property: "og:url",
        content: "https://tracemap.tools/reviewer-quickstart/"
      }),
      "Open Graph URL"
    ]
  ];

  for (const [passed, label] of checks) {
    if (!passed) {
      errors.push(withEvidence(`Reviewer quickstart page is missing required metadata: ${label}`, pageArtifact));
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
      errors.push(withEvidence(`Reviewer quickstart page links to unresolved internal route: ${route}`, pageArtifact));
    }
  }
}

async function validateInboundLinks({ dist, errors }) {
  const missing = [];

  for (const route of reviewerQuickstartInboundRoutes) {
    const pagePath = resolve(dist, route.replace(/^\/|\/$/g, ""), "index.html");
    if (!(await fileExists(pagePath))) {
      continue;
    }

    const html = await readFile(pagePath, "utf8");
    if (!hasHref(html, reviewerQuickstartRoute)) {
      missing.push(route);
    }
  }

  if (missing.length > 0) {
    errors.push(withEvidence(`Reviewer quickstart is missing inbound links from live adjacent routes: ${missing.join(", ")}`, "adjacent route HTML"));
  }
}

function validateForbiddenClaims({ artifact = pageArtifact, errors, text, label }) {
  for (const pattern of forbiddenClaimPatterns) {
    const flags = pattern.flags.includes("g") ? pattern.flags : `${pattern.flags}g`;
    const globalPattern = new RegExp(pattern.source, flags);
    for (const match of text.matchAll(globalPattern)) {
      if (!hasNegatedContext(text, match.index)) {
        errors.push(withEvidence(`Reviewer quickstart contains forbidden public claim in ${label}: ${match[0]}`, artifact));
        break;
      }
    }
  }
}

function validateRawMaterial({ errors, text, label }) {
  for (const pattern of rawMaterialPatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Reviewer quickstart contains forbidden raw/private material ${label}: ${pattern.source}`, pageArtifact));
    }
  }
}

function validateHardPrivateMaterial({ errors, text, label }) {
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Reviewer quickstart contains hard private material in ${label}: ${pattern.source}`, pageArtifact));
    }
  }
}

function hasNegatedContext(value, index) {
  const prefix = value.slice(Math.max(0, index - 56), index).toLowerCase();
  return /(?:not|no|without|cannot|can't|does not|do not|never)\s+(?:a\s+)?(?:complete\s+)?(?:new\s+)?$/.test(prefix);
}

function hasDataRow(html, attribute, value) {
  const escaped = escapeRegExp(value);
  return new RegExp(`\\b${attribute}\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function stripSanctionedBoundaryRegions(html) {
  return html.replace(sanctionedBoundarySectionPattern, " ");
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
  return String(value).replace(/\s+/g, " ").trim();
}

function extractMainHtml(html) {
  const match = html.match(/<main\b[^>]*>([\s\S]*?)<\/main>/i);
  return match ? match[1] : html;
}

function stripTagsTight(html) {
  let text = "";
  let insideTag = false;
  let quote = "";
  const value = String(html);

  for (let index = 0; index < value.length; index += 1) {
    const char = value[index];

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
      if (value.startsWith("<!--", index)) {
        const end = value.indexOf("-->", index + 4);
        index = end === -1 ? value.length : end + 2;
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

function countWords(text) {
  const words = text.match(/[A-Za-z0-9][A-Za-z0-9'/-]*/g);
  return words ? words.length : 0;
}

function findTagAttributes(html, tagName) {
  const pattern = new RegExp(`<${tagName}\\b([^>]*)>`, "gi");
  return [...html.matchAll(pattern)].map((match) => match[1]);
}

function findAllTagAttributes(html) {
  return [...String(html).matchAll(/<[a-z][a-z0-9:-]*\b([^>]*)>/gi)].map((match) => match[1]);
}

function getAttribute(attributes, name) {
  const escaped = escapeRegExp(name);
  const match = String(attributes).match(new RegExp(`\\s${escaped}\\s*=\\s*("[^"]*"|'[^']*'|[^\\s>]+)`, "i"));
  return match ? unquoteAttributeValue(match[1]) : null;
}

function unquoteAttributeValue(value) {
  return String(value).replace(/^["']|["']$/g, "");
}

function hasHref(html, expectedHref) {
  const normalizedExpected = normalizeRouteHref(expectedHref);
  return extractHrefs(html).some((href) => normalizeRouteHref(href) === normalizedExpected);
}

function extractHrefs(html) {
  return [...String(html).matchAll(/\shref\s*=\s*("[^"]*"|'[^']*'|[^\s>]+)/gi)].map((match) =>
    unquoteAttributeValue(match[1])
  );
}

function normalizeRouteHref(href) {
  const value = String(href).trim();
  if (!value.startsWith("/") || value.startsWith("//")) {
    return value;
  }

  const withoutHash = value.split("#")[0].split("?")[0];
  if (withoutHash === "") {
    return "/";
  }

  return withoutHash.endsWith("/") ? withoutHash : `${withoutHash}/`;
}

function withEvidence(message, artifact) {
  return `${message} [${artifact}]`;
}
