import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  fileExists,
  normalizeBaseUrl,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const blogProofPathSeriesRoute = "/blog/what-a-proof-path-is/";
export const blogProofPathSeriesSlug = "what-a-proof-path-is";
export const blogProofPathRequiredLinks = [
  "/proof-paths/",
  "/proof-paths/tour/",
  "/proof-source-catalog/",
  "/evidence/",
  "/packets/",
  "/packets/assembly/",
  "/review-claim-checklist/",
  "/static-vs-runtime/",
  "/limitations/",
  "/validation/",
  "/demo/result/",
  "/questions/"
];

const pageArtifact = "blog/what-a-proof-path-is/index.html";
const blogIndexArtifact = "blog/index.html";
const sitemapArtifact = "sitemap.xml";
const metadataArtifact = "src/_blog/articles.json";
const articleBodyArtifact = "src/_blog/articles/what-a-proof-path-is.html";

const existingBlogSlugs = new Set([
  "why-tracemap-exists",
  "what-tracemap-solves-for-engineering-teams",
  "building-tracemap-with-codex-kiro-qodo"
]);
const requiredBlocks = [
  "opening-problem",
  "evidence-backed-claim-example",
  "proof-path-reading-steps",
  "proof-surfaces",
  "limitations-and-non-claims",
  "safe-language-examples",
  "unsafe-language-examples",
  "closing-handoff-action"
];
const requiredText = [
  "Public claim level: concept",
  "The problem is claim drift.",
  "An evidence-backed claim names what would support it.",
  "Read a proof path in order.",
  "Limitations and non-claims are part of the proof path.",
  "Safe language examples",
  "Unsafe language examples, framed as wording to avoid",
  "Close the loop with a handoff, not a bigger claim."
];
const forbiddenClaimPatterns = [
  /\b(?:TraceMap\s+)?(?:proves?|proven|proof of)\s+(?:runtime behavior|production traffic|endpoint performance|outage cause|release approval|release safety|operational safety|complete coverage|product behavior)\b/i,
  /\b(?:certifies?|guarantees?|verifies?)\s+(?:runtime behavior|production traffic|endpoint performance|release safety|operational safety|complete coverage|product behavior)\b/i,
  /\b(?:monitors?|knows?)\s+production traffic\b/i,
  /\bmeasures?\s+endpoint performance\b/i,
  /\bidentifies?\s+outage cause\b/i,
  /\bgrants?\s+release approval\b/i,
  /\bprovides?\s+operational safety\b/i,
  /\bperforms?\s+(?:AI impact analysis|LLM analysis)\b/i,
  /\buses?\s+(?:embeddings|vector databases|prompt classification)\b/i,
  /\bautonomously\s+approves?\b/i,
  /\breplaces?\s+(?:tests|code review|source review|runtime observability|human judgment|human review|telemetry|logs|traces|release process)\b/i,
  /\b(?:is|are|was|were)\s+(?:impacted|safe|unsafe|approved|blocked|validated for release|production proven)\b/i,
  /\broot cause\b/i
];
const rawMaterialPatterns = [
  /\bfacts\.ndjson\b/i,
  /\bindex\.sqlite\b/i,
  /\bscan-manifest\.json\b/i,
  /\breport\.md\b/i,
  /\blogs\/analyzer\.log\b/i,
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
  /\bhidden validation details\b/i,
  /\braw command output\b/i,
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
const boundarySectionPattern =
  /<section\b(?=[^>]*\bdata-tm-boundary\s*=\s*["'][^"']+["'])[^>]*>[\s\S]*?<\/section>/gi;

export async function validateBlogProofPathSeriesDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors,
  root
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "blog", blogProofPathSeriesSlug, "index.html");
  const blogIndexPath = resolve(dist, "blog", "index.html");

  if (root) {
    await validateMetadata({ errors: localErrors, root });
  }

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Proof-path blog article is missing required route: ${blogProofPathSeriesRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  await validateBlogIndex({ blogIndexPath, errors: localErrors });
  await validateArticlePage({ pagePath, errors: localErrors });

  errors.push(...localErrors);
}

