import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  escapeRegExp,
  fileExists,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const routeFlowEvidenceStoryRoute = "/proof-paths/route-flow/";
export const routeFlowEvidenceStoryRequiredLinks = [
  "/proof-paths/",
  "/proof-path-stories/",
  "/proof-paths/tour/",
  "/evidence/",
  "/limitations/",
  "/static-vs-runtime/",
  "/review-claim-checklist/",
  "/review-room/",
  "/capabilities/",
  "/demo/evidence-trail/",
  "/glossary/"
];
export const routeFlowEvidenceStoryInboundRoutes = ["/proof-paths/"];

const pageArtifact = "proof-paths/route-flow/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";
const stateArtifact = ".kiro/specs/site-tracemap-tools-route-flow-evidence-story/implementation-state.md";
const allowedBoundarySections = new Map([
  ["route-flow-story-static-boundary", "route-flow-static-boundary"],
  ["route-flow-story-rejected-patterns", "route-flow-rejected-patterns"],
  ["route-flow-story-stop-conditions", "route-flow-stop-conditions"]
]);

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "This concept page explains how a reader can inspect route-centered static evidence",
  "not a public demo result",
  "not a real TraceMap finding",
  "Checked-in route-flow evidence supports vocabulary",
  "Selected context must join through the proof path"
];

const requiredAnchors = [
  "route-flow-story-positioning",
  "route-flow-story-anatomy",
  "route-flow-story-current-evidence",
  "route-flow-story-rows",
  "route-flow-story-static-boundary",
  "route-flow-story-review-language",
  "route-flow-story-rejected-patterns",
  "route-flow-story-stop-conditions",
  "route-flow-story-continue"
];

const requiredProofFields = [
  "static question",
  "evidence path",
  "rule ID or rule family",
  "evidence tier",
  "coverage label",
  "supporting IDs",
  "source context",
  "limitation",
  "next owner"
];

const requiredEvidenceTiers = ["Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"];
const requiredClassifications = [
  "StrongStaticRouteFlow",
  "ProbableStaticRouteFlow",
  "NeedsReviewStaticRouteFlow",
  "NoRouteFlowEvidence",
  "UnknownAnalysisGap"
];
const requiredCoverageTerms = ["full", "partial", "reduced", "unknown", "unavailable", "future-only", "gap-labeled"];
const requiredRowTerms = [
  "selector",
  "endpoint/root",
  "route/root evidence",
  "bridge state",
  "static flow row",
  "context group",
  "service/helper",
  "repository/data",
  "query or SQL shape",
  "dependency surface",
  "value origin",
  "implementation candidate",
  "gap",
  "limitation",
  "owner follow-up"
];
const requiredStopTerms = [
  "missing proof path",
  "missing rule ID or rule family",
  "missing evidence tier",
  "missing coverage label",
  "missing limitation",
  "missing supporting public-safe source context",
  "private-only evidence",
  "hidden detail",
  "unjoined adjacent context",
  "ambiguous endpoint/root",
  "runtime-only binding",
  "reduced coverage",
  "schema/extractor gap",
  "unsupported demo claim",
  "forbidden runtime"
];
const requiredReviewOutcomes = [
  "show as static evidence",
  "show as context",
  "label the gap",
  "downgrade",
  "keep internal",
  "owner follow-up",
  "do not repeat"
];
const requiredSafeVerbs = ["inspect", "follow", "compare", "record", "label", "downgrade", "hold", "hand off", "escalate"];
const metadataNonClaimTerms = [
  "runtime behavior",
  "production traffic",
  "endpoint performance",
  "outage cause",
  "release safety",
  "operational safety",
  "release approval",
  "complete coverage",
  "business impact",
  "AI impact analysis",
  "LLM analysis",
  "autonomous approval",
  "replacement"
];

