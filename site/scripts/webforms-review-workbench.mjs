import { existsSync } from "node:fs";
import { readFile } from "node:fs/promises";
import { basename, dirname, resolve } from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

import { decodeHtmlEntities, fileExists, normalizeRenderedText, readSitemapLocSet, stripTagsQuoteAware } from "./validate-utils.mjs";

export const route = "/webforms/review-workbench/";
const moduleSiteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const defaultRepositoryRoot = resolve(moduleSiteRoot, "..");
const repair803Sha = "af05c289a799c896862983fd9f4f73a28d882d0c";
const proofAssetRoute = "/assets/webforms-source-compiled-proof.json";

const requiredLinks = [
  "/webforms/",
  "/webforms/source-plus-compiled-proof/",
  "/webforms/local-demo/",
  proofAssetRoute,
  "/manager-packet/",
  "/proof-paths/for-managers/",
  "/docs/",
  "/outputs/",
  "/review-room/demo-path/",
  "/legacy-modernization/review-handoff/",
  "/static-vs-runtime/",
  "/limitations/",
  "/limitations/reduced-coverage/",
  "/evidence/gaps/",
  "/roadmap/#claim-ledger",
  "https://github.com/joefeser/tracemap/issues/744"
];

const inboundRoutes = [
  "/webforms/",
  "/webforms/source-plus-compiled-proof/",
  "/manager-packet/",
  "/proof-paths/for-managers/",
  "/docs/",
  "/outputs/",
  "/review-room/demo-path/",
  "/legacy-modernization/review-handoff/",
  "/roadmap/"
];

