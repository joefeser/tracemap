import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import { decodeHtmlEntities, escapeRegExp, fileExists, normalizeBaseUrl, normalizeRenderedText, readSitemapLocSet, stripTagsQuoteAware } from "./validate-utils.mjs";

export const graphHistoryRoute = "/blog/bugs-hiding-in-graph-history/";
export const graphHistoryCompanionRoute = "/blog/csharp-extraction-without-plausible-wrong-graphs/";
export const graphHistoryRequiredLinks = [graphHistoryCompanionRoute, "/evidence/", "/evidence/gaps/", "/static-vs-runtime/", "/proof-paths/for-managers/", "/validation/", "/limitations/"];

const artifact = "blog/bugs-hiding-in-graph-history/index.html";
const blocks = ["manager-question", "identity-collapse", "receiver-guessing", "direction", "snapshots", "omission", "legacy", "downstream", "review-questions", "limitations", "non-claims", "bottom-line"];
const requiredText = [
  "Public claim level: demo",
  "csharp.semantic.symbolidentity.v1",
  "csharp.semantic.declarations.v1",
  "csharp.semantic.callgraph.v1",
  "csharp.semantic.workspace.v1",
  "csharp.syntax.invocation.v1",
  "csharp.syntax.callgraph.v1",
  "canonical IDs",
  "exact call-site span",
  "commit SHA",
  "extractor ID/version",
  "Tier 1",
  "Tier 3",
  "Tier 4",
  "immutable full snapshots",
  "source-byte identity",
  "in-place incremental replacement",
  "Unicode-equivalent",
  "Loud incompleteness is safer"
];
const forbiddenClaims = [
  /\bTraceMap\b[^.]{0,160}\b(?:proves?|guarantees?|certifies?|establishes?)\b[^.]{0,160}\b(?:runtime|complete|correct|safe|production|dispatch|reachability)\b/i,
  /\b(?:the )?graph (?:is|was) (?:complete|correct)\b/i,
  /\b(?:validation passed|release approved|runtime reachability confirmed|production correctness established)\b/i
];
const rawPatterns = [/\bnamespace\s+[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*\s*\{/i, /\bclass\s+[A-Za-z_]\w*\s*\{/i, /\b(?:public|private|internal|protected)\s+(?:static\s+)?[A-Za-z_]\w*(?:<[^>]+>)?\s+[A-Za-z_]\w*\s*\([^)]*\)\s*\{/i, /\b(?:Password|Server|User Id|ConnectionString)\s*=/i];
const privatePatterns = [/\/Users\//i, /\/private\//i, /\/home\//i, /\/tmp\//i, /\/var\/folders\//i, /~\//, /\bC:\\/i, /\bfile:\/\//i, /\bgit@/i, /\bsk-[A-Za-z0-9_-]{12,}\b/i];

export async function validateGraphHistoryBugsDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, artifact);
  if (!(await fileExists(pagePath))) { errors.push(withEvidence(`Graph history article is missing required route: ${graphHistoryRoute}`, artifact)); return; }

  const sitemapPath = resolve(dist, "sitemap.xml");
  if (await fileExists(sitemapPath) && !(await readSitemapLocSet(sitemapPath)).has(`${cleanBaseUrl}${graphHistoryRoute}`)) localErrors.push(withEvidence("Graph history sitemap route is missing.", "sitemap.xml"));
  await validateIndexes(dist, localErrors);
  await validatePage(pagePath, cleanBaseUrl, localErrors);
  errors.push(...localErrors);
}

async function validateIndexes(dist, errors) {
  const blogPath = resolve(dist, "blog/index.html");
  if (!(await fileExists(blogPath)) || !hasHref(await readFile(blogPath, "utf8"), graphHistoryRoute)) errors.push(withEvidence("Graph history blog index link is missing.", "blog/index.html"));
  const companionPath = resolve(dist, "blog/csharp-extraction-without-plausible-wrong-graphs/index.html");
  if (!(await fileExists(companionPath)) || !hasHref(await readFile(companionPath, "utf8"), graphHistoryRoute)) errors.push(withEvidence("Graph history reciprocal companion link is missing.", "blog/csharp-extraction-without-plausible-wrong-graphs/index.html"));
  const path = resolve(dist, "routes-index.json");
  if (!(await fileExists(path))) { errors.push(withEvidence("Graph history discovery output is missing.", "routes-index.json")); return; }
  let discovery;
  try { discovery = JSON.parse(await readFile(path, "utf8")); }
  catch { errors.push(withEvidence("Graph history discovery output is not valid JSON.", "routes-index.json")); return; }
  if (!discovery || typeof discovery !== "object" || !Array.isArray(discovery.entries)) { errors.push(withEvidence("Graph history discovery output must contain an entries array.", "routes-index.json")); return; }
  const entry = discovery.entries.find((candidate) => candidate.path === graphHistoryRoute);
  if (!entry) errors.push(withEvidence("Graph history discovery entry is missing.", "routes-index.json"));
  else {
    if (entry.publicClaimLevel !== "demo") errors.push(withEvidence("Graph history discovery claim level must be demo.", "routes-index.json"));
    if (entry.preferredProofPath !== "/evidence/") errors.push(withEvidence("Graph history preferred proof path must remain /evidence/.", "routes-index.json"));
    if (!Array.isArray(entry.limitations) || entry.limitations.length < 2) errors.push(withEvidence("Graph history discovery must retain at least two limitations.", "routes-index.json"));
    if (!Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) errors.push(withEvidence("Graph history discovery must retain at least two non-claims.", "routes-index.json"));
  }
}

async function validatePage(pagePath, baseUrl, errors) {
  const html = await readFile(pagePath, "utf8");
  const browserDecoded = decodeBrowserNumericEntities(html);
  const decoded = decodeHtmlEntities(browserDecoded);
  const rendered = normalizeRenderedText(browserDecoded);
  const tagCollapsed = decodeHtmlEntities(stripTagsQuoteAware(decoded)).replace(/\s+/g, " ").trim();
  const surfaces = [decoded, rendered, tagCollapsed];
  if (!html.includes("<title>The Bugs Hiding in Graph History | TraceMap</title>")) errors.push(withEvidence("Graph history title is missing.", artifact));
  if (!new RegExp(`<link\\b[^>]*rel=["']canonical["'][^>]*href=["']${escapeRegExp(baseUrl)}${escapeRegExp(graphHistoryRoute)}["']`, "i").test(html)) errors.push(withEvidence("Graph history canonical URL is missing or incorrect.", artifact));
  for (const block of blocks) if (!new RegExp(`<section\\b[^>]*data-graph-history-block\\s*=\\s*["']${block}["']`, "i").test(html)) errors.push(withEvidence(`Graph history article is missing block: ${block}`, artifact));
  for (const phrase of requiredText) if (!rendered.toLowerCase().includes(phrase.toLowerCase())) errors.push(withEvidence(`Graph history article is missing required text: ${phrase}`, artifact));
  for (const link of graphHistoryRequiredLinks) if (!hasHref(html, link)) errors.push(withEvidence(`Graph history article is missing required link: ${link}`, artifact));
  const words = rendered.split(/\s+/).filter(Boolean).length;
  if (words < 1100 || words > 2100) errors.push(withEvidence(`Graph history word count must be between 1100 and 2100 words, got ${words}`, artifact));
  for (const pattern of forbiddenClaims) if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Graph history article contains unsupported positive claim: ${pattern}`, artifact));
  for (const pattern of rawPatterns) if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Graph history article contains source or executable material: ${pattern}`, artifact));
  for (const pattern of privatePatterns) if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`Graph history article contains hard private material: ${pattern}`, artifact));
}

function decodeBrowserNumericEntities(value) { return String(value).replace(/&#(?:x[0-9a-f]+|[0-9]+);?/gi, (entity) => decodeHtmlEntities(entity.endsWith(";") ? entity : `${entity};`)); }
function hasHref(html, href) { return new RegExp(`<a\\b[^>]*href\\s*=\\s*["']${escapeRegExp(href)}["'][^>]*>`, "i").test(html); }
function withEvidence(message, evidenceArtifact) { return `${message} Evidence: ${evidenceArtifact}.`; }
