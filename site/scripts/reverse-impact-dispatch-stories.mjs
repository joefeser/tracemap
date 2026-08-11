import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import { decodeHtmlEntities, escapeRegExp, fileExists, normalizeBaseUrl, normalizeRenderedText, readSitemapLocSet, stripTagsQuoteAware } from "./validate-utils.mjs";

export const reverseImpactRoute = "/blog/what-depends-on-this-symbol/";
export const staticDispatchRoute = "/blog/interfaces-make-blast-radius-harder/";

const stories = [
  {
    route: reverseImpactRoute,
    artifact: "blog/what-depends-on-this-symbol/index.html",
    title: "<title>What Depends on This Symbol? | TraceMap</title>",
    marker: "data-reverse-impact-block",
    blocks: ["question", "persisted", "traversal", "bounds", "evidence", "families", "differences", "review", "non-claims", "bottom-line"],
    requiredText: ["Public claim level: demo", "tracemap reverse-impact", "canonical symbol ID", "direct dependents", "transitive dependents", "relationship allowlist", "impact.reverse.traversal.v1", "impact.reverse.gap.v1", "maximum depth", "frontier", "cycle", "rule ID", "evidence tier", "commit SHA", "extractor ID/version", "combined-index", "tracemap reverse"],
    requiredLinks: [staticDispatchRoute, "/evidence/", "/evidence/gaps/", "/static-vs-runtime/", "/proof-paths/for-managers/", "/capabilities/", "/use-cases/change-review/"]
  },
  {
    route: staticDispatchRoute,
    artifact: "blog/interfaces-make-blast-radius-harder/index.html",
    title: "<title>Interfaces Make Blast Radius Harder | TraceMap</title>",
    marker: "data-static-dispatch-block",
    blocks: ["question", "identity", "states", "di", "fanout", "consumers", "classifications", "review", "non-claims", "bottom-line"],
    requiredText: ["Public claim level: demo", "ImplementsInterfaceMember", "Overrides", "combined.dispatch-candidate.v1", "combined.dispatch-gap.v1", "SymbolBackedCandidate", "WeakerCandidate", "CandidateGap", "registration context", "fan-out", "review tier", "vault", "evidence documents", "selected implementation"],
    requiredLinks: [reverseImpactRoute, "/evidence/", "/evidence/gaps/", "/static-vs-runtime/", "/proof-paths/for-managers/", "/capabilities/", "/use-cases/change-review/", "/proof-paths/route-flow/"]
  }
];

