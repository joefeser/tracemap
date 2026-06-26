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

export const buildReviewWorkflowStorySlug = "building-tracemap-under-review-pressure";
export const buildReviewWorkflowStoryRoute = `/blog/${buildReviewWorkflowStorySlug}/`;
export const buildReviewWorkflowRequiredLinks = [
  "/blog/building-tracemap-with-codex-kiro-qodo/",
  "/proof-paths/",
  "/site-claim-guardrails/",
  "/review-claim-checklist/",
  "/validation/",
  "/limitations/"
];

const articleBody = `articles/${buildReviewWorkflowStorySlug}.html`;
const pageArtifact = `blog/${buildReviewWorkflowStorySlug}/index.html`;
const oldWorkflowArtifact = "blog/building-tracemap-with-codex-kiro-qodo/index.html";
const blogIndexArtifact = "blog/index.html";
const sitemapArtifact = "sitemap.xml";
const metadataArtifact = "src/_blog/articles.json";
const articleBodyArtifact = `src/_blog/${articleBody}`;
const nonClaimMarker = "data-non-claim-region";
const rejectedMarker = "data-rejected-pattern-region";

const existingBlogSlugs = new Set([
  "what-a-proof-path-is",
  "building-tracemap-with-codex-kiro-qodo",
  "why-tracemap-exists",
  "what-tracemap-solves-for-engineering-teams"
]);

const requiredBlocks = [
  "claim-level-note",
  "pressure-shaped-workflow",
  "specs-before-implementation",
  "reviewable-diffs",
  "review-loop-coordination",
  "workflow-does-not-prove",
  "lessons-evidence-led-specs",
  "validation-publication-checklist"
];

const requiredText = [
  "Building TraceMap Under Review Pressure",
  "Public claim level: concept",
  "No public conclusion without evidence",
  "Claim-level note",
  "The pressure that shaped the workflow",
  "Specs before implementation",
  "Implementation with reviewable diffs",
  "Kiro, Qodo, and review-loop coordination",
  "What the workflow does not prove",
  "Lessons for evidence-led specs",
  "Validation and publication checklist",
  "Human ownership remains necessary for merge, publication, product claims, and unresolved judgment calls."
];

const forbiddenClaimPatterns = [
  /\bTraceMap uses AI\b/i,
  /\bAI impact analysis\b/i,
  /\bLLM impact analysis\b/i,
  /\bembeddings?\b/i,
  /\bvector databases?\b/i,
  /\bprompt classification\b/i,
  /\bproduction traffic\b/i,
  /\bendpoint performance\b/i,
  /\boutage cause\b/i,
  /\brelease safe\b/i,
  /\bsafe to release\b/i,
  /\bapproved by Codex\b/i,
  /\bapproved by Kiro\b/i,
  /\bapproved by Qodo\b/i,
  /\bcertified by\b/i,
  /\bendorsed by\b/i,
  /\bautonomous merge\b/i,
  /\bcomplete coverage\b/i,
  /\btools consume TraceMap\b/i,
  /\bconsume TraceMap output\b/i,
  /\b(?:Codex|Kiro|Qodo)\s+(?:certifies?|endorses?|approves?)\b/i,
  /\b(?:review loop|review-loop coordination layer)\s+(?:approves?|certifies?|endorses?)\b/i
];

const privateMaterialPatterns = [
  /\bprivate session IDs?\b/i,
  /\bhidden run IDs?\b/i,
  /\braw bot transcripts?\b/i,
  /\braw review logs?\b/i,
  /\braw source snippets?\b/i,
  /\braw facts?\b/i,
  /\braw SQLite(?: content)?\b/i,
  /\banalyzer logs?\b/i,
  /\braw SQL\b/i,
  /\bconfiguration values?\b/i,
  /\bconfig values?\b/i,
  /\bgenerated scan directories\b/i,
  /\bsecrets?\b/i,
  /\bcredential-like values?\b/i,
  /\blocal paths?\b/i,
  /\braw remotes?\b/i,
  /\bprivate sample names?\b/i,
  /\bhidden validation details\b/i,
  /\bfacts\.ndjson\b/i,
  /\bindex\.sqlite\b/i,
  /\blogs\/analyzer\.log\b/i
];

