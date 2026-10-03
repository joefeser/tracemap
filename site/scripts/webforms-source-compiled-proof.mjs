import { createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { fileExists, normalizeRenderedText, readSitemapLocSet } from "./validate-utils.mjs";
import { stableStringify } from "./generate-webforms-source-compiled-proof.mjs";

export const route = "/webforms/source-plus-compiled-proof/";
export const assetRoute = "/assets/webforms-source-compiled-proof.json";
export const expectedCommit = "5ffd4a54176c002e4c6d41ce0133eab5963ad79b";

const moduleRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const requiredInboundRoutes = [
  "/blog/modernizing-web-forms-without-running-it/",
  "/legacy-dotnet/evidence/",
  "/legacy-modernization/evidence-map/",
  "/legacy-modernization/review-handoff/",
  "/manager-packet/",
  "/proof-paths/for-managers/",
  "/limitations/",
  "/roadmap/"
];
const requiredOutboundRoutes = [
  assetRoute,
  "/blog/modernizing-web-forms-without-running-it/",
  "/manager-packet/",
  "/proof-paths/for-managers/",
  "/legacy-modernization/evidence-map/",
  "/legacy-modernization/review-handoff/",
  "/static-vs-runtime/",
  "/evidence/gaps/",
  "/limitations/reduced-coverage/",
  "/roadmap/#claim-ledger",
  "/legacy-dotnet/evidence/",
  "/capabilities/",
  "/limitations/"
];
const tiers = ["Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"];
const requiredRules = [
  "combined.paths.compiled-il-bridge.v1",
  "combined.paths.compiled-command-value.v1",
  "combined.paths.projectless-publish-candidate.v1",
  "dotnet.compiled.member.v1",
  "vb.syntax.declarations.v1"
];
const forbiddenMaterial = [
  /(?:\/Users\/|\/home\/|\/private\/|[A-Z]:\\Users\\|file:\/\/)/i,
  /\b(?:Server|Password|User Id)\s*=/i,
  /\b(?:ConnectionString|api[_-]?key|secret\s*=|sk-[A-Za-z0-9_-]{12,})\b/i,
  /\b(?:SELECT\s+.+?\s+FROM|INSERT\s+INTO|UPDATE\s+\w+\s+SET|DELETE\s+FROM|CREATE\s+TABLE|ALTER\s+TABLE|DROP\s+TABLE)\b/i,
  /\bpublic\.synthetic_[A-Za-z0-9_]+\b/i
];
const protectedKeys = /(?:sqlText|rawSql|sourceSnippet|literalHash|parameterValue|connectionString|credential|secret|absolutePath|privateIdentity|analyzerOutput|rawIndex)/i;

export async function validateWebFormsSourceCompiledProofDist({ baseUrl = "https://tracemap.tools", dist, errors, root = moduleRoot }) {
  const pagePath = resolve(dist, "webforms", "source-plus-compiled-proof", "index.html");
  const assetPath = resolve(dist, "assets", "webforms-source-compiled-proof.json");
  if (!(await fileExists(pagePath))) errors.push(`Web Forms source + compiled proof is missing route: ${route}`);
  if (!(await fileExists(assetPath))) errors.push(`Web Forms source + compiled proof is missing asset: ${assetRoute}`);
  if (!(await fileExists(pagePath)) || !(await fileExists(assetPath))) return;

  const html = await readFile(pagePath, "utf8");
  const assetText = await readFile(assetPath, "utf8");
  const text = normalizeRenderedText(html);
  if (!html.includes(`<link rel="canonical" href="${baseUrl}${route}">`)) errors.push("Web Forms proof canonical URL is missing or incorrect.");
  if ((html.match(/<h1\b/gi) ?? []).length !== 1) errors.push("Web Forms proof must contain exactly one h1.");
  for (const phrase of ["Public claim level: demo", expectedCommit, "Dynamic email lookup", "Literal audit call", "independent Fill terminal", "Windows-only", "No runtime execution"]) {
    if (!text.includes(phrase)) errors.push(`Web Forms proof is missing required bounded-claim text: ${phrase}`);
  }
  for (const tier of tiers) if (!text.includes(tier)) errors.push(`Web Forms proof is missing evidence tier: ${tier}`);
  for (const rule of requiredRules) if (!text.includes(rule)) errors.push(`Web Forms proof is missing rule ID: ${rule}`);
  for (const link of requiredOutboundRoutes) if (!hasHref(html, link)) errors.push(`Web Forms proof is missing required outbound link: ${link}`);

  let packet;
  try { packet = JSON.parse(assetText); } catch (error) { errors.push(`Web Forms proof asset is not valid JSON: ${error.message}`); return; }
  validatePacket(packet, errors);
  await validateHashes({ packet, root, errors });

  const combined = `${html}\n${assetText}`;
  for (const pattern of forbiddenMaterial) if (pattern.test(combined)) errors.push(`Web Forms proof contains forbidden public material: ${pattern}`);
  validateProtectedKeys(packet, "$", errors);

  const discovery = JSON.parse(await readFile(resolve(root, "src", "_site", "discovery.json"), "utf8"));
  const entry = discovery.find((item) => item.path === route);
  if (!entry || entry.publicClaimLevel !== "demo" || entry.preferredProofPath !== assetRoute || !entry.limitations?.length || !entry.nonClaims?.length) {
    errors.push("Web Forms proof discovery metadata is missing its demo boundary, asset proof path, limitations, or non-claims.");
  }

  const sitemap = await readSitemapLocSet(resolve(dist, "sitemap.xml"));
  if (!sitemap.has(`${baseUrl}${route}`)) errors.push("Web Forms proof route is missing from sitemap.xml.");
  for (const inbound of requiredInboundRoutes) {
    const inboundPath = resolve(dist, inbound.slice(1), "index.html");
    if (!(await fileExists(inboundPath))) { errors.push(`Web Forms proof required inbound route is missing: ${inbound}`); continue; }
    if (!hasHref(await readFile(inboundPath, "utf8"), route)) errors.push(`Web Forms proof is missing inbound link from: ${inbound}`);
  }
  const plannedLanding = resolve(dist, "webforms", "index.html");
  if (await fileExists(plannedLanding)) {
    if (!hasHref(await readFile(plannedLanding, "utf8"), route)) errors.push("Web Forms landing page exists but does not link to the source + compiled proof.");
  }
}

function validatePacket(packet, errors) {
  if (packet.schemaVersion !== "tracemap.webforms-source-compiled-proof.v1") errors.push("Web Forms proof asset schema version is incorrect.");
  if (packet.publicClaimLevel !== "demo" || packet.commitSha !== expectedCommit) errors.push("Web Forms proof asset is not bound to the expected demo claim and exact main revision.");
  if (packet.proofBoundary !== "checked-in-public-synthetic-fixtures") errors.push("Web Forms proof asset has an invalid proof boundary.");
  if (!/^[0-9a-f]{64}$/.test(packet.provenance?.generatorSha256 ?? "") || !/^[0-9a-f]{64}$/.test(packet.provenance?.boundedInputSha256 ?? "")) errors.push("Web Forms proof asset requires valid generator and bounded-input SHA-256 values.");
  if (!packet.provenance?.inputProjection?.includes("privacy projection")) errors.push("Web Forms proof asset must identify its bounded input as a privacy projection.");
  if (packet.coverage?.maxDepth !== 20 || packet.coverage?.maxPaths !== 256 || packet.coverage?.maxTraversalWork !== 100000 || packet.coverage?.truncatedByPathOrWorkLimit !== false) errors.push("Web Forms proof asset does not preserve the pinned traversal bounds and truncation state.");
  const examples = packet.bridgeTierExamples ?? [];
  for (const tier of ["Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual"]) {
    if (!examples.some((row) => row.ruleId === "combined.paths.compiled-il-bridge.v1" && row.evidenceTier === tier)) errors.push(`Web Forms proof asset must preserve compiled bridge emission at ${tier}.`);
  }
  if (examples.some((row) => row.evidenceTier === "Tier3SyntaxOrTextual" && /proven IL (?:call|destination)/i.test(row.meaning ?? ""))) errors.push("Web Forms proof asset upgrades a Tier3 candidate to a proven IL call.");
  const outcomes = packet.outcomes ?? [];
  if (outcomes.map((row) => row.id).join(",") !== "dynamic-email,literal-audit,fill") errors.push("Web Forms proof asset must contain the three ordered outcomes.");
  for (const outcome of outcomes) {
    if (!Array.isArray(outcome.orderedHops) || outcome.orderedHops.length < 4) errors.push(`Web Forms proof outcome has an incomplete ordered chain: ${outcome.id}`);
    for (const hop of outcome.orderedHops ?? []) {
      if (!requiredRules.includes(hop.ruleId) && hop.ruleId !== "vb.syntax.callgraph.v1") errors.push(`Web Forms proof hop has an unapproved rule ID: ${hop.ruleId}`);
      if (!tiers.includes(hop.evidenceTier) || !/^samples\//.test(hop.filePath ?? "") || !Number.isInteger(hop.startLine) || !Number.isInteger(hop.endLine) || hop.startLine < 1 || hop.endLine < hop.startLine || !hop.supportingEvidenceIds?.length) errors.push(`Web Forms proof hop has incomplete tier, span, or support provenance: ${hop.id}`);
    }
  }
  if (outcomes.find((row) => row.id === "dynamic-email")?.commandTextState !== "unresolved-operand") errors.push("Dynamic email outcome must remain unresolved.");
  if (outcomes.find((row) => row.id === "literal-audit")?.commandTextState !== "constant-on-encoded-call-path") errors.push("Literal audit outcome must preserve its categorical encoded-path state.");
  if (outcomes.find((row) => row.id === "fill")?.terminal !== "Fill") errors.push("Fill outcome must remain independently terminal-scoped.");
}

async function validateHashes({ packet, root, errors }) {
  const generatorPath = resolve(root, "scripts", "generate-webforms-source-compiled-proof.mjs");
  const inputPath = resolve(root, "src", "_data", "webforms-source-compiled-proof-input.json");
  const generatorHash = sha256(await readFile(generatorPath));
  const input = JSON.parse(await readFile(inputPath, "utf8"));
  const inputHash = sha256(stableStringify(input));
  if (packet.provenance.generatorSha256 !== generatorHash) errors.push("Web Forms proof asset generator SHA-256 does not match the exact generator bytes.");
  if (packet.provenance.boundedInputSha256 !== inputHash) errors.push("Web Forms proof asset bounded-input SHA-256 does not match the canonical privacy-projected input.");
}

function validateProtectedKeys(value, path, errors) {
  if (Array.isArray(value)) return value.forEach((item, index) => validateProtectedKeys(item, `${path}[${index}]`, errors));
  if (!value || typeof value !== "object") return;
  for (const [key, child] of Object.entries(value)) {
    if (protectedKeys.test(key)) errors.push(`Web Forms proof asset contains protected key at ${path}.${key}`);
    validateProtectedKeys(child, `${path}.${key}`, errors);
  }
}

function sha256(value) { return createHash("sha256").update(value).digest("hex"); }
function hasHref(html, href) { return new RegExp(`href\\s*=\\s*["']${href.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}["']`, "i").test(html); }