const forbiddenClaims = [
  /\bTraceMap\b[^.]{0,160}\b(?:proves?|guarantees?|certifies?|establishes?)\b[^.]{0,160}\b(?:runtime|complete|correct|safe|production|dispatch|reachability|impact)\b/i,
  /\b(?:analysis|coverage|implementation|release|graph|path|candidate)\b[^.]{0,120}\b(?:is|was)\s+(?:complete|correct|safe|approved|production-ready|selected at runtime)\b/i,
  /\b(?:validation passed|release approved|runtime reachability confirmed|production correctness established|safe to merge|safe to run)\b/i
];
const rawPatterns = [/\bnamespace\s+[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*\s*\{/i, /\bclass\s+[A-Za-z_]\w*\s*\{/i, /\b(?:SELECT|INSERT|UPDATE|DELETE|MERGE)\s+[^<]{1,80}\b(?:FROM|INTO|SET)\b/i, /\b(?:Password|Server|User Id|ConnectionString)\s*=/i];
const privatePatterns = [/\/Users\//i, /\/private\//i, /\/home\//i, /\/tmp\//i, /\/var\/folders\//i, /~\//, /\bC:\\/i, /\bfile:\/\//i, /\bgit@/i, /\bsk-[A-Za-z0-9_-]{12,}\b/i];

export async function validateReverseImpactDispatchStoriesDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) localErrors.push(withEvidence("Reverse-impact and dispatch story sitemap is missing.", "sitemap.xml"));
  else {
    const sitemap = await readSitemapLocSet(sitemapPath);
    for (const story of stories) if (!sitemap.has(`${cleanBaseUrl}${story.route}`)) localErrors.push(withEvidence(`Story sitemap route is missing: ${story.route}`, "sitemap.xml"));
  }
  await validateIndexes(dist, localErrors);
  for (const story of stories) await validateStory(dist, cleanBaseUrl, story, localErrors);
  errors.push(...localErrors);
}

async function validateIndexes(dist, errors) {
  const blogPath = resolve(dist, "blog/index.html");
  if (!(await fileExists(blogPath))) errors.push(withEvidence("Story blog index is missing.", "blog/index.html"));
  else {
    const blogHtml = await readFile(blogPath, "utf8");
    for (const story of stories) {
      const card = extractLinkedAnchor(blogHtml, story.route);
      if (!card) errors.push(withEvidence(`Story blog card is missing: ${story.route}`, "blog/index.html"));
      else scanSafetySurfaces(card, "Story blog card", "blog/index.html", errors);
    }
  }
  const discoveryPath = resolve(dist, "routes-index.json");
  if (!(await fileExists(discoveryPath))) { errors.push(withEvidence("Story discovery output is missing.", "routes-index.json")); return; }
  let discovery;
  try { discovery = JSON.parse(await readFile(discoveryPath, "utf8")); }
  catch { errors.push(withEvidence("Story discovery output is not valid JSON.", "routes-index.json")); return; }
  if (!Array.isArray(discovery?.entries)) { errors.push(withEvidence("Story discovery output must contain an entries array.", "routes-index.json")); return; }
  for (const story of stories) {
    const entry = discovery.entries.find((candidate) => candidate?.path === story.route);
    if (!entry) { errors.push(withEvidence(`Story discovery entry is missing: ${story.route}`, "routes-index.json")); continue; }
    if (entry.publicClaimLevel !== "demo") errors.push(withEvidence(`Story discovery claim level must be demo: ${story.route}`, "routes-index.json"));
    if (entry.preferredProofPath !== "/evidence/") errors.push(withEvidence(`Story preferred proof path must remain /evidence/: ${story.route}`, "routes-index.json"));
    if (!Array.isArray(entry.limitations) || entry.limitations.length < 2) errors.push(withEvidence(`Story discovery needs at least two limitations: ${story.route}`, "routes-index.json"));
    if (!Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) errors.push(withEvidence(`Story discovery needs at least two non-claims: ${story.route}`, "routes-index.json"));
    scanSafetySurfaces([entry.title, entry.summary, ...(entry.limitations ?? []), ...(entry.nonClaims ?? [])].filter((value) => typeof value === "string"), "Story discovery entry", "routes-index.json", errors);
  }
}

async function validateStory(dist, baseUrl, story, errors) {
  const pagePath = resolve(dist, story.artifact);
  if (!(await fileExists(pagePath))) { errors.push(withEvidence(`Story route is missing: ${story.route}`, story.artifact)); return; }
  const html = await readFile(pagePath, "utf8");
  const browserDecoded = decodeBrowserNumericEntities(html);
  const decoded = decodeHtmlEntities(browserDecoded);
  const rendered = normalizeRenderedText(browserDecoded);
  const tagCollapsed = decodeHtmlEntities(stripTagsQuoteAware(decoded)).replace(/\s+/g, " ").trim();
  if (!html.includes(story.title)) errors.push(withEvidence(`Story title is missing: ${story.route}`, story.artifact));
  if (!new RegExp(`<link\\b[^>]*rel=["']canonical["'][^>]*href=["']${escapeRegExp(baseUrl)}${escapeRegExp(story.route)}["']`, "i").test(html)) errors.push(withEvidence(`Story canonical URL is missing: ${story.route}`, story.artifact));
  for (const block of story.blocks) if (!new RegExp(`<section\\b[^>]*${story.marker}\\s*=\\s*["']${block}["']`, "i").test(html)) errors.push(withEvidence(`Story is missing block ${block}: ${story.route}`, story.artifact));
  for (const phrase of story.requiredText) if (!rendered.toLowerCase().includes(phrase.toLowerCase())) errors.push(withEvidence(`Story is missing required text ${phrase}: ${story.route}`, story.artifact));
  for (const link of story.requiredLinks) if (!hasHref(html, link)) errors.push(withEvidence(`Story is missing required link ${link}: ${story.route}`, story.artifact));
  const words = rendered.split(/\s+/).filter(Boolean).length;
  if (words < 800 || words > 1900) errors.push(withEvidence(`Story word count must be between 800 and 1900 words, got ${words}: ${story.route}`, story.artifact));
  scanSafetySurfaces([decoded, rendered, tagCollapsed], "Story article", story.artifact, errors);
}

function decodeBrowserNumericEntities(value) { return String(value).replace(/&#(?:x[0-9a-f]+|[0-9]+);?/gi, (entity) => decodeHtmlEntities(entity.endsWith(";") ? entity : `${entity};`)); }
function hasHref(html, href) { return new RegExp(`<a\\b[^>]*\\shref\\s*=\\s*["']${escapeRegExp(href)}["'][^>]*>`, "i").test(html); }
function extractLinkedAnchor(html, href) { return String(html).match(new RegExp(`<a\\b[^>]*\\shref\\s*=\\s*["']${escapeRegExp(href)}["'][^>]*>[\\s\\S]*?<\\/a>`, "i"))?.[0] ?? ""; }
function scanSafetySurfaces(value, label, evidenceArtifact, errors) {
  const raw = Array.isArray(value) ? value.join(" ") : String(value);
  const browserDecoded = decodeBrowserNumericEntities(raw);
  const decoded = decodeHtmlEntities(browserDecoded);
  const surfaces = [decoded, normalizeRenderedText(browserDecoded), decodeHtmlEntities(stripTagsQuoteAware(decoded)).replace(/\s+/g, " ").trim()];
  for (const pattern of forbiddenClaims) if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`${label} contains unsupported positive claim: ${pattern}`, evidenceArtifact));
  for (const pattern of rawPatterns) if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`${label} contains source or executable material: ${pattern}`, evidenceArtifact));
  for (const pattern of privatePatterns) if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`${label} contains hard private material: ${pattern}`, evidenceArtifact));
}
function withEvidence(message, evidenceArtifact) { return `${message} Evidence: ${evidenceArtifact}.`; }
