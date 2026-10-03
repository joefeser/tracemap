import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

import { fileExists, normalizeRenderedText, readSitemapLocSet } from "./validate-utils.mjs";

export const route = "/webforms/local-demo/";
const moduleSiteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const defaultRepositoryRoot = resolve(moduleSiteRoot, "..");
const testedHead = "afe6152ccb58ad11b61ef4782ba82afdfe7651b7";
const treeEquivalentMerge = "d76358f663ce40532fe0954ca9888612d51a4121";
const testedTree = "1163de1ea0bc4f57cd762ebc3a85ee014bc6fff2";
const repair803Sha = "af05c289a799c896862983fd9f4f73a28d882d0c";
const durableRunUrl = "https://github.com/joefeser/tracemap/actions/runs/37125942534";
const sourceGeneratorHash = "529f399c5492f89d12785beb091eda70b7ea6191be60c8fd53a81e818027ec39";
const sourceInputHash = "90120a5650f5da8472f7ffc734b05a74e9b4ef2075d123fafc35f07addc96a12";

const requiredLinks = [
  "/webforms/",
  "/webforms/source-plus-compiled-proof/",
  "/assets/webforms-source-compiled-proof.json",
  "/legacy-modernization/review-handoff/",
  "/validation/",
  "/outputs/",
  "/limitations/",
  "/examples/",
  "/legacy-dotnet/evidence/",
  "/legacy-modernization/evidence-map/",
  "/capabilities/",
  "/roadmap/#claim-ledger",
  "https://github.com/joefeser/tracemap/blob/main/docs/VALIDATION.md",
  "https://github.com/joefeser/tracemap/blob/main/docs/WEBFORMS_NATIVE_WORKFLOW.md",
  "https://github.com/joefeser/tracemap/blob/main/samples/fixture-build/lazy-constructor/README.md",
  durableRunUrl
];

const inboundRoutes = [
  "/webforms/",
  "/webforms/source-plus-compiled-proof/",
  "/validation/",
  "/examples/",
  "/outputs/",
  "/limitations/",
  "/capabilities/",
  "/roadmap/",
  "/legacy-dotnet/evidence/",
  "/legacy-modernization/evidence-map/"
];

const requiredPhrases = [
  "Public claim level: demo",
  "PowerShell 7",
  "-RequireWindowsPublish",
  "Windows Server 2025",
  "148 passed · 0 failed · 0 skipped",
  "validation.deep-projectless-corpus.v1",
  "Tier2Structural",
  "attached",
  "separate-dll-only",
  "reversed",
  "missing",
  "TruncatedByLimit",
  "SelectorNoMatch",
  "Tier4Unknown",
  "DEEP_CORPUS_OUTPUT_NOT_FRESH",
  "DEEP_CORPUS_WINDOWS_REQUIRED",
  "DEEP_CORPUS_TEST_FAILED;outputs-preserved",
  "DEEP_CORPUS_INPUT_CHANGED;outputs-preserved-not-admitted",
  "A non-Windows refusal or skipped Windows theory is not a pass",
  "Repeat success does not prove that a prior intermittent failure was fixed",
  "No runtime page"
];

const forbiddenMaterial = [
  /(?:~\/|\/(?:Users|home|private|tmp)\/|\/var\/folders\/|[A-Z]:\\(?:Users|Temp)\\|file:\/\/)/i,
  /\b(?:Server|Password|User Id)\s*=/i,
  /\b(?:ConnectionString|api[_-]?key|secret\s*=|sk-[A-Za-z0-9_-]{12,})\b/i,
  /\b(?:SELECT\s+.+?\s+FROM|INSERT\s+INTO|UPDATE\s+\w+\s+SET|DELETE\s+FROM|CREATE\s+TABLE|ALTER\s+TABLE|DROP\s+TABLE)\b/i
];

const forbiddenAffirmativeClaims = [
  /TraceMap (?:ran|executed|launched) (?:the )?(?:Web Forms|website|page|database)/i,
  /(?:customer compatibility|migration parity|complete cross-service tracing) (?:is|was) (?:proven|verified|guaranteed)/i,
  /(?:safe to run|safe to release|release approved)/i,
  /(?:runtime dispatch|branch feasibility) (?:is|was) (?:proven|verified)/i,
  /(?:publication|database execution) (?:succeeded|passed|was verified) for (?:customers|production|this page)/i
];

