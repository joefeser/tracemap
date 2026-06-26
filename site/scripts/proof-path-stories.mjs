import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const proofPathStoriesRoute = "/proof-path-stories/";
export const proofPathStoriesRequiredLinks = [
  "/proof-paths/",
  "/proof-source-catalog/",
  "/proof-paths/tour/",
  "/packets/examples/",
  "/review-claim-checklist/",
  "/limitations/",
  "/glossary/",
  "/roadmap/"
];

const pageArtifact = "proof-path-stories/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

const requiredText = ["Public claim level: concept", "No public conclusion without evidence"];
const requiredAnchors = [
  "story-contract",
  "proof-path-anatomy",
  "evidence-packet-references",
  "coverage-and-limitations",
  "stop-conditions-and-routing",
  "non-claims-and-forbidden-wording",
  "gallery-validation"
];
const requiredStoryFields = [
  "static question",
  "story category",
  "claim level",
  "coverage label",
  "proof path steps",
  "evidence packet references",
  "rule IDs or rule families",
  "evidence tiers",
  "supporting IDs",
  "limitation or non-claim",
  "stop condition",
  "next owner or next question"
];
const requiredWalkthroughFields = [
  "static question",
  "root/source surface",
  "destination or stopping surface",
  "rule IDs or rule families",
  "evidence tiers",
  "coverage labels",
  "supporting IDs",
  "limitation or non-claim",
  "stop condition",
  "next owner or next question"
];
const requiredCategories = [
  "endpoint/service orientation",
  "data/config orientation",
  "package/dependency orientation",
  "generated artifact orientation",
  "reduced-coverage orientation"
];
const requiredTiers = ["Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"];
const requiredStopConditions = [
  "no public-safe evidence",
  "reduced coverage",
  "semantic gap",
  "syntax-only fallback",
  "private-only evidence",
  "hidden detail",
  "missing rule ID",
  "requires reducer evidence"
];
const allowedWalkthroughEndings = [
  "evidence-backed static path",
  "reduced coverage",
  "needs owner follow-up",
  "internal only",
  "hidden",
  "stop: no public-safe evidence"
];
const requiredEvidenceReferenceTerms = [
  "Rule family",
  "tier",
  "coverage",
  "source context",
  "supporting ID",
  "limitation",
  "stop condition"
];
const metadataNonClaimTerms = [
  "runtime behavior",
  "production traffic",
  "endpoint performance",
  "release approval",
  "release safety",
  "operational safety",
  "complete coverage",
  "AI impact analysis",
  "LLM analysis",
  "embeddings",
  "vector databases",
  "prompt classification",
  "automated approval"
];

const rawMaterialPatterns = [
  /\bfacts\.ndjson\b/i,
  /\bindex\.sqlite\b/i,
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
  /\bprivate labels?\b/i,
  /\bcommand output\b/i,
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
  /\b(?:TraceMap\s+)?(?:proves?|proven|proof of)\s+(?:runtime behavior|production traffic|endpoint performance|outage cause|release approval|release safety|operational safety|complete coverage|product behavior)\b/i,
  /\b(?:certifies?|guarantees?|verifies?)\s+(?:runtime behavior|production traffic|endpoint performance|release safety|operational safety|complete coverage|product behavior)\b/i,
  /\b(?:monitors?|knows?)\s+production traffic\b/i,
  /\bmeasures?\s+endpoint performance\b/i,
  /\bgrants?\s+release approval\b/i,
  /\bprovides?\s+operational safety\b/i,
  /\bperforms?\s+(?:AI impact analysis|LLM analysis)\b/i,
  /\buses?\s+(?:embeddings|vector databases|prompt classification)\b/i,
  /\bautomated approval\b/i,
  /\brelease-ready\b/i,
  /\b(?:found|finds)\s+(?:an?\s+)?impacted\b/i
];

export async function validateProofPathStoriesDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "proof-path-stories", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence("Proof-path story gallery page is missing required public route: /proof-path-stories/", pageArtifact));
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }

  const routeContext = await readRouteContext({ dist, errors: localErrors });
  await validateProofPathStoriesPage({ pagePath, routeContext, errors: localErrors });

  errors.push(...localErrors);
}

