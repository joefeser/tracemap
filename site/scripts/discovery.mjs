import { readFile, stat, writeFile, mkdir } from "node:fs/promises";
import { dirname, resolve, sep } from "node:path";

export const discoveryBaseUrl = "https://tracemap.tools";
export const discoverySharedPrinciple = "No public conclusion without evidence.";

const sourceTypes = new Set(["site-page", "repo-doc"]);
const hintCategories = ["start", "evidence", "limitations", "demo", "repo-doc", "roadmap", "use-case"];
const hintCategorySet = new Set(hintCategories);
const publicClaimLevels = new Set(["main", "demo", "concept", "planned", "dev-only", "hidden", "future"]);
const nonShippedClaimLevels = new Set(["concept", "planned", "dev-only", "hidden", "future"]);
const routeSections = {
  start: "Start Here",
  evidence: "Evidence And Proof",
  limitations: "Limitations",
  demo: "Demo",
  roadmap: "Limitations",
  "use-case": "Limitations"
};
const llmsSections = ["Start Here", "Evidence And Proof", "Limitations", "Demo", "Repository Docs", "Non-Claims"];
const deniedPhrases = [
  "/Users/",
  "C:\\",
  "SELECT *",
  "connection string",
  "ConnectionString",
  "Server=",
  "User Id=",
  "Password=",
  "facts.ndjson",
  "index.sqlite",
  "logs/analyzer.log",
  "analyzer.log",
  "raw SQL",
  "raw source snippets",
  "local output roots",
  "AI impact analysis",
  "embedding",
  "vector database",
  "prompt-based classification"
];
const nonClaimLines = [
  "No AI impact analysis, LLM calls, embeddings, vector databases, or prompt-based classification in the TraceMap scanner, reducer, or core product.",
  "No runtime behavior, production usage, deployment state, endpoint performance, release approval, release safety, or operational safety proof.",
  "No publication of private paths, raw source snippets, raw SQL, config values, secrets, facts.ndjson, index.sqlite, logs/analyzer.log, analyzer logs, or local output roots."
];

export async function readDiscoveryEntries(context) {
  const sourcePath = resolve(context.siteSource, "discovery.json");
  let raw;

  try {
    raw = await readFile(sourcePath, "utf8");
  } catch (error) {
    if (error?.code === "ENOENT") {
      throw new Error(`Missing discovery metadata file: src/_site/discovery.json`, { cause: error });
    }

    throw new Error(`Unable to read discovery metadata file: src/_site/discovery.json`, { cause: error });
  }

  try {
    return parseDiscoveryEntries(raw, "src/_site/discovery.json");
  } catch (error) {
    throw new Error(`Discovery metadata is not valid: ${error.message}`, { cause: error });
  }
}

export async function writeDiscoveryOutputs(context, entries) {
  const outputs = await createDiscoveryOutputs(entries, {
    dist: context.dist,
    resolveInternalPaths: true
  });

  await writeTextOutput(resolve(context.dist, "llms.txt"), outputs.llmsText);
  await writeTextOutput(resolve(context.dist, "docs-index.json"), outputs.docsIndexJson);
  await writeTextOutput(resolve(context.dist, "routes-index.json"), outputs.routesIndexJson);
}

export async function createDiscoveryOutputs(entries, options = {}) {
  const normalizedEntries = await normalizeDiscoveryEntries(entries, options);
  const docsEntries = sortByPublicKey(normalizedEntries.filter((entry) => entry.sourceType === "repo-doc"));
  const routeEntries = sortByPublicKey(normalizedEntries.filter((entry) => entry.sourceType === "site-page"));
  const llmsText = renderLlmsText({ docsEntries, routeEntries });
  const docsIndexJson = renderDiscoveryJson(docsEntries);
  const routesIndexJson = renderDiscoveryJson(routeEntries);

  validateDiscoveryOutputSafety({ docsIndexJson, llmsText, routesIndexJson });

  return {
    docsEntries,
    docsIndexJson,
    llmsText,
    routeEntries,
    routesIndexJson
  };
}