export async function validateWebFormsLocalDemoDist({
  baseUrl = "https://tracemap.tools",
  dist,
  errors,
  root,
  repositoryRoot = defaultRepositoryRoot,
  implementationStatePath = resolve(repositoryRoot, ".kiro/specs/site-webforms-local-demo/implementation-state.md")
}) {
  const pagePath = resolve(dist, "webforms", "local-demo", "index.html");
  if (!(await fileExists(pagePath))) {
    errors.push(`Web Forms local demo is missing route: ${route}`);
    return;
  }

  const requiredFiles = [
    [resolve(dist, "assets", "webforms-source-compiled-proof.json"), "proof asset"],
    [resolve(root, "src", "_site", "discovery.json"), "discovery metadata"],
    [resolve(root, "src", "_site", "pages.json"), "page registry"],
    [implementationStatePath, "implementation state"]
  ];
  let missingRequiredFile = false;
  for (const [path, label] of requiredFiles) {
    if (!(await fileExists(path))) {
      errors.push(`Web Forms local demo is missing required ${label}.`);
      missingRequiredFile = true;
    }
  }
  if (missingRequiredFile) return;

  const [html, assetText, discoveryText, pagesText, implementationState] = await Promise.all([
    readFile(pagePath, "utf8"),
    ...requiredFiles.map(([path]) => readFile(path, "utf8"))
  ]);
  const activeHtml = stripHtmlComments(html);
  const text = normalizeRenderedText(activeHtml);
  const publishedSurface = `${text} ${normalizeAttributeValues(activeHtml)}`;
  const hrefs = activeAnchorHrefs(activeHtml);

  if (!activeHtml.includes(`<link rel="canonical" href="${baseUrl}${route}">`)) errors.push("Web Forms local demo canonical URL is missing or incorrect.");
  if (!activeHtml.includes(`<meta property="og:url" content="${baseUrl}${route}">`)) errors.push("Web Forms local demo Open Graph URL is missing or incorrect.");
  if ((activeHtml.match(/<h1\b/gi) ?? []).length !== 1) errors.push("Web Forms local demo must contain exactly one h1.");
  if (!activeHtml.includes('data-responsive-artifact-map="table-scroll"')) errors.push("Web Forms local demo is missing its responsive artifact-map contract.");
  for (const phrase of requiredPhrases) if (!text.includes(phrase)) errors.push(`Web Forms local demo is missing required bounded text: ${phrase}`);
  for (const link of requiredLinks) if (!hrefs.has(link)) errors.push(`Web Forms local demo is missing active required link: ${link}`);

  validateBoundary({ html: activeHtml, implementationState, repositoryRoot, errors });
  validateProvenance({ html: activeHtml, text, assetText, errors });

  const discovery = safeJson(discoveryText, "Web Forms local demo discovery metadata", errors);
  const pages = safeJson(pagesText, "Web Forms local demo page registry", errors);
  const entry = Array.isArray(discovery) ? discovery.find((item) => item?.path === route) : null;
  if (!entry || entry.publicClaimLevel !== "demo" || entry.sourceType !== "site-page" || entry.preferredProofPath !== "/webforms/source-plus-compiled-proof/" || entry.limitations?.length < 3 || entry.nonClaims?.length < 2) {
    errors.push("Web Forms local demo discovery entry is missing its demo boundary, proof path, limitations, or non-claims.");
  }
  if (!Array.isArray(pages) || !pages.some((item) => item?.path === route)) errors.push("Web Forms local demo is missing from the page registry.");

  const sitemap = await readSitemapLocSet(resolve(dist, "sitemap.xml"));
  if (!sitemap.has(`${baseUrl}${route}`)) errors.push("Web Forms local demo route is missing from sitemap.xml.");
  for (const inbound of inboundRoutes) {
    const inboundPath = resolve(dist, inbound.slice(1), "index.html");
    if (!(await fileExists(inboundPath))) {
      errors.push(`Web Forms local demo required inbound route is missing: ${inbound}`);
      continue;
    }
    if (!activeAnchorHrefs(await readFile(inboundPath, "utf8")).has(route)) errors.push(`Web Forms local demo is missing inbound link from: ${inbound}`);
  }

  for (const pattern of [...forbiddenMaterial, ...forbiddenAffirmativeClaims]) {
    if (pattern.test(publishedSurface)) errors.push(`Web Forms local demo contains forbidden public material or claim: ${pattern}`);
  }
  if (entry) {
    const metadataSurface = normalizeRenderedText(JSON.stringify(entry));
    for (const pattern of [...forbiddenMaterial, ...forbiddenAffirmativeClaims]) {
      if (pattern.test(metadataSurface)) errors.push(`Web Forms local demo discovery contains forbidden public material or claim: ${pattern}`);
    }
  }
}

