import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { decodeHtmlEntities, escapeRegExp, fileExists, normalizeBaseUrl, normalizeRenderedText, readSitemapLocSet, stripTagsQuoteAware } from "./validate-utils.mjs";

export const graphifyLessonsRoute = "/blog/what-tracemap-learned-from-graphify/";
export const graphifyLessonsRequiredLinks = ["https://github.com/Graphify-Labs/graphify", "/blog/how-a-gap-becomes-a-rule/", "/blog/bugs-hiding-in-graph-history/", "/blog/csharp-extraction-without-plausible-wrong-graphs/", "/blog/what-depends-on-this-symbol/", "/blog/interfaces-make-blast-radius-harder/", "/evidence/", "/evidence/gaps/", "/static-vs-runtime/", "/proof-paths/for-managers/", "/limitations/"];

const artifact = "blog/what-tracemap-learned-from-graphify/index.html";
const blocks = ["boundary", "shared-premise", "trust", "sites", "identity", "regressions", "different-focus", "declined", "independence", "review", "non-claims", "bottom-line"];
const requiredText = ["Public claim level: concept", "external research", "not TraceMap evidence", "persistent graph", "relationship trust", "relationship site", "Roslyn", "compiler identity", "independent", "synthetic fixtures", "reverse-impact", "incremental", "Apache-2.0", "MIT notice", "attribution", "feature parity", "vector index", "rule IDs", "evidence tiers", "coverage labels", "limitations"];
const forbiddenClaims = [/\bTraceMap\b[^.]{0,160}\b(?:proves?|guarantees?|certifies?|establishes?)\b[^.]{0,160}\b(?:runtime|complete|correct|safe|production|superior|parity)\b/i, /\bTraceMap (?:copied|ported|forked) Graphify\b/i, /\b(?:Graphify|TraceMap) is (?:better|superior|complete|production-ready|safe)\b/i, /\b(?:release approved|runtime reachability confirmed|production correctness established|safe to merge|safe to run)\b/i];
const rawPatterns = [/\bnamespace\s+[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*\s*\{/i, /\bclass\s+[A-Za-z_]\w*\s*\{/i, /\b(?:SELECT|INSERT|UPDATE|DELETE|MERGE)\s+[^<]{1,80}\b(?:FROM|INTO|SET)\b/i, /\b(?:Password|Server|User Id|ConnectionString)\s*=/i];
const privatePatterns = [/\/Users\//i, /\/private\//i, /\/home\//i, /\/tmp\//i, /\/var\/folders\//i, /~\//, /\bC:\\/i, /\bfile:\/\//i, /\bgit@/i, /\bsk-[A-Za-z0-9_-]{12,}\b/i];

export async function validateGraphifyLessonsDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, artifact);
  if (!(await fileExists(pagePath))) { errors.push(withEvidence(`Graphify lessons article is missing required route: ${graphifyLessonsRoute}`, artifact)); return; }
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) localErrors.push(withEvidence("Graphify lessons sitemap is missing.", "sitemap.xml"));
  else if (!(await readSitemapLocSet(sitemapPath)).has(`${cleanBaseUrl}${graphifyLessonsRoute}`)) localErrors.push(withEvidence("Graphify lessons sitemap route is missing.", "sitemap.xml"));
  await validateIndexes(dist, localErrors);
  await validatePage(pagePath, cleanBaseUrl, localErrors);
  errors.push(...localErrors);
}

async function validateIndexes(dist, errors) {
  const blogPath = resolve(dist, "blog/index.html");
  if (!(await fileExists(blogPath))) errors.push(withEvidence("Graphify lessons blog index is missing.", "blog/index.html"));
  else { const card = extractLinkedAnchor(await readFile(blogPath, "utf8"), graphifyLessonsRoute); if (!card) errors.push(withEvidence("Graphify lessons blog card is missing.", "blog/index.html")); else scanSafety(card, "Graphify lessons blog card", "blog/index.html", errors); }
  const path = resolve(dist, "routes-index.json");
  if (!(await fileExists(path))) { errors.push(withEvidence("Graphify lessons discovery output is missing.", "routes-index.json")); return; }
  let discovery;
  try { discovery = JSON.parse(await readFile(path, "utf8")); } catch { errors.push(withEvidence("Graphify lessons discovery output is not valid JSON.", "routes-index.json")); return; }
  if (!Array.isArray(discovery?.entries)) { errors.push(withEvidence("Graphify lessons discovery output must contain an entries array.", "routes-index.json")); return; }
  const entry = discovery.entries.find((candidate) => candidate?.path === graphifyLessonsRoute);
  if (!entry) errors.push(withEvidence("Graphify lessons discovery entry is missing.", "routes-index.json"));
  else {
    if (entry.publicClaimLevel !== "concept") errors.push(withEvidence("Graphify lessons discovery claim level must be concept.", "routes-index.json"));
    if (entry.preferredProofPath !== "/evidence/") errors.push(withEvidence("Graphify lessons preferred proof path must remain /evidence/.", "routes-index.json"));
    if (!Array.isArray(entry.limitations) || entry.limitations.length < 2) errors.push(withEvidence("Graphify lessons discovery needs at least two limitations.", "routes-index.json"));
    if (!Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) errors.push(withEvidence("Graphify lessons discovery needs at least two non-claims.", "routes-index.json"));
    scanSafety([entry.title, entry.summary, ...(entry.limitations ?? []), ...(entry.nonClaims ?? [])].filter((value) => typeof value === "string"), "Graphify lessons discovery entry", "routes-index.json", errors);
  }
}

async function validatePage(pagePath, baseUrl, errors) {
  const html = await readFile(pagePath, "utf8"); const decoded = decodeHtmlEntities(decodeBrowserNumericEntities(html)); const rendered = normalizeRenderedText(html); const collapsed = decodeHtmlEntities(stripTagsQuoteAware(decoded)).replace(/\s+/g, " ").trim();
  if (!html.includes("<title>What TraceMap Learned from Graphify—Without Becoming Graphify | TraceMap</title>")) errors.push(withEvidence("Graphify lessons title is missing.", artifact));
  if (!new RegExp(`<link\\b[^>]*rel=["']canonical["'][^>]*href=["']${escapeRegExp(baseUrl)}${escapeRegExp(graphifyLessonsRoute)}["']`, "i").test(html)) errors.push(withEvidence("Graphify lessons canonical URL is missing.", artifact));
  for (const block of blocks) if (!new RegExp(`<section\\b[^>]*data-graphify-block\\s*=\\s*["']${block}["']`, "i").test(html)) errors.push(withEvidence(`Graphify lessons article is missing block: ${block}`, artifact));
  for (const phrase of requiredText) if (!rendered.toLowerCase().includes(phrase.toLowerCase())) errors.push(withEvidence(`Graphify lessons article is missing required text: ${phrase}`, artifact));
  for (const link of graphifyLessonsRequiredLinks) if (!hasHref(html, link)) errors.push(withEvidence(`Graphify lessons article is missing required link: ${link}`, artifact));
  const words = rendered.split(/\s+/).filter(Boolean).length; if (words < 1000 || words > 2000) errors.push(withEvidence(`Graphify lessons word count must be between 1000 and 2000 words, got ${words}`, artifact));
  scanSafety([decoded, rendered, collapsed], "Graphify lessons article", artifact, errors);
}

function decodeBrowserNumericEntities(value) { return String(value).replace(/&#(?:x[0-9a-f]+|[0-9]+);?/gi, (entity) => decodeHtmlEntities(entity.endsWith(";") ? entity : `${entity};`)); }
function hasHref(html, href) { return new RegExp(`<a\\b[^>]*href\\s*=\\s*["']${escapeRegExp(href)}["'][^>]*>`, "i").test(html); }
function extractLinkedAnchor(html, href) { return String(html).match(new RegExp(`<a\\b[^>]*href\\s*=\\s*["']${escapeRegExp(href)}["'][^>]*>[\\s\\S]*?<\\/a>`, "i"))?.[0] ?? ""; }
function scanSafety(value, label, evidenceArtifact, errors) { const raw = Array.isArray(value) ? value.join(" ") : String(value); const decoded = decodeHtmlEntities(decodeBrowserNumericEntities(raw)); const surfaces = [decoded, normalizeRenderedText(raw), decodeHtmlEntities(stripTagsQuoteAware(decoded)).replace(/\s+/g, " ").trim()]; for (const pattern of forbiddenClaims) if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`${label} contains unsupported positive claim: ${pattern}`, evidenceArtifact)); for (const pattern of rawPatterns) if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`${label} contains source or executable material: ${pattern}`, evidenceArtifact)); for (const pattern of privatePatterns) if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`${label} contains hard private material: ${pattern}`, evidenceArtifact)); }
function withEvidence(message, evidenceArtifact) { return `${message} Evidence: ${evidenceArtifact}.`; }