const forbiddenClaimPatterns = [
  /\b(?:TraceMap\s+|route-flow\s+)?(?:proves?|proven|proof of)\s+(?:runtime behavior|runtime request execution|production traffic|endpoint performance|outage cause|release approval|release safety|operational safety|complete coverage|business impact|product behavior|runtime dependency-injection target selection|branch feasibility|SQL execution|database state|data contents)\b/i,
  /\b(?:certifies?|guarantees?|verifies?|approves?)\s+(?:runtime behavior|production traffic|endpoint performance|release safety|operational safety|complete coverage|business impact|product behavior|release)\b/i,
  /\bidentifies?\s+outage (?:root )?cause\b/i,
  /\b(?:safe to release|production-proven)\b/i,
  /\bperforms?\s+(?:AI impact analysis|LLM impact analysis|LLM analysis)\b/i,
  /\buses?\s+(?:embeddings|vector databases|prompt classification)\b/i,
  /\bautonomously\s+approves?\b/i,
  /\breplaces?\s+(?:tests|code review|source review|runtime observability|service-owner judgment|human judgment|human review|telemetry|logs|traces|release controls)\b/i
];

const rawMaterialPatterns = [
  /\bfacts\.ndjson\b/i,
  /\bindex\.sqlite\b/i,
  /\bscan-manifest\.json\b/i,
  /\blogs\/analyzer\.log\b/i,
  /\braw facts?\b/i,
  /\braw SQLite(?: content)?\b/i,
  /\banalyzer logs?\b/i,
  /\braw source\b/i,
  /\braw SQL\b/i,
  /\braw config\b/i,
  /\bconfig values?\b/i,
  /\bsecrets?\b/i,
  /\braw local paths?\b/i,
  /\blocal paths?\b/i,
  /\braw repository remotes?\b/i,
  /\braw remotes?\b/i,
  /\bprivate sample names?\b/i,
  /\bprivate route values?\b/i,
  /\bgenerated output directories\b/i,
  /\bgenerated scan directories\b/i,
  /\bhidden validation details?\b/i,
  /\braw command output\b/i,
  /\bcredential-like values\b/i
];

const hardPrivatePatterns = [
  /\/Users\//i,
  /\/private\//i,
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

const blamePatterns = [
  /\b(?:blame|fault|guilty|negligent|culprit)\b/i,
  /\b(?:team|owner|reviewer|service|customer)\s+(?:failed|broke|caused)\b/i
];

const sanctionedBoundarySectionPattern =
  /<section\b(?=[^>]*\bdata-tm-boundary\s*=\s*["'][^"']+["'])[^>]*>[\s\S]*?<\/section>/gi;

export async function validateRouteFlowEvidenceStoryDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors,
  root
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "proof-paths", "route-flow", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Route-flow evidence story page is missing required public route: ${routeFlowEvidenceStoryRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }

  const routeContext = await readRouteContext({ dist, errors: localErrors });
  await validateRouteFlowEvidenceStoryPage({ pagePath, routeContext, errors: localErrors });
  await validateInboundLinks({ dist, errors: localErrors });

  if (root) {
    await validateImplementationState({ root, errors: localErrors });
  }

  errors.push(...localErrors);
}

