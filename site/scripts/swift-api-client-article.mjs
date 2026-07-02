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

export const swiftApiClientArticleSlug = "how-tracemap-reads-swift-api-clients";
export const swiftApiClientArticleRoute = `/blog/${swiftApiClientArticleSlug}/`;
export const swiftApiClientArticleRequiredLinks = [
  "/swift/api-client-walkthrough/",
  "/swift/surface-discovery/",
  "/swift/real-world-smoke/",
  "/swift/claim-language/"
];

const pageArtifact = `blog/${swiftApiClientArticleSlug}/index.html`;
const blogIndexArtifact = "blog/index.html";
const sitemapArtifact = "sitemap.xml";
const requiredBlocks = [
  "mobile-dependencies",
  "static-candidates",
  "four-shapes",
  "read-a-row",
  "non-claims",
  "where-next"
];
const requiredText = [
  "Public claim level: demo",
  "mobile apps hide backend dependencies in client code",
  "static API-client candidates",
  "URLSession",
  "URLRequest",
  "Alamofire",
  "Moya",
  "rule ID",
  "evidence tier",
  "coverage label",
  "limitation",
  "non-claim",
  "review, migration planning, endpoint inventory, and change-risk conversations",
  "not runtime tracing",
  "endpoint reachability",
  "backend compatibility",
  "request success",
  "auth flow",
  "production traffic",
  "API correctness",
  "TraceMap core scanning and reduction do not use LLM analysis, prompt-based classification, embeddings, or vector databases"
];

const forbiddenPositiveClaims = [
  /\bTraceMap\b[^.]{0,140}\b(?:proves|guarantees|certifies|approves|validates)\b[^.]{0,140}\b(?:runtime|endpoint reachability|backend compatibility|request success|auth flow|production traffic|API correctness|release safety|complete Swift semantic analysis)\b/i,
  /\bSwift\b[^.]{0,140}\b(?:proves|guarantees|certifies|approves|validates)\b[^.]{0,140}\b(?:runtime|endpoint reachability|backend compatibility|request success|auth flow|production traffic|API correctness|release safety|complete Swift semantic analysis)\b/i,
  /\b(?:safe to release|ready to release|approved to merge|complete Swift analysis|runtime tracing confirms)\b/i,
  /\bTraceMap\b[^.]{0,140}\b(?:uses|performs|provides|runs|adds)\b[^.]{0,140}\b(?:AI impact analysis|LLM analysis|prompt-based classification|embeddings?|embedding-backed search|vector databases?|vector database analysis)\b/i
];

const rawMaterialPatterns = [
  /\bscan-manifest\.json\b/i,
  /\bfacts\.ndjson\b/i,
  /\bindex\.sqlite\b/i,
  /\breport\.md\b/i,
  /\blogs\/analyzer\.log\b/i,
  /\banalyzer\.log\b/i,
  /\braw source snippets?\b/i,
  /\braw SQL\b/i,
  /\bsecrets?\b/i,
  /\bcredentials?\b/i,
  /\blocal absolute paths?\b/i,
  /\braw remotes?\b/i
];

