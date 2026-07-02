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

export const swiftStoryPages = [
  {
    route: "/swift/static-inventory/",
    title: "Swift Static Inventory",
    claimLevel: "shipped",
    requiredText: [
      "Public claim level: shipped",
      "Swift static inventory",
      "package/project inventory",
      "source file selection",
      "module-ish metadata",
      "reduced-coverage labels",
      "not Xcode build proof"
    ]
  },
  {
    route: "/swift/symbols-calls/",
    title: "Swift Symbols And Calls",
    claimLevel: "shipped",
    requiredText: [
      "Public claim level: shipped",
      "Swift symbols and calls",
      "SwiftSyntax-backed declarations",
      "call candidates",
      "construction candidates",
      "syntax evidence",
      "not runtime dispatch proof"
    ]
  },
  {
    route: "/swift/surface-discovery/",
    title: "Swift Surface Discovery",
    claimLevel: "demo",
    requiredText: [
      "Public claim level: demo",
      "Swift surface discovery",
      "HTTP/API client surfaces",
      "SwiftUI/UIKit-ish surfaces",
      "package/dependency surfaces",
      "dynamic composition gaps",
      "not runtime network reachability"
    ]
  },
  {
    route: "/swift/storage-data/",
    title: "Swift Storage And Data Surfaces",
    claimLevel: "demo",
    requiredText: [
      "Public claim level: demo",
      "Swift storage and data surfaces",
      "CoreData metadata",
      "UserDefaults keys",
      "Keychain access patterns",
      "SQLite SQL text/shape evidence",
      "not stored-value proof"
    ]
  },
  {
    route: "/swift/evidence-safety/",
    title: "Swift Evidence Safety",
    claimLevel: "shipped",
    requiredText: [
      "Public claim level: shipped",
      "Swift evidence safety",
      "rule IDs",
      "evidence tiers",
      "coverage labels",
      "hashed sensitive identifiers",
      "public pages must not publish raw source snippets"
    ]
  }
];

export const swiftStoryPageRoutes = swiftStoryPages.map((page) => page.route);
export const swiftStoryPageRequiredLinks = [
  "/swift/",
  "/swift/story/",
  "/swift/real-world-smoke/",
  "/validation/",
  "/limitations/",
  "/limitations/reduced-coverage/",
  "/site-claim-guardrails/"
];

const sitemapArtifact = "sitemap.xml";
const routesIndexArtifact = "routes-index.json";

const hardPrivatePatterns = [
  /\/Users\//i,
  /\/private\//i,
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

const forbiddenPositiveClaims = [
  /\bTraceMap\b[^.]{0,120}\b(?:proves|guarantees|certifies|approves|validates)\b[^.]{0,120}\b(?:runtime|build|navigation|production|release|stored values?|query execution|live schema|complete Swift understanding)\b/i,
  /\bSwift\b[^.]{0,120}\b(?:proves|guarantees|certifies|approves|validates)\b[^.]{0,120}\b(?:runtime|build|navigation|production|release|stored values?|query execution|live schema|complete Swift understanding)\b/i,
  /\bTraceMap\b[^.]{0,120}\b(?:uses|performs|provides|runs|adds)\b[^.]{0,120}\b(?:AI impact analysis|LLM analysis|prompt-based classification|embedding search|vector database analysis)\b/i,
  /\b(?:safe to release|ready to release|approved to merge|complete Swift analysis)\b/i
];

export async function validateSwiftStoryPagesDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl, localErrors);

  if (cleanBaseUrl) {
    await validateSitemap({ baseUrl: cleanBaseUrl, dist, errors: localErrors });
  }
  await validateRoutesIndex({ dist, errors: localErrors });

  for (const page of swiftStoryPages) {
    await validatePage({ dist, page, errors: localErrors });
  }

  errors.push(...localErrors);
}

async function validateSitemap({ baseUrl, dist, errors }) {
  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) {
    return;
  }

  const sitemapUrls = await readSitemapLocSet(sitemapPath);
  for (const page of swiftStoryPages) {
    if (!sitemapUrls.has(`${baseUrl}${page.route}`)) {
      errors.push(withEvidence(`Swift story pages sitemap is missing required route: ${baseUrl}${page.route}`, sitemapArtifact));
    }
  }
}

