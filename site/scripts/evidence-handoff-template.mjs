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

export const evidenceHandoffTemplateRoute = "/handoff/template/";
export const evidenceHandoffTemplateRequiredLinks = [
  "/team-evidence-handoff/",
  "/incident-evidence-handoff/",
  "/packets/assembly/",
  "/reviewer-quickstart/",
  "/owners/follow-up/",
  "/decisions/evidence-record/",
  "/proof-paths/",
  "/limitations/",
  "/validation/"
];
export const evidenceHandoffTemplateInboundRoutes = ["/team-evidence-handoff/", "/packets/assembly/"];

const pageArtifact = "handoff/template/index.html";
const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

const requiredText = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "It is the receiver, while owner to ask is the role for the next open question",
  "synthetic-sha-0001"
];

const requiredSections = [
  "when-to-use-it",
  "neighbor-distinctions",
  "template",
  "filled-synthetic-example",
  "unsafe-example",
  "handoff-checklist",
  "stop-conditions",
  "non-claims"
];

const requiredFieldLabels = [
  "handoff question",
  "audience",
  "proof path",
  "public claim level",
  "rule ID/family",
  "evidence tier",
  "coverage label",
  "public-safe path/span",
  "commit SHA",
  "extractor version",
  "limitation",
  "non-claim",
  "validation evidence",
  "owner to ask",
  "stop condition"
];

const requiredEvidenceTiers = ["Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"];

const requiredStopConditions = [
  "missing proof path",
  "private-only support",
  "raw or private material",
  "unknown or reduced coverage without label",
  "unsupported runtime proof wording",
  "unsupported release or safety wording",
  "unsupported complete-coverage wording",
  "AI or LLM analysis wording",
  "no validation evidence",
  "no owner to ask",
  "blame language"
];

const requiredNonClaims = [
  "generate this handoff",
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
  "autonomous review",
  "replacement of human review",
  "real organization ownership",
  "source review",
  "ownership decisions",
  "telemetry",
  "logs",
  "traces",
  "APM",
  "tests",
  "release controls",
  "incident response",
  "service-owner judgment",
  "database-owner judgment",
  "security review",
  "compliance review",
  "manager judgment",
  "human judgment"
];

const neighborDistinctions = new Map([
  ["/team-evidence-handoff/", "Receiver-specific wording"],
  ["/incident-evidence-handoff/", "incident-adjacent static evidence transfer"],
  ["/packets/assembly/", "broader human workflow"],
  ["/reviewer-quickstart/", "Reviewer onboarding"],
  ["/owners/follow-up/", "without claiming real organization ownership"],
  ["/decisions/evidence-record/", "not a final decision"]
]);

const metadataNonClaimTerms = [
  "runtime behavior",
  "production traffic",
  "endpoint performance",
  "outage cause",
  "release approval",
  "release safety",
  "operational safety",
  "real organization ownership",
  "complete coverage",
  "AI impact analysis",
  "LLM analysis",
  "autonomous review",
  "generated handoff feature",
  "replacement of human review"
];

const forbiddenPositiveClaimPatterns = [
  /\b(?:TraceMap\s+)?generates?\s+(?:handoffs?|evidence handoffs?)\b/i,
  /\b(?:proves?|proven|proof of)\s+(?:runtime behavior|production traffic|endpoint performance|outage cause|release approval|release safety|operational safety|complete coverage)\b/i,
  /\bproduction proven\b/i,
  /\bapproved for release\b/i,
  /\b(?:performs?|provides?)\s+(?:AI impact analysis|LLM analysis|autonomous review)\b/i,
  /\breplaces?\s+(?:human review|source review|ownership decisions|telemetry|logs|traces|APM|tests|release controls|incident response|service-owner judgment|database-owner judgment|security review|compliance review|manager judgment|human judgment)\b/i,
  /\bAI-powered\b/i,
  /\bLLM-powered\b/i
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
  /\bsk-[A-Za-z0-9_-]{12,}\b/i,
  /\b[a-f0-9]{40}\b/,
  /\b(?=[a-f0-9]{7,12}\b)(?=[a-f0-9]*\d)(?=[a-f0-9]*[a-f])[a-f0-9]+\b/
];

