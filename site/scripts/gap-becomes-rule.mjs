import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import { decodeHtmlEntities, escapeRegExp, fileExists, normalizeBaseUrl, normalizeRenderedText, readSitemapLocSet, stripTagsQuoteAware } from "./validate-utils.mjs";

export const gapBecomesRuleRoute = "/blog/how-a-gap-becomes-a-rule/";
export const gapBecomesRuleRequiredLinks = ["/blog/csharp-extraction-without-plausible-wrong-graphs/", "/blog/bugs-hiding-in-graph-history/", "/blog/what-depends-on-this-symbol/", "/blog/interfaces-make-blast-radius-harder/", "/evidence/", "/evidence/gaps/", "/proof-paths/", "/limitations/"];

const artifact = "blog/how-a-gap-becomes-a-rule/index.html";
const blocks = ["question", "observe", "invariant", "fixture", "rule", "provenance", "emit", "readback", "limitations", "owner", "non-claims", "bottom-line"];
const requiredText = ["Public claim level: concept", "fail-closed invariant", "synthetic fixture", "versioned rule", "rule ID", "evidence tier", "Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown", "commit SHA", "extractor ID/version", "AnalysisGap", "coverage reduced", "persistence", "readback", "downstream propagation", "documented limitations", "owner question"];
const forbiddenClaims = [/\bTraceMap\b[^.]{0,160}\b(?:proves?|guarantees?|certifies?|establishes?|automatically fixes?)\b[^.]{0,160}\b(?:runtime|complete|correct|safe|production|coverage|every gap)\b/i, /\b(?:analysis|coverage|implementation|release|validation)\b[^.]{0,120}\b(?:is|was)\s+(?:complete|correct|safe|approved|passed|production-ready)\b/i, /\b(?:every gap is fixed|every backlog item becomes a rule|owner approval is unnecessary|release approved|safe to merge|safe to run)\b/i];
const rawPatterns = [/\bnamespace\s+[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*\s*\{/i, /\bclass\s+[A-Za-z_]\w*\s*\{/i, /\b(?:SELECT|INSERT|UPDATE|DELETE|MERGE)\s+[^<]{1,80}\b(?:FROM|INTO|SET)\b/i, /\b(?:Password|Server|User Id|ConnectionString)\s*=/i];
const privatePatterns = [/\/Users\//i, /\/private\//i, /\/home\//i, /\/tmp\//i, /\/var\/folders\//i, /~\//, /\bC:\\/i, /\bfile:\/\//i, /\bgit@/i, /\bsk-[A-Za-z0-9_-]{12,}\b/i];

export async function validateGapBecomesRuleDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, artifact);
  if (!(await fileExists(pagePath))) { errors.push(withEvidence(`Gap-to-rule article is missing required route: ${gapBecomesRuleRoute}`, artifact)); return; }
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) localErrors.push(withEvidence("Gap-to-rule sitemap is missing.", "sitemap.xml"));
  else if (!(await readSitemapLocSet(sitemapPath)).has(`${cleanBaseUrl}${gapBecomesRuleRoute}`)) localErrors.push(withEvidence("Gap-to-rule sitemap route is missing.", "sitemap.xml"));
  await validateIndexes(dist, localErrors);
  await validatePage(pagePath, cleanBaseUrl, localErrors);
  errors.push(...localErrors);
}

async function validateIndexes(dist, errors) {
  const blogPath = resolve(dist, "blog/index.html");
  if (!(await fileExists(blogPath))) errors.push(withEvidence("Gap-to-rule blog index is missing.", "blog/index.html"));
  else {
    const card = extractLinkedAnchor(await readFile(blogPath, "utf8"), gapBecomesRuleRoute);
    if (!card) errors.push(withEvidence("Gap-to-rule blog card is missing.", "blog/index.html"));
    else scanSafety(card, "Gap-to-rule blog card", "blog/index.html", errors);
  }
  const path = resolve(dist, "routes-index.json");
  if (!(await fileExists(path))) { errors.push(withEvidence("Gap-to-rule discovery output is missing.", "routes-index.json")); return; }
  let discovery;
  try { discovery = JSON.parse(await readFile(path, "utf8")); }
  catch { errors.push(withEvidence("Gap-to-rule discovery output is not valid JSON.", "routes-index.json")); return; }
  if (!Array.isArray(discovery?.entries)) { errors.push(withEvidence("Gap-to-rule discovery output must contain an entries array.", "routes-index.json")); return; }
  const entry = discovery.entries.find((candidate) => candidate?.path === gapBecomesRuleRoute);
  if (!entry) errors.push(withEvidence("Gap-to-rule discovery entry is missing.", "routes-index.json"));
  else {
    if (entry.publicClaimLevel !== "concept") errors.push(withEvidence("Gap-to-rule discovery claim level must be concept.", "routes-index.json"));
    if (entry.preferredProofPath !== "/evidence/gaps/") errors.push(withEvidence("Gap-to-rule preferred proof path must remain /evidence/gaps/.", "routes-index.json"));
    if (!Array.isArray(entry.limitations) || entry.limitations.length < 2) errors.push(withEvidence("Gap-to-rule discovery needs at least two limitations.", "routes-index.json"));
    if (!Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) errors.push(withEvidence("Gap-to-rule discovery needs at least two non-claims.", "routes-index.json"));
    scanSafety([entry.title, entry.summary, ...(entry.limitations ?? []), ...(entry.nonClaims ?? [])].filter((value) => typeof value === "string"), "Gap-to-rule discovery entry", "routes-index.json", errors);
  }
}

async function validatePage(pagePath, baseUrl, errors) {
  const html = await readFile(pagePath, "utf8");
  const decoded = decodeHtmlEntities(decodeBrowserNumericEntities(html));
  const rendered = normalizeRenderedText(html);
  const collapsed = decodeHtmlEntities(stripTagsQuoteAware(decoded)).replace(/\s+/g, " ").trim();
  if (!html.includes("<title>How a Gap Becomes a Rule | TraceMap</title>")) errors.push(withEvidence("Gap-to-rule title is missing.", artifact));
  if (!new RegExp(`<link\\b(?=[^>]*\\brel\\s*=\\s*["']canonical["'])(?=[^>]*\\bhref\\s*=\\s*["']${escapeRegExp(baseUrl)}${escapeRegExp(gapBecomesRuleRoute)}["'])[^>]*>`, "i").test(html)) errors.push(withEvidence("Gap-to-rule canonical URL is missing.", artifact));
  for (const block of blocks) if (!new RegExp(`<section\\b[^>]*data-gap-rule-block\\s*=\\s*["']${block}["']`, "i").test(html)) errors.push(withEvidence(`Gap-to-rule article is missing block: ${block}`, artifact));
  for (const phrase of requiredText) if (!rendered.toLowerCase().includes(phrase.toLowerCase())) errors.push(withEvidence(`Gap-to-rule article is missing required text: ${phrase}`, artifact));
  for (const link of gapBecomesRuleRequiredLinks) if (!hasHref(html, link)) errors.push(withEvidence(`Gap-to-rule article is missing required link: ${link}`, artifact));
  const words = rendered.split(/\s+/).filter(Boolean).length;
  if (words < 950 || words > 1900) errors.push(withEvidence(`Gap-to-rule word count must be between 950 and 1900 words, got ${words}`, artifact));
  scanSafety([decoded, rendered, collapsed], "Gap-to-rule article", artifact, errors);
}

function decodeBrowserNumericEntities(value) { return String(value).replace(/&#(?:x[0-9a-f]+|[0-9]+);?/gi, (entity) => decodeHtmlEntities(entity.endsWith(";") ? entity : `${entity};`)); }
function hasHref(html, href) { return new RegExp(`<a\\b[^>]*href\\s*=\\s*["']${escapeRegExp(href)}["'][^>]*>`, "i").test(html); }
function extractLinkedAnchor(html, href) { return String(html).match(new RegExp(`<a\\b[^>]*href\\s*=\\s*["']${escapeRegExp(href)}["'][^>]*>[\\s\\S]*?<\\/a>`, "i"))?.[0] ?? ""; }
function decodePercentEscapes(value) { try { return decodeURIComponent(value); } catch { return String(value).replace(/(?:%[0-9a-f]{2})+/gi, (segment) => { try { return decodeURIComponent(segment); } catch { return segment; } }); } }
function scanSafety(value, label, evidenceArtifact, errors) { const raw = Array.isArray(value) ? value.join(" ") : String(value); const browserDecoded = decodeBrowserNumericEntities(raw); const decoded = decodeHtmlEntities(browserDecoded); const percentDecoded = decodeHtmlEntities(decodePercentEscapes(browserDecoded)); const surfaces = [decoded, percentDecoded, normalizeRenderedText(raw), normalizeRenderedText(percentDecoded), decodeHtmlEntities(stripTagsQuoteAware(decoded)).replace(/\s+/g, " ").trim(), decodeHtmlEntities(stripTagsQuoteAware(percentDecoded)).replace(/\s+/g, " ").trim()]; for (const pattern of forbiddenClaims) if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`${label} contains unsupported positive claim: ${pattern}`, evidenceArtifact)); for (const pattern of rawPatterns) if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`${label} contains source or executable material: ${pattern}`, evidenceArtifact)); for (const pattern of privatePatterns) if (surfaces.some((surface) => pattern.test(surface))) errors.push(withEvidence(`${label} contains hard private material: ${pattern}`, evidenceArtifact)); }
function withEvidence(message, evidenceArtifact) { return `${message} Evidence: ${evidenceArtifact}.`; }