const hardPrivatePatterns = [
  { label: "home directory path", pattern: /(?:^|[\s"'(])(?:\/Users\/|\/home\/|~\/)[^\s<>"']*/i },
  { label: "Windows user directory path", pattern: /[A-Z]:\\Users\\/i },
  { label: "file URL", pattern: /\bfile:\/\//i },
  { label: "localhost", pattern: /\blocalhost\b/i },
  { label: "loopback address", pattern: /\b127\.0\.0\.1\b/ },
  { label: "raw git remote", pattern: /\bgit@[\w.-]+:/i },
  { label: "raw ssh remote", pattern: /\bssh:\/\/[^\s<>"']+/i },
  { label: "credential-like value", pattern: /\b(?:Password|Secret|Token|ApiKey|ConnectionString)\s*=/i },
  { label: "OpenAI-style secret", pattern: /\bsk-[A-Za-z0-9_-]{12,}\b/i }
];

export async function validateBuildReviewWorkflowStoryDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors,
  root
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "blog", buildReviewWorkflowStorySlug, "index.html");

  if (root) {
    await validateMetadata({ errors: localErrors, root });
  }

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Build-review workflow story is missing required route: ${buildReviewWorkflowStoryRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  await validateBlogIndex({ dist, errors: localErrors });
  await validateOldWorkflowCrossLink({ dist, errors: localErrors });
  await validateArticlePage({ pagePath, errors: localErrors });

  errors.push(...localErrors);
}

async function validateMetadata({ errors, root }) {
  const metadataPath = resolve(root, "_blog", "articles.json");
  const bodyPath = resolve(root, "_blog", "articles", `${buildReviewWorkflowStorySlug}.html`);

  if (!(await fileExists(metadataPath))) {
    return;
  }

  let articles;
  try {
    articles = JSON.parse(await readFile(metadataPath, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Build-review workflow story metadata could not be parsed: ${error.message}`, metadataArtifact));
    return;
  }

  if (!Array.isArray(articles)) {
    errors.push(withEvidence("Build-review workflow story metadata must be an array.", metadataArtifact));
    return;
  }

  const seen = new Set();
  for (const article of articles) {
    if (typeof article?.slug === "string") {
      if (seen.has(article.slug)) {
        errors.push(withEvidence(`Build-review workflow story metadata has duplicate slug: ${article.slug}`, metadataArtifact));
      }
      seen.add(article.slug);
    }
  }

  const article = articles.find((entry) => entry?.slug === buildReviewWorkflowStorySlug);
  if (!article) {
    errors.push(withEvidence(`Build-review workflow story metadata is missing slug: ${buildReviewWorkflowStorySlug}`, metadataArtifact));
    return;
  }

  if (existingBlogSlugs.has(article.slug)) {
    errors.push(withEvidence(`Build-review workflow story reuses an existing article slug: ${article.slug}`, metadataArtifact));
  }

  const expectedFields = {
    body: articleBody,
    category: "Workflow governance",
    title: "Building TraceMap Under Review Pressure",
    h1: "Building TraceMap Under Review Pressure"
  };

  for (const [field, expected] of Object.entries(expectedFields)) {
    if (article[field] !== expected) {
      errors.push(withEvidence(`Build-review workflow story metadata expected ${field} ${expected}, got ${String(article[field])}`, metadataArtifact));
    }
  }

  if (article.category === "Project workflow") {
    errors.push(withEvidence("Build-review workflow story must use a category distinct from the earlier Codex/Kiro/Qodo article.", metadataArtifact));
  }

  if (typeof article.title !== "string" || article.title.length > 70) {
    errors.push(withEvidence("Build-review workflow story metadata title must be 70 characters or fewer.", metadataArtifact));
  }

  for (const field of ["description", "ogDescription", "cardDescription"]) {
    if (typeof article[field] !== "string" || article[field].length > 180) {
      errors.push(withEvidence(`Build-review workflow story metadata ${field} must be 180 characters or fewer.`, metadataArtifact));
    }
  }

  for (const [field, value] of Object.entries(article)) {
    if (typeof value !== "string") {
      continue;
    }

    const text = normalizeRenderedText(decodeHtmlEntities(value));
    validateForbiddenClaims(text, errors, metadataArtifact);
    validatePrivateMaterial(text, errors, metadataArtifact);
    validateHardPrivateMaterial(text, errors, metadataArtifact);
  }

  if (!/concept-level|concept/i.test(`${article.description} ${article.ogDescription} ${article.cardDescription} ${article.hero}`)) {
    errors.push(withEvidence("Build-review workflow story metadata must stay concept-level.", metadataArtifact));
  }

  if (!(await fileExists(bodyPath))) {
    errors.push(withEvidence("Build-review workflow story source body is missing.", articleBodyArtifact));
  }
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${buildReviewWorkflowStoryRoute}`)) {
    errors.push(withEvidence(`Build-review workflow story sitemap is missing required route: ${baseUrl}${buildReviewWorkflowStoryRoute}`, sitemapArtifact));
  }
}

async function validateBlogIndex({ dist, errors }) {
  const blogIndexPath = resolve(dist, "blog", "index.html");
  if (!(await fileExists(blogIndexPath))) {
    errors.push(withEvidence("Build-review workflow story blog index is missing.", blogIndexArtifact));
    return;
  }

  const html = await readFile(blogIndexPath, "utf8");
  if (!hasHref(html, buildReviewWorkflowStoryRoute)) {
    errors.push(withEvidence(`Build-review workflow story blog index is missing article link: ${buildReviewWorkflowStoryRoute}`, blogIndexArtifact));
  }

  const text = normalizeRenderedText(html);
  if (!text.includes("Workflow governance")) {
    errors.push(withEvidence("Build-review workflow story blog index card must use the workflow governance category.", blogIndexArtifact));
  }
}

async function validateOldWorkflowCrossLink({ dist, errors }) {
  const oldPath = resolve(dist, "blog", "building-tracemap-with-codex-kiro-qodo", "index.html");
  if (!(await fileExists(oldPath))) {
    return;
  }

  const html = await readFile(oldPath, "utf8");
  if (!hasHref(html, buildReviewWorkflowStoryRoute)) {
    errors.push(withEvidence(`Earlier workflow article must link to ${buildReviewWorkflowStoryRoute}`, oldWorkflowArtifact));
  }
}

async function validateArticlePage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const renderedText = normalizeRenderedText(decodedHtml);
  const metadataText = collectMetadataText(decodedHtml);
  const attributeText = collectAttributeText(decodedHtml);
  const articleBodyHtml = extractArticleBody(html);
  const decodedArticleBodyHtml = decodeHtmlEntities(articleBodyHtml);
  const unmarkedHtml = decodeHtmlEntities(stripMarkedRegions(articleBodyHtml));
  const unmarkedText = normalizeRenderedText(unmarkedHtml);
  const unmarkedCollapsedText = collapseTagSplitText(unmarkedHtml);
  const wordCount = countWords(normalizeRenderedText(decodedArticleBodyHtml));

  if (!html.includes('<meta property="og:type" content="article">')) {
    errors.push(withEvidence('Build-review workflow story must include <meta property="og:type" content="article">.', pageArtifact));
  }

  if (!html.includes(`href="https://tracemap.tools${buildReviewWorkflowStoryRoute}"`)) {
    errors.push(withEvidence(`Build-review workflow story canonical URL must target ${buildReviewWorkflowStoryRoute}`, pageArtifact));
  }

  for (const phrase of requiredText) {
    if (!renderedText.includes(phrase)) {
      errors.push(withEvidence(`Build-review workflow story is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const block of requiredBlocks) {
    if (!hasDataBlock(html, block)) {
      errors.push(withEvidence(`Build-review workflow story is missing required block: ${block}`, pageArtifact));
    }
  }

  const nonClaimSection = getTaggedBlockByAttribute(decodedHtml, "section", nonClaimMarker, "workflow-does-not-prove");
  if (!nonClaimSection) {
    errors.push(withEvidence(`Build-review workflow story non-claims section must use ${nonClaimMarker}.`, pageArtifact));
  }

  for (const link of buildReviewWorkflowRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Build-review workflow story is missing required link: ${link}`, pageArtifact));
    }
  }

  if (wordCount < 700 || wordCount > 1600) {
    errors.push(withEvidence(`Build-review workflow story word count must be between 700 and 1600 words, got ${wordCount}`, pageArtifact));
  }

  validateForbiddenClaims(`${unmarkedHtml} ${unmarkedText} ${unmarkedCollapsedText} ${metadataText} ${attributeText}`, errors);
  validatePrivateMaterial(`${unmarkedHtml} ${unmarkedText} ${unmarkedCollapsedText} ${metadataText} ${attributeText}`, errors);
  validateHardPrivateMaterial(`${decodedHtml} ${renderedText} ${metadataText} ${attributeText}`, errors);
}

function stripMarkedRegions(html) {
  let stripped = html;
  for (const attribute of [nonClaimMarker, rejectedMarker, "data-tm-boundary"]) {
    stripped = stripped.replace(
      new RegExp(`<([a-z][a-z0-9:-]*)\\b(?=[^>]*\\b${attribute}\\b)[^>]*>[\\s\\S]*?<\\/\\1>`, "gi"),
      " "
    );
  }
  return stripped;
}

function collectMetadataText(html) {
  return [...html.matchAll(/<meta\b[^>]*>/gi)]
    .map((match) => getHtmlAttribute(match[0], "content") ?? "")
    .filter(Boolean)
    .join(" ");
}

function collectAttributeText(html) {
  return [...html.matchAll(/\b(?:aria-label|title|alt)\s*=\s*["']([^"']*)["']/gi)].map((match) => match[1]).join(" ");
}

function extractArticleBody(html) {
  const match = html.match(/<div\b[^>]*\bclass\s*=\s*["'][^"']*\barticle-body\b[^"']*["'][^>]*>([\s\S]*?)<\/div>\s*<\/article>/i);
  return match ? match[1] : html;
}

function getTaggedBlockByAttribute(html, tag, attribute, value) {
  const escapedAttribute = escapeRegExp(attribute);
  const escapedValue = escapeRegExp(value);
  const pattern = new RegExp(
    `<${tag}\\b(?=[^>]*\\b${escapedAttribute}\\s*=\\s*["']${escapedValue}["'])[^>]*>[\\s\\S]*?<\\/${tag}>`,
    "i"
  );
  return html.match(pattern)?.[0] ?? null;
}

function hasDataBlock(html, block) {
  const escaped = escapeRegExp(block);
  return new RegExp(`\\bdata-build-review-block\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escaped}["']`, "i").test(html);
}

function getHtmlAttribute(tag, name) {
  const escaped = escapeRegExp(name);
  const match = tag.match(new RegExp(`\\b${escaped}\\s*=\\s*["']([^"']*)["']`, "i"));
  return match ? decodeHtmlEntities(match[1]) : null;
}

function collapseTagSplitText(html) {
  return normalizeRenderedText(String(html).replace(/<[^>]+>/g, ""));
}

function countWords(text) {
  return [...text.matchAll(/\b[A-Za-z0-9][A-Za-z0-9'-]*\b/g)].length;
}

function validateForbiddenClaims(text, errors, artifact = pageArtifact) {
  for (const pattern of forbiddenClaimPatterns) {
    const match = text.match(pattern);
    if (match) {
      errors.push(withEvidence(`Build-review workflow story contains forbidden claim outside marked regions: ${match[0]}`, artifact));
    }
  }
}

function validatePrivateMaterial(text, errors, artifact = pageArtifact) {
  for (const pattern of privateMaterialPatterns) {
    const match = text.match(pattern);
    if (match) {
      errors.push(withEvidence(`Build-review workflow story contains forbidden private/raw material outside marked regions: ${match[0]}`, artifact));
    }
  }
}

function validateHardPrivateMaterial(text, errors, artifact = pageArtifact) {
  for (const { label, pattern } of hardPrivatePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Build-review workflow story contains hard private or credential-like text: ${label}`, artifact));
    }
  }
}

function withEvidence(message, artifact) {
  return `${message} (${artifact})`;
}