function normalizeBaseUrl(value, errors) {
  let url;

  try {
    url = new URL(value);
  } catch {
    errors.push(withEvidence(`Proof-path story gallery baseUrl must be a valid absolute URL: ${String(value)}`, "baseUrl input"));
    return null;
  }

  if (url.protocol !== "https:" && url.protocol !== "http:") {
    errors.push(withEvidence(`Proof-path story gallery baseUrl must use http or https: ${String(value)}`, "baseUrl input"));
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
  if (!sitemapUrls.has(`${baseUrl}${proofPathStoriesRoute}`)) {
    errors.push(withEvidence(`Proof-path story gallery sitemap is missing required route: ${baseUrl}${proofPathStoriesRoute}`, sitemapArtifact));
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
    errors.push(withEvidence(`Proof-path story gallery could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  if (!parsed || typeof parsed !== "object" || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Proof-path story gallery routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  for (const entry of parsed.entries) {
    if (typeof entry?.path === "string") {
      routes.add(normalizeRouteHref(entry.path));
    }
  }

  routeEntry = parsed.entries.find((entry) => normalizeRouteHref(entry?.path ?? "") === proofPathStoriesRoute) ?? null;
  validateRouteEntry(routeEntry, errors);

  return { routeEntry, routes, sitemapRoutes };
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(withEvidence(`Proof-path story gallery routes-index.json is missing required route: ${proofPathStoriesRoute}`, routesIndexArtifact));
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
      errors.push(withEvidence(`Proof-path story gallery routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  if (!String(routeEntry.title ?? "").toLowerCase().includes("story")) {
    errors.push(withEvidence("Proof-path story gallery routes-index.json title must identify the story gallery.", routesIndexArtifact));
  }

  if (!String(routeEntry.summary ?? "").toLowerCase().includes("concept")) {
    errors.push(withEvidence("Proof-path story gallery routes-index.json summary must keep concept-level wording.", routesIndexArtifact));
  }

  if (!Array.isArray(routeEntry.limitations) || routeEntry.limitations.length === 0) {
    errors.push(withEvidence("Proof-path story gallery routes-index.json must include limitations metadata.", routesIndexArtifact));
  }

  if (!Array.isArray(routeEntry.nonClaims) || routeEntry.nonClaims.length === 0) {
    errors.push(withEvidence("Proof-path story gallery routes-index.json must include nonClaims metadata.", routesIndexArtifact));
    return;
  }

  const normalizedNonClaimsText = normalizeRenderedText(routeEntry.nonClaims.join(" ")).toLowerCase();
  for (const term of metadataNonClaimTerms) {
    if (!normalizedNonClaimsText.includes(term.toLowerCase())) {
      errors.push(withEvidence(`Proof-path story gallery routes-index.json nonClaims are missing required term: ${term}`, routesIndexArtifact));
    }
  }

  const publicMetadataText = [routeEntry.title, routeEntry.summary, ...(routeEntry.limitations ?? [])].join(" ");
  validateForbiddenClaims({ errors, text: publicMetadataText, label: "metadata" });
}

async function validateProofPathStoriesPage({ pagePath, routeContext, errors }) {
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

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Proof-path story gallery page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const anchor of requiredAnchors) {
    if (!hasId(html, anchor)) {
      errors.push(withEvidence(`Proof-path story gallery page is missing required anchor: #${anchor}`, pageArtifact));
    }
  }

  for (const tier of requiredTiers) {
    if (!pageText.includes(tier)) {
      errors.push(withEvidence(`Proof-path story gallery page is missing evidence tier: ${tier}`, pageArtifact));
    }
  }

  for (const link of proofPathStoriesRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Proof-path story gallery page is missing required link: ${link}`, pageArtifact));
    }
  }

  validateStoryCards(html, errors);
  validateWalkthroughs(html, errors);
  validateEvidenceReferences(html, errors);
  validateStopConditions(html, errors);
  validatePageMetadata(html, errors);
  validateInternalRouteLinks(html, routeContext, errors);

  validateForbiddenClaims({
    errors,
    text: `${strippedText} ${strippedTextTight} ${metadataText} ${attributeText}`,
    label: "page copy outside sanctioned boundary sections"
  });
  validateRawMaterial({
    errors,
    text: `${normalizeRenderedText(strippedHtml)} ${normalizeTightHtmlText(strippedHtml)} ${metadataText} ${attributeText}`,
    label: "outside sanctioned boundary sections"
  });
  validateHardPrivateMaterial({
    errors,
    text: `${html} ${decodedHtml} ${pageText} ${allTextTight} ${metadataText} ${allAttributeText} ${JSON.stringify(routeContext.routeEntry ?? {})}`,
    label: "page, attributes, or metadata"
  });

  if (!lowerPageText.includes("story-oriented reading aid")) {
    errors.push(withEvidence("Proof-path story gallery must say why it is a story-oriented reading aid.", pageArtifact));
  }
}

function validateStoryCards(html, errors) {
  const cards = [...html.matchAll(/<article\b(?=[^>]*\bdata-proof-story-card\b)[^>]*>[\s\S]*?<\/article>/gi)].map(
    (match) => match[0]
  );

  if (cards.length < requiredCategories.length) {
    errors.push(withEvidence(`Proof-path story gallery must include at least ${requiredCategories.length} story cards, got ${cards.length}`, pageArtifact));
  }

  const seenCategories = new Set();
  for (const card of cards) {
    const category = getAttributeFromTag(card, "data-story-category");
    const claimLevel = getAttributeFromTag(card, "data-claim-level");
    const coverageLabel = getAttributeFromTag(card, "data-coverage-label");
    if (category) {
      seenCategories.add(category);
    }
    if (claimLevel !== "concept") {
      errors.push(withEvidence(`Proof-path story card must remain concept-level, got ${String(claimLevel)}`, pageArtifact));
    }
    if (!coverageLabel) {
      errors.push(withEvidence("Proof-path story card is missing data-coverage-label.", pageArtifact));
    }
    for (const field of requiredStoryFields) {
      if (!hasDataField(card, "data-story-field", field)) {
        errors.push(withEvidence(`Proof-path story card is missing required field: ${field}`, pageArtifact));
      }
    }
  }

  for (const category of requiredCategories) {
    if (!seenCategories.has(category)) {
      errors.push(withEvidence(`Proof-path story gallery is missing required story category: ${category}`, pageArtifact));
    }
  }
}

function validateWalkthroughs(html, errors) {
  const walkthroughs = [
    ...html.matchAll(/<article\b(?=[^>]*\bdata-proof-walkthrough\b)[^>]*>[\s\S]*?<\/article>/gi)
  ].map((match) => match[0]);

  if (walkthroughs.length < allowedWalkthroughEndings.length) {
    errors.push(withEvidence(`Proof-path story gallery must include ${allowedWalkthroughEndings.length} walkthrough endings, got ${walkthroughs.length}`, pageArtifact));
  }

  const seenEndings = new Set();
  for (const walkthrough of walkthroughs) {
    const ending = getAttributeFromTag(walkthrough, "data-walkthrough-ending");
    if (!allowedWalkthroughEndings.includes(ending)) {
      errors.push(withEvidence(`Proof-path walkthrough has unsupported ending: ${String(ending)}`, pageArtifact));
    } else {
      seenEndings.add(ending);
    }

    for (const field of requiredWalkthroughFields) {
      if (!hasDataField(walkthrough, "data-walkthrough-field", field)) {
        errors.push(withEvidence(`Proof-path walkthrough is missing required field: ${field}`, pageArtifact));
      }
    }
  }

  for (const ending of allowedWalkthroughEndings) {
    if (!seenEndings.has(ending)) {
      errors.push(withEvidence(`Proof-path story gallery is missing walkthrough ending: ${ending}`, pageArtifact));
    }
  }
}

function validateEvidenceReferences(html, errors) {
  const references = [...html.matchAll(/<div\b(?=[^>]*\bdata-evidence-reference\s*=)[^>]*>[\s\S]*?<\/div>/gi)].map(
    (match) => match[0]
  );

  if (references.length < requiredCategories.length) {
    errors.push(withEvidence(`Proof-path story gallery must include at least ${requiredCategories.length} evidence packet references, got ${references.length}`, pageArtifact));
  }

  for (const reference of references) {
    const text = normalizeRenderedText(reference);
    for (const term of requiredEvidenceReferenceTerms) {
      if (!text.toLowerCase().includes(term.toLowerCase())) {
        errors.push(withEvidence(`Proof-path evidence reference is missing term: ${term}`, pageArtifact));
      }
    }

    const supportingIdMatch = text.match(/\bsupporting ID\s*:?\s*([^;.,]+)/i);
    const supportingIdValue = supportingIdMatch?.[1]?.trim() ?? "";
    if (!supportingIdValue) {
      errors.push(withEvidence("Proof-path evidence reference is missing a concrete supporting ID value.", pageArtifact));
      continue;
    }

    if (!/^[A-Z]{2}-C\d{2}(?:\s*(?:,|and)\s*[A-Z]{2}-C\d{2})*$/.test(supportingIdValue)) {
      errors.push(withEvidence(`Proof-path evidence reference has non-public-safe supporting ID value: ${supportingIdValue}`, pageArtifact));
    }
  }
}

function validateStopConditions(html, errors) {
  for (const condition of requiredStopConditions) {
    const pattern = new RegExp(`\\bdata-stop-condition\\s*=\\s*["']${escapeRegExp(condition)}["']`, "i");
    if (!pattern.test(html)) {
      errors.push(withEvidence(`Proof-path story gallery is missing stop condition: ${condition}`, pageArtifact));
    }
  }

  for (const match of html.matchAll(/<div\b(?=[^>]*\bdata-stop-condition\s*=)[^>]*>/gi)) {
    if (!/\bdata-owner-route\s*=\s*["'][^"']+["']/i.test(match[0])) {
      errors.push(withEvidence("Proof-path stop condition is missing owner/question routing.", pageArtifact));
    }
  }
}

function validatePageMetadata(html, errors) {
  const metaTags = findTagAttributes(html, "meta");
  const linkTags = findTagAttributes(html, "link");
  const checks = [
    [/<title>[^<]*Proof-Path Story Gallery[^<]*<\/title>/i.test(html), "title"],
    [hasMeta(metaTags, { name: "description", content: "non-empty" }), "description"],
    [
      linkTags.some(
        (attributes) =>
          hasRel(attributes, "canonical") &&
          getAttribute(attributes, "href") === "https://tracemap.tools/proof-path-stories/"
      ),
      "canonical URL"
    ],
    [hasMeta(metaTags, { property: "og:type", content: "article" }), "Open Graph type"],
    [hasMeta(metaTags, { property: "og:title", content: "non-empty" }), "Open Graph title"],
    [hasMeta(metaTags, { property: "og:description", content: "non-empty" }), "Open Graph description"],
    [
      hasMeta(metaTags, {
        property: "og:url",
        content: "https://tracemap.tools/proof-path-stories/"
      }),
      "Open Graph URL"
    ]
  ];

  for (const [passed, label] of checks) {
    if (!passed) {
      errors.push(withEvidence(`Proof-path story gallery page is missing required metadata: ${label}`, pageArtifact));
    }
  }

  const metadataText = collectMetadataText(html);
  if (!metadataText.toLowerCase().includes("concept")) {
    errors.push(withEvidence("Proof-path story gallery metadata must keep concept-level wording.", pageArtifact));
  }
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
      errors.push(withEvidence(`Proof-path story gallery page links to unresolved internal route: ${route}`, pageArtifact));
    }
  }
}

function validateForbiddenClaims({ errors, text, label }) {
  for (const pattern of forbiddenClaimPatterns) {
    const flags = pattern.flags.includes("g") ? pattern.flags : `${pattern.flags}g`;
    const globalPattern = new RegExp(pattern.source, flags);
    for (const match of text.matchAll(globalPattern)) {
      if (!hasNegatedContext(text, match.index)) {
        errors.push(withEvidence(`Proof-path story gallery contains forbidden public claim in ${label}: ${match[0]}`, pageArtifact));
        break;
      }
    }
  }
}

function validateRawMaterial({ errors, text, label }) {
  for (const pattern of rawMaterialPatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Proof-path story gallery contains forbidden raw/private material ${label}: ${pattern.source}`, pageArtifact));
    }
  }
}

function validateHardPrivateMaterial({ errors, text, label }) {
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Proof-path story gallery contains hard private material in ${label}: ${pattern.source}`, pageArtifact));
    }
  }
}

