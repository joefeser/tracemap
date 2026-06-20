import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const defaultRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
export const legacyModernizationEvidenceMapTargetPath = "legacy-modernization/evidence-map/index.html";

const requiredAnchors = [
  "reader-questions",
  "evidence-map",
  "coverage-gaps",
  "hidden-material",
  "proof-paths",
  "non-claims"
];

const expectedRows = new Map([
  ["old-frameworks-toolchains", { category: "general-model", status: "concept" }],
  ["project-load-build-gaps", { category: "general-model", status: "concept" }],
  ["syntax-fallback", { category: "general-model", status: "concept" }],
  ["config-project-metadata", { category: "general-model", status: "concept" }],
  ["wcf-service-references", { category: "legacy-surface-detection", status: "hidden" }],
  ["wcf-metadata", { category: "legacy-surface-detection", status: "hidden" }],
  ["asmx-soap-services", { category: "legacy-surface-detection", status: "hidden" }],
  ["remoting", { category: "legacy-surface-detection", status: "hidden" }],
  ["winforms-navigation-events", { category: "legacy-surface-detection", status: "hidden" }],
  ["webforms-event-route-navigation", { category: "legacy-surface-detection", status: "hidden" }],
  ["legacy-data-metadata", { category: "legacy-surface-detection", status: "hidden" }],
  ["build-environment-diagnostics", { category: "legacy-surface-detection", status: "hidden" }],
  ["flow-composition", { category: "legacy-surface-detection", status: "hidden" }]
]);

const requiredPhrases = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "repository snapshots and checked-in artifacts",
  "Named hidden surface family only",
  "Failed build or project load is reduced coverage, never a clean repository result",
  "This row does not assert service binding detection, service-reference detection, endpoint extraction, or connection-value extraction"
];

const requiredLinks = [
  "/legacy-evidence/",
  "/legacy-validation/",
  "/capabilities/",
  "/limitations/",
  "/validation/",
  "/proof-paths/",
  "/manager-packet/",
  "/roadmap/",
  "/review-claim-checklist/",
  "/adoption/"
];

