import { mkdir, readdir, readFile, stat, writeFile } from "node:fs/promises";
import { dirname, isAbsolute, relative, resolve, sep } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const defaultSiteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const supportedSummaryVersions = new Set(["1.0"]);

export const defaultFixturePath = resolve(defaultSiteRoot, "src", "_data", "demo-public-summary.json");

export const knownSectionIds = new Map([
  ["toolchains", "toolchains"],
  ["python", "python"],
  ["jvm", "jvm"],
  ["build", "build"],
  ["sample-scans", "sample-scans"],
  ["combine-and-dependency-report", "combine-and-dependency-report"],
  ["paths-and-reverse", "paths-and-reverse"],
  ["portfolio", "portfolio"],
  ["diff", "diff"],
  ["impact", "impact"],
  ["release-review", "release-review"]
]);

const reasonRequiredStatuses = new Set(["deferred", "unavailable", "failed"]);
const rawArtifactFamilies = new Map([["scans", "scan-reports"]]);
const rejectedArtifactFamilies = new Set(["combined", "logs"]);
const rejectedArtifactNames = new Set([
  "facts.ndjson",
  "index.sqlite",
  "scan-manifest.json",
  "analyzer.log"
]);

const unsafeValueChecks = [
  ["local-absolute-path", /(?<![:/])\/(?:Users|home|private\/tmp|tmp|var\/folders|workspace|workspaces|github\/workspace|runner\/work|__w|mnt|opt\/hostedtoolcache|builds)\/[^\s`"'<>|)]+/i],
  ["windows-absolute-path", /(?:^|[\s"'`])(?:[A-Za-z]:\\|\\\\)[^\s"'`<>|)]+/i],
  ["file-url", /\bfile:\/\//i],
  ["raw-repository-remote", /\b(?:git@|https?:\/\/[^/\s]+\/[^/\s]+\/[^/\s]+\.git\b)/i],
  ["git-path", /(?:^|[\/\\])\.git(?:[\/\\]|$)/i],
  ["url-credential", /https?:\/\/[^/\s:@]+:[^/\s@]+@/i],
  ["connection-string", /\b(?:Password|Pwd|User Id|AccountKey|SharedAccessKey|ConnectionString)\s*=/i],
  ["secret-looking-value", /\b(?:token|secret|password|apikey|api_key|credential)\b\s*[:=]\s*["']?[A-Za-z0-9_./+=-]{8,}/i],
  ["sql-sentinel", /\b(?:TRACEMAP_SQL_SENTINEL|select\s+\*\s+from\s+private)\b/i]
];
const publicOutputContentChecks = unsafeValueChecks;

export async function refreshDemoSummary({ demoRoot, fixturePath = defaultFixturePath } = {}) {
  if (!demoRoot) {
    throw new Error("refresh-demo-summary requires an explicit public demo output root.");
  }

  const root = resolve(demoRoot);
  await assertDirectory(root, "public demo output root");
  await assertFile(resolve(root, "demo-summary.md"), "demo-summary.md");
  const summaryPath = resolve(root, "demo-summary.json");
  await assertFile(summaryPath, "demo-summary.json");

  const publicFiles = await validatePublicDemoOutput(root);

  const summary = await readJsonFile(summaryPath, "demo-summary.json");
  const fixture = createDemoSummaryFixture(summary);
  const fixtureErrors = validateDemoSummaryFixture(fixture);
  if (fixtureErrors.length > 0) {
    throw new Error(`generated demo summary fixture is invalid:\n- ${fixtureErrors.join("\n- ")}`);
  }
  assertFixtureArtifactsExist(fixture, publicFiles, root);

  await mkdir(dirname(fixturePath), { recursive: true });
  await writeFile(fixturePath, `${stableStringify(fixture)}\n`, "utf8");
  return fixture;
}

export function createDemoSummaryFixture(summary) {
  validateSummaryInput(summary);

  return {
    version: "1.0",
    publicClaimLevel: "demo",
    source: {
      generator: "scripts/demo-public.sh",
      demoSummary: {
        version: summary.version,
        outputRootHash: summary.outputRootHash
      }
    },
    sections: summary.sections.map((section) => scrubSection(section))
  };
}

export async function validatePublicDemoOutput(root) {
  const files = await collectPublicFiles(root);
  if (files.length === 0) {
    throw new Error("public demo output contains no approved public-safe files to inspect.");
  }

  const failures = [];
  const rootChecks = localAbsolutePathChecks(root);

  for (const file of files) {
    const text = await readFile(file, "utf8");
    const label = relative(root, file).split(sep).join("/");
    for (const [category, pattern] of [...rootChecks, ...publicOutputContentChecks]) {
      if (pattern.test(text)) {
        failures.push(`${label} (${category})`);
      }
    }

    if (label === "portfolio-manifest.json") {
      try {
        validatePortfolioManifest(JSON.parse(text), label);
      } catch (error) {
        throw new Error(`Failed to parse or validate ${label}: ${error.message}`);
      }
    }
  }

  if (failures.length > 0) {
    throw new Error(`public demo output failed safety checks:\n- ${failures.sort().join("\n- ")}`);
  }

  return files;
}

export async function collectPublicFiles(root) {
  const files = [];
  await collectPublicFilesInto(resolve(root), resolve(root), files);
  return files.sort();
}

export function validateDemoSummaryFixture(fixture) {
  const errors = [];

  try {
    validateFixtureShape(fixture);
    assertSafeFixtureValues(fixture);
  } catch (error) {
    errors.push(error.message);
  }

  return errors;
}

function validateSummaryInput(summary) {
  if (!isPlainObject(summary)) {
    throw new Error("demo-summary.json must contain a JSON object.");
  }

  if (!supportedSummaryVersions.has(summary.version)) {
    throw new Error(`Unsupported demo-summary.json version: ${summary.version ?? "<missing>"}`);
  }

  if (!/^path-hash:[0-9a-f]{24}$/i.test(summary.outputRootHash ?? "")) {
    throw new Error("demo-summary.json must include a path-hash outputRootHash.");
  }

  if (!Array.isArray(summary.sections) || summary.sections.length === 0) {
    throw new Error("demo-summary.json sections must be a non-empty array.");
  }

  const names = new Set();
  for (const [index, section] of summary.sections.entries()) {
    if (!isPlainObject(section)) {
      throw new Error(`demo-summary.json section at index ${index} must be an object.`);
    }

    if (typeof section.name !== "string" || section.name.trim() === "") {
      throw new Error(`demo-summary.json section at index ${index} is missing name.`);
    }

    if (!knownSectionIds.has(section.name)) {
      throw new Error(`Unknown demo-summary.json section name: ${section.name}`);
    }

    if (names.has(section.name)) {
      throw new Error(`Duplicate demo-summary.json section name: ${section.name}`);
    }
    names.add(section.name);

    for (const field of ["status", "classification", "evidenceTier", "reportCoverage"]) {
      if (typeof section[field] !== "string" || section[field].trim() === "") {
        throw new Error(`Section ${section.name} is missing ${field}.`);
      }
    }

    if (!Array.isArray(section.ruleIds) || section.ruleIds.length === 0 || !section.ruleIds.every(nonEmptyString)) {
      throw new Error(`Section ${section.name} is missing ruleIds.`);
    }

    if (reasonRequiredStatuses.has(section.status) && !nonEmptyString(section.reason)) {
      throw new Error(`Section ${section.name} must include a reason for status ${section.status}.`);
    }

    if (!Array.isArray(section.artifactPaths)) {
      throw new Error(`Section ${section.name} artifactPaths must be an array.`);
    }

    if (!isPlainObject(section.counts)) {
      throw new Error(`Section ${section.name} counts must be an object.`);
    }
  }
}

function scrubSection(section) {
  const artifacts = [];
  const localOnlyArtifactFamilies = new Set();

  for (const artifact of section.artifactPaths) {
    const scrubbed = scrubArtifactPath(artifact, section.name);
    if (scrubbed.kind === "public") {
      artifacts.push(scrubbed.path);
    } else {
      localOnlyArtifactFamilies.add(scrubbed.family);
    }
  }

  const result = {
    id: knownSectionIds.get(section.name),
    name: section.name,
    status: section.status,
    classification: section.classification,
    evidenceTier: section.evidenceTier,
    ruleIds: [...section.ruleIds],
    coverage: section.reportCoverage,
    counts: stableCounts(section.counts),
    reason: section.reason ?? "",
    artifacts: artifacts.sort()
  };

  if (localOnlyArtifactFamilies.size > 0) {
    result.localOnlyArtifactFamilies = [...localOnlyArtifactFamilies].sort();
  }

  return result;
}

function scrubArtifactPath(value, sectionName) {
  if (typeof value !== "string" || value.trim() === "") {
    throw new Error(`Section ${sectionName} has an empty artifact path.`);
  }

  const path = normalizeRelativePath(value, `Section ${sectionName} artifact path`);
  const [family] = path.split("/");
  const basename = path.split("/").at(-1);

  if (rawArtifactFamilies.has(family)) {
    return { kind: "local-only", family: rawArtifactFamilies.get(family) };
  }

  if (rejectedArtifactFamilies.has(family) || rejectedArtifactNames.has(basename)) {
    throw new Error(`Section ${sectionName} references raw artifact family: ${path}`);
  }

  if (path === "demo-summary.md" || path === "demo-summary.json" || path === "portfolio-manifest.json") {
    return { kind: "public", path };
  }

  if (/^reports\/.+\.(?:md|json)$/i.test(path)) {
    return { kind: "public", path };
  }

  throw new Error(`Section ${sectionName} references an unknown artifact family: ${path}`);
}

function stableCounts(counts) {
  const result = {};
  for (const key of Object.keys(counts).sort()) {
    const value = counts[key];
    if (typeof value !== "number" || !Number.isFinite(value)) {
      throw new Error(`Count ${key} must be a finite number.`);
    }
    result[key] = value;
  }
  return result;
}

function validateFixtureShape(fixture) {
  if (!isPlainObject(fixture)) {
    throw new Error("demo summary fixture must be a JSON object.");
  }

  if (fixture.version !== "1.0") {
    throw new Error(`demo summary fixture version must be 1.0: ${fixture.version ?? "<missing>"}`);
  }

  if (fixture.publicClaimLevel !== "demo") {
    throw new Error("demo summary fixture publicClaimLevel must be demo.");
  }

  if (fixture.source?.generator !== "scripts/demo-public.sh") {
    throw new Error("demo summary fixture source.generator must be scripts/demo-public.sh.");
  }

  if (!supportedSummaryVersions.has(fixture.source?.demoSummary?.version)) {
    throw new Error("demo summary fixture source.demoSummary.version is unsupported.");
  }

  if (!/^path-hash:[0-9a-f]{24}$/i.test(fixture.source?.demoSummary?.outputRootHash ?? "")) {
    throw new Error("demo summary fixture source.demoSummary.outputRootHash must be a path hash.");
  }

  if (!Array.isArray(fixture.sections) || fixture.sections.length === 0) {
    throw new Error("demo summary fixture sections must be a non-empty array.");
  }

  const ids = new Set();
  for (const section of fixture.sections) {
    validateFixtureSection(section, ids);
  }
}

function validateFixtureSection(section, ids) {
  if (!isPlainObject(section)) {
    throw new Error("demo summary fixture section must be an object.");
  }

  if (!knownSectionIds.has(section.name) || knownSectionIds.get(section.name) !== section.id) {
    throw new Error(`demo summary fixture section has unknown id/name mapping: ${section.id ?? "<missing>"}`);
  }

  if (ids.has(section.id)) {
    throw new Error(`demo summary fixture has duplicate section id: ${section.id}`);
  }
  ids.add(section.id);

  for (const field of ["status", "classification", "evidenceTier", "coverage", "reason"]) {
    if (typeof section[field] !== "string") {
      throw new Error(`demo summary fixture section ${section.id} is missing string field ${field}.`);
    }
  }

  if (!Array.isArray(section.ruleIds) || section.ruleIds.length === 0 || !section.ruleIds.every(nonEmptyString)) {
    throw new Error(`demo summary fixture section ${section.id} must include ruleIds.`);
  }

  if (!isPlainObject(section.counts)) {
    throw new Error(`demo summary fixture section ${section.id} counts must be an object.`);
  }

  for (const [key, value] of Object.entries(section.counts)) {
    if (!/^[a-z][A-Za-z0-9]*$/.test(key) || typeof value !== "number" || !Number.isFinite(value)) {
      throw new Error(`demo summary fixture section ${section.id} has invalid count ${key}.`);
    }
  }

  if (!Array.isArray(section.artifacts)) {
    throw new Error(`demo summary fixture section ${section.id} artifacts must be an array.`);
  }

  for (const artifact of section.artifacts) {
    const normalized = normalizeRelativePath(artifact, `Fixture artifact for ${section.id}`);
    if (normalized !== artifact || !isApprovedPublicArtifactPath(normalized)) {
      throw new Error(`demo summary fixture section ${section.id} has unsafe artifact path: ${artifact}`);
    }
  }

  if (section.localOnlyArtifactFamilies !== undefined) {
    if (
      !Array.isArray(section.localOnlyArtifactFamilies) ||
      !section.localOnlyArtifactFamilies.every((family) => family === "scan-reports")
    ) {
      throw new Error(`demo summary fixture section ${section.id} has invalid localOnlyArtifactFamilies.`);
    }
  }

  if (reasonRequiredStatuses.has(section.status) && !nonEmptyString(section.reason)) {
    throw new Error(`demo summary fixture section ${section.id} must include a reason for ${section.status}.`);
  }
}

function assertSafeFixtureValues(value, path = "fixture") {
  if (typeof value === "string") {
    for (const [category, pattern] of unsafeValueChecks) {
      if (pattern.test(value)) {
        throw new Error(`${path} contains unsafe value (${category}).`);
      }
    }
    return;
  }

  if (Array.isArray(value)) {
    value.forEach((item, index) => assertSafeFixtureValues(item, `${path}[${index}]`));
    return;
  }

  if (isPlainObject(value)) {
    for (const [key, item] of Object.entries(value)) {
      assertSafeFixtureValues(item, `${path}.${key}`);
    }
  }
}

function validatePortfolioManifest(manifest, label) {
  if (!isPlainObject(manifest) || !Array.isArray(manifest.inputs)) {
    throw new Error(`${label} must include an inputs array.`);
  }

  for (const [index, input] of manifest.inputs.entries()) {
    normalizeRelativePath(input?.indexPath, `${label} inputs[${index}].indexPath`);
  }
}

function assertFixtureArtifactsExist(fixture, publicFiles, root) {
  const relativePublicFiles = new Set(publicFiles.map((file) => relative(root, file).split(sep).join("/")));
  for (const section of fixture.sections) {
    for (const artifact of section.artifacts) {
      if (!relativePublicFiles.has(artifact)) {
        throw new Error(`Section ${section.id} references missing public artifact: ${artifact}`);
      }
    }
  }
}

async function collectPublicFilesInto(root, directory, files) {
  const entries = await readdir(directory, { withFileTypes: true });

  for (const entry of entries) {
    const fullPath = resolve(directory, entry.name);
    const relativePath = relative(root, fullPath).split(sep).join("/");

    if (entry.isDirectory()) {
      if (directory === root && (entry.name === "scans" || entry.name === "combined" || entry.name === "logs")) {
        continue;
      }
      await collectPublicFilesInto(root, fullPath, files);
      continue;
    }

    if (!entry.isFile()) {
      continue;
    }

    if (
      relativePath === "demo-summary.md" ||
      relativePath === "demo-summary.json" ||
      relativePath === "portfolio-manifest.json" ||
      /^reports\/.+\.(?:md|json)$/i.test(relativePath)
    ) {
      files.push(fullPath);
    }
  }
}

function isApprovedPublicArtifactPath(path) {
  return (
    path === "demo-summary.md" ||
    path === "demo-summary.json" ||
    path === "portfolio-manifest.json" ||
    /^reports\/.+\.(?:md|json)$/i.test(path)
  );
}

function normalizeRelativePath(value, label) {
  if (typeof value !== "string" || value.trim() === "") {
    throw new Error(`${label} must be a non-empty relative path.`);
  }

  if (value.includes("\\")) {
    throw new Error(`${label} must use POSIX path separators: ${value}`);
  }

  if (isAbsolute(value) || /^[A-Za-z]:/.test(value) || value.startsWith("//")) {
    throw new Error(`${label} must be relative: ${value}`);
  }

  const parts = value.split("/");
  if (parts.some((part) => part === "" || part === "." || part === "..")) {
    throw new Error(`${label} must not contain empty, current, or parent path segments: ${value}`);
  }

  return parts.join("/");
}

function localAbsolutePathChecks(root) {
  return [["local-absolute-path", new RegExp(`${escapeRegExp(root)}(?:[/\\\\]|$)`)]];
}

async function readJsonFile(path, label) {
  try {
    return JSON.parse(await readFile(path, "utf8"));
  } catch (error) {
    throw new Error(`Unable to read valid JSON from ${label}: ${error.message}`);
  }
}

async function assertDirectory(path, label) {
  try {
    const info = await stat(path);
    if (!info.isDirectory()) {
      throw new Error(`${label} is not a directory: ${path}`);
    }
  } catch (error) {
    if (error?.code === "ENOENT") {
      throw new Error(`Missing ${label}: ${path}`);
    }
    throw error;
  }
}

async function assertFile(path, label) {
  try {
    const info = await stat(path);
    if (!info.isFile()) {
      throw new Error(`${label} is not a file: ${path}`);
    }
  } catch (error) {
    if (error?.code === "ENOENT") {
      throw new Error(`Missing ${label}: ${path}`);
    }
    throw error;
  }
}

function stableStringify(value) {
  return JSON.stringify(sortJson(value), null, 2);
}

function sortJson(value) {
  if (Array.isArray(value)) {
    return value.map(sortJson);
  }

  if (!isPlainObject(value)) {
    return value;
  }

  const result = {};
  for (const key of Object.keys(value).sort()) {
    result[key] = sortJson(value[key]);
  }
  return result;
}

function isPlainObject(value) {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function nonEmptyString(value) {
  return typeof value === "string" && value.trim() !== "";
}

function escapeRegExp(value) {
  return String(value).replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

async function main() {
  const [demoRoot, outputPath] = process.argv.slice(2);
  if (!demoRoot || process.argv.includes("--help") || process.argv.includes("-h")) {
    console.error("Usage: node scripts/refresh-demo-summary.mjs <demo-output-root> [fixture-path]");
    process.exit(demoRoot ? 0 : 1);
  }

  const fixturePath = outputPath ? resolve(outputPath) : defaultFixturePath;
  await refreshDemoSummary({ demoRoot, fixturePath });
  console.log(`Refreshed ${relative(defaultSiteRoot, fixturePath).split(sep).join("/")}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(error.message);
    process.exit(1);
  });
}