const forbiddenPrivateNamePatterns = [
  /\bAlice\b/i,
  /\bBob\b/i,
  /\bCarol\b/i,
  /\bJane\s+Doe\b/i,
  /\bJohn\s+Doe\b/i,
  /\bJoe\s+Feser\b/i
];

export async function validateEvidenceHandoffTemplateDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "handoff", "template", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Evidence handoff template is missing required public route: ${evidenceHandoffTemplateRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  const routeContext = await readRouteContext({ dist, errors: localErrors });
  await validatePage({ pagePath, routeContext, errors: localErrors });
  await validateInboundLinks({ dist, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, sitemapArtifact);
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${evidenceHandoffTemplateRoute}`)) {
    errors.push(withEvidence(`Evidence handoff template sitemap is missing required route: ${baseUrl}${evidenceHandoffTemplateRoute}`, sitemapArtifact));
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
        // Aggregate validation reports malformed sitemap URLs separately.
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
    errors.push(withEvidence(`Evidence handoff template could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !Array.isArray(parsed.entries)) {
    errors.push(withEvidence("Evidence handoff template routes-index.json is invalid: expected entries array", routesIndexArtifact));
    return { routeEntry, routes, sitemapRoutes };
  }

  for (const entry of parsed.entries) {
    if (typeof entry?.path === "string") {
      routes.add(normalizeRouteHref(entry.path));
    }
  }

  routeEntry = parsed.entries.find((entry) => normalizeRouteHref(entry?.path ?? "") === evidenceHandoffTemplateRoute) ?? null;
  validateRouteEntry(routeEntry, errors);

  return { routeEntry, routes, sitemapRoutes };
}