function normalizeBaseUrl(value, errors) {
  let url;

  try {
    url = new URL(value);
  } catch {
    errors.push(withEvidence(`Route-flow evidence story baseUrl must be a valid absolute URL: ${String(value)}`, "baseUrl input"));
    return null;
  }

  if (url.protocol !== "https:" && url.protocol !== "http:") {
    errors.push(withEvidence(`Route-flow evidence story baseUrl must use http or https: ${String(value)}`, "baseUrl input"));
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
  if (!sitemapUrls.has(`${baseUrl}${routeFlowEvidenceStoryRoute}`)) {
    errors.push(withEvidence(`Route-flow evidence story sitemap is missing required route: ${baseUrl}${routeFlowEvidenceStoryRoute}`, sitemapArtifact));
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
    errors.push(withEvidence(`Route-flow evidence story could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Route-flow evidence story routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  for (const entry of parsed.entries) {
    if (typeof entry?.path === "string") {
      routes.add(normalizeRouteHref(entry.path));
    }
  }

  routeEntry = parsed.entries.find((entry) => normalizeRouteHref(entry?.path ?? "") === routeFlowEvidenceStoryRoute) ?? null;
  validateRouteEntry(routeEntry, errors);

  return { routeEntry, routes, sitemapRoutes };
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(withEvidence(`Route-flow evidence story routes-index.json is missing required route: ${routeFlowEvidenceStoryRoute}`, routesIndexArtifact));
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
      errors.push(withEvidence(`Route-flow evidence story routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  if (!Array.isArray(routeEntry.limitations) || routeEntry.limitations.length === 0) {
    errors.push(withEvidence("Route-flow evidence story routes-index.json must include limitations metadata.", routesIndexArtifact));
  }

  if (!Array.isArray(routeEntry.nonClaims) || routeEntry.nonClaims.length === 0) {
    errors.push(withEvidence("Route-flow evidence story routes-index.json must include nonClaims metadata.", routesIndexArtifact));
    return;
  }

  const normalizedNonClaimsText = normalizeScanText(routeEntry.nonClaims.join(" ")).toLowerCase();
  for (const term of metadataNonClaimTerms) {
    if (!normalizedNonClaimsText.includes(term.toLowerCase())) {
      errors.push(withEvidence(`Route-flow evidence story routes-index.json nonClaims are missing required term: ${term}`, routesIndexArtifact));
    }
  }

  const publicMetadataText = [routeEntry.title, routeEntry.summary, ...(routeEntry.limitations ?? [])].join(" ");
  validateForbiddenClaims({ errors, text: publicMetadataText, label: "metadata" });
}

async function validateRouteFlowEvidenceStoryPage({ pagePath, routeContext, errors }) {
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
      errors.push(withEvidence(`Route-flow evidence story page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const anchor of requiredAnchors) {
    if (!hasId(html, anchor)) {
      errors.push(withEvidence(`Route-flow evidence story page is missing required anchor: #${anchor}`, pageArtifact));
    }
  }

  for (const field of requiredProofFields) {
    if (!hasRouteFlowField(html, field)) {
      errors.push(withEvidence(`Route-flow evidence story page is missing proof-field marker: ${field}`, pageArtifact));
    }
  }

  for (const collection of [
    requiredEvidenceTiers,
    requiredClassifications,
    requiredCoverageTerms,
    requiredRowTerms,
    requiredStopTerms,
    requiredReviewOutcomes,
    requiredSafeVerbs
  ]) {
    for (const term of collection) {
      if (!lowerPageText.includes(term.toLowerCase())) {
        errors.push(withEvidence(`Route-flow evidence story page is missing required term: ${term}`, pageArtifact));
      }
    }
  }

  for (const link of routeFlowEvidenceStoryRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Route-flow evidence story page is missing required link: ${link}`, pageArtifact));
    }
  }

  validateIllustrativePatterns(html, errors);
  validateBoundarySections(html, errors);
  validatePageMetadata(html, errors);
  validateInternalRouteLinks(html, routeContext, errors);

  if (wordCount < 800 || wordCount > 1800) {
    errors.push(withEvidence(`Route-flow evidence story page word count must be between 800 and 1800 words, got ${wordCount}`, pageArtifact));
  }

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
  validateBlameLanguage({
    errors,
    text: `${strippedText} ${metadataText}`,
    label: "visible copy outside sanctioned boundary sections"
  });
  validateHardPrivateMaterial({
    errors,
    text: `${html} ${decodedHtml} ${pageText} ${allTextTight} ${metadataText} ${allAttributeText} ${JSON.stringify(routeContext.routeEntry ?? {})}`,
    label: "page, attributes, or metadata"
  });
}

function validateIllustrativePatterns(html, errors) {
  const reviewSection = html.match(/<section\b(?=[^>]*\bid\s*=\s*["']route-flow-story-review-language["'])([\s\S]*?)<\/section>/i);
  if (!reviewSection) {
    errors.push(withEvidence("Route-flow evidence story page is missing review language section.", pageArtifact));
    return;
  }

  const reviewText = normalizeRenderedText(reviewSection[0]);
  if (!/Illustrative safe pattern/i.test(reviewText) || !/Illustrative gap pattern/i.test(reviewText)) {
    errors.push(withEvidence("Route-flow evidence story safe examples must be visibly labeled illustrative.", pageArtifact));
  }

  if (!/not a real TraceMap finding/i.test(reviewText)) {
    errors.push(withEvidence("Route-flow evidence story illustrative safe example must say it is not a real TraceMap finding.", pageArtifact));
  }

  const rejectedSection = html.match(/<section\b(?=[^>]*\bid\s*=\s*["']route-flow-story-rejected-patterns["'])([\s\S]*?)<\/section>/i);
  if (!rejectedSection) {
    errors.push(withEvidence("Route-flow evidence story page is missing rejected patterns section.", pageArtifact));
    return;
  }

  if (!/data-tm-boundary\s*=\s*["']route-flow-rejected-patterns["']/i.test(rejectedSection[0])) {
    errors.push(withEvidence("Route-flow evidence story rejected patterns must be inside a sanctioned boundary section.", pageArtifact));
  }
}

function validateBoundarySections(html, errors) {
  const seen = new Map();

  for (const section of findSectionTags(html)) {
    const id = getAttribute(section.attributes, "id");
    const boundary = getAttribute(section.attributes, "data-tm-boundary");
    if (!boundary) {
      continue;
    }

    if (!id || allowedBoundarySections.get(id) !== boundary) {
      errors.push(
        withEvidence(
          `Route-flow evidence story has unsupported boundary section: id=${String(id)} data-tm-boundary=${boundary}`,
          pageArtifact
        )
      );
      continue;
    }

    seen.set(id, boundary);
  }

  for (const [id, boundary] of allowedBoundarySections) {
    if (seen.get(id) !== boundary) {
      errors.push(withEvidence(`Route-flow evidence story is missing sanctioned boundary section: #${id}`, pageArtifact));
    }
  }
}

function validatePageMetadata(html, errors) {
  const metaTags = findTagAttributes(html, "meta");
  const linkTags = findTagAttributes(html, "link");
  const checks = [
    [/<title>Route-Flow Evidence Story \| TraceMap<\/title>/i.test(html), "title"],
    [hasMeta(metaTags, { name: "description", content: "non-empty" }), "description"],
    [
      linkTags.some(
        (attributes) =>
          hasRel(attributes, "canonical") &&
          getAttribute(attributes, "href") === "https://tracemap.tools/proof-paths/route-flow/"
      ),
      "canonical URL"
    ],
    [hasMeta(metaTags, { property: "og:type", content: "article" }), "Open Graph type"],
    [hasMeta(metaTags, { property: "og:title", content: "non-empty" }), "Open Graph title"],
    [hasMeta(metaTags, { property: "og:description", content: "non-empty" }), "Open Graph description"],
    [
      hasMeta(metaTags, {
        property: "og:url",
        content: "https://tracemap.tools/proof-paths/route-flow/"
      }),
      "Open Graph URL"
    ]
  ];

  for (const [passed, label] of checks) {
    if (!passed) {
      errors.push(withEvidence(`Route-flow evidence story page is missing required metadata: ${label}`, pageArtifact));
    }
  }
}

async function validateInboundLinks({ dist, errors }) {
  const missing = [];

  for (const route of routeFlowEvidenceStoryInboundRoutes) {
    const pagePath = resolve(dist, route.replace(/^\/|\/$/g, ""), "index.html");
    if (!(await fileExists(pagePath))) {
      continue;
    }

    const html = await readFile(pagePath, "utf8");
    if (!hasHref(html, routeFlowEvidenceStoryRoute)) {
      missing.push(route);
    }
  }

  if (missing.length > 0) {
    errors.push(withEvidence(`Route-flow evidence story is missing inbound links from live adjacent routes: ${missing.join(", ")}`, "adjacent route HTML"));
  }
}

async function validateImplementationState({ root, errors }) {
  const statePath = await firstExistingPath([resolve(root, "..", stateArtifact), resolve(root, stateArtifact)]);
  if (!(await fileExists(statePath))) {
    errors.push(withEvidence("Route-flow evidence story implementation-state file is missing.", stateArtifact));
    return;
  }

  const state = await readFile(statePath, "utf8");
  const requiredStateText = [
    "Selected placement: `/proof-paths/route-flow/`",
    "Rejected placement alternatives",
    "Adjacent route decisions",
    "Current-branch evidence statements",
    "Browser sanity checks"
  ];

  for (const phrase of requiredStateText) {
    if (!state.includes(phrase)) {
      errors.push(withEvidence(`Route-flow evidence story implementation-state is missing required record: ${phrase}`, stateArtifact));
    }
  }
}

async function firstExistingPath(paths) {
  for (const path of paths) {
    if (await fileExists(path)) {
      return path;
    }
  }

  return paths[0];
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
      errors.push(withEvidence(`Route-flow evidence story page links to unresolved internal route: ${route}`, pageArtifact));
    }
  }
}

function validateForbiddenClaims({ errors, text, label }) {
  for (const pattern of forbiddenClaimPatterns) {
    const flags = pattern.flags.includes("g") ? pattern.flags : `${pattern.flags}g`;
    const globalPattern = new RegExp(pattern.source, flags);
    for (const match of text.matchAll(globalPattern)) {
      if (!hasNegatedContext(text, match.index)) {
        errors.push(withEvidence(`Route-flow evidence story contains forbidden public claim in ${label}: ${match[0]}`, pageArtifact));
        break;
      }
    }
  }
}

function validateRawMaterial({ errors, text, label }) {
  for (const pattern of rawMaterialPatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Route-flow evidence story contains forbidden raw/private material ${label}: ${pattern.source}`, pageArtifact));
    }
  }
}

function validateHardPrivateMaterial({ errors, text, label }) {
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Route-flow evidence story contains hard private material in ${label}: ${pattern.source}`, pageArtifact));
    }
  }
}