async function validateRoutesIndex({ dist, errors }) {
  const indexPath = resolve(dist, "routes-index.json");
  if (!(await fileExists(indexPath))) {
    return;
  }

  let parsed;
  try {
    parsed = JSON.parse(await readFile(indexPath, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Swift story pages could not parse routes-index.json: ${error.message}`, routesIndexArtifact));
    return;
  }

  if (!Array.isArray(parsed?.entries)) {
    errors.push(withEvidence("Swift story pages routes-index.json is invalid: expected entries array.", routesIndexArtifact));
    return;
  }

  for (const page of swiftStoryPages) {
    const routeEntry = parsed.entries.find((entry) => entry?.path === page.route);
    if (!routeEntry) {
      errors.push(withEvidence(`Swift story pages routes-index.json is missing required route: ${page.route}`, routesIndexArtifact));
      continue;
    }

    const expectedFields = {
      publicClaimLevel: page.claimLevel,
      hintCategory: "evidence",
      sourceType: "site-page",
      preferredProofPath: "/swift/"
    };

    for (const [field, expected] of Object.entries(expectedFields)) {
      if (routeEntry[field] !== expected) {
        errors.push(withEvidence(`Swift story pages routes-index.json expected ${page.route} ${field} ${expected}, got ${String(routeEntry[field])}`, routesIndexArtifact));
      }
    }

    const metadataText = normalizeRenderedText(
      [
        routeEntry.title,
        routeEntry.summary,
        ...(Array.isArray(routeEntry.limitations) ? routeEntry.limitations : []),
        ...(Array.isArray(routeEntry.nonClaims) ? routeEntry.nonClaims : [])
      ].join(" ")
    );
    const routePolicyText = decodeHtmlEntities(metadataText);

    for (const phrase of ["runtime behavior", "build success", "release safety", "AI impact analysis", "raw source snippets"]) {
      if (!metadataText.toLowerCase().includes(phrase.toLowerCase())) {
        errors.push(withEvidence(`Swift story pages route metadata for ${page.route} is missing boundary phrase: ${phrase}`, routesIndexArtifact));
      }
    }

    scanPolicyText({ errors, label: `${page.route} route metadata`, text: routePolicyText, artifact: routesIndexArtifact });
  }
}

async function validatePage({ dist, page, errors }) {
  const pathParts = page.route.replace(/^\/|\/$/g, "").split("/");
  const pageArtifact = `${pathParts.join("/")}/index.html`;
  const pagePath = resolve(dist, ...pathParts, "index.html");

  if (!(await fileExists(pagePath))) {
    errors.push(withEvidence(`Swift story page is missing required public route: ${page.route}`, pageArtifact));
    return;
  }

  const html = await readFile(pagePath, "utf8");
  const decodedHtml = decodeHtmlEntities(html);
  const pageText = normalizeRenderedText(html);
  const policyText = `${decodedHtml} ${pageText}`;

  if (!new RegExp(`<title>${escapeRegExp(page.title)} \\| TraceMap<\\/title>`, "i").test(html)) {
    errors.push(withEvidence(`Swift story page ${page.route} is missing expected title.`, pageArtifact));
  }

  if (!/\bog:type["']\s+content=["']article["']/i.test(html) && !/\bog:type["']\s+content\s*=\s*["']article["']/i.test(html)) {
    errors.push(withEvidence(`Swift story page ${page.route} must use article metadata.`, pageArtifact));
  }

  if (!/\bdata-swift-story-page\b/i.test(html)) {
    errors.push(withEvidence(`Swift story page ${page.route} is missing the story page marker.`, pageArtifact));
  }

  for (const phrase of page.requiredText) {
    if (!pageText.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Swift story page ${page.route} is missing required text: ${phrase}`, pageArtifact));
    }
  }

  for (const link of swiftStoryPageRequiredLinks) {
    if (!hasHref(html, link)) {
      errors.push(withEvidence(`Swift story page ${page.route} is missing required link: ${link}`, pageArtifact));
    }
  }

  if (!hasHref(html, "https://github.com/joefeser/tracemap/pull/425")) {
    errors.push(withEvidence(`Swift story page ${page.route} is missing Swift v0 proof link.`, pageArtifact));
  }

  scanPolicyText({ errors, label: `${page.route} page`, text: policyText, artifact: pageArtifact });
}

function scanPolicyText({ errors, label, text, artifact }) {
  for (const pattern of hardPrivatePatterns) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Swift story pages ${label} contains forbidden private material: ${redactPattern(pattern)}`, artifact));
    }
  }

  for (const pattern of forbiddenPositiveClaims) {
    if (pattern.test(text)) {
      errors.push(withEvidence(`Swift story pages ${label} contains unsupported Swift claim wording: ${pattern}`, artifact));
    }
  }
}

function hasHref(html, href) {
  return new RegExp(`<a\\b(?=[^>]*\\bhref\\s*=\\s*["']${escapeRegExp(href)}["'])[^>]*>`, "i").test(html);
}

function redactPattern(pattern) {
  return `redacted ${pattern.source.slice(0, 24)}...`;
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