function validateRouteEntry(routeEntry, errors) {
  if (!routeEntry) {
    errors.push(withEvidence(`Evidence handoff template routes-index.json is missing required route: ${evidenceHandoffTemplateRoute}`, routesIndexArtifact));
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
      errors.push(withEvidence(`Evidence handoff template routes-index.json expected ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
    }
  }

  if (!Array.isArray(routeEntry.limitations) || routeEntry.limitations.length === 0) {
    errors.push(withEvidence("Evidence handoff template routes-index.json must include limitations metadata.", routesIndexArtifact));
  }
  const limitations = Array.isArray(routeEntry.limitations) ? routeEntry.limitations : [];

  if (!Array.isArray(routeEntry.nonClaims) || routeEntry.nonClaims.length === 0) {
    errors.push(withEvidence("Evidence handoff template routes-index.json must include nonClaims metadata.", routesIndexArtifact));
    return;
  }

  const nonClaimsText = normalizeScanText(routeEntry.nonClaims.join(" ")).toLowerCase();
  for (const term of metadataNonClaimTerms) {
    if (!nonClaimsText.includes(term.toLowerCase())) {
      errors.push(withEvidence(`Evidence handoff template routes-index.json nonClaims are missing required term: ${term}`, routesIndexArtifact));
    }
  }

  validateForbiddenPositiveClaims({
    errors,
    text: [routeEntry.title, routeEntry.summary, ...limitations].join(" "),
    label: "route metadata"
  });
  validateHardPrivateMaterial({
    errors,
    text: JSON.stringify(routeEntry),
    label: "route metadata"
  });
}

async function validatePage({ pagePath, routeContext, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const mainHtml = extractMainHtml(html);
  const pageText = normalizeRenderedText(mainHtml);
  const lowerPageText = pageText.toLowerCase();
  const strippedHtml = stripSanctionedBoundaryRegions(html);
  const strippedMainHtml = extractMainHtml(strippedHtml);
  const strippedText = normalizeRenderedText(strippedMainHtml);
  const metadataText = collectMetadataText(html);
  const attributeText = collectDecodedAttributeText(strippedHtml);
  const wordCount = countWords(pageText);

  for (const phrase of requiredText) {
    if (!pageText.includes(phrase)) {
      errors.push(withEvidence(`Evidence handoff template page is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const section of requiredSections) {
    if (!hasSection(html, section)) {
      errors.push(withEvidence(`Evidence handoff template page is missing required section: ${section}`, pageArtifact));
    }
  }

  for (const label of requiredFieldLabels) {
    if (!hasFieldRow(html, label)) {
      errors.push(withEvidence(`Evidence handoff template page is missing template field label: ${label}`, pageArtifact));
    }
    if (!hasSyntheticExampleField(html, label)) {
      errors.push(withEvidence(`Evidence handoff template synthetic example is missing field label: ${label}`, pageArtifact));
    }
  }

  for (const tier of requiredEvidenceTiers) {
    if (!pageText.includes(tier)) {
      errors.push(withEvidence(`Evidence handoff template page is missing evidence tier: ${tier}`, pageArtifact));
    }
  }

  for (const condition of requiredStopConditions) {
    if (!lowerPageText.includes(condition.toLowerCase())) {
      errors.push(withEvidence(`Evidence handoff template page is missing stop condition: ${condition}`, pageArtifact));
    }
  }

  const nonClaimText = normalizeRenderedText(extractSection(html, "non-claims")).toLowerCase();
  for (const term of requiredNonClaims) {
    if (!nonClaimText.includes(term.toLowerCase())) {
      errors.push(withEvidence(`Evidence handoff template non-claims are missing required term: ${term}`, pageArtifact));
    }
  }

  for (const [route, distinction] of neighborDistinctions) {
    if (!hasHref(html, route)) {
      errors.push(withEvidence(`Evidence handoff template page is missing neighbor link: ${route}`, pageArtifact));
    }
    if (!normalizeRenderedText(extractSection(html, "neighbor-distinctions")).toLowerCase().includes(distinction.toLowerCase())) {
      errors.push(withEvidence(`Evidence handoff template page is missing neighbor distinction for ${route}: ${distinction}`, pageArtifact));
    }
  }

  for (const link of evidenceHandoffTemplateRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Evidence handoff template page is missing required link: ${link}`, pageArtifact));
    }
  }

  validatePageMetadata(html, errors);
  validateInternalRouteLinks(html, routeContext, errors);
  validateChecklist(html, errors);

  if (!/data-context=["']synthetic-example["']/i.test(extractSection(html, "filled-synthetic-example"))) {
    errors.push(withEvidence("Evidence handoff template filled example must be visibly and machine-labeled synthetic.", pageArtifact));
  }

  if (!/data-context=["']unsafe-example["']/i.test(extractSection(html, "unsafe-example"))) {
    errors.push(withEvidence("Evidence handoff template unsafe example must be machine-labeled unsafe.", pageArtifact));
  }

  if (wordCount < 500) {
    errors.push(withEvidence(`Evidence handoff template page word count must be at least 500 words, got ${wordCount}`, pageArtifact));
  }

  if (wordCount > 1600) {
    errors.push(withEvidence(`Evidence handoff template page word count exceeds standalone target of 1600 words, got ${wordCount}`, pageArtifact));
  }

  validateForbiddenPositiveClaims({
    errors,
    text: `${strippedText} ${metadataText} ${attributeText}`,
    label: "page copy outside sanctioned boundary regions"
  });
  validateHardPrivateMaterial({
    errors,
    text: `${html} ${decodedHtml} ${pageText} ${metadataText} ${collectDecodedAttributeText(html)}`,
    label: "page, attributes, or metadata"
  });
  validatePrivateNames({
    errors,
    text: `${pageText} ${metadataText}`,
    label: "visible page or metadata"
  });
}

function validatePageMetadata(html, errors) {
  const metaTags = findTagAttributes(html, "meta");
  const linkTags = findTagAttributes(html, "link");
  const checks = [
    [/<title>Evidence Handoff Template \| TraceMap<\/title>/i.test(html), "title"],
    [hasMeta(metaTags, { name: "description", content: "non-empty" }), "description"],
    [
      linkTags.some(
        (attributes) =>
          hasRel(attributes, "canonical") &&
          getAttribute(attributes, "href") === "https://tracemap.tools/handoff/template/"
      ),
      "canonical URL"
    ],
    [hasMeta(metaTags, { property: "og:type", content: "article" }), "Open Graph type"],
    [hasMeta(metaTags, { property: "og:title", content: "non-empty" }), "Open Graph title"],
    [hasMeta(metaTags, { property: "og:description", content: "non-empty" }), "Open Graph description"],
    [
      hasMeta(metaTags, {
        property: "og:url",
        content: "https://tracemap.tools/handoff/template/"
      }),
      "Open Graph URL"
    ]
  ];

  for (const [passed, label] of checks) {
    if (!passed) {
      errors.push(withEvidence(`Evidence handoff template page is missing required metadata: ${label}`, pageArtifact));
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
      errors.push(withEvidence(`Evidence handoff template page links to unresolved internal route: ${route}`, pageArtifact));
    }
  }
}

async function validateInboundLinks({ dist, errors }) {
  const missing = [];

  for (const route of evidenceHandoffTemplateInboundRoutes) {
    const pagePath = resolve(dist, route.replace(/^\/|\/$/g, ""), "index.html");
    if (!(await fileExists(pagePath))) {
      continue;
    }

    const html = await readFile(pagePath, "utf8");
    if (!hasHref(html, evidenceHandoffTemplateRoute)) {
      missing.push(route);
    }
  }

  if (missing.length > 0) {
    errors.push(withEvidence(`Evidence handoff template is missing inbound links from live adjacent routes: ${missing.join(", ")}`, "adjacent route HTML"));
  }
}

function validateChecklist(html, errors) {
  const checklistHtml = extractSection(html, "handoff-checklist");
  const checklistText = normalizeRenderedText(checklistHtml).toLowerCase();
  const requiredChecklistItems = [
    "handoff question",
    "public claim level",
    "proof path",
    "rule id or family",
    "evidence tier",
    "coverage label",
    "limitation",
    "non-claim",
    "validation evidence",
    "owner to ask",
    "stop condition"
  ];

  for (const item of requiredChecklistItems) {
    if (!checklistText.includes(item)) {
      errors.push(withEvidence(`Evidence handoff template checklist is missing required item: ${item}`, pageArtifact));
    }
  }

  if (!checklistText.includes("audience")) {
    errors.push(withEvidence("Evidence handoff template checklist must include audience or a same-role omission note.", pageArtifact));
  }

  for (const omitted of ["public-safe path/span", "commit sha", "extractor version"]) {
    if (!checklistText.includes(omitted)) {
      errors.push(withEvidence(`Evidence handoff template checklist reduced-subset note is missing omitted field: ${omitted}`, pageArtifact));
    }
  }
}

function validateForbiddenPositiveClaims({ errors, text, label }) {
  for (const pattern of forbiddenPositiveClaimPatterns) {
    const flags = pattern.flags.includes("g") ? pattern.flags : `${pattern.flags}g`;
    const globalPattern = new RegExp(pattern.source, flags);
    for (const match of text.matchAll(globalPattern)) {
      if (!hasNegatedContext(text, match.index)) {
        errors.push(withEvidence(`Evidence handoff template contains forbidden public claim in ${label}: ${match[0]}`, pageArtifact));
        break;
      }
    }
  }
}

function validateHardPrivateMaterial({ errors, text, label }) {
  const sanitized = text.replace(/\bsynthetic-sha-0001\b/g, "synthetic-sha");
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(sanitized)) {
      errors.push(withEvidence(`Evidence handoff template contains hard private material in ${label}: ${pattern.source}`, pageArtifact));
    }
  }
}

function validatePrivateNames({ errors, text, label }) {
  for (const pattern of forbiddenPrivateNamePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Evidence handoff template contains personal name in ${label}: ${pattern.source}`, pageArtifact));
    }
  }
}