const hardLeakChecks = [
  {
    id: "local-absolute-path",
    pattern: /(?:^|[\s"'(=])(?:\/Users\/|\/home\/|\/tmp\/|\/var\/folders\/|\/private\/var\/|[A-Za-z]:[\\/])[^\s<>"')]+/gi
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

const forbiddenSupportClaims = [
  /\bimpacted\b/i,
  /\bTraceMap (?:proves|validates|guarantees) (?:a )?migration\b/i,
  /\bTraceMap (?:proves|validates|guarantees) runtime behavior\b/i,
  /\bTraceMap (?:proves|validates|guarantees) production (?:traffic|telemetry|usage)\b/i,
  /\bTraceMap replaces (?:architects|reviewers|tests|telemetry|migration planning|human judgment)\b/i,
  /\bshipped (?:WCF|ASMX|SOAP|Remoting|WinForms|WebForms|legacy data|build environment|flow composition)\b/i,
  /\bdemo-backed (?:WCF|ASMX|SOAP|Remoting|WinForms|WebForms|legacy data|build environment|flow composition)\b/i
];

export async function validateLegacyModernizationEvidenceMap({ root = defaultRoot } = {}) {
  const target = resolve(root, "dist", legacyModernizationEvidenceMapTargetPath);
  let html;

  try {
    html = await readFile(target, "utf8");
  } catch (error) {
    if (error?.code === "ENOENT") {
      throw new Error(`Legacy modernization evidence map target is missing: ${legacyModernizationEvidenceMapTargetPath}`);
    }

    throw error;
  }

  const errors = validateLegacyModernizationEvidenceMapHtml(html, {
    label: legacyModernizationEvidenceMapTargetPath
  });

  if (errors.length > 0) {
    throw new Error(`Legacy modernization evidence map validation failed:\n- ${errors.join("\n- ")}`);
  }

  return {
    rowCount: expectedRows.size,
    targetPath: legacyModernizationEvidenceMapTargetPath
  };
}

export function validateLegacyModernizationEvidenceMapHtml(
  html,
  { label = legacyModernizationEvidenceMapTargetPath } = {}
) {
  const errors = [];
  const text = normalizeRenderedContent(decodeHtmlEntities(stripTags(html)));
  const tightText = normalizeRenderedContent(decodeHtmlEntities(stripTagsTight(html)));
  const raw = normalizeRenderedContent(decodeHtmlEntities(html));

  if (!/<title>Legacy Modernization Evidence Map \| TraceMap<\/title>/i.test(html)) {
    errors.push(`${label} is missing the expected page title.`);
  }

  for (const phrase of requiredPhrases) {
    if (!text.includes(phrase)) {
      errors.push(`${label} is missing required phrase: ${phrase}`);
    }
  }

  for (const anchor of requiredAnchors) {
    if (!new RegExp(`\\bid=["']${escapeRegExp(anchor)}["']`, "i").test(html)) {
      errors.push(`${label} is missing required section anchor: ${anchor}`);
    }
  }

  if (!/\bdata-legacy-modernization-evidence-map\b/.test(html)) {
    errors.push(`${label} is missing the evidence map table marker.`);
  }

  for (const [row, expectation] of expectedRows) {
    const rowMatch = html.match(new RegExp(`<tr\\b[^>]*data-map-row=["']${escapeRegExp(row)}["'][^>]*>`, "i"));
    if (!rowMatch) {
      errors.push(`${label} is missing evidence-map row: ${row}`);
      continue;
    }

    const tag = rowMatch[0];
    const category = getAttribute(tag, "data-row-category");
    const status = getAttribute(tag, "data-public-status");

    if (category !== expectation.category) {
      errors.push(`${label} row ${row} has data-row-category ${category}; expected ${expectation.category}.`);
    }

    if (status !== expectation.status) {
      errors.push(`${label} row ${row} has data-public-status ${status}; expected ${expectation.status}.`);
    }

    if (expectation.status === "hidden") {
      const rowHtml = sliceRowHtml(html, rowMatch.index ?? 0);
      const rowText = normalizeRenderedContent(decodeHtmlEntities(stripTags(rowHtml)));
      if (!/\bhidden\b/i.test(rowText)) {
        errors.push(`${label} row ${row} is missing visible hidden labeling.`);
      }

      if (/\b(?:main|shipped|demo-backed|dev-only)\b/i.test(rowText)) {
        errors.push(`${label} row ${row} uses a public support label instead of hidden labeling.`);
      }
    }
  }

  for (const href of requiredLinks) {
    if (!new RegExp(`href=["']${escapeRegExp(href)}["']`, "i").test(html)) {
      errors.push(`${label} is missing required public-safe proof link: ${href}`);
    }
  }

  for (const check of hardLeakChecks) {
    const match = raw.match(check.pattern)?.[0] ?? text.match(check.pattern)?.[0] ?? tightText.match(check.pattern)?.[0];
    if (match) {
      errors.push(`${label} contains forbidden ${check.id}: ${redactEvidence(check.id)}`);
    }
  }

  for (const pattern of forbiddenSupportClaims) {
    if (pattern.test(text)) {
      errors.push(`${label} contains forbidden unsupported support wording: ${pattern}`);
    }
  }

  return errors;
}

function getAttribute(tag, name) {
  const match = tag.match(new RegExp(`\\b${name}=["']([^"']*)["']`, "i"));
  return match ? decodeHtmlEntities(match[1]) : null;
}

function sliceRowHtml(html, rowStart) {
  const rest = html.slice(rowStart);
  const end = rest.search(/<\/tr\s*>/i);
  return end === -1 ? rest : rest.slice(0, end + rest.slice(end).match(/^<\/tr\s*>/i)[0].length);
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
  let afterEquals = false;

  for (const char of html) {
    if (insideTag) {
      if (quote) {
        if (char === quote) {
          quote = "";
        }
        continue;
      }

      if ((char === '"' || char === "'") && afterEquals) {
        quote = char;
        afterEquals = false;
        continue;
      }

      if (char === ">") {
        insideTag = false;
        afterEquals = false;
        text += separator;
        continue;
      }

      if (char === "=") {
        afterEquals = true;
        continue;
      }

      if (!/\s/.test(char)) {
        afterEquals = false;
      }

      continue;
    }

    if (char === "<") {
      insideTag = true;
      afterEquals = false;
      text += separator;
      continue;
    }

    text += char;
  }

  return text;
}

function decodeHtmlEntities(value) {
  return value
    .replace(/&nbsp;/gi, " ")
    .replace(/&amp;/gi, "&")
    .replace(/&lt;/gi, "<")
    .replace(/&gt;/gi, ">")
    .replace(/&quot;/gi, '"')
    .replace(/&apos;/gi, "'")
    .replace(/&#39;/g, "'")
    .replace(/&#x([0-9a-f]+);/gi, (_, hex) => String.fromCodePoint(Number.parseInt(hex, 16)))
    .replace(/&#(\d+);/g, (_, decimal) => String.fromCodePoint(Number.parseInt(decimal, 10)));
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function redactEvidence(id) {
  return `[redacted ${id}]`;
}