export function parseDiscoveryEntries(raw, label = "discovery metadata") {
  let parsed;

  try {
    parsed = JSON.parse(raw);
  } catch (error) {
    throw new Error(`${label} is not valid JSON: ${error.message}`);
  }

  if (!Array.isArray(parsed)) {
    throw new Error(`${label} must be an array.`);
  }

  return parsed;
}

export async function validateDiscoveryDist({ baseUrl = discoveryBaseUrl, dist, errors }) {
  const llmsPath = resolve(dist, "llms.txt");
  const docsPath = resolve(dist, "docs-index.json");
  const routesPath = resolve(dist, "routes-index.json");
  const privateSourcePath = resolve(dist, "discovery.json");

  await validateRequiredDiscoveryFile(llmsPath, "llms.txt", errors);
  await validateRequiredDiscoveryFile(docsPath, "docs-index.json", errors);
  await validateRequiredDiscoveryFile(routesPath, "routes-index.json", errors);

  if (await fileExists(privateSourcePath)) {
    errors.push("Generated output must not publish private discovery source: discovery.json");
  }

  if (!(await fileExists(llmsPath)) || !(await fileExists(docsPath)) || !(await fileExists(routesPath))) {
    return;
  }

  const llmsText = await readFile(llmsPath, "utf8");
  const docsIndexJson = await readFile(docsPath, "utf8");
  const routesIndexJson = await readFile(routesPath, "utf8");

  try {
    validateLlmsHeadings(llmsText);
    validateDiscoveryOutputSafety({ docsIndexJson, llmsText, routesIndexJson });
    const docsIndex = parseGeneratedIndex(docsIndexJson, "docs-index.json");
    const routesIndex = parseGeneratedIndex(routesIndexJson, "routes-index.json");

    await validateGeneratedEntries({
      baseUrl,
      dist,
      entries: [...docsIndex.entries, ...routesIndex.entries]
    });
  } catch (error) {
    errors.push(error.message);
  }
}

export function validateRobotsDiscoveryComment({ baseUrl = discoveryBaseUrl, errors, robots }) {
  const expected = `# LLM discovery: ${baseUrl}/llms.txt`;

  if (!robots.split(/\r?\n/).some((line) => line.trim() === expected)) {
    errors.push(`robots.txt must include "${expected}".`);
  }
}

export function validateDiscoveryNotInSitemap({ errors, sitemapUrls }) {
  for (const url of sitemapUrls) {
    if (/(?:\/llms\.txt|\/docs-index\.json|\/routes-index\.json)$/.test(url)) {
      errors.push(`Discovery file must not be listed in sitemap.xml: ${url}`);
    }
  }
}