const hardPrivatePatterns = [
  /\/Users\//i,
  /\/private\//i,
  /\/home\//i,
  /\/tmp\//i,
  /\/var\/folders\//i,
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

export async function validateSwiftApiClientArticleDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors
}) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl);
  const pagePath = resolve(dist, "blog", swiftApiClientArticleSlug, "index.html");
  const blogIndexPath = resolve(dist, "blog", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Swift API-client article is missing required route: ${swiftApiClientArticleRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  await validateBlogIndex({ blogIndexPath, errors: localErrors });
  await validateArticlePage({ pagePath, errors: localErrors });

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  if (!sitemapUrls.has(`${baseUrl}${swiftApiClientArticleRoute}`)) {
    errors.push(withEvidence(`Swift API-client article sitemap is missing required route: ${baseUrl}${swiftApiClientArticleRoute}`, sitemapArtifact));
  }
}

async function validateBlogIndex({ blogIndexPath, errors }) {
  if (!(await fileExists(blogIndexPath))) {
    errors.push(withEvidence("Swift API-client article blog index is missing.", blogIndexArtifact));
    return;
  }

  const html = await readFile(blogIndexPath, "utf8");
  if (!hasHref(html, swiftApiClientArticleRoute)) {
    errors.push(withEvidence(`Swift API-client article blog index is missing article link: ${swiftApiClientArticleRoute}`, blogIndexArtifact));
  }
}

async function validateArticlePage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const renderedText = normalizeRenderedText(html);
  const lowerText = renderedText.toLowerCase();
  const unsanctionedHtml = stripBoundarySections(decodedHtml);
  const unsanctionedText = normalizeRenderedText(unsanctionedHtml);
  const wordCount = countWords(renderedText);

  if (!html.includes("<title>How TraceMap Reads Swift API Clients Without Pretending They Ran | TraceMap</title>")) {
    errors.push(withEvidence("Swift API-client article is missing expected title.", pageArtifact));
  }

  if (!hasTagWithAttributes(html, "meta", { property: "og:type", content: "article" })) {
    errors.push(withEvidence("Swift API-client article must use article metadata.", pageArtifact));
  }

  if (!hasTagWithAttributes(html, "link", { rel: "canonical", href: `https://tracemap.tools${swiftApiClientArticleRoute}` })) {
    errors.push(withEvidence(`Swift API-client article canonical URL must target ${swiftApiClientArticleRoute}`, pageArtifact));
  }

  for (const block of requiredBlocks) {
    if (!findTagWithAttribute(html, "section", "data-swift-api-blog-block", block)) {
      errors.push(withEvidence(`Swift API-client article is missing required block: ${block}`, pageArtifact));
    }
  }

  for (const phrase of requiredText) {
    if (!lowerText.includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Swift API-client article is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const link of swiftApiClientArticleRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Swift API-client article is missing required link: ${link}`, pageArtifact));
    }
  }

  if (wordCount < 700 || wordCount > 1700) {
    errors.push(withEvidence(`Swift API-client article word count must be between 700 and 1700 words, got ${wordCount}`, pageArtifact));
  }

  scanPositiveClaimText({ errors, label: "article", text: renderedText, artifact: pageArtifact });
  scanRawMaterialText({ errors, label: "article", text: unsanctionedText, artifact: pageArtifact });
  scanHardPrivateText({ errors, label: "article", text: decodedHtml, artifact: pageArtifact });
}

function scanPositiveClaimText({ errors, label, text, artifact }) {
  for (const pattern of forbiddenPositiveClaims) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Swift API-client ${label} contains unsupported public claim: ${pattern}`, artifact));
    }
  }
}

function scanRawMaterialText({ errors, label, text, artifact }) {
  for (const pattern of rawMaterialPatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Swift API-client ${label} contains raw/private material outside a boundary section: ${pattern}`, artifact));
    }
  }
}

function scanHardPrivateText({ errors, label, text, artifact }) {
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Swift API-client ${label} contains hard private material: ${pattern}`, artifact));
    }
  }
}

function hasHref(html, href) {
  return new RegExp(`<a\\b[^>]*href\\s*=\\s*["']${escapeRegExp(href)}["'][^>]*>`, "i").test(html);
}

function hasTagWithAttributes(html, tagName, attributes) {
  return Boolean(findTagWithAttributes(html, tagName, attributes));
}

function findTagWithAttribute(html, tagName, attributeName, attributeValue) {
  return findTagWithAttributes(html, tagName, { [attributeName]: attributeValue });
}

function findTagWithAttributes(html, tagName, attributes) {
  const tagPattern = new RegExp(`<${escapeRegExp(tagName)}\\b[^>]*>`, "gi");
  let match;
  while ((match = tagPattern.exec(html)) !== null) {
    const tagHtml = match[0];
    const hasAllAttributes = Object.entries(attributes).every(([name, value]) => hasAttribute(tagHtml, name, value));
    if (hasAllAttributes) {
      return match;
    }
  }

  return null;
}

function hasAttribute(tagHtml, attributeName, attributeValue) {
  return new RegExp(`\\b${escapeRegExp(attributeName)}\\s*=\\s*["']${escapeRegExp(attributeValue)}["']`, "i").test(tagHtml);
}

function stripBoundarySections(html) {
  let output = "";
  let cursor = 0;

  while (cursor < html.length) {
    const boundaryMatch = findBoundarySection(html, cursor);
    if (!boundaryMatch) {
      output += html.slice(cursor);
      break;
    }

    output += html.slice(cursor, boundaryMatch.index);
    cursor = findSectionEnd(html, boundaryMatch.index + boundaryMatch.tagHtml.length);
  }

  return output;
}

function findBoundarySection(html, startIndex) {
  const sectionPattern = /<section\b[^>]*>/gi;
  sectionPattern.lastIndex = startIndex;

  let match;
  while ((match = sectionPattern.exec(html)) !== null) {
    const tagHtml = match[0];
    if (/\bdata-tm-boundary\s*=\s*["'][^"']+["']/i.test(tagHtml)) {
      return { index: match.index, tagHtml };
    }
  }

  return null;
}

function findSectionEnd(html, contentStart) {
  const tagPattern = /<\/?section\b[^>]*>/gi;
  tagPattern.lastIndex = contentStart;
  let depth = 1;

  let match;
  while ((match = tagPattern.exec(html)) !== null) {
    if (match[0].startsWith("</")) {
      depth -= 1;
      if (depth === 0) {
        return tagPattern.lastIndex;
      }
    } else {
      depth += 1;
    }
  }

  return html.length;
}

function countWords(text) {
  return text.split(/\s+/).filter(Boolean).length;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
