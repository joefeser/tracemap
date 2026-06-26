import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const defaultRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
export const legacyDataSurfaceTargetPath = "legacy-data-surface/index.html";
export const legacyDataSurfaceRoute = "/legacy-data-surface/";

const requiredPhrases = [
  "Public claim level: concept",
  "No public conclusion without evidence",
  "design-time metadata",
  "data model metadata",
  "ORM/mapping clues",
  "SQL/query-facing references",
  "storage/persistence context",
  "limitations",
  "analysis gaps"
];

const requiredLinks = [
  "/legacy-dotnet/evidence/",
  "/legacy-evidence/",
  "/legacy-modernization/evidence-map/",
  "/legacy-validation/",
  "/proof-paths/",
  "/validation/",
  "/limitations/",
  "/outputs/",
  "/docs/"
];

const requiredHeaders = [
  "Evidence family",
  "Possible static evidence",
  "Evidence status",
  "Proof path requirement",
  "Limitation",
  "Owner follow-up",
  "Allowed wording",
  "Forbidden wording"
];

const expectedRows = new Map([
  ["design-time-metadata", "concept"],
  ["data-model-metadata", "concept"],
  ["orm-mapping-clues", "future"],
  ["sql-query-facing-references", "future"],
  ["storage-persistence-context", "concept"],
  ["analysis-gaps", "gap"]
]);

const allowedStatuses = new Set(["concept", "future", "demo", "hidden", "reduced", "partial", "gap", "unknown"]);

const requiredDiscovery = {
  path: legacyDataSurfaceRoute,
  publicClaimLevel: "concept",
  preferredProofPath: "/legacy-evidence/"
};

