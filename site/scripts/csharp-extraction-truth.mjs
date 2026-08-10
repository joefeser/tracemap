import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import { decodeHtmlEntities, escapeRegExp, fileExists, normalizeRenderedText, readSitemapLocSet, stripTagsQuoteAware } from "./validate-utils.mjs";

export const csharpExtractionTruthRoute = "/blog/csharp-extraction-without-plausible-wrong-graphs/";
export const csharpExtractionTruthRequiredLinks = ["/evidence/", "/evidence/gaps/", "/static-vs-runtime/", "/proof-paths/for-managers/", "/capabilities/", "/use-cases/change-review/", "/validation/", "/limitations/"];

const artifact = "blog/csharp-extraction-without-plausible-wrong-graphs/index.html";
const blocks = ["manager-question", "identity", "call-sites", "fallback", "refusals", "snapshot", "bounds", "legacy", "remaining-gaps", "non-claims", "bottom-line"];
const requiredText = [
  "Public claim level: demo",
  "csharp.semantic.symbolidentity.v1",
  "csharp.semantic.declarations.v1",
  "csharp.semantic.callgraph.v1",
  "csharp.semantic.workspace.v1",
  "csharp.syntax.invocation.v1",
  "csharp.syntax.callgraph.v1",
  "canonical ID",
  "display name",
  "exact call-site span",
  "commit SHA",
  "extractor ID/version",
  "Tier 1",
  "Tier 3",
  "Tier 4",
  "immutable full snapshots",
  "in-place incremental replacement",
  "multiple build configurations"
];
const forbiddenClaims = [
  /\bTraceMap\b[^.]{0,160}\b(?:proves?|guarantees?|certifies?|establishes?)\b[^.]{0,160}\b(?:runtime|complete|correct|safe|production|dispatch|reachability)\b/i,
  /\b(?:graph|analysis|coverage|implementation|release)\b[^.]{0,120}\b(?:is|was)\s+(?:complete|correct|safe|approved|production-ready)\b/i,
  /\b(?:validation passed|release approved|runtime reachability confirmed|production correctness established)\b/i
];
const rawPatterns = [/\bnamespace\s+[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*\s*\{/i, /\bclass\s+[A-Za-z_]\w*\s*\{/i, /\b(?:public|private|internal|protected)\s+(?:static\s+)?[A-Za-z_]\w*(?:<[^>]+>)?\s+[A-Za-z_]\w*\s*\([^)]*\)\s*\{/i, /\b(?:Password|Server|User Id|ConnectionString)\s*=/i];
const privatePatterns = [/\/Users\//i, /\/private\//i, /\/home\//i, /\/tmp\//i, /\/var\/folders\//i, /~\//, /\bC:\\/i, /\bfile:\/\//i, /\bgit@/i, /\bsk-[A-Za-z0-9_-]{12,}\b/i];

export async function validateCsharpExtractionTruthDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  let cleanBaseUrl;
  try { cleanBaseUrl = new URL(baseUrl).origin; }
  catch { errors.push(withEvidence(`C# extraction truth base URL is invalid: ${baseUrl}`, artifact)); return; }
  const pagePath = resolve(dist, artifact);
  if (!(await fileExists(pagePath))) { errors.push(withEvidence(`C# extraction truth article is missing required route: ${csharpExtractionTruthRoute}`, artifact)); return; }

  const sitemapPath = resolve(dist, "sitemap.xml");
  if (await fileExists(sitemapPath)) if (!(await readSitemapLocSet(sitemapPath)).has(`${cleanBaseUrl}${csharpExtractionTruthRoute}`)) localErrors.push(withEvidence("C# extraction truth sitemap route is missing.", "sitemap.xml"));
  await validateIndexes(dist, localErrors);
  await validatePage(pagePath, cleanBaseUrl, localErrors);
  errors.push(...localErrors);
}

async function validateIndexes(dist, errors) {
  const blogPath = resolve(dist, "blog/index.html");
  if (!(await fileExists(blogPath))) errors.push(withEvidence("C# extraction truth blog index link is missing.", "blog/index.html"));
  else {
    const blogHtml = await readFile(blogPath, "utf8");
    if (!hasHref(blogHtml, csharpExtractionTruthRoute)) errors.push(withEvidence("C# extraction truth blog index link is missing.", "blog/index.html"));
    else {
      const card = extractLinkedAnchor(blogHtml, csharpExtractionTruthRoute);
      if (!card) errors.push(withEvidence("C# extraction truth blog card could not be isolated.", "blog/index.html"));
      else scanSafetySurfaces(card, "C# extraction truth blog card", "blog/index.html", errors);
    }
  }
  const path = resolve(dist, "routes-index.json");
  if (!(await fileExists(path))) { errors.push(withEvidence("C# extraction truth discovery output is missing.", "routes-index.json")); return; }
  let discovery;
  try { discovery = JSON.parse(await readFile(path, "utf8")); }
  catch { errors.push(withEvidence("C# extraction truth discovery output is not valid JSON.", "routes-index.json")); return; }
  if (!discovery || typeof discovery !== "object" || !Array.isArray(discovery.entries)) { errors.push(withEvidence("C# extraction truth discovery output must contain an entries array.", "routes-index.json")); return; }
  const entry = discovery.entries.find((candidate) => candidate?.path === csharpExtractionTruthRoute);
  if (!entry) errors.push(withEvidence("C# extraction truth discovery entry is missing.", "routes-index.json"));
  else {
    if (entry.publicClaimLevel !== "demo") errors.push(withEvidence("C# extraction truth discovery claim level must be demo.", "routes-index.json"));
    if (entry.preferredProofPath !== "/evidence/") errors.push(withEvidence("C# extraction truth preferred proof path must remain /evidence/.", "routes-index.json"));
    if (!Array.isArray(entry.limitations) || entry.limitations.length < 2) errors.push(withEvidence("C# extraction truth discovery must retain at least two limitations.", "routes-index.json"));
    if (!Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) errors.push(withEvidence("C# extraction truth discovery must retain at least two non-claims.", "routes-index.json"));
    const discoveryText = [entry.title, entry.summary, ...(Array.isArray(entry.limitations) ? entry.limitations : []), ...(Array.isArray(entry.nonClaims) ? entry.nonClaims : [])].filter((value) => typeof value === "string").join(" ");
    scanSafetySurfaces(discoveryText, "C# extraction truth discovery entry", "routes-index.json", errors);
  }
}

async function validatePage(pagePath, baseUrl, errors) {
  const html = await readFile(pagePath, "utf8");
  const browserDecoded = decodeBrowserNumericEntities(html);
  const decoded = decodeHtmlEntities(browserDecoded);
  const rendered = normalizeRenderedText(browserDecoded);
  const tagCollapsed = decodeHtmlEntities(stripTagsQuoteAware(decoded)).replace(/\s+/g, " ").trim();
  if (!html.includes("<title>C# Extraction Without Plausible Wrong Graphs | TraceMap</title>")) errors.push(withEvidence("C# extraction truth title is missing.", artifact));
  if (!new RegExp(`<link\\b[^>]*rel=["']canonical["'][^>]*href=["']${escapeRegExp(baseUrl)}${escapeRegExp(csharpExtractionTruthRoute)}["']`, "i").test(html)) errors.push(withEvidence("C# extraction truth canonical URL is missing or incorrect.", artifact));
  for (const block of blocks) if (!new RegExp(`<section\\b[^>]*data-csharp-truth-block\\s*=\\s*["']${block}["']`, "i").test(html)) errors.push(withEvidence(`C# extraction truth article is missing block: ${block}`, artifact));
  for (const phrase of requiredText) if (!rendered.toLowerCase().includes(phrase.toLowerCase())) errors.push(withEvidence(`C# extraction truth article is missing required text: ${phrase}`, artifact));
  for (const link of csharpExtractionTruthRequiredLinks) if (!hasHref(html, link)) errors.push(withEvidence(`C# extraction truth article is missing required link: ${link}`, artifact));
  const words = rendered.split(/\s+/).filter(Boolean).length;
  if (words < 1100 || words > 2100) errors.push(withEvidence(`C# extraction truth word count must be between 1100 and 2100 words, got ${words}`, artifact));
  scanSafetySurfaces([decoded, rendered, tagCollapsed], "C# extraction truth article", artifact, errors);
}

function decodeBrowserNumericEntities(value) { return String(value).replace(/&#(?:x[0-9a-f]+|[0-9]+);?/gi, (entity) => decodeHtmlEntities(entity.endsWith(";") ? entity : `${entity};`)); }
function hasHref(html, href) { return new RegExp(`<a\\b[^>]*href\\s*=\\s*["']${escapeRegExp(href)}["'][^>]*>`, "i").test(html); }
function extractLinkedAnchor(html, href) { return String(html).match(new RegExp(`<a\\b[^>]*href\\s*=\\s*["']${escapeRegExp(href)}["'][^>]*>[\\s\\S]*?<\\/a>`, "i"))?.[0] ?? ""; }
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
