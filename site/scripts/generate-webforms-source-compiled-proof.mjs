import { createHash } from "node:crypto";
import { readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptPath = fileURLToPath(import.meta.url);
const siteRoot = resolve(dirname(scriptPath), "..");
const defaultInput = resolve(siteRoot, "src", "_data", "webforms-source-compiled-proof-input.json");
const defaultOutput = resolve(siteRoot, "src", "assets", "webforms-source-compiled-proof.json");

const safeKeys = new Set([
  "schemaVersion", "publicClaimLevel", "repository", "commitSha", "proofBoundary", "fixtureRoots",
  "extractorVersions", "visualBasicSemantic", "visualBasicSyntax", "managedMetadata", "ilBodyEvidence",
  "pathReporterAlgorithm", "coverage", "label", "maxDepth", "maxPaths", "maxTraversalWork",
  "truncatedByPathOrWorkLimit", "hostBoundary", "bridgeTierExamples", "id", "ruleId", "evidenceTier",
  "coverageLabel", "meaning", "limitation", "outcomes", "title", "terminal", "commandTypeState",
  "commandTextState", "orderedHops", "kind", "filePath", "startLine", "endLine",
  "supportingEvidenceIds", "gaps", "reviewQuestion", "classification", "limitations", "reproduction",
  "workingDirectory", "fixtureCommand", "operatorCommand", "safety"
]);

const forbiddenKey = /(?:sql|query|commandBody|connection|credential|secret|sourceSnippet|raw|absolute|privateIdentity|literalHash)/i;
const forbiddenValue = /(?:\/Users\/|\/home\/|\/private\/|[A-Z]:\\Users\\|file:\/\/|Server\s*=|Password\s*=|User Id\s*=|ConnectionString)/i;

export function stableStringify(value) {
  if (Array.isArray(value)) return `[${value.map(stableStringify).join(",")}]`;
  if (value && typeof value === "object") {
    return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${stableStringify(value[key])}`).join(",")}}`;
  }
  return JSON.stringify(value);
}

export function sha256(value) {
  return createHash("sha256").update(value).digest("hex");
}

function validateProjectedInput(value, path = "$") {
  if (Array.isArray(value)) {
    value.forEach((item, index) => validateProjectedInput(item, `${path}[${index}]`));
    return;
  }
  if (!value || typeof value !== "object") {
    if (typeof value === "string" && forbiddenValue.test(value)) throw new Error(`Forbidden value at ${path}`);
    return;
  }
  for (const [key, child] of Object.entries(value)) {
    if (!safeKeys.has(key) || forbiddenKey.test(key)) throw new Error(`Unallowlisted or protected key at ${path}.${key}`);
    validateProjectedInput(child, `${path}.${key}`);
  }
}

export async function generateWebFormsSourceCompiledProof({ inputPath = defaultInput, outputPath = defaultOutput } = {}) {
  const input = JSON.parse(await readFile(inputPath, "utf8"));
  validateProjectedInput(input);
  if (input.publicClaimLevel !== "demo") throw new Error("Projection input must explicitly request demo claim level.");
  if (!/^[0-9a-f]{40}$/.test(input.commitSha)) throw new Error("Projection input requires one exact commit SHA.");

  const generatorSha256 = sha256(await readFile(scriptPath));
  const boundedInputSha256 = sha256(stableStringify(input));
  const output = {
    schemaVersion: "tracemap.webforms-source-compiled-proof.v1",
    publicClaimLevel: input.publicClaimLevel,
    repository: input.repository,
    commitSha: input.commitSha,
    provenance: {
      generator: "site/scripts/generate-webforms-source-compiled-proof.mjs",
      generatorSha256,
      boundedInput: "site/src/_data/webforms-source-compiled-proof-input.json",
      boundedInputSha256,
      inputProjection: "canonical allowlisted privacy projection of checked-in public synthetic fixtures"
    },
    proofBoundary: input.proofBoundary,
    fixtureRoots: input.fixtureRoots,
    extractorVersions: input.extractorVersions,
    coverage: input.coverage,
    bridgeTierExamples: input.bridgeTierExamples,
    outcomes: input.outcomes,
    gaps: input.gaps,
    limitations: input.limitations,
    reproduction: input.reproduction
  };
  await writeFile(outputPath, `${JSON.stringify(output, null, 2)}\n`, "utf8");
  return output;
}

if (process.argv[1] && resolve(process.argv[1]) === scriptPath) {
  await generateWebFormsSourceCompiledProof();
}