const privateDisclosureChecks = [
  {
    id: "local-absolute-path",
    pattern: /(?:^|[\s"'(=])(?:\/Users\/|\/home\/|\/tmp\/|\/var\/folders\/|\/private\/var\/|[A-Za-z]:[\\/])[^\s<>"')]+/gi
  },
  {
    id: "generated-output-root",
    pattern: /(?:^|[\s"'(=])(?:site\/)?(?:dist|output)\/[^\s<>"')]+|(?:^|[\s"'(=])\.tmp\/[^\s<>"')]+/gi
  },
  {
    id: "connection-string-value",
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
    id: "raw-artifact-category",
    pattern: /\b(?:raw source snippets?|raw SQL|raw config values?|connection strings?|database contents?|table dumps?|raw fact streams?|raw SQLite content|analyzer logs?|raw remotes?|local absolute paths?|generated scan directories|private sample names?|hidden validation details?|raw command output|private URLs?|credential-like values)\b/gi
  }
];

const overclaimPhrases = [
  "proves database behavior",
  "proves query behavior",
  "proves runtime SQL",
  "reads production data",
  "executes SQL",
  "connects to your database",
  "validates migration success",
  "proves schema compatibility",
  "complete data coverage",
  "runtime data lineage",
  "production data understanding",
  "AI-powered impact analysis",
  "LLM-powered migration analysis"
];

export async function validateLegacyDataSurface({ root = defaultRoot } = {}) {
  const target = resolve(root, "dist", legacyDataSurfaceTargetPath);
  let html;

  try {
    html = await readFile(target, "utf8");
  } catch (error) {
    if (error?.code === "ENOENT") {
      throw new Error(`Legacy data surface target is missing: ${legacyDataSurfaceTargetPath}`);
    }

    throw error;
  }

  const errors = validateLegacyDataSurfaceHtml(html, {
    label: legacyDataSurfaceTargetPath
  });

  await validateRouteMetadata({ errors, root });

  if (errors.length > 0) {
    throw new Error(`Legacy data surface validation failed:\n- ${errors.join("\n- ")}`);
  }

  return {
    rowCount: expectedRows.size,
    targetPath: legacyDataSurfaceTargetPath
  };
}

export function validateLegacyDataSurfaceHtml(html, { label = legacyDataSurfaceTargetPath } = {}) {
  const errors = [];
  const text = normalizeRenderedContent(decodeHtmlEntities(stripTags(html)));
  const mainText = normalizeRenderedContent(decodeHtmlEntities(stripTags(extractElement(html, "main") || html)));
  const raw = normalizeRenderedContent(decodeHtmlEntities(html));

  if (!/<title>Legacy Data Surface Evidence Story \| TraceMap<\/title>/i.test(html)) {
    errors.push(`${label} is missing the expected page title.`);
  }

  if (!hasTagWithAttributes(html, "link", { rel: "canonical", href: "https://tracemap.tools/legacy-data-surface/" })) {
    errors.push(`${label} is missing the expected canonical URL.`);
  }

  if (!hasTagWithAttributes(html, "meta", { property: "og:type", content: "website" })) {
    errors.push(`${label} is missing og:type website metadata.`);
  }

  for (const meta of ["og:title", "og:description", "og:url"]) {
    if (!hasTagWithAttributes(html, "meta", { property: meta }, ["content"])) {
      errors.push(`${label} is missing ${meta} metadata.`);
    }
  }

  for (const phrase of requiredPhrases) {
    if (!text.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(`${label} is missing required phrase: ${phrase}`);
    }
  }

  for (const href of requiredLinks) {
    if (!new RegExp(`href\\s*=\\s*["']${escapeRegExp(href)}["']`, "i").test(html)) {
      errors.push(`${label} is missing required public-safe link: ${href}`);
    }
  }

  validateMatrix(html, { errors, label });
  validateWordCount(mainText, { errors, label });
  validatePrivateDisclosures(raw, { errors, label });
  validateAffirmativeOverclaims(text, { errors, label });
  validateAffirmativeOverclaims(extractAttributeValues(html).join(". "), { errors, label, source: "metadata attributes" });

  return errors;
}

async function validateRouteMetadata({ errors, root }) {
  const pagesPath = resolve(root, "src", "_site", "pages.json");
  const discoveryPath = resolve(root, "src", "_site", "discovery.json");
  let pages;
  let discovery;

  try {
    pages = JSON.parse(await readFile(pagesPath, "utf8"));
  } catch (error) {
    errors.push(`Unable to read sitemap metadata for ${legacyDataSurfaceRoute}: ${error.message}`);
    pages = [];
  }

  try {
    discovery = JSON.parse(await readFile(discoveryPath, "utf8"));
  } catch (error) {
    errors.push(`Unable to read discovery metadata for ${legacyDataSurfaceRoute}: ${error.message}`);
    discovery = [];
  }

  const pageEntry = Array.isArray(pages) ? pages.find((entry) => entry?.path === legacyDataSurfaceRoute) : null;
  if (!pageEntry) {
    errors.push(`Sitemap metadata is missing ${legacyDataSurfaceRoute}.`);
  }

  const discoveryEntry = Array.isArray(discovery)
    ? discovery.find((entry) => entry?.path === requiredDiscovery.path)
    : null;
  if (!discoveryEntry) {
    errors.push(`Discovery metadata is missing ${legacyDataSurfaceRoute}.`);
    return;
  }

  for (const [field, expected] of Object.entries(requiredDiscovery)) {
    if (discoveryEntry[field] !== expected) {
      errors.push(`Discovery metadata ${field} is ${discoveryEntry[field]}; expected ${expected}.`);
    }
  }

  for (const field of ["title", "summary", "limitations", "nonClaims"]) {
    if (Array.isArray(discoveryEntry[field]) ? discoveryEntry[field].length === 0 : typeof discoveryEntry[field] !== "string") {
      errors.push(`Discovery metadata is missing non-empty ${field}.`);
    }
  }

  const discoveryText = normalizeRenderedContent(
    [discoveryEntry.title, discoveryEntry.summary, ...(discoveryEntry.limitations ?? []), ...(discoveryEntry.nonClaims ?? [])]
      .join(" ")
  );
  for (const phrase of ["legacy .NET evidence lane", "proof", "limitations"]) {
    if (!new RegExp(escapeRegExp(phrase), "i").test(discoveryText)) {
      errors.push(`Discovery metadata is missing required context: ${phrase}.`);
    }
  }
}

function validateMatrix(html, { errors, label }) {
  const matrix = extractMarkedTable(html, "data-legacy-data-surface-matrix");
  if (!matrix) {
    errors.push(`${label} is missing the legacy data surface evidence-status matrix.`);
    return;
  }

  const headerCells = extractCells(extractElement(matrix, "thead") || "", "th");
  const normalizedHeaders = headerCells.map((cell) => normalizeRenderedContent(decodeHtmlEntities(stripTags(cell))));
  const headerIndex = new Map(normalizedHeaders.map((header, index) => [header.toLowerCase(), index]));

  for (const header of requiredHeaders) {
    if (!headerIndex.has(header.toLowerCase())) {
      errors.push(`${label} matrix is missing required column: ${header}`);
    }
  }

  const body = extractElement(matrix, "tbody") || matrix;
  const rows = [...body.matchAll(/<tr\b[^>]*data-surface-row\s*=\s*["']([^"']+)["'][^>]*>[\s\S]*?<\/tr>/gi)];
  const seenStatuses = new Set();

  if (requiredHeaders.some((header) => !headerIndex.has(header.toLowerCase()))) {
    return;
  }

  for (const [rowId, expectedStatus] of expectedRows) {
    const row = rows.find((match) => match[1] === rowId);
    if (!row) {
      errors.push(`${label} is missing evidence-status matrix row: ${rowId}`);
      continue;
    }

    const rowHtml = row[0];
    const declaredStatus = getAttribute(rowHtml.match(/^<tr\b[^>]*>/i)?.[0] ?? "", "data-evidence-status");
    const cells = extractCells(rowHtml, "td").map((cell) => normalizeRenderedContent(decodeHtmlEntities(stripTags(cell))));

    if (cells.length !== requiredHeaders.length) {
      errors.push(`${label} row ${rowId} has ${cells.length} cells; expected ${requiredHeaders.length}.`);
      continue;
    }

    if (declaredStatus !== expectedStatus) {
      errors.push(`${label} row ${rowId} has data-evidence-status ${declaredStatus}; expected ${expectedStatus}.`);
    }

    const statusIndex = headerIndex.get("evidence status");
    const renderedStatus = extractStatus(cells[statusIndex]);
    seenStatuses.add(renderedStatus);
    if (!allowedStatuses.has(renderedStatus)) {
      errors.push(`${label} row ${rowId} uses unsupported evidence status: ${renderedStatus}.`);
    }

    if (renderedStatus !== expectedStatus) {
      errors.push(`${label} row ${rowId} renders evidence status ${renderedStatus}; expected ${expectedStatus}.`);
    }

    const limitation = cells[headerIndex.get("limitation")];
    if (!limitation || !/\b(?:does not|cannot|not |no |without|gap|reduced|unknown)\b/i.test(limitation)) {
      errors.push(`${label} row ${rowId} has an empty or unbounded limitation cell.`);
    }

    const proofPath = cells[headerIndex.get("proof path requirement")];
    if (!/\b(?:rule|evidence tier|coverage label|proof|gap|limitation)\b/i.test(proofPath)) {
      errors.push(`${label} row ${rowId} is missing a proof path requirement.`);
    }

    const followUp = cells[headerIndex.get("owner follow-up")];
    if (!/\bowner\b/i.test(followUp)) {
      errors.push(`${label} row ${rowId} is missing owner follow-up language.`);
    }

    const forbidden = cells[headerIndex.get("forbidden wording")];
    if (!/^Forbidden example:/i.test(forbidden)) {
      errors.push(`${label} row ${rowId} forbidden wording must be explicitly labeled as an example.`);
    }
  }

  for (const row of rows) {
    const status = getAttribute(row[0].match(/^<tr\b[^>]*>/i)?.[0] ?? "", "data-evidence-status");
    if (status && !allowedStatuses.has(status)) {
      errors.push(`${label} row ${row[1]} declares unsupported evidence status: ${status}.`);
    }
  }

  if (seenStatuses.size === 0) {
    errors.push(`${label} matrix has no readable evidence statuses.`);
  }
}

function validateWordCount(mainText, { errors, label }) {
  const words = mainText.split(/\s+/).filter((word) => /[A-Za-z0-9]/.test(word));
  if (words.length < 450 || words.length > 1800) {
    errors.push(`${label} visible main word count is ${words.length}; expected 450 to 1800 words.`);
  }
}

function validatePrivateDisclosures(rawHtml, { errors, label }) {
  const scanHtml = removeAllowedPrivateRegions(rawHtml);
  const scanText = normalizeRenderedContent(decodeHtmlEntities(stripTags(scanHtml)));
  const scanRaw = normalizeRenderedContent(decodeHtmlEntities(scanHtml));

  for (const check of privateDisclosureChecks) {
    const scanTarget = check.id === "raw-artifact-category" ? scanText : `${scanText} ${scanRaw}`;
    for (const match of scanTarget.matchAll(check.pattern)) {
      const evidence = match[0];
      const start = match.index ?? 0;
      const window = scanTarget.slice(Math.max(0, start - 90), Math.min(scanTarget.length, start + evidence.length + 90));
      if (check.id === "raw-artifact-category" && isAllowedCategoryBoundary(window)) {
        continue;
      }

      errors.push(`${label} contains forbidden private/raw disclosure (${check.id}): ${redactEvidence(check.id)}`);
      break;
    }
  }
}

function validateAffirmativeOverclaims(text, { errors, label, source = "rendered text" }) {
  const sentences = splitSentences(text);
  for (const sentence of sentences) {
    if (/\b(?:Forbidden example|does not|do not|No |not |without|cannot|must not)\b/i.test(sentence)) {
      continue;
    }

    if (!/\b(?:TraceMap|this page|the page|the tool|tool)\b/i.test(sentence)) {
      continue;
    }

    for (const phrase of overclaimPhrases) {
      if (new RegExp(`\\b${escapeRegExp(phrase)}\\b`, "i").test(sentence)) {
        errors.push(`${label} contains affirmative overclaim in ${source} with TraceMap/page/tool subject: ${phrase}`);
      }
    }
  }
}

function removeAllowedPrivateRegions(html) {
  let reduced = html;
  reduced = reduced.replace(/<ul\b[^>]*data-non-claim-region\b[^>]*>[\s\S]*?<\/ul>/gi, " ");
  reduced = reduced.replace(/<td\b[^>]*>\s*Forbidden example:[\s\S]*?<\/td>/gi, " ");
  return reduced;
}

function isAllowedCategoryBoundary(window) {
  return /\b(?:without|must not|does not|do not|No |not publish|forbidden|limitation|non-claims?)\b/i.test(window);
}

function extractMarkedTable(html, marker) {
  const match = html.match(new RegExp(`<table\\b[^>]*${escapeRegExp(marker)}[^>]*>[\\s\\S]*?<\\/table>`, "i"));
  return match?.[0] ?? null;
}

function extractElement(html, tagName) {
  const match = html.match(new RegExp(`<${tagName}\\b[^>]*>[\\s\\S]*?<\\/${tagName}>`, "i"));
  return match?.[0] ?? null;
}

function extractCells(html, tagName) {
  return [...html.matchAll(new RegExp(`<${tagName}\\b[^>]*>[\\s\\S]*?<\\/${tagName}>`, "gi"))].map((match) => match[0]);
}

function getAttribute(tag, name) {
  const match = tag.match(new RegExp(`\\b${name}\\s*=\\s*["']([^"']*)["']`, "i"));
  return match ? decodeHtmlEntities(match[1]) : null;
}

function hasTagWithAttributes(html, tagName, expectedAttributes, requiredAttributes = []) {
  for (const match of html.matchAll(new RegExp(`<${tagName}\\b[^>]*>`, "gi"))) {
    const tag = match[0];
    let matches = true;

    for (const [name, expected] of Object.entries(expectedAttributes)) {
      if (getAttribute(tag, name) !== expected) {
        matches = false;
        break;
      }
    }

    if (!matches) {
      continue;
    }

    if (requiredAttributes.every((name) => {
      const value = getAttribute(tag, name);
      return typeof value === "string" && value.trim() !== "";
    })) {
      return true;
    }
  }

  return false;
}

function extractAttributeValues(html) {
  return [...html.matchAll(/\b(?:content|title|aria-label|alt)\s*=\s*(["'])(.*?)\1/gis)]
    .map((match) => decodeHtmlEntities(match[2]))
    .filter((value) => value.trim() !== "");
}

function extractStatus(value) {
  const match = value.match(/\b(?:concept|future|demo|hidden|reduced|partial|gap|unknown)\b/i);
  return match ? match[0].toLowerCase() : value.toLowerCase();
}

function splitSentences(text) {
  return normalizeRenderedContent(text)
    .split(/(?<=[.!?])\s+/)
    .map((sentence) => sentence.trim())
    .filter(Boolean);
}

function normalizeRenderedContent(value) {
  return value
    .normalize("NFKC")
    .replace(/\p{Cf}/gu, "")
    .replace(/\s+/g, " ")
    .trim();
}

function stripTags(html) {
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
        text += " ";
        continue;
      }

      afterEquals = char === "=";
      continue;
    }

    if (char === "<") {
      insideTag = true;
      text += " ";
      continue;
    }

    text += char;
  }

  return text;
}

function decodeHtmlEntities(value) {
  return value
    .replace(/&#x([0-9a-f]+);/gi, (_, hex) => String.fromCodePoint(Number.parseInt(hex, 16)))
    .replace(/&#(\d+);/g, (_, decimal) => String.fromCodePoint(Number.parseInt(decimal, 10)))
    .replace(/&nbsp;/g, " ")
    .replace(/&amp;/g, "&")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'");
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function redactEvidence(id) {
  return `[redacted ${id}]`;
}