function validateBoundary({ html, implementationState, repositoryRoot, errors }) {
  const pageBase = html.match(/\bdata-main-boundary=["']([0-9a-f]{40})["']/i)?.[1]?.toLowerCase();
  const pageRepairState = html.match(/\bdata-repairs-803=["'](shipped|not-shipped)["']/i)?.[1]?.toLowerCase();
  const recordedBase = implementationState.match(/^Exact base:\s*`([0-9a-f]{40})`\s*$/im)?.[1]?.toLowerCase();
  const recordedRepairState = implementationState.match(/^PR #803 ancestry at exact base:\s*`(shipped|not-shipped)`\s*$/im)?.[1]?.toLowerCase();
  if (!pageBase || !pageRepairState || !recordedBase || !recordedRepairState) {
    errors.push("Web Forms local demo is missing page/spec implementation-boundary evidence.");
    return;
  }
  if (pageBase !== recordedBase) errors.push("Web Forms local demo page boundary does not match the implementation record.");
  if (gitCommitExists(repositoryRoot, pageBase) && !gitIsAncestor(repositoryRoot, pageBase, "HEAD")) errors.push("Web Forms local demo implementation base exists locally but is not an ancestor of the validation checkout.");
  if (!gitCommitExists(repositoryRoot, repair803Sha)) {
    if (recordedRepairState !== "not-shipped" || pageRepairState !== "not-shipped") errors.push("Web Forms local demo cannot verify an affirmative #803 shipped claim without the repair commit.");
    return;
  }
  const expected = gitIsAncestor(repositoryRoot, repair803Sha, pageBase) ? "shipped" : "not-shipped";
  if (recordedRepairState !== expected || pageRepairState !== expected) errors.push(`Web Forms local demo #803 ancestry claim must be ${expected} for the recorded implementation base.`);
}

function validateProvenance({ html, text, assetText, errors }) {
  let asset;
  try { asset = JSON.parse(assetText); } catch (error) { errors.push(`Web Forms local demo proof asset is invalid JSON: ${error.message}`); return; }
  for (const value of [testedHead, treeEquivalentMerge, testedTree, durableRunUrl, sourceGeneratorHash, sourceInputHash]) {
    if (!text.includes(value) && !html.includes(value)) errors.push(`Web Forms local demo is missing exact result provenance: ${value}`);
  }
  const projectionGenerator = asset?.provenance?.generatorSha256;
  const projectionInput = asset?.provenance?.boundedInputSha256;
  if (asset?.publicClaimLevel !== "concept" || !/^[0-9a-f]{64}$/.test(projectionGenerator ?? "") || !/^[0-9a-f]{64}$/.test(projectionInput ?? "")) errors.push("Web Forms local demo requires the #806 concept projection and both projection hashes.");
  for (const digest of [projectionGenerator, projectionInput]) if (digest && !text.includes(digest)) errors.push(`Web Forms local demo is stale relative to projection digest: ${digest}`);
  if (new Set([sourceGeneratorHash, sourceInputHash, projectionGenerator, projectionInput]).size !== 4) errors.push("Web Forms local demo source-receipt and public-projection hashes must remain distinct.");
  if (!text.includes("Covers the admitted source roster, execution/fixture assemblies, and test receipt") || !text.includes("Covers only the allowlisted privacy-projected input")) errors.push("Web Forms local demo must keep source-receipt and public-projection bounded inputs distinct.");
}

function gitCommitExists(repositoryRoot, sha) {
  return spawnSync("git", ["cat-file", "-e", `${sha}^{commit}`], { cwd: repositoryRoot, stdio: "ignore" }).status === 0;
}

function gitIsAncestor(repositoryRoot, ancestor, descendant) {
  return spawnSync("git", ["merge-base", "--is-ancestor", ancestor, descendant], { cwd: repositoryRoot, stdio: "ignore" }).status === 0;
}

function stripHtmlComments(value) { return String(value).replace(/<!--[\s\S]*?-->/g, " "); }

function activeAnchorHrefs(value) {
  const hrefs = new Set();
  for (const match of stripHtmlComments(value).matchAll(/<a\b[^>]*\bhref\s*=\s*(["'])(.*?)\1/gi)) hrefs.add(decodeEntities(match[2]));
  return hrefs;
}

function normalizeAttributeValues(value) {
  const values = [];
  for (const tag of value.matchAll(/<[^>]+>/g)) for (const attribute of tag[0].matchAll(/\b[\w:-]+\s*=\s*(["'])(.*?)\1/g)) values.push(decodeEntities(attribute[2]));
  return values.join(" ").replace(/\s+/g, " ").trim();
}

function decodeEntities(value) {
  return String(value)
    .replace(/&#(\d+);?/g, (_, code) => String.fromCodePoint(Number(code)))
    .replace(/&#x([0-9a-f]+);?/gi, (_, code) => String.fromCodePoint(Number.parseInt(code, 16)))
    .replace(/&(?:nbsp|Tab|NewLine);?/gi, " ")
    .replace(/&amp;/gi, "&")
    .replace(/&quot;/gi, '"')
    .replace(/&apos;/gi, "'");
}

function safeJson(value, label, errors) {
  try { return JSON.parse(value); } catch (error) { errors.push(`${label} is invalid JSON: ${error.message}`); return null; }
}
