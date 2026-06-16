import { readdir, readFile } from "node:fs/promises";
import { dirname, extname, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

import { buildSite } from "./build.mjs";

const defaultRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
export const legacyStoryTargetPath = "legacy-evidence/index.html";

const allowedDisclaimerSentences = [
  "No runtime proof, UI reachability, production traffic, deployment state, endpoint performance, exploitability, database existence, package compatibility, incident cause, release approval, or release safety is claimed by this concept page.",
  "Static evidence does not claim runtime behavior, UI reachability, production traffic, deployment state, endpoint performance, exploitability, database existence, package compatibility, incident cause, release approval, or release safety.",
  "No runtime behavior, production traffic, deployment state, endpoint performance, release safety, or release approval proof.",
  "No database existence, query execution, schema compatibility, or production data result is claimed here."
];

const affirmativeOverclaimPhrases = [
  "runtime proof",
  "runtime behavior",
  "UI reachability",
  "production traffic",
  "deployment state",
  "endpoint performance",
  "exploitability",
  "database existence",
  "package compatibility",
  "incident cause",
  "release approval",
  "release safety"
];

const hiddenThemePatterns = [
  /WCF\/service-reference mapping/gi,
  /WCF metadata normalization/gi,
  /\.NET Remoting detection/gi,
  /WebForms event flow/gi,
  /Legacy data metadata/gi,
  /Build diagnostics/gi,
  /Flow composition/gi
];

const hardLeakChecks = [
  {
    id: "local-absolute-path",
    pattern: /(?:^|[\s"'(=])(?:\/Users\/|\/home\/|\/tmp\/|\/var\/folders\/|\/private\/var\/|[A-Z]:\\)[^\s<>"')]+/gi
  },
  {
    id: "generated-output-root",
    pattern: /(?:^|[\s"'(=])(?:site\/)?(?:dist|output)\/[^\s<>"')]+|(?:^|[\s"'(=])\.tmp\/[^\s<>"')]+/gi
  },
  {
    id: "connection-string",
    pattern: /\b(?:Server|Data Source|Initial Catalog|Database|User ID|User Id|Uid|Password|Pwd)\s*=\s*[^;\s<>"']+(?:\s*;\s*(?:Server|Data Source|Initial Catalog|Database|User ID|User Id|Uid|Password|Pwd)\s*=\s*[^;\s<>"']+)+/gi
  },
  {
    id: "credential-assignment",
    pattern: /\b(?:api[_-]?key|access[_-]?token|secret|password|passwd|pwd|client[_-]?secret|connection[_-]?string)\s*(?:=|:)\s*["']?[^"'\s<>{}]+/gi
  },
  {
    id: "private-local-url",
    pattern: /\b(?:https?:\/\/(?:localhost|127(?:\.\d{1,3}){3}|10(?:\.\d{1,3}){3}|192\.168(?:\.\d{1,3}){2}|172\.(?:1[6-9]|2\d|3[0-1])(?:\.\d{1,3}){2}|[^/\s<>"']+\.local)(?::\d+)?(?:\/[^\s<>"']*)?|file:\/\/[^\s<>"']*)/gi
  },
  {
    id: "raw-repository-remote",
    pattern: /\b(?:git@[^:\s<>"']+:[^\s<>"']+|ssh:\/\/git@[^/\s<>"']+\/[^\s<>"']+|https:\/\/[^/\s<>"']+\/[^\s<>"']+\/[^\s<>"']+\.git)\b/gi
  },
  {
    id: "raw-source-snippet",
    pattern: /\bsource snippets?\s*:\s*\S+/gi
  }
];

const evidenceRedactions = [
  {
    id: "connection-string",
    pattern: /\b(?:Server|Data Source|Initial Catalog|Database|User ID|User Id|Uid|Password|Pwd)\s*=\s*[^;\s<>"']+(?:\s*;\s*(?:Server|Data Source|Initial Catalog|Database|User ID|User Id|Uid|Password|Pwd)\s*=\s*[^;\s<>"']+)+/gi,
    replacement: "[redacted connection string]"
  },
  {
    id: "credential-assignment",
    pattern: /\b(?:api[_-]?key|access[_-]?token|secret|password|passwd|pwd|client[_-]?secret|connection[_-]?string)\s*(?:=|:)\s*["']?[^"'\s<>{}]+/gi,
    replacement: "[redacted credential assignment]"
  },
  {
    id: "private-local-url",
    pattern: /\b(?:https?:\/\/(?:localhost|127(?:\.\d{1,3}){3}|10(?:\.\d{1,3}){3}|192\.168(?:\.\d{1,3}){2}|172\.(?:1[6-9]|2\d|3[0-1])(?:\.\d{1,3}){2}|[^/\s<>"']+\.local)(?::\d+)?(?:\/[^\s<>"']*)?|file:\/\/[^\s<>"']*)/gi,
    replacement: "[redacted private URL]"
  },
  {
    id: "local-absolute-path",
    pattern: /(?:^|[\s"'(=])(?:\/Users\/|\/home\/|\/tmp\/|\/var\/folders\/|\/private\/var\/|[A-Z]:\\)[^\s<>"')]+/gi,
    replacement: "[redacted local path]"
  },
  {
    id: "raw-repository-remote",
    pattern: /\b(?:git@[^:\s<>"']+:[^\s<>"']+|ssh:\/\/git@[^/\s<>"']+\/[^\s<>"']+|https:\/\/[^/\s<>"']+\/[^\s<>"']+\/[^\s<>"']+\.git)\b/gi,
    replacement: "[redacted repository remote]"
  }
];

export async function validateLegacyStorySafety({ root = defaultRoot } = {}) {
  const dist = resolve(root, "dist");
  const errors = [];
  const scannedFiles = await collectLegacyStoryTargets(dist, errors);

  if (scannedFiles.length === 0) {
    errors.push(`Legacy story safety found no rendered HTML files for ${legacyStoryTargetPath}.`);
  }

  if (!scannedFiles.some((file) => formatDistPath(dist, file) === legacyStoryTargetPath)) {
    errors.push(`Legacy story safety did not scan required target: ${legacyStoryTargetPath}.`);
  }

  for (const file of scannedFiles) {
    const html = await readFile(file, "utf8");
    errors.push(...validateRenderedLegacyStoryHtml(html, { label: formatDistPath(dist, file) }));
  }

  if (errors.length > 0) {
    throw new Error(`Legacy story content safety failed:\n- ${errors.join("\n- ")}`);
  }

  return {
    scannedFileCount: scannedFiles.length,
    targetPath: legacyStoryTargetPath
  };
}

export async function buildAndValidateLegacyStorySafety({ log = () => {}, root = defaultRoot } = {}) {
  await buildSite({ log, root });
  return validateLegacyStorySafety({ root });
}

export function validateRenderedLegacyStoryHtml(html, { label = legacyStoryTargetPath } = {}) {
  const errors = [];
  const normalized = normalizeRenderedContent(decodeHtmlEntities(stripTags(html)));
  const normalizedTight = normalizeRenderedContent(decodeHtmlEntities(stripTagsTight(html)));
  const normalizedRaw = normalizeRenderedContent(decodeHtmlEntities(html));
  const normalizedForClaims = removeAllowedDisclaimers(normalized);

  for (const check of hardLeakChecks) {
    const match = [normalizedRaw, normalized, normalizedTight].flatMap((value) => value.match(check.pattern) ?? [])[0];
    if (match) {
      errors.push(`${label} contains forbidden ${check.id}: ${trimEvidence(match)}`);
    }
  }

  for (const evidence of findBareInternalSpecPaths(normalizedRaw)) {
    errors.push(`${label} contains bare internal spec path: ${trimEvidence(evidence)}`);
  }

  for (const phrase of affirmativeOverclaimPhrases) {
    const pattern = new RegExp(`\\b${escapeRegExp(phrase)}\\b`, "i");
    if (pattern.test(normalizedForClaims)) {
      errors.push(`${label} contains affirmative overclaim phrase outside an approved disclaimer: ${phrase}`);
    }
  }

  for (const issue of findUnlabeledHiddenThemeMentions(normalized)) {
    errors.push(`${label} mentions hidden legacy theme without adjacent hidden or omission label: ${issue}`);
  }

  return errors;
}

async function collectLegacyStoryTargets(dist, errors) {
  const files = await collectFiles(dist, errors);
  return files.filter((file) => extname(file) === ".html" && formatDistPath(dist, file) === legacyStoryTargetPath);
}

async function collectFiles(directory, errors) {
  const files = [];
  let entries;

  try {
    entries = await readdir(directory, { withFileTypes: true });
  } catch (error) {
    errors.push(`Unable to read generated output directory ${directory}: ${error.message}`);
    return files;
  }

  for (const entry of entries) {
    const path = resolve(directory, entry.name);

    if (entry.isDirectory()) {
      files.push(...(await collectFiles(path, errors)));
      continue;
    }

    if (entry.isFile()) {
      files.push(path);
    }
  }

  return files;
}

function findBareInternalSpecPaths(value) {
  const matches = [];

  for (const match of value.matchAll(/\.kiro\/specs\/[^\s<>"')]+/gi)) {
    const start = match.index ?? 0;
    const before = value.slice(Math.max(0, start - 80), start);
    if (/https:\/\/github\.com\/joefeser\/tracemap\/(?:blob|tree)\/(?:main|v\d+\.\d+\.\d+)\/$/i.test(before)) {
      continue;
    }

    matches.push(match[0]);
  }

  return matches;
}

function findUnlabeledHiddenThemeMentions(value) {
  const issues = [];

  for (const pattern of hiddenThemePatterns) {
    for (const match of value.matchAll(pattern)) {
      const start = match.index ?? 0;
      const window = value.slice(Math.max(0, start - 120), Math.min(value.length, start + match[0].length + 160));
      if (!/\b(?:hidden|omitted|omission|pending validation|public results remain hidden)\b/i.test(window)) {
        issues.push(match[0]);
      }
    }
  }

  return issues;
}

function removeAllowedDisclaimers(value) {
  let reduced = value;

  for (const sentence of allowedDisclaimerSentences) {
    reduced = reduced.replaceAll(normalizeRenderedContent(sentence), "");
  }

  return reduced;
}

function normalizeRenderedContent(value) {
  return value
    .normalize("NFKC")
    .replace(/\p{Cf}/gu, "")
    .replace(/\s+/g, " ")
    .trim();
}

function stripTags(html) {
  return stripTagsWithSeparator(html, " ");
}

function stripTagsTight(html) {
  return stripTagsWithSeparator(html, "");
}

function stripTagsWithSeparator(html, separator) {
  let text = "";
  let insideTag = false;
  let quote = "";
  let skippingRawText = "";

  for (let index = 0; index < html.length; index += 1) {
    const char = html[index];

    if (skippingRawText) {
      const closingTag = `</${skippingRawText}`;
      if (html.slice(index, index + closingTag.length).toLowerCase() === closingTag) {
        skippingRawText = "";
        insideTag = true;
        quote = "";
        text += separator;
        index += closingTag.length - 1;
      }
      continue;
    }

    if (insideTag) {
      if (quote) {
        if (char === quote) {
          quote = "";
        }
        continue;
      }

      if (char === '"' || char === "'") {
        quote = char;
        continue;
      }

      if (char === ">") {
        insideTag = false;
        text += separator;
      }
      continue;
    }

    if (char === "<") {
      const tagName = html.slice(index + 1).match(/^\s*\/?\s*([a-z][a-z0-9-]*)\b/i)?.[1]?.toLowerCase();
      insideTag = true;
      quote = "";
      if (tagName === "script" || tagName === "style") {
        skippingRawText = tagName;
      }
      continue;
    }

    text += char;
  }

  return text;
}

function decodeHtmlEntities(value) {
  return value
    .replace(/&#x([0-9a-f]+);/gi, (_, hex) => String.fromCodePoint(Number.parseInt(hex, 16)))
    .replace(/&#([0-9]+);/g, (_, decimal) => String.fromCodePoint(Number.parseInt(decimal, 10)))
    .replaceAll("&nbsp;", " ")
    .replaceAll("&amp;", "&")
    .replaceAll("&lt;", "<")
    .replaceAll("&gt;", ">")
    .replaceAll("&quot;", '"')
    .replaceAll("&#39;", "'")
    .replaceAll("&apos;", "'");
}

function trimEvidence(value) {
  let redacted = value.trim();

  for (const rule of evidenceRedactions) {
    redacted = redacted.replace(rule.pattern, rule.replacement);
  }

  if (redacted === value.trim()) {
    return "[redacted evidence]";
  }

  return redacted.slice(0, 120);
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function formatDistPath(dist, file) {
  return relative(dist, file).split(sep).join("/");
}