function hasNegatedContext(value, index) {
  const prefix = value.slice(Math.max(0, index - 56), index).toLowerCase();
  return /(?:not|no|without|cannot|can't|does not|do not|never)\s+(?:a\s+)?(?:real\s+)?(?:new\s+)?$/.test(prefix);
}

function hasId(html, id) {
  return new RegExp(`\\bid\\s*=\\s*["']${escapeRegExp(id)}["']`, "i").test(html);
}

function hasHref(html, href) {
  return new RegExp(`\\bhref\\s*=\\s*["']${escapeRegExp(href)}["']`, "i").test(html);
}

function hasDataField(html, attribute, field) {
  return new RegExp(`\\b${escapeRegExp(attribute)}\\s*=\\s*["']${escapeRegExp(field)}["']`, "i").test(html);
}

function getAttributeFromTag(html, name) {
  const startTag = html.match(/^<\w+\b[^>]*>/i)?.[0] ?? "";
  return getAttribute(startTag, name);
}

function stripSanctionedBoundaryRegions(html) {
  return html.replace(
    /<section\b(?=[^>]*(?:\bdata-boundary\s*=\s*["'][^"']+["']|\bdata-tm-boundary\s*=\s*["'][^"']+["']|\bid\s*=\s*["']non-claims-and-forbidden-wording["']|\bclass\s*=\s*["'][^"']*(?:boundary-example|rejected-example|non-claim-context)[^"']*["']))[^>]*>[\s\S]*?<\/section>/gi,
    " "
  );
}

function collectMetadataText(html) {
  const values = [];
  for (const attributes of findTagAttributes(html, "meta")) {
    const content = getAttribute(attributes, "content");
    if (content) {
      values.push(content);
    }
  }

  values.push(...[...html.matchAll(/<title>([\s\S]*?)<\/title>/gi)].map((match) => match[1]));
  return normalizeRenderedText(values.join(" "));
}

function collectDecodedAttributeText(html) {
  return normalizeRenderedText(findAllAttributeValues(html).join(" "));
}

function normalizeTightHtmlText(html) {
  return decodeHtmlEntities(html)
    .replace(/<!--[\s\S]*?-->/g, " ")
    .replace(/<[^>]+>/g, "")
    .replace(/[^a-z0-9]+/gi, "")
    .toLowerCase();
}

function extractMainHtml(html) {
  return html.match(/<main\b[^>]*>([\s\S]*?)<\/main>/i)?.[1] ?? html;
}

function findTagAttributes(html, tagName) {
  return [...html.matchAll(new RegExp(`<${tagName}\\b([^>]*)>`, "gi"))].map((match) => match[1]);
}

function findAllAttributeValues(html) {
  return [...html.matchAll(/\s[a-zA-Z_:][-a-zA-Z0-9_:.]*\s*=\s*(["'])([\s\S]*?)\1/g)].map((match) =>
    decodeHtmlEntities(match[2])
  );
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

function getAttribute(attributes, name) {
  const escaped = escapeRegExp(name);
  const pattern = new RegExp(`\\b${escaped}\\s*=\\s*(["'])([\\s\\S]*?)\\1`, "i");
  const match = attributes.match(pattern);
  return match ? decodeHtmlEntities(match[2]) : null;
}

function extractHrefs(html) {
  return findTagAttributes(html, "a")
    .map((attributes) => getAttribute(attributes, "href"))
    .filter((href) => typeof href === "string" && href.trim() !== "");
}

function normalizeRouteHref(href) {
  if (!href) {
    return "";
  }

  const withoutHash = String(href).split("#")[0].split("?")[0];
  if (withoutHash === "") {
    return "/";
  }

  return withoutHash.endsWith("/") ? withoutHash : `${withoutHash}/`;
}

function withEvidence(message, artifact) {
  return `${message} [${artifact}]`;
}