export function splitLlmsSections(llmsText) {
  const sections = new Map();
  let current = null;
  let content = [];

  for (const line of llmsText.split(/\r?\n/)) {
    const heading = line.match(/^## (.+)$/);
    if (heading) {
      if (current) {
        sections.set(current, content.join("\n"));
      }

      current = heading[1];
      content = [];
      continue;
    }

    if (current) {
      content.push(line);
    }
  }

  if (current) {
    sections.set(current, content.join("\n"));
  }

  return sections;
}

async function normalizeDiscoveryEntries(entries, { dist, resolveInternalPaths = false } = {}) {
  if (!Array.isArray(entries)) {
    throw new Error("Discovery entries must be an array.");
  }

  const normalized = [];
  const seenKeys = new Set();

  for (const [index, entry] of entries.entries()) {
    validateEntryShape(entry, index);
    const key = publicKey(entry);

    if (seenKeys.has(key)) {
      throw new Error(`Discovery entry is duplicated: ${key}`);
    }

    seenKeys.add(key);
    validateSafeEntryText(entry, index);

    if (entry.sourceType === "site-page") {
      await validateInternalPath(entry.path, { dist, field: "path", index, resolveInternalPaths });
    } else {
      validateRepoDocUrl(entry.url, index);
    }

    const normalizedEntry = {
      title: entry.title.trim(),
      summary: entry.summary.trim(),
      publicClaimLevel: entry.publicClaimLevel.trim(),
      sourceType: entry.sourceType,
      hintCategory: entry.hintCategory,
      limitations: normalizeStringArray(entry.limitations),
      nonClaims: normalizeStringArray(entry.nonClaims)
    };

    if (entry.sourceType === "site-page") {
      normalizedEntry.path = normalizePublicPath(entry.path);
      normalizedEntry.url = `${discoveryBaseUrl}${normalizedEntry.path}`;
    } else {
      normalizedEntry.url = entry.url.trim();
    }

    if (Object.hasOwn(entry, "preferredProofPath")) {
      const preferredProofPath = normalizeOptionalPath(entry.preferredProofPath, index);
      await validatePreferredProofPath(preferredProofPath, { dist, index, resolveInternalPaths });
      normalizedEntry.preferredProofPath = preferredProofPath;
    }

    normalized.push(normalizedEntry);
  }

  return normalized;
}

function validateEntryShape(entry, index) {
  if (!isPlainObject(entry)) {
    throw new Error(`Discovery entry at index ${index} must be an object.`);
  }

  for (const field of ["title", "summary", "sourceType", "publicClaimLevel", "hintCategory"]) {
    if (typeof entry[field] !== "string" || entry[field].trim() === "") {
      throw new Error(`Discovery entry at index ${index} is missing required string field: ${field}`);
    }
  }

  if (!sourceTypes.has(entry.sourceType)) {
    throw new Error(`Discovery entry at index ${index} has invalid sourceType: ${entry.sourceType}`);
  }

  if (!hintCategorySet.has(entry.hintCategory)) {
    throw new Error(`Discovery entry at index ${index} has invalid hintCategory: ${entry.hintCategory}`);
  }

  if (!publicClaimLevels.has(entry.publicClaimLevel)) {
    throw new Error(`Discovery entry at index ${index} has invalid publicClaimLevel: ${entry.publicClaimLevel}`);
  }

  if (entry.sourceType === "site-page") {
    if (typeof entry.path !== "string" || entry.path.trim() === "") {
      throw new Error(`Discovery site-page entry at index ${index} is missing required string field: path`);
    }
  } else if (typeof entry.url !== "string" || entry.url.trim() === "") {
    throw new Error(`Discovery repo-doc entry at index ${index} is missing required string field: url`);
  }

  validateStringArray(entry.limitations, "limitations", index);
  validateStringArray(entry.nonClaims, "nonClaims", index);

  if (entry.limitations.length === 0 || entry.nonClaims.length === 0) {
    throw new Error(`Discovery entry at index ${index} must include limitations and nonClaims metadata.`);
  }
}

function validateStringArray(value, field, index) {
  if (!Array.isArray(value)) {
    throw new Error(`Discovery entry at index ${index} must include ${field} as an array.`);
  }

  for (const [valueIndex, item] of value.entries()) {
    if (typeof item !== "string" || item.trim() === "") {
      throw new Error(`Discovery entry at index ${index} ${field}[${valueIndex}] must be a non-empty string.`);
    }
  }
}

function validateSafeEntryText(entry, index) {
  for (const [field, value] of Object.entries(entry)) {
    if (field === "nonClaims") {
      continue;
    }

    if (typeof value === "string") {
      assertNoDeniedPhrase(value, `discovery entry ${index} field ${field}`);
      continue;
    }

    if (Array.isArray(value)) {
      for (const [valueIndex, item] of value.entries()) {
        if (typeof item === "string") {
          assertNoDeniedPhrase(item, `discovery entry ${index} field ${field}[${valueIndex}]`);
        }
      }
    }
  }

  if (nonShippedClaimLevels.has(entry.publicClaimLevel)) {
    const text = `${entry.title} ${entry.summary}`;
    if (/\b(?:available|shipped|released|deployed)\b/i.test(text)) {
      throw new Error(`Discovery entry at index ${index} uses shipped wording for ${entry.publicClaimLevel} content.`);
    }
  }
}

async function validateInternalPath(pathname, { dist, field, index, resolveInternalPaths }) {
  const normalizedPath = normalizePublicPath(pathname);

  if (!/^\/(?:[a-z0-9]+(?:-[a-z0-9]+)*\/)*$/.test(normalizedPath)) {
    throw new Error(`Discovery entry at index ${index} has invalid ${field}: ${normalizedPath}`);
  }

  if (resolveInternalPaths && !(await publicPathExists(dist, normalizedPath))) {
    throw new Error(`Discovery entry at index ${index} references missing public path: ${normalizedPath}`);
  }
}

async function validatePreferredProofPath(pathname, { dist, index, resolveInternalPaths }) {
  if (/^https?:\/\//.test(pathname)) {
    validateStablePublicUrl(pathname, index, "preferredProofPath");
    return;
  }

  if (!pathname.startsWith("/")) {
    throw new Error(`Discovery entry at index ${index} has invalid preferredProofPath: ${pathname}`);
  }

  if (resolveInternalPaths && !(await publicPathExists(dist, pathname))) {
    throw new Error(`Discovery entry at index ${index} references missing preferredProofPath: ${pathname}`);
  }
}

function validateRepoDocUrl(url, index) {
  let parsed;

  try {
    parsed = new URL(url);
  } catch {
    throw new Error(`Discovery entry at index ${index} has invalid url: ${url}`);
  }

  if (parsed.hostname !== "github.com") {
    throw new Error(`Discovery entry at index ${index} has non-public url: ${url}`);
  }

  validatePinnedRepositoryDocPath(parsed.pathname, url, index);
}

function validateStablePublicUrl(url, index, field) {
  let parsed;

  try {
    parsed = new URL(url);
  } catch {
    throw new Error(`Discovery entry at index ${index} has invalid ${field}: ${url}`);
  }

  if (parsed.hostname === "tracemap.tools") {
    return;
  }

  if (parsed.hostname !== "github.com") {
    throw new Error(`Discovery entry at index ${index} has non-public ${field}: ${url}`);
  }

  validatePinnedRepositoryDocPath(parsed.pathname, url, index);
}

function validatePinnedRepositoryDocPath(pathname, url, index) {
  if (!/^\/joefeser\/tracemap\/blob\/(?:main|v\d+\.\d+\.\d+)\/.+/.test(pathname)) {
    throw new Error(`Discovery entry at index ${index} must pin repository docs to main or a release tag: ${url}`);
  }
}

function normalizeOptionalPath(value, index) {
  if (typeof value !== "string") {
    throw new Error(`Discovery entry at index ${index} preferredProofPath must be a non-empty string when present.`);
  }

  const normalized = value.trim();
  if (normalized === "") {
    throw new Error(`Discovery entry at index ${index} preferredProofPath must be a non-empty string when present.`);
  }

  return normalized;
}

function sortByPublicKey(entries) {
  return [...entries].sort((left, right) => compareOrdinal(publicKey(left), publicKey(right)));
}

function sortByHint(entries) {
  return [...entries].sort((left, right) => {
    const hintDelta = hintCategories.indexOf(left.hintCategory) - hintCategories.indexOf(right.hintCategory);
    return hintDelta === 0 ? compareOrdinal(publicKey(left), publicKey(right)) : hintDelta;
  });
}

function publicKey(entry) {
  return entry.path ?? entry.url;
}

function compareOrdinal(left, right) {
  if (left < right) {
    return -1;
  }

  if (left > right) {
    return 1;
  }

  return 0;
}

function renderDiscoveryJson(entries) {
  return `${JSON.stringify(
    {
      schemaVersion: 1,
      sharedPrinciple: discoverySharedPrinciple,
      entries
    },
    null,
    2
  )}\n`;
}

function renderLlmsText({ docsEntries, routeEntries }) {
  const lines = [
    "# TraceMap",
    "",
    "> Deterministic static evidence for repository indexing and contract-change review. Discovery metadata routes readers to public-safe proof, limitations, and source documents; it does not infer conclusions.",
    "",
    discoverySharedPrinciple,
    ""
  ];
  const sections = new Map(llmsSections.map((section) => [section, []]));

  for (const entry of sortByHint(routeEntries)) {
    const section = routeSections[entry.hintCategory];
    if (!section) {
      continue;
    }

    sections.get(section).push(renderEntryBullet(entry));
  }

  for (const entry of sortByHint(docsEntries)) {
    sections.get("Repository Docs").push(renderEntryBullet(entry));
  }

  for (const line of collectNonClaims()) {
    sections.get("Non-Claims").push(`- ${line}`);
  }

  for (const section of llmsSections) {
    const items = sections.get(section);
    if (section !== "Non-Claims" && items.length === 0) {
      continue;
    }

    lines.push(`## ${section}`, "", ...items, "");
  }

  return `${lines.join("\n").replace(/\n{3,}/g, "\n\n").trimEnd()}\n`;
}

function renderEntryBullet(entry) {
  const url = entry.url ?? `${discoveryBaseUrl}${entry.path}`;
  const proof = entry.preferredProofPath ? ` Preferred proof path: ${absoluteDiscoveryUrl(entry.preferredProofPath)}.` : "";
  const label = nonShippedClaimLevels.has(entry.publicClaimLevel) ? `${entry.publicClaimLevel}: ` : "";
  return `- [${entry.title}](${url}) - Public claim level: ${entry.publicClaimLevel}. ${label}${entry.summary}${proof}`;
}

function collectNonClaims() {
  return [...nonClaimLines].sort(compareOrdinal);
}

function absoluteDiscoveryUrl(value) {
  return value.startsWith("/") ? `${discoveryBaseUrl}${value}` : value;
}

function parseGeneratedIndex(raw, label) {
  let parsed;

  try {
    parsed = JSON.parse(raw);
  } catch (error) {
    throw new Error(`${label} is not valid JSON: ${error.message}`);
  }

  if (!isPlainObject(parsed) || parsed.schemaVersion !== 1 || parsed.sharedPrinciple !== discoverySharedPrinciple) {
    throw new Error(`${label} has an invalid discovery index header.`);
  }

  if (!Array.isArray(parsed.entries)) {
    throw new Error(`${label} must include an entries array.`);
  }

  return parsed;
}

async function validateGeneratedEntries({ baseUrl, dist, entries }) {
  const normalizedEntries = await normalizeDiscoveryEntries(entries, {
    dist,
    resolveInternalPaths: true
  });

  const byKind = new Map([
    ["site-page", sortByPublicKey(normalizedEntries.filter((entry) => entry.sourceType === "site-page"))],
    ["repo-doc", sortByPublicKey(normalizedEntries.filter((entry) => entry.sourceType === "repo-doc"))]
  ]);

  for (const [sourceType, expected] of byKind) {
    const actual = normalizedEntries.filter((entry) => entry.sourceType === sourceType);
    if (JSON.stringify(actual.map(publicKey)) !== JSON.stringify(expected.map(publicKey))) {
      throw new Error(`Discovery ${sourceType} entries are not sorted deterministically.`);
    }
  }

  for (const entry of normalizedEntries) {
    if (entry.sourceType === "site-page") {
      const expectedUrl = `${baseUrl}${entry.path}`;
      if (entry.url !== expectedUrl) {
        throw new Error(`Discovery route URL must match its path: ${entry.path}`);
      }
    }
  }
}

function validateLlmsHeadings(llmsText) {
  if (!llmsText.includes(discoverySharedPrinciple)) {
    throw new Error("llms.txt must include the shared site principle.");
  }

  const headings = [...llmsText.matchAll(/^## (.+)$/gm)].map((match) => match[1]);
  const expectedPresent = llmsSections.filter((section) => section === "Non-Claims" || headings.includes(section));

  if (JSON.stringify(headings) !== JSON.stringify(expectedPresent)) {
    throw new Error(`llms.txt H2 sections are out of order: ${headings.join(", ")}`);
  }

  if (!headings.includes("Non-Claims")) {
    throw new Error("llms.txt must include a Non-Claims section.");
  }

  const evidenceIndex = headings.indexOf("Evidence And Proof");
  const limitationsIndex = headings.indexOf("Limitations");
  const demoIndex = headings.indexOf("Demo");
  if (demoIndex !== -1 && evidenceIndex !== -1 && evidenceIndex > demoIndex) {
    throw new Error("llms.txt must list evidence hints before demo hints.");
  }

  if (limitationsIndex !== -1 && demoIndex !== -1 && limitationsIndex > demoIndex) {
    throw new Error("llms.txt must list limitation hints before demo hints.");
  }
}

function validateDiscoveryOutputSafety({ docsIndexJson, llmsText, routesIndexJson }) {
  validateLlmsTextSafety(llmsText);
  validateJsonTextSafety(docsIndexJson, "docs-index.json");
  validateJsonTextSafety(routesIndexJson, "routes-index.json");
}

function validateLlmsTextSafety(llmsText) {
  const sections = splitLlmsSections(llmsText);
  const nonClaims = sections.get("Non-Claims") ?? "";

  for (const phrase of deniedPhrases) {
    const outside = llmsText.replace(nonClaims, "");
    if (outside.includes(phrase)) {
      throw new Error(`llms.txt contains denied phrase outside Non-Claims: ${phrase}`);
    }
  }
}

function validateJsonTextSafety(jsonText, label) {
  let parsed;

  try {
    parsed = JSON.parse(jsonText);
  } catch (error) {
    throw new Error(`${label} is not valid JSON: ${error.message}`);
  }

  walkJsonStrings(parsed, [], (value, path) => {
    for (const phrase of deniedPhrases) {
      if (!value.includes(phrase)) {
        continue;
      }

      if (isDirectNonClaimString(parsed, path)) {
        continue;
      }

      throw new Error(`${label} contains denied phrase outside nonClaims: ${phrase}`);
    }
  });
}

function assertNoDeniedPhrase(value, context) {
  for (const phrase of deniedPhrases) {
    if (value.includes(phrase)) {
      throw new Error(`${context} contains denied phrase outside nonClaims: ${phrase}`);
    }
  }
}

function walkJsonStrings(value, path, visit) {
  if (typeof value === "string") {
    visit(value, path);
    return;
  }

  if (Array.isArray(value)) {
    value.forEach((item, index) => walkJsonStrings(item, [...path, index], visit));
    return;
  }

  if (isPlainObject(value)) {
    for (const [key, item] of Object.entries(value)) {
      walkJsonStrings(item, [...path, key], visit);
    }
  }
}

function isDirectNonClaimString(root, path) {
  if (path.length < 2 || typeof path.at(-1) !== "number" || path.at(-2) !== "nonClaims") {
    return false;
  }

  const parentPath = path.slice(0, -1);
  let parent = root;
  for (const segment of parentPath) {
    parent = parent?.[segment];
  }

  return Array.isArray(parent) && typeof parent[path.at(-1)] === "string";
}

async function validateRequiredDiscoveryFile(path, label, errors) {
  if (!(await fileExists(path))) {
    errors.push(`Missing required generated discovery file: ${label}`);
  }
}

async function writeTextOutput(path, text) {
  await mkdir(dirname(path), { recursive: true });
  await writeFile(path, text, "utf8");
}

async function publicPathExists(dist, pathname) {
  const resolved = resolvePublicPath(dist, pathname);

  if (resolved && (await fileExists(resolved))) {
    return true;
  }

  if (!pathname.endsWith("/")) {
    const indexPath = resolvePublicPath(dist, `${pathname}/`);
    return indexPath ? fileExists(indexPath) : false;
  }

  return false;
}

function resolvePublicPath(dist, pathname) {
  let decoded;

  try {
    decoded = decodeURIComponent(pathname);
  } catch {
    return null;
  }

  if (!decoded.startsWith("/")) {
    return null;
  }

  const filePath = decoded.endsWith("/") ? resolve(dist, `.${decoded}`, "index.html") : resolve(dist, `.${decoded}`);
  const safeRoot = dist.endsWith(sep) ? dist : dist + sep;

  if (filePath !== dist && !filePath.startsWith(safeRoot)) {
    return null;
  }

  return filePath;
}

async function fileExists(path) {
  try {
    const info = await stat(path);
    return info.isFile();
  } catch {
    return false;
  }
}

function normalizeStringArray(value) {
  return value.map((item) => item.trim());
}

function normalizePublicPath(value) {
  const normalized = value.trim();
  return normalized.endsWith("/") ? normalized : `${normalized}/`;
}

function isPlainObject(value) {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
