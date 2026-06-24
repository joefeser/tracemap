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

export const evidenceDecisionRecordRoute = "/decisions/evidence-record/";
export const evidenceDecisionRecordRequiredLinks = [
  "/review-room/",
  "/packets/assembly/",
  "/review-claim-checklist/",
  "/manager-packet/",
  "/questions/objections/",
  "/proof-paths/tour/",
  "/proof-paths/",
  "/limitations/",
  "/validation/"
];
export const evidenceDecisionRecordInboundRoutes = ["/review-room/", "/packets/assembly/"];

const pageArtifact = "decisions/evidence-record/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";
const implementationStateArtifact = ".kiro/specs/site-tracemap-tools-evidence-decision-record/implementation-state.md";

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "TraceMap provides evidence, not the decision"
];

const requiredSections = [
  "why-record-the-decision",
  "record-template",
  "example-safe-record",
  "unsafe-record-examples",
  "stop-conditions",
  "follow-up-owners",
  "non-claims"
];

const requiredSectionHeadings = [
  "Why record the decision",
  "Record template",
  "Example safe record",
  "Unsafe record examples",
  "Stop conditions",
  "Follow-up owners",
  "Non-claims"
];

const requiredFields = [
  "decision question",
  "decision owner",
  "public claim level",
  "proof path",
  "rule ID/family",
  "evidence tier",
  "coverage label",
  "commit SHA",
  "extractor version",
  "limitation",
  "non-claim",
  "validation evidence",
  "rejected interpretation",
  "follow-up owner",
  "review date placeholder",
  "residual risk"
];

const requiredEvidenceTiers = ["Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"];

const metadataNonClaimTerms = [
  "autonomous decision",
  "approval workflow",
  "release approval",
  "release safety",
  "operational safety",
  "runtime proof",
  "production proof",
  "absence-of-impact proof",
  "complete coverage",
  "AI analysis",
  "LLM analysis",
  "replacement of tests",
  "human judgment"
];

const requiredNonClaimTerms = [
  "autonomous decisions",
  "approval workflow",
  "release approval",
  "release safety",
  "operational safety",
  "runtime behavior",
  "production behavior",
  "endpoint performance",
  "outage cause",
  "absence of impact",
  "complete coverage",
  "AI analysis",
  "LLM analysis",
  "embeddings",
  "vector databases",
  "prompt classification",
  "human judgment"
];

const requiredOwnerRoles = [
  "service owner",
  "runtime observability owner",
  "release owner",
  "test owner",
  "reviewer",
  "security owner",
  "repository owner",
  "manager",
  "TraceMap owner"
];

const allowedValidationContexts = [
  "unsafe-example",
  "non-claim",
  "limitation",
  "stop-condition",
  "rejected-interpretation",
  "residual-risk"
];

const forbiddenPositiveClaimPatterns = [
  /\bTraceMap\b[^.]{0,90}\b(?:decides?|approves?|blocks?|certifies?|validates?|clears?)\b/i,
  /\bTraceMap\b[^.]{0,90}\b(?:proves?|guarantees?)\s+(?:runtime|runtime behavior|production|production behavior|production traffic|endpoint performance|outage cause|release safety|operational safety|absence of impact|no impact|complete coverage)\b/i,
  /\b(?:approved for release|safe to release|validated for release|certified for release|production-proven|runtime-proven|proves no impact|no impact proof|complete coverage proof)\b/i,
  /\b(?:AI-powered decision|LLM-powered decision|AI impact analysis|LLM impact analysis|embedding-backed|vector database reasoning|prompt classification)\b/i,
  /\breplaces?\s+(?:tests|code review|source review|runtime observability|telemetry|release process|service-owner review|governance|human judgment)\b/i
];