const requiredReviewStates = ["partial", "truncated", "query-omitted", "path-work-limited", "candidate", "input-missing", "command-unresolved"];
const forbiddenMaterial = [
  /(?:~\/|\/(?:Users|home|private|tmp)\/|\/var\/folders\/|[A-Z]:\\(?:Users|Temp)\\|file:\/\/)/i,
  /\b(?:Server|Password|User Id)\s*=/i,
  /\b(?:ConnectionString|api[_-]?key|secret\s*=|sk-[A-Za-z0-9_-]{12,})\b/i,
  /\b(?:SELECT\s+(?:\*|[\w.[\]",]+(?:\s*,\s*[\w.[\]",]+)*)\s+FROM|INSERT\s+INTO|UPDATE\s+\w+\s+SET|DELETE\s+FROM|CREATE\s+TABLE|ALTER\s+TABLE|DROP\s+TABLE)\b/i
];
const forbiddenClaims = [
  /TraceMap (?:ran|executed|observed|activated) (?:the )?(?:Web Forms|page|handler|database)/i,
  /(?:runtime (?:execution|dispatch|reachability)|branch feasibility|provider selection) (?:is|was) (?:proven|verified|observed)/i,
  /(?:complete application coverage|migration parity|customer compatibility|release approval|operational safety) (?:is|was) (?:proven|verified|guaranteed)/i,
  /(?:safe to run|safe to release|release approved)/i,
  /(?:every|all) (?:requested )?(?:#744|workbench) (?:feature|features) (?:has|have) shipped/i
];

export async function validateWebFormsReviewWorkbenchDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors,
  root = moduleSiteRoot,
  repositoryRoot = defaultRepositoryRoot,
  implementationStatePath = resolve(repositoryRoot, ".kiro/specs/site-webforms-review-workbench/implementation-state.md")
}) {
  const pagePath = resolve(dist, "webforms", "review-workbench", "index.html");
  const proofPath = resolve(dist, "assets", "webforms-source-compiled-proof.json");
  const requiredFiles = [
    [pagePath, "route"],
    [proofPath, "#806 proof asset"],
    [resolve(root, "src", "_site", "discovery.json"), "discovery metadata"],
    [resolve(root, "src", "_site", "pages.json"), "page registry"],
    [implementationStatePath, "implementation state"]
  ];
  for (const [path, label] of requiredFiles) if (!(await fileExists(path))) errors.push(`Web Forms review workbench is missing required ${label}.`);
  if (requiredFiles.some(([path]) => !existsSync(path))) return;

  const [html, proofText, discoveryText, pagesText, implementationState] = await Promise.all(requiredFiles.map(([path]) => readFile(path, "utf8")));
  const activeHtml = stripHtmlComments(html);
  const text = normalizeRenderedText(activeHtml);
  const publishedSurfaces = [...safetySurfaces(activeHtml, { html: true }), normalizeAttributeValues(activeHtml)];
  const hrefs = activeAnchorHrefs(activeHtml);

  if (!activeHtml.includes(`<link rel="canonical" href="${baseUrl}${route}">`)) errors.push("Web Forms review workbench canonical URL is missing or incorrect.");
  if (!activeHtml.includes(`<meta property="og:url" content="${baseUrl}${route}">`)) errors.push("Web Forms review workbench Open Graph URL is missing or incorrect.");
  if ((activeHtml.match(/<h1\b/gi) ?? []).length !== 1) errors.push("Web Forms review workbench must contain exactly one h1.");
  if (!/\bdata-public-claim-level=["']demo["']/i.test(activeHtml) || !/\bdata-proof-claim-level=["']concept["']/i.test(activeHtml)) errors.push("Web Forms review workbench must keep the demo walkthrough and concept proof levels distinct.");

  validateStepOrder(activeHtml, errors);
  const proof = validateRouteAndDetail({ html: activeHtml, text, proofText, errors });
  validateReviewStates(activeHtml, errors);
  validateInventoryBounds({ html: activeHtml, text, errors });
  validateBranchBoundary({ html: activeHtml, implementationState, repositoryRoot, errors });

  for (const link of requiredLinks) if (!hrefs.has(link)) errors.push(`Web Forms review workbench is missing active required link: ${link}`);
  for (const phrase of [
    "Public claim level: demo",
    "supporting evidence IDs",
    "unresolved-operand",
    "IlCommandOperandValueUnresolved",
    "IlCommandVirtualDispatchUnproven",
    "Tier4Unknown",
    "Issue #744 remains open",
    "PR #803 repairs are not-shipped",
    "No independent extractor output",
    "No runtime execution"
  ]) if (!text.includes(phrase)) errors.push(`Web Forms review workbench is missing required bounded text: ${phrase}`);

  const discovery = safeJson(discoveryText, "Web Forms review workbench discovery metadata", errors);
  const pages = safeJson(pagesText, "Web Forms review workbench page registry", errors);
  const entry = Array.isArray(discovery) ? discovery.find((item) => item?.path === route) : null;
  if (!entry || entry.publicClaimLevel !== "demo" || entry.sourceType !== "site-page" || entry.preferredProofPath !== proofAssetRoute || entry.limitations?.length < 3 || entry.nonClaims?.length < 2) errors.push("Web Forms review workbench discovery entry is missing its demo boundary, proof dependency, limitations, or non-claims.");
  if (entry && proof?.commitSha && !entry.limitations?.some((limitation) => String(limitation).includes(proof.commitSha))) errors.push("Web Forms review workbench discovery limitations are stale relative to the #806 projection commit.");
  if (!Array.isArray(pages) || !pages.some((item) => item?.path === route)) errors.push("Web Forms review workbench is missing from the page registry.");

  const roadmapPath = resolve(dist, "roadmap", "index.html");
  if (!(await fileExists(roadmapPath)) || !stripHtmlComments(await readFile(roadmapPath, "utf8")).includes('id="claim-webforms-review-workbench"')) errors.push("Web Forms review workbench claim-ledger record is missing.");
  const sitemap = await readSitemapLocSet(resolve(dist, "sitemap.xml"));
  if (!sitemap.has(`${baseUrl}${route}`)) errors.push("Web Forms review workbench route is missing from sitemap.xml.");
  for (const inbound of inboundRoutes) {
    const inboundPath = resolve(dist, inbound.slice(1), "index.html");
    if (!(await fileExists(inboundPath))) { errors.push(`Web Forms review workbench required inbound route is missing: ${inbound}`); continue; }
    if (!activeAnchorHrefs(await readFile(inboundPath, "utf8")).has(route)) errors.push(`Web Forms review workbench is missing inbound link from: ${inbound}`);
  }

  for (const pattern of [...forbiddenMaterial, ...forbiddenClaims]) if (publishedSurfaces.some((surface) => pattern.test(surface))) errors.push(`Web Forms review workbench contains forbidden public material or claim: ${pattern}`);
  if (proof) {
    const proofSurfaces = safetySurfaces(JSON.stringify(proof));
    for (const pattern of [...forbiddenMaterial, ...forbiddenClaims]) if (proofSurfaces.some((surface) => pattern.test(surface))) errors.push(`Web Forms review workbench proof asset contains forbidden public material or claim: ${pattern}`);
  }
  if (entry) {
    const metadataSurface = normalizeRenderedText(JSON.stringify(entry));
    for (const pattern of [...forbiddenMaterial, ...forbiddenClaims]) if (pattern.test(metadataSurface)) errors.push(`Web Forms review workbench discovery contains forbidden public material or claim: ${pattern}`);
  }
}

function validateStepOrder(html, errors) {
  const steps = [...html.matchAll(/\bdata-review-step=["']([1-7])["']/gi)].map((match) => Number(match[1]));
  if (steps.join(",") !== "1,2,3,4,5,6,7") errors.push(`Web Forms review workbench step order must be exactly 1 through 7; received ${steps.join(",") || "none"}.`);
}

function validateRouteAndDetail({ html, text, proofText, errors }) {
  const proof = safeJson(proofText, "Web Forms review workbench #806 proof asset", errors);
  if (!proof) return null;
  if (proof.publicClaimLevel !== "concept" || proof.schemaVersion !== "tracemap.webforms-source-compiled-proof.v1") errors.push("Web Forms review workbench requires the validated #806 concept projection.");
  if (!/^[0-9a-f]{40}$/.test(proof.commitSha ?? "") || !text.includes(proof.commitSha)) errors.push("Web Forms review workbench is missing the exact #806 projection commit.");
  if (!Array.isArray(proof.outcomes)) { errors.push("Web Forms review workbench #806 proof outcomes must be an array."); return proof; }
  const outcome = proof.outcomes.find((item) => item?.id === "dynamic-email");
  if (!outcome || outcome.commandTextState !== "unresolved-operand") { errors.push("Web Forms review workbench requires the unresolved dynamic-email proof outcome."); return proof; }
  if (!Array.isArray(outcome.orderedHops)) { errors.push("Web Forms review workbench dynamic-email orderedHops must be an array."); return proof; }
  const routeHops = [...html.matchAll(/\bdata-route-hop=["']([^"']+)["']/gi)].map((match) => match[1]);
  const expectedHops = outcome.orderedHops.map((hop) => hop?.id);
  if (routeHops.join(",") !== expectedHops.join(",")) errors.push("Web Forms review workbench ordered route does not match the #806 proof asset.");
  for (const hop of outcome.orderedHops) {
    if (!hop || typeof hop.id !== "string" || typeof hop.ruleId !== "string" || typeof hop.evidenceTier !== "string" || typeof hop.filePath !== "string" || !Number.isInteger(hop.startLine) || !Number.isInteger(hop.endLine)) { errors.push("Web Forms review workbench #806 proof contains a malformed ordered hop."); continue; }
    const rendered = `${hop.ruleId} · ${hop.evidenceTier}`;
    const span = `${basename(hop.filePath)}:${hop.startLine}-${hop.endLine}`;
    if (!text.includes(rendered) || !text.includes(span)) errors.push(`Web Forms review workbench is stale relative to proof hop ${hop.id}.`);
  }
  for (const digest of [proof.provenance?.generatorSha256, proof.provenance?.boundedInputSha256]) if (!/^[0-9a-f]{64}$/.test(digest ?? "") || !text.includes(digest)) errors.push("Web Forms review workbench is missing an exact #806 projection digest.");
  for (const version of Object.values(proof.extractorVersions ?? {})) {
    const renderedVersion = version === "1.3" ? "path-reporter/1.3" : String(version);
    if (!text.includes(renderedVersion)) errors.push(`Web Forms review workbench is missing proof extractor/reporter version: ${version}`);
  }
  const commandSection = html.match(/(<section\b[^>]*\bdata-command-state=["'][^"']+["'][^>]*>)[\s\S]*?<\/section>/i);
  const commandTag = commandSection?.[1] ?? "";
  const commandText = normalizeRenderedText(commandSection?.[0] ?? "");
  const commandFields = [outcome.commandTypeState, outcome.commandTextState, outcome.coverageLabel];
  if (commandFields.some((field) => typeof field !== "string" || !commandText.includes(field)) || !new RegExp(`\\bdata-command-state=["']${escapeRegex(outcome.commandTextState)}["']`, "i").test(commandTag)) errors.push("Web Forms review workbench command state or outcome coverage is stale relative to the #806 projection.");
  if (!Array.isArray(outcome.gaps) || outcome.gaps.some((gap) => !text.includes(gap))) errors.push("Web Forms review workbench retained outcome gaps are stale relative to the #806 projection.");

  const coverage = proof.coverage;
  if (!coverage || typeof coverage !== "object") errors.push("Web Forms review workbench #806 proof coverage must be an object.");
  else {
    for (const rendered of [coverage.label, coverage.resultStatus, `maxDepth=${coverage.maxDepth}`, `maxPaths=${coverage.maxPaths}`, `maxTraversalWork=${coverage.maxTraversalWork}`, `truncatedByPathOrWorkLimit=${coverage.truncatedByPathOrWorkLimit}`]) if (!text.includes(String(rendered))) errors.push(`Web Forms review workbench coverage is stale relative to the #806 projection: ${rendered}`);
  }

  const gaps = Array.isArray(proof.gaps) ? proof.gaps : null;
  if (!gaps) errors.push("Web Forms review workbench #806 proof gaps must be an array.");
  const dispatchGap = gaps?.find((gap) => gap?.classification === "IlCommandVirtualDispatchUnproven");
  if (!dispatchGap || typeof dispatchGap.filePath !== "string" || !Number.isInteger(dispatchGap.startLine) || !Number.isInteger(dispatchGap.endLine)) errors.push("Web Forms review workbench requires a complete virtual-dispatch proof gap.");
  else {
    const gapTag = html.match(/<section\b[^>]*\bdata-coverage-gap=["']IlCommandVirtualDispatchUnproven["'][^>]*>/i)?.[0] ?? "";
    for (const [name, value] of Object.entries({
      "data-rule-id": dispatchGap.ruleId,
      "data-evidence-tier": dispatchGap.evidenceTier,
      "data-coverage-label": dispatchGap.coverageLabel
    })) if (!new RegExp(`\\b${name}=["']${escapeRegex(value)}["']`, "i").test(gapTag)) errors.push(`Web Forms review workbench coverage gap is stale for ${name}.`);
    const span = `${dispatchGap.filePath}:${dispatchGap.startLine}-${dispatchGap.endLine}`;
    if (!text.includes(span)) errors.push("Web Forms review workbench coverage-gap span is stale relative to the #806 projection.");
  }

  const detailHop = outcome.orderedHops.find((hop) => hop?.id === "dynamic-getter");
  const detailTag = html.match(/<section\b[^>]*\bdata-evidence-detail=["']dynamic-getter["'][^>]*>/i)?.[0] ?? "";
  for (const [name, value] of Object.entries({
    "data-rule-id": detailHop?.ruleId,
    "data-evidence-tier": detailHop?.evidenceTier,
    "data-coverage-label": proof.coverage?.label,
    "data-provenance-ref": "#provenance"
  })) if (typeof value !== "string" || !new RegExp(`\\b${name}=["']${escapeRegex(value)}["']`, "i").test(detailTag)) errors.push(`Web Forms review workbench evidence detail is missing ${name}.`);
  if (!text.includes("unavailable-in-concept-projection") || !text.includes("public-projection:tracemap.webforms-source-compiled-proof.v1")) errors.push("Web Forms review workbench must expose the missing supporting-reference and namespace boundary.");
  return proof;
}

function validateReviewStates(html, errors) {
  const states = new Set([...html.matchAll(/\bdata-review-state=["']([^"']+)["']/gi)].map((match) => match[1]));
  for (const state of requiredReviewStates) if (!states.has(state)) errors.push(`Web Forms review workbench is missing review state: ${state}`);
}

function validateInventoryBounds({ html, text, errors }) {
  if (!/\bdata-client-inventory-limit=["']10000["']/i.test(html) || !/\bdata-server-inventory-limit=["']10000["']/i.test(html)) errors.push("Web Forms review workbench must declare independent 10,000-row client and server inventory bounds.");
  for (const phrase of ["first 10,000 client behavior rows", "first 10,000 server behavior rows", "WebFormsModernizationClientBehaviorLimitReached", "WebFormsModernizationServerBehaviorLimitReached", "WebFormsModernizationGapLimitReached", "specific client/server label is not guaranteed", "not handler, page, compiled-path, graph-node, total-fact, query, or general workbench limits"]) {
    if (!text.includes(phrase)) errors.push(`Web Forms review workbench is missing bounded inventory text: ${phrase}`);
  }
}

function validateBranchBoundary({ html, implementationState, repositoryRoot, errors }) {
  const pageBase = html.match(/\bdata-implementation-base=["']([0-9a-f]{40})["']/i)?.[1]?.toLowerCase();
  const pageRepair = html.match(/\bdata-repairs-803=["'](shipped|not-shipped)["']/i)?.[1]?.toLowerCase();
  const stateBase = implementationState.match(/^Exact implementation base:\s*`([0-9a-f]{40})`\s*$/im)?.[1]?.toLowerCase();
  const stateRepair = implementationState.match(/^PR #803 ancestry at exact base:\s*`(shipped|not-shipped)`\s*$/im)?.[1]?.toLowerCase();
  if (!pageBase || !pageRepair || !stateBase || !stateRepair) { errors.push("Web Forms review workbench is missing page/spec branch-boundary evidence."); return; }
  if (pageBase !== stateBase) errors.push("Web Forms review workbench page boundary does not match the implementation record.");
  const baseExists = gitCommitExists(repositoryRoot, pageBase);
  if (!baseExists) errors.push("Web Forms review workbench implementation base cannot be resolved as a commit.");
  else if (!gitIsAncestor(repositoryRoot, pageBase, "HEAD")) errors.push("Web Forms review workbench implementation base is not an ancestor of the validation checkout.");
  if (gitCommitExists(repositoryRoot, repair803Sha)) {
    const expected = gitIsAncestor(repositoryRoot, repair803Sha, pageBase) ? "shipped" : "not-shipped";
    if (pageRepair !== expected || stateRepair !== expected) errors.push(`Web Forms review workbench #803 ancestry claim must be ${expected}.`);
  } else if (pageRepair !== "not-shipped" || stateRepair !== "not-shipped") {
    errors.push("Web Forms review workbench cannot verify an affirmative #803 claim without the repair commit.");
  }
}

function gitCommitExists(root, sha) { return spawnSync("git", ["cat-file", "-e", `${sha}^{commit}`], { cwd: root, stdio: "ignore" }).status === 0; }
function gitIsAncestor(root, ancestor, descendant) { return spawnSync("git", ["merge-base", "--is-ancestor", ancestor, descendant], { cwd: root, stdio: "ignore" }).status === 0; }
function stripHtmlComments(value) { return String(value).replace(/<!--[\s\S]*?-->/g, ""); }
function escapeRegex(value) { return String(value).replace(/[.*+?^${}()|[\]\\]/g, "\\$&"); }
function activeAnchorHrefs(value) { return new Set([...stripHtmlComments(value).matchAll(/<a\b[^>]*\bhref\s*=\s*(["'])(.*?)\1/gi)].map((match) => decodeEntities(match[2]))); }
function normalizeAttributeValues(value) { return [...value.matchAll(/<[^>]+>/g)].flatMap((tag) => [...tag[0].matchAll(/\b[\w:-]+\s*=\s*(["'])(.*?)\1/g)].map((attribute) => decodeEntities(attribute[2]))).join(" ").replace(/\s+/g, " ").trim(); }
function decodeEntities(value) { return String(value).replace(/&#(\d+);?/g, (_, code) => String.fromCodePoint(Number(code))).replace(/&#x([0-9a-f]+);?/gi, (_, code) => String.fromCodePoint(Number.parseInt(code, 16))).replace(/&(?:nbsp|Tab|NewLine);?/gi, " ").replace(/&amp;/gi, "&").replace(/&quot;/gi, '"').replace(/&apos;/gi, "'"); }
function decodeBrowserNumericEntities(value) { return String(value).replace(/&#(?:x[0-9a-f]+|[0-9]+);?/gi, (entity) => decodeHtmlEntities(entity.endsWith(";") ? entity : `${entity};`)); }
function safetySurfaces(value, { html = false } = {}) {
  const browserDecoded = decodeBrowserNumericEntities(value);
  const decoded = decodeHtmlEntities(browserDecoded);
  if (!html) return [decoded, normalizeRenderedText(decoded)];
  return [decoded, normalizeRenderedText(browserDecoded), decodeHtmlEntities(stripTagsQuoteAware(decoded)).replace(/\s+/g, " ").trim()];
}
function safeJson(value, label, errors) { try { return JSON.parse(value); } catch (error) { errors.push(`${label} is invalid JSON: ${error.message}`); return null; } }
