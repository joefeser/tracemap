import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const defaultRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
export const legacyDotnetEvidenceLaneTargetPath = "legacy-dotnet/evidence/index.html";

const requiredAnchors = [
  "overview",
  "branch-boundary",
  "surface-families",
  "evidence-status-matrix",
  "modernization-review",
  "proof-paths",
  "limitations",
  "non-claims"
];

const expectedRows = new Map([
  ["status-vocabulary", { category: "general-model", status: "future" }],
  ["evidence-tier-model", { category: "general-model", status: "future" }],
  ["reduced-coverage", { category: "general-model", status: "future" }],
  ["wcf", { category: "legacy-surface", status: "hidden" }],
  ["asmx-soap", { category: "legacy-surface", status: "hidden" }],
  ["dotnet-remoting", { category: "legacy-surface", status: "hidden" }],
  ["webforms", { category: "legacy-surface", status: "hidden" }],
  ["winforms", { category: "legacy-surface", status: "hidden" }],
  ["legacy-data-metadata", { category: "legacy-surface", status: "hidden" }],
  ["toolchain-diagnostics", { category: "legacy-surface", status: "hidden" }],
  ["modernization-review", { category: "review-use", status: "future" }]
]);

const requiredPhrases = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "repository snapshots and checked-in artifacts",
  "not runtime traffic, production telemetry, live system inspection, or migration execution",
  "rule ID",
  "evidence tier",
  "coverage label",
  "line span",
  "commit SHA",
  "extractor version",
  "Failed build or failed project load means reduced coverage, not a clean repository",
  "Syntax fallback is useful evidence, not compiler-resolved semantic proof",
  "General evidence-model rows are separated from legacy-surface rows"
];

const requiredStatuses = ["shipped", "demo", "dev", "future", "hidden"];

const requiredLinks = [
  "/legacy-evidence/",
  "/legacy-modernization/evidence-map/",
  "/legacy-validation/",
  "/capabilities/",
  "/limitations/",
  "/validation/",
  "/proof-paths/",
  "/manager-packet/",
  "/roadmap/",
  "/review-claim-checklist/"
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
  /\bshipped (?:WCF|ASMX|SOAP|Remoting|WinForms|WebForms|legacy data|toolchain|project diagnostics)\b/i,
  /\bdemo-backed (?:WCF|ASMX|SOAP|Remoting|WinForms|WebForms|legacy data|toolchain|project diagnostics)\b/i,
  /\bTraceMap (?:approves|certifies) (?:the )?(?:modernization|release|migration)\b/i
];

export async function validateLegacyDotnetEvidenceLane({ root = defaultRoot } = {}) {
  const target = resolve(root, "dist", legacyDotnetEvidenceLaneTargetPath);
  let html;

  try {
    html = await readFile(target, "utf8");
  } catch (error) {
    if (error?.code === "ENOENT") {
      throw new Error(`Legacy .NET evidence lane target is missing: ${legacyDotnetEvidenceLaneTargetPath}`);
    }

    throw error;
  }

  const errors = validateLegacyDotnetEvidenceLaneHtml(html, {
    label: legacyDotnetEvidenceLaneTargetPath
  });

  if (errors.length > 0) {
    throw new Error(`Legacy .NET evidence lane validation failed:\n- ${errors.join("\n- ")}`);
  }

  return {
    rowCount: expectedRows.size,
    targetPath: legacyDotnetEvidenceLaneTargetPath
  };
}

export function validateLegacyDotnetEvidenceLaneHtml(html, { label = legacyDotnetEvidenceLaneTargetPath } = {}) {
  const errors = [];
  const text = normalizeRenderedContent(decodeHtmlEntities(stripTags(html)));
  const tightText = normalizeRenderedContent(decodeHtmlEntities(stripTagsTight(html)));
  const raw = normalizeRenderedContent(decodeHtmlEntities(html));

  if (!/<title>Legacy \.NET Evidence Lane \| TraceMap<\/title>/i.test(html)) {
    errors.push(`${label} is missing the expected page title.`);
  }

  for (const phrase of requiredPhrases) {
    if (!text.includes(phrase)) {
      errors.push(`${label} is missing required phrase: ${phrase}`);
    }
  }

  for (const status of requiredStatuses) {
    if (!new RegExp(`\\b${escapeRegExp(status)}\\b`, "i").test(text)) {
      errors.push(`${label} is missing required status vocabulary: ${status}`);
    }
  }

  for (const anchor of requiredAnchors) {
    if (!new RegExp(`\\bid=["']${escapeRegExp(anchor)}["']`, "i").test(html)) {
      errors.push(`${label} is missing required section anchor: ${anchor}`);
    }
  }

  if (!/\bdata-legacy-dotnet-evidence-lane\b/.test(html)) {
    errors.push(`${label} is missing the legacy .NET evidence lane table marker.`);
  }

  for (const [row, expectation] of expectedRows) {
    const rowMatch = html.match(new RegExp(`<tr\\b[^>]*data-lane-row=["']${escapeRegExp(row)}["'][^>]*>`, "i"));
    if (!rowMatch) {
      errors.push(`${label} is missing evidence-lane row: ${row}`);
      continue;
    }

    const tag = rowMatch[0];
    const category = getAttribute(tag, "data-row-category");
    const status = getAttribute(tag, "data-public-status");
    const rowHtml = sliceRowHtml(html, rowMatch.index ?? 0);
    const rowText = normalizeRenderedContent(decodeHtmlEntities(stripTags(rowHtml)));
    const cellCount = (rowHtml.match(/<td\b/gi) ?? []).length;

    if (category !== expectation.category) {
      errors.push(`${label} row ${row} has data-row-category ${category}; expected ${expectation.category}.`);
    }

    if (status !== expectation.status) {
      errors.push(`${label} row ${row} has data-public-status ${status}; expected ${expectation.status}.`);
    }

    if (cellCount !== 8) {
      errors.push(`${label} row ${row} has ${cellCount} data cells; expected 8 matrix fields.`);
    }

    if (!/\bproof\b/i.test(rowText)) {
      errors.push(`${label} row ${row} is missing proof-path requirement text.`);
    }

    if (!/\b(?:limitation|No |not |cannot|do not|does not|without)\b/i.test(rowText)) {
      errors.push(`${label} row ${row} is missing adjacent limitation text.`);
    }

    if (expectation.status === "hidden") {
      if (!/\bhidden\b/i.test(rowText)) {
        errors.push(`${label} row ${row} is missing visible hidden labeling.`);
      }

      if (/\b(?:shipped|demo-backed|dev-only|main-backed)\b/i.test(rowText)) {
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
  if (end === -1) {
    return rest;
  }

  const close = rest.slice(end).match(/^<\/tr\s*>/i);
  return rest.slice(0, end + close[0].length);
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

      afterEquals = char === "=";
      continue;
    }

    if (char === "<") {
      insideTag = true;
      quote = "";
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
    .replace(/&#39;|&apos;/gi, "'");
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function redactEvidence(id) {
  return `[redacted ${id}]`;
}