const rawMaterialPatterns = [
  /\braw facts?\b/i,
  /\braw SQLite(?: content)?\b/i,
  /\banalyzer logs?\b/i,
  /\braw source snippets?\b/i,
  /\braw SQL\b/i,
  /\bconfig values?\b/i,
  /\braw remotes?\b/i,
  /\bgenerated scan directories\b/i,
  /\bprivate sample names?\b/i,
  /\braw command output\b/i,
  /\bhidden validation details\b/i,
  /\bcredential-like values?\b/i
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

const blamePatterns = [
  /\bfault of\b/i,
  /\bat fault\b/i,
  /\bbad team\b/i,
  /\bbad vendor\b/i,
  /\bbad code\b/i,
  /\bcareless reviewer\b/i,
  /\bbroken codebase\b/i,
  /\bfailed team\b/i
];

export async function validateEvidenceDecisionRecordDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "decisions", "evidence-record", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Evidence decision record is missing required public route: ${evidenceDecisionRecordRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  const routeContext = await readRouteContext({ dist, errors: localErrors });
  await validatePage({ pagePath, routeContext, errors: localErrors });
  await validateInboundLinks({ dist, errors: localErrors });
  await validateImplementationState({ dist, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, sitemapArtifact);
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${evidenceDecisionRecordRoute}`)) {
    errors.push(withEvidence(`Evidence decision record sitemap is missing required route: ${baseUrl}${evidenceDecisionRecordRoute}`, sitemapArtifact));
  }
}

async function readRouteContext({ dist, errors }) {
  const routesIndexPath = resolve(dist, routesIndexArtifact);
  const sitemapPath = resolve(dist, sitemapArtifact);
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

  if (!(await fileExists(routesIndexPath))) {
    return { routeEntry, routes, sitemapRoutes };
  }

  let parsed;
  try {
    parsed = JSON.parse(await readFile(routesIndexPath, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Evidence decision record could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Evidence decision record routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  for (const entry of parsed.entries) {
    if (typeof entry?.path === "string") {
      routes.add(normalizeRouteHref(entry.path));
    }
  }

  routeEntry = parsed.entries.find((entry) => normalizeRouteHref(entry?.path ?? "") === evidenceDecisionRecordRoute) ?? null;
  validateRouteEntry(routeEntry, errors);

  return { routeEntry, routes, sitemapRoutes };
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(withEvidence(`Evidence decision record routes-index.json is missing required route: ${evidenceDecisionRecordRoute}`, routesIndexArtifact));
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
      errors.push(withEvidence(`Evidence decision record routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  if (!Array.isArray(routeEntry.limitations) || routeEntry.limitations.length === 0) {
    errors.push(withEvidence("Evidence decision record routes-index.json must include limitations metadata.", routesIndexArtifact));
  }

  if (!Array.isArray(routeEntry.nonClaims) || routeEntry.nonClaims.length === 0) {
    errors.push(withEvidence("Evidence decision record routes-index.json must include nonClaims metadata.", routesIndexArtifact));
    return;
  }

  const nonClaimsText = normalizeScanText(routeEntry.nonClaims.join(" "));
  for (const term of metadataNonClaimTerms) {
    if (!nonClaimsText.includes(term.toLowerCase())) {
      errors.push(withEvidence(`Evidence decision record routes-index.json nonClaims are missing required term: ${term}`, routesIndexArtifact));
    }
  }

  validateForbiddenPositiveClaims({
    errors,
    text: [routeEntry.title, routeEntry.summary, ...(routeEntry.limitations ?? [])].join(" "),
    label: "metadata",
    artifact: routesIndexArtifact
  });
}

async function validatePage({ pagePath, routeContext, errors }) {
  const html = await readFile(pagePath, "utf8");
  const mainHtml = extractMainHtml(html);
  const pageText = normalizeRenderedText(mainHtml);
  const strippedHtml = stripAllowedBoundaryContexts(mainHtml);
  const strippedText = normalizeRenderedText(strippedHtml);
  const metadataText = collectMetadataText(html);
  const attributeText = collectDecodedAttributeText(html);
  const strippedAttributeText = collectDecodedAttributeText(strippedHtml);
  const wordCount = countWords(normalizeRenderedText(stripRepeatedFieldLabels(mainHtml)));

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Evidence decision record page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const sectionId of requiredSections) {
    if (!hasSectionId(html, sectionId)) {
      errors.push(withEvidence(`Evidence decision record page is missing required section anchor: ${sectionId}`, pageArtifact));
    }
  }

  for (const heading of requiredSectionHeadings) {
    if (!pageText.includes(heading)) {
      errors.push(withEvidence(`Evidence decision record page is missing required section heading: ${heading}`, pageArtifact));
    }
  }

  validateFields(html, errors);
  validateRequiredLinks(html, routeContext, errors);
  validatePageMetadata(html, errors);
  validateTierVocabulary(pageText, errors);
  validateOwnerRoles(pageText, errors);
  validatePlaceholderEvidence(pageText, errors);
  validateNonClaimTerms(pageText, routeContext.routeEntry, errors);
  validateContextMarkers(html, errors);

  if (wordCount < 700 || wordCount > 2500) {
    errors.push(withEvidence(`Evidence decision record page word count must be between 700 and 2500 words, got ${wordCount}`, pageArtifact));
  }

  validateForbiddenPositiveClaims({
    errors,
    text: `${strippedText} ${normalizeTightHtmlText(strippedHtml)} ${metadataText} ${strippedAttributeText}`,
    label: "page copy outside allowed boundary contexts",
    artifact: pageArtifact
  });
  validateRawMaterial({
    errors,
    text: `${strippedText} ${normalizeTightHtmlText(strippedHtml)} ${metadataText} ${strippedAttributeText}`,
    label: "outside allowed boundary contexts",
    artifact: pageArtifact
  });
  validateHardPrivateMaterial({
    errors,
    text: `${html} ${decodeHtmlEntities(html)} ${pageText} ${normalizeTightHtmlText(html)} ${metadataText} ${attributeText} ${JSON.stringify(routeContext.routeEntry ?? {})}`,
    label: "page, attributes, or metadata",
    artifact: pageArtifact
  });
  validateBlameLanguage(`${strippedText} ${metadataText} ${strippedAttributeText}`, errors, pageArtifact);
}

function validateFields(html, errors) {
  for (const field of requiredFields) {
    if (!hasDataValue(html, "data-record-field", field)) {
      errors.push(withEvidence(`Evidence decision record template is missing required field: ${field}`, pageArtifact));
    }

    if (!hasDataValue(html, "data-safe-record-field", field)) {
      errors.push(withEvidence(`Evidence decision record safe example is missing required field: ${field}`, pageArtifact));
    }
  }

  if (!/<table\b(?=[^>]*\bdata-evidence-decision-template\b)[^>]*>[\s\S]*?<th\b[^>]*\bscope\s*=\s*["']col["'][^>]*>Field<\/th>/i.test(html)) {
    errors.push(withEvidence("Evidence decision record template must use semantic table headers.", pageArtifact));
  }
}

function validateRequiredLinks(html, routeContext, errors) {
  for (const link of evidenceDecisionRecordRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Evidence decision record page is missing required link: ${link}`, pageArtifact));
    }

    if (routeContext.routes.size > 0 && !routeContext.routes.has(normalizeRouteHref(link))) {
      errors.push(withEvidence(`Evidence decision record required link is not present in discovery route index: ${link}`, routesIndexArtifact));
    }
  }

  validateInternalRouteLinks(html, routeContext, errors);
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
          getAttribute(attributes, "href") === "https://tracemap.tools/decisions/evidence-record/"
      ),
      "canonical URL"
    ],
    [hasMeta(metaTags, { property: "og:type", content: "article" }), "Open Graph type"],
    [hasMeta(metaTags, { property: "og:title", content: "non-empty" }), "Open Graph title"],
    [hasMeta(metaTags, { property: "og:description", content: "non-empty" }), "Open Graph description"],
    [
      hasMeta(metaTags, {
        property: "og:url",
        content: "https://tracemap.tools/decisions/evidence-record/"
      }),
      "Open Graph URL"
    ]
  ];

  for (const [passed, label] of checks) {
    if (!passed) {
      errors.push(withEvidence(`Evidence decision record page is missing required metadata: ${label}`, pageArtifact));
    }
  }
}

function validateTierVocabulary(pageText, errors) {
  for (const tier of requiredEvidenceTiers) {
    if (!pageText.includes(tier)) {
      errors.push(withEvidence(`Evidence decision record page is missing evidence tier: ${tier}`, pageArtifact));
    }
  }

  for (const match of pageText.matchAll(/\bTier\d[A-Za-z]+\b/g)) {
    if (!requiredEvidenceTiers.includes(match[0])) {
      errors.push(withEvidence(`Evidence decision record page contains unsupported evidence tier: ${match[0]}`, pageArtifact));
    }
  }
}

function validateOwnerRoles(pageText, errors) {
  for (const role of requiredOwnerRoles) {
    if (!pageText.includes(role)) {
      errors.push(withEvidence(`Evidence decision record page is missing follow-up owner role: ${role}`, pageArtifact));
    }
  }

  if (!pageText.includes("reviewer role placeholder")) {
    errors.push(withEvidence("Evidence decision record example must use a public role placeholder for decision owner.", pageArtifact));
  }
}

function validatePlaceholderEvidence(pageText, errors) {
  if (!pageText.includes("YYYY-MM-DD")) {
    errors.push(withEvidence("Evidence decision record example must use review date placeholder YYYY-MM-DD.", pageArtifact));
  }

  if (!pageText.includes("example-public-sha")) {
    errors.push(withEvidence("Evidence decision record example must use synthetic commit SHA placeholder.", pageArtifact));
  }

  if (!pageText.includes("example-extractor-version")) {
    errors.push(withEvidence("Evidence decision record example must use synthetic extractor version placeholder.", pageArtifact));
  }

  if (/\b[0-9a-f]{40}\b/i.test(pageText)) {
    errors.push(withEvidence("Evidence decision record example must not publish a real-looking 40-character commit SHA.", pageArtifact));
  }

  if (!/Public-safe validation summary/i.test(pageText)) {
    errors.push(withEvidence("Evidence decision record example must name public-safe validation evidence.", pageArtifact));
  }
}

function validateNonClaimTerms(pageText, routeEntry, errors) {
  const nonClaimText = normalizeScanText(`${pageText} ${routeEntry?.nonClaims?.join(" ") ?? ""}`);
  for (const term of requiredNonClaimTerms) {
    if (!nonClaimText.includes(term.toLowerCase())) {
      errors.push(withEvidence(`Evidence decision record is missing required non-claim term: ${term}`, pageArtifact));
    }
  }
}

function validateContextMarkers(html, errors) {
  const contexts = [...html.matchAll(/\bdata-tracemap-validation-context\s*=\s*["']([^"']+)["']/gi)].map((match) =>
    decodeHtmlEntities(match[1])
  );

  for (const context of allowedValidationContexts) {
    if (!contexts.includes(context)) {
      errors.push(withEvidence(`Evidence decision record is missing validation context marker: ${context}`, pageArtifact));
    }
  }

  for (const context of contexts) {
    if (!allowedValidationContexts.includes(context)) {
      errors.push(withEvidence(`Evidence decision record contains unsupported validation context marker: ${context}`, pageArtifact));
    }
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
      errors.push(withEvidence(`Evidence decision record page links to unresolved internal route: ${route}`, pageArtifact));
    }
  }
}

async function validateInboundLinks({ dist, errors }) {
  const missing = [];

  for (const route of evidenceDecisionRecordInboundRoutes) {
    const pagePath = resolve(dist, route.replace(/^\/|\/$/g, ""), "index.html");
    if (!(await fileExists(pagePath))) {
      continue;
    }

    const html = await readFile(pagePath, "utf8");
    if (!hasHref(html, evidenceDecisionRecordRoute)) {
      missing.push(route);
    }
  }

  if (missing.length > 0) {
    errors.push(withEvidence(`Evidence decision record is missing inbound links from live adjacent routes: ${missing.join(", ")}`, "adjacent route HTML"));
  }
}

async function validateImplementationState({ dist, errors }) {
  const statePath = await findImplementationStatePath(dist);
  if (!statePath) {
    errors.push(withEvidence("Evidence decision record implementation state is missing placement decision record.", implementationStateArtifact));
    return;
  }

  const text = await readFile(statePath, "utf8");
  const requiredPhrases = [
    "Selected placement: `/decisions/evidence-record/`",
    "Rejected alternatives",
    "`/review-room/decision-record/`",
    "section on `/review-room/`",
    "section on `/packets/assembly/`",
    "decision-after-evidence record",
    "review-room agenda",
    "packet assembly checklist",
    "claim checklist",
    "manager packet",
    "objection guide",
    "proof-path tour",
    "release gate",
    "runtime workflow",
    "approval workflow",
    "autonomous decision system"
  ];

  for (const phrase of requiredPhrases) {
    if (!text.includes(phrase)) {
      errors.push(withEvidence(`Evidence decision record implementation-state is missing placement record phrase: ${phrase}`, implementationStateArtifact));
    }
  }
}

async function findImplementationStatePath(dist) {
  // Check one level up first (dist/../.kiro/...) — this works for both the test
  // setup (dist = root/dist, so dist/.. = root) and the common production layout
  // where dist is one directory below the project root.
  // The two-levels-up candidate (dist/../../.kiro/...) is omitted: in tests it
  // would escape the temp dir and in production it would point to the wrong root.
  const candidates = [
    resolve(dist, "..", implementationStateArtifact),
    resolve(dist, "..", "..", implementationStateArtifact)
  ];

  for (const candidate of candidates) {
    if (await fileExists(candidate)) {
      return candidate;
    }
  }

  return null;
}

function validateForbiddenPositiveClaims({ errors, text, label, artifact }) {
  const normalized = normalizeScanText(text).replace(/\bpublic-safe\b/gi, "public evidence");
  for (const pattern of forbiddenPositiveClaimPatterns) {
    const flags = pattern.flags.includes("g") ? pattern.flags : `${pattern.flags}g`;
    const globalPattern = new RegExp(pattern.source, flags);
    for (const match of normalized.matchAll(globalPattern)) {
      if (!hasNegationForMatchedClaim(match[0]) && !isNegated(normalized, match.index ?? 0)) {
        errors.push(withEvidence(`Evidence decision record contains forbidden positive claim in ${label}: ${match[0]}`, artifact));
      }
    }
  }
}

function validateRawMaterial({ errors, text, label, artifact }) {
  const values = privateMaterialScanValues(text);
  for (const pattern of rawMaterialPatterns) {
    if (values.some((value) => pattern.test(value))) {
      errors.push(withEvidence(`Evidence decision record contains forbidden raw/private material ${label}: ${pattern.source}`, artifact));
    }
  }
}

function validateHardPrivateMaterial({ errors, text, label, artifact }) {
  const values = privateMaterialScanValues(text);
  for (const pattern of hardPrivatePatterns) {
    if (values.some((value) => pattern.test(value))) {
      errors.push(withEvidence(`Evidence decision record contains hard private material in ${label}: ${pattern.source}`, artifact));
    }
  }
}

function validateBlameLanguage(value, errors, artifact) {
  for (const pattern of blamePatterns) {
    if (pattern.test(value)) {
      errors.push(withEvidence(`Evidence decision record contains blame-oriented wording: ${pattern.source}`, artifact));
    }
  }
}

function stripAllowedBoundaryContexts(html) {
  let stripped = html;

  for (const id of ["unsafe-record-examples", "stop-conditions", "non-claims"]) {
    stripped = stripped.replace(sectionByIdPattern(id), " ");
  }

  stripped = stripped.replace(
    /<tr\b(?=[^>]*\bdata-record-field\s*=\s*["'](?:limitation|non-claim|rejected interpretation|residual risk)["'])[^>]*>[\s\S]*?<\/tr>/gi,
    " "
  );

  stripped = stripped.replace(
    /<div\b(?=[^>]*\bdata-safe-record-field\s*=\s*["'](?:limitation|non-claim|rejected interpretation|residual risk)["'])[^>]*>[\s\S]*?<\/div>/gi,
    " "
  );

  // Codex P2: also strip data-tracemap-validation-context="non-claim" containers
  // so a forbidden positive claim inside such a marker does not trigger a false positive.
  stripped = stripped.replace(
    /<div\b(?=[^>]*\bdata-tracemap-validation-context\s*=\s*["'](?:non-claim|limitation|residual-risk)["'])[^>]*>[\s\S]*?<\/div>/gi,
    " "
  );

  return stripped;
}

function stripRepeatedFieldLabels(html) {
  return html
    .replace(/<thead\b[^>]*>[\s\S]*?<\/thead>/gi, " ")
    .replace(/<strong>[^<]+<\/strong>/gi, " ");
}

function sectionByIdPattern(id) {
  return new RegExp(`<section\\b(?=[^>]*\\bid\\s*=\\s*["']${escapeRegExp(id)}["'])[^>]*>[\\s\\S]*?<\\/section>`, "gi");
}

function extractSectionHtml(html, id) {
  return html.match(sectionByIdPattern(id))?.[0] ?? "";
}

function hasSectionId(html, id) {
  return new RegExp(`<section\\b(?=[^>]*\\bid\\s*=\\s*["']${escapeRegExp(id)}["'])`, "i").test(html);
}

function hasDataValue(html, attribute, value) {
  return new RegExp(`\\b${escapeRegExp(attribute)}\\s*=\\s*["']${escapeRegExp(value)}["']`, "i").test(html);
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href.replace(/\/$/, ""));
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escaped}\\/?["']`, "i").test(html);
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
  const match = attributes.match(new RegExp(`\\b${escapeRegExp(name)}\\s*=\\s*("[^"]*"|'[^']*')`, "i"));
  return match ? decodeHtmlEntities(unquoteAttributeValue(match[1])) : null;
}

function findTagAttributes(html, tagName) {
  return [...html.matchAll(new RegExp(`<${escapeRegExp(tagName)}\\b([^>]*)>`, "gi"))].map((match) => match[1]);
}

function findAllTagAttributes(html) {
  return [...html.matchAll(/<([a-z][a-z0-9:-]*)\b([^>]*)>/gi)].map((match) => match[2]);
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

function extractHrefs(html) {
  return [...html.matchAll(/<a\b[^>]*\bhref\s*=\s*("[^"]*"|'[^']*')/gi)].map((match) =>
    unquoteAttributeValue(match[1])
  );
}

function unquoteAttributeValue(value) {
  return value.slice(1, -1);
}

function extractMainHtml(html) {
  return html.match(/<main\b[^>]*>([\s\S]*?)<\/main>/i)?.[1] ?? html;
}

function normalizeTightHtmlText(html) {
  return normalizeScanText(decodeHtmlEntities(stripTagsTight(html)));
}

function stripTagsTight(html) {
  return html.replace(/<[^>]+>/g, "");
}

function privateMaterialScanValues(value) {
  const decoded = decodeHtmlEntities(value);
  return [decoded, normalizeRenderedText(decoded), decoded.replace(/<[^>]+>/g, "")];
}

function normalizeRouteHref(value) {
  const route = String(value).split("#")[0].split("?")[0];
  return `/${route.replace(/^\/+|\/+$/g, "")}/`;
}

function normalizeScanText(value) {
  return normalizeRenderedText(String(value)).toLowerCase();
}

function isNegated(value, index) {
  // Scope to the sentence containing the match: find the last sentence boundary
  // before the match position so an unrelated negation in a prior sentence does
  // not suppress a finding in the current sentence. (Codex P2: limit negation
  // checks to the matched claim's sentence, not the whole normalized text.)
  const sentenceStart = Math.max(0, value.lastIndexOf('.', index - 1) + 1, value.lastIndexOf('!', index - 1) + 1, value.lastIndexOf('?', index - 1) + 1);
  const prefix = value.slice(Math.max(sentenceStart, index - 40), index).toLowerCase();
  return /\b(?:cannot|can't|does not|do not|not|no|without|never)\s*$/.test(prefix);
}

function hasNegationForMatchedClaim(value) {
  const lower = value.toLowerCase();
  const verbs = [
    ...lower.matchAll(/\b(?:decides?|approves?|blocks?|certifies?|validates?|clears?|proves?|guarantees?|replaces?)\b/g)
  ];
  const verbIndex = verbs.at(-1)?.index;
  if (verbIndex === undefined) {
    return false;
  }

  const prefix = lower.slice(Math.max(0, verbIndex - 40), verbIndex);
  return /\b(?:cannot|can't|does not|do not|not|no|without|never)\s*$/.test(prefix);
}

function countWords(value) {
  return (value.match(/\b[\w'-]+\b/g) ?? []).length;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