function validateBlameLanguage({ errors, text, label }) {
  for (const pattern of blamePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Route-flow evidence story contains blame language in ${label}: ${pattern.source}`, pageArtifact));
    }
  }
}

function hasNegatedContext(value, index) {
  const prefix = value.slice(Math.max(0, index - 72), index).toLowerCase();
  return /(?:not|no|without|cannot|can't|does not|do not|never|outside|rejected pattern:\s*)\s+(?:a\s+)?(?:real\s+)?(?:new\s+)?(?:public\s+)?$/.test(prefix);
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

function hasId(html, id) {
  const escaped = escapeRegExp(id);
  return new RegExp(`\\bid\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasRouteFlowField(html, field) {
  const escaped = escapeRegExp(field);
  return new RegExp(`\\bdata-route-flow-field\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasHref(html, href) {
  const normalizedHref = normalizeRouteHref(href);
  return extractHrefs(html).some((candidate) => normalizeRouteHref(candidate) === normalizedHref);
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
      insideTag = true;
      continue;
    }

    text += char;
  }

  return text;
}

function extractMainHtml(html) {
  const match = html.match(/<main\b[^>]*>([\s\S]*?)<\/main>/i);
  return match ? match[1] : html;
}

function extractHrefs(html) {
  return [...html.matchAll(/\bhref\s*=\s*("[^"]*"|'[^']*')/gi)]
    .map((match) => decodeHtmlEntities(unquoteAttributeValue(match[1])))
    .filter((href) => !href.startsWith("#"));
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

function findSectionTags(html) {
  const tags = [];
  let from = 0;

  while (from < html.length) {
    const tag = findNextTag(html, "section", from);
    if (!tag) {
      break;
    }

    tags.push(tag);
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