function hasNegatedContext(value, index) {
  const prefix = value.slice(Math.max(0, index - 72), index).toLowerCase();
  if (/\bnot\s+only\b/.test(prefix)) {
    return false;
  }

  return /(?:does not|do not|cannot|can't|is not|are not|without|never|not a|not an|not the|no)\s+(?:a\s+|an\s+|the\s+|new\s+|this\s+)?[^.]{0,40}$/.test(prefix);
}

function hasSection(html, id) {
  return new RegExp(`<section\\b(?=[^>]*(?:^|\\s)id\\s*=\\s*["']${escapeRegExp(id)}["'])`, "i").test(html);
}

function extractSection(html, id) {
  const escaped = escapeRegExp(id);
  const match = html.match(new RegExp(`<section\\b(?=[^>]*(?:^|\\s)id\\s*=\\s*["']${escaped}["'])[^>]*>[\\s\\S]*?<\\/section>`, "i"));
  return match?.[0] ?? "";
}

function hasFieldRow(html, field) {
  const escaped = escapeRegExp(field);
  return new RegExp(`<tr\\b(?=[^>]*(?:^|\\s)data-handoff-field\\s*=\\s*["']${escaped}["'])`, "i").test(html);
}

function hasSyntheticExampleField(html, field) {
  const section = extractSection(html, "filled-synthetic-example");
  return new RegExp(`<code>\\s*${escapeRegExp(field)}\\s*<\\/code>`, "i").test(section);
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`(?:^|\\s)href\\s*=\\s*(["'])${escaped}\\1`, "i").test(html);
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

function findAllTagAttributes(html) {
  return [...html.matchAll(/<([a-z][a-z0-9:-]*)\b([^>]*)>/gi)].map((match) => match[2] ?? "");
}

function findTagAttributes(html, tagName) {
  const pattern = new RegExp(`<${escapeRegExp(tagName)}\\b([^>]*)>`, "gi");
  return [...html.matchAll(pattern)].map((match) => match[1] ?? "");
}

function getAttribute(attributes, name) {
  const pattern = new RegExp(`(?:^|\\s)${escapeRegExp(name)}\\s*=\\s*("[^"]*"|'[^']*'|[^\\s>]+)`, "i");
  const match = attributes.match(pattern);
  return match ? decodeHtmlEntities(unquoteAttributeValue(match[1]).trim()) : null;
}

function unquoteAttributeValue(value) {
  const trimmed = String(value).trim();
  if ((trimmed.startsWith("\"") && trimmed.endsWith("\"")) || (trimmed.startsWith("'") && trimmed.endsWith("'"))) {
    return trimmed.slice(1, -1);
  }
  return trimmed;
}

function extractHrefs(html) {
  return [...html.matchAll(/(?:^|\s)href\s*=\s*("[^"]*"|'[^']*')/gi)].map((match) => decodeHtmlEntities(unquoteAttributeValue(match[1])));
}

function extractMainHtml(html) {
  const match = html.match(/<main\b[^>]*>([\s\S]*?)<\/main>/i);
  return match?.[1] ?? html;
}

function stripSanctionedBoundaryRegions(html) {
  return html.replace(
    /<section\b(?=[^>]*(?:^|\s)data-context\s*=\s*["'](?:non-claim|unsafe-example|stop-condition|handoff-checklist|template-field|neighbor-distinction|when-to-use|caution|synthetic-example)["'])[^>]*>[\s\S]*?<\/section>/gi,
    " "
  );
}

function normalizeRouteHref(href) {
  const path = href.split("#")[0].split("?")[0] || "/";
  return path.endsWith("/") ? path : `${path}/`;
}

function countWords(text) {
  const words = text.match(/[A-Za-z0-9][A-Za-z0-9/-]*/g);
  return words?.length ?? 0;
}

function normalizeScanText(value) {
  return decodeHtmlEntities(String(value)).replace(/\s+/g, " ").trim();
}

function withEvidence(message, artifact) {
  return `${message} [${artifact}]`;
}