async function validateMetadata({ errors, root }) {
  const metadataPath = resolve(root, "_blog", "articles.json");
  const bodyPath = resolve(root, "_blog", "articles", `${blogProofPathSeriesSlug}.html`);

  if (!(await fileExists(metadataPath))) {
    return;
  }

  let articles;
  try {
    articles = JSON.parse(await readFile(metadataPath, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Proof-path blog metadata could not be parsed: ${error.message}`, metadataArtifact));
    return;
  }

  if (!Array.isArray(articles)) {
    errors.push(withEvidence("Proof-path blog metadata must be an array.", metadataArtifact));
    return;
  }

  const seen = new Set();
  for (const article of articles) {
    if (typeof article?.slug === "string") {
      if (seen.has(article.slug)) {
        errors.push(withEvidence(`Proof-path blog metadata has duplicate slug: ${article.slug}`, metadataArtifact));
      }
      seen.add(article.slug);
    }
  }

  const article = articles.find((entry) => entry?.slug === blogProofPathSeriesSlug);
  if (!article) {
    errors.push(withEvidence(`Proof-path blog metadata is missing slug: ${blogProofPathSeriesSlug}`, metadataArtifact));
    return;
  }

  if (existingBlogSlugs.has(article.slug)) {
    errors.push(withEvidence(`Proof-path blog article reuses an existing article slug: ${article.slug}`, metadataArtifact));
  }

  if (article.body !== `articles/${blogProofPathSeriesSlug}.html`) {
    errors.push(withEvidence(`Proof-path blog metadata has unexpected body path: ${String(article.body)}`, metadataArtifact));
  }

  if (typeof article.title !== "string" || article.title.length > 70) {
    errors.push(withEvidence("Proof-path blog metadata title must be 70 characters or fewer.", metadataArtifact));
  }

  for (const field of ["description", "ogDescription", "cardDescription"]) {
    if (typeof article[field] !== "string" || article[field].length > 160) {
      errors.push(withEvidence(`Proof-path blog metadata ${field} must be 160 characters or fewer.`, metadataArtifact));
    }
  }

  if (!(await fileExists(bodyPath))) {
    errors.push(withEvidence("Proof-path blog article source body is missing.", articleBodyArtifact));
  }
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${blogProofPathSeriesRoute}`)) {
    errors.push(withEvidence(`Proof-path blog sitemap is missing required route: ${baseUrl}${blogProofPathSeriesRoute}`, sitemapArtifact));
  }
}

async function validateBlogIndex({ blogIndexPath, errors }) {
  if (!(await fileExists(blogIndexPath))) {
    errors.push(withEvidence("Proof-path blog index is missing.", blogIndexArtifact));
    return;
  }

  const html = await readFile(blogIndexPath, "utf8");
  if (!hasHref(html, blogProofPathSeriesRoute)) {
    errors.push(withEvidence(`Proof-path blog index is missing article link: ${blogProofPathSeriesRoute}`, blogIndexArtifact));
  }
}

async function validateArticlePage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const renderedText = normalizeRenderedText(html);
  const lowerText = renderedText.toLowerCase();
  const unsanctionedHtml = decodedHtml.replace(boundarySectionPattern, " ");
  const unsanctionedText = normalizeRenderedText(unsanctionedHtml);
  const wordCount = countWords(renderedText);

  if (!html.includes('<meta property="og:type" content="article">')) {
    errors.push(withEvidence('Proof-path blog article must include <meta property="og:type" content="article">.', pageArtifact));
  }

  if (!html.includes(`href="https://tracemap.tools${blogProofPathSeriesRoute}"`)) {
    errors.push(withEvidence(`Proof-path blog article canonical URL must target ${blogProofPathSeriesRoute}`, pageArtifact));
  }

  for (const phrase of requiredText) {
    if (!lowerText.includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Proof-path blog article is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const block of requiredBlocks) {
    if (!hasDataBlock(html, block)) {
      errors.push(withEvidence(`Proof-path blog article is missing required block: ${block}`, pageArtifact));
    }
  }

  for (const link of blogProofPathRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Proof-path blog article is missing required link: ${link}`, pageArtifact));
    }
  }

  if (wordCount < 900 || wordCount > 1800) {
    errors.push(withEvidence(`Proof-path blog article word count must be between 900 and 1800 words, got ${wordCount}`, pageArtifact));
  }

  for (const pattern of forbiddenClaimPatterns) {
    const match = unsanctionedText.match(pattern);
    if (match) {
      errors.push(withEvidence(`Proof-path blog article contains forbidden public claim outside a boundary/example section: ${match[0]}`, pageArtifact));
    }
  }

  for (const pattern of rawMaterialPatterns) {
    const match = unsanctionedText.match(pattern);
    if (match) {
      errors.push(withEvidence(`Proof-path blog article contains forbidden raw/private material outside a boundary section: ${match[0]}`, pageArtifact));
    }
  }

  for (const pattern of hardPrivatePatterns) {
    const match = decodedHtml.match(pattern) ?? renderedText.match(pattern);
    if (match) {
      errors.push(withEvidence(`Proof-path blog article contains hard private material: ${match[0]}`, pageArtifact));
    }
  }
}

function hasHref(html, href) {
  const escaped = href.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  return new RegExp(`<a\\b[^>]*\\bhref=["']${escaped}["']`, "i").test(html);
}

function hasDataBlock(html, block) {
  const escaped = block.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  return new RegExp(`\\bdata-proof-blog-block=["']${escaped}["']`, "i").test(html);
}

function countWords(text) {
  return [...text.matchAll(/\b[A-Za-z0-9][A-Za-z0-9'-]*\b/g)].length;
}

function withEvidence(message, artifact) {
  return `${message} (${artifact})`;
}
