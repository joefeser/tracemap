import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { spawnSync } from "node:child_process";

const route = "/webforms/";
const requiredText = [
  "terminal wizard",
  "not a gui",
  "website folder",
  "solution",
  ".csproj",
  ".vbproj",
  "projectless web site",
  "projectless web site entry",
  "explicit selection",
  "forms.txt",
  "code 2",
  "template-regeneration pause",
  "--continue",
  "private operator state",
  "types build",
  "declining consent executes no build",
  "windows_build_required",
  "external windows operator step",
  "typing ready",
  "--add-project",
  "--repair-project",
  "stop conditions and owner handoff",
  "partial, reduced, unresolved, unsupported, paused, declined, failed",
  "does not launch the website or execute sql"
];

const requiredLinks = [
  "/blog/modernizing-web-forms-without-running-it/",
  "/evidence/",
  "/evidence/gaps/",
  "/legacy-dotnet/evidence/",
  "/legacy-modernization/evidence-map/",
  "/limitations/",
  "/limitations/reduced-coverage/",
  "/proof-paths/",
  "/static-vs-runtime/",
  "/webforms/source-plus-compiled-proof/",
  "https://github.com/joefeser/tracemap/blob/main/docs/WEBFORMS_NATIVE_WORKFLOW.md"
];

const repair803Sha = "af05c289a799c896862983fd9f4f73a28d882d0c";
const unavailableRepairClaims = [
  /linked review\/output paths are rejected/i,
  /exact os aliases .* are admitted/i,
  /independent 10,?000-row limit/i,
  /empty project director(?:y|ies) (?:is|are) rejected/i
];

const forbiddenAffirmativeClaims = [
  /tracemap (?:ran|launched|executed) the (?:website|application|page)/i,
  /publication (?:is|was) (?:successful|verified)/i,
  /customer compatibility (?:is|was) (?:proven|verified|guaranteed)/i,
  /migration parity (?:is|was) (?:proven|verified|guaranteed)/i,
  /safe to (?:run|release|deploy)/i,
  /complete (?:application |cross-service )?coverage (?:is|was) (?:proven|verified|guaranteed)/i,
  /native (?:gui|desktop) wizard/i
];

const forbiddenMaterial = [
  /\/(?:Users|home|private|tmp)\//i,
  /[A-Z]:\\(?:Users|source|work|private)\\/i,
  /(?:password|pwd|secret|token)\s*[:=]\s*[^\s<]+/i,
  /(?:server|data source)\s*=\s*[^;<]+/i,
  /\bselect\s+(?:\*|[a-z_][\w.]*(?:\s*,\s*[a-z_][\w.]*)*)\s+from\s+[a-z_][\w.]*/i,
  /\binsert\s+into\s+\S+/i,
  /\bupdate\s+\S+\s+set\s+/i,
  /\bdelete\s+from\s+\S+/i
];

export async function validateWebformsGuidedSetupDist({
  baseUrl,
  dist,
  errors,
  root,
  repositoryRoot = resolve(root, ".."),
  implementationStatePath = resolve(repositoryRoot, ".kiro/specs/site-webforms-guided-setup/implementation-state.md")
}) {
  const pagePath = resolve(dist, "webforms/index.html");
  const articlePath = resolve(dist, "blog/modernizing-web-forms-without-running-it/index.html");
  const discoveryPath = resolve(root, "src/_site/discovery.json");
  const pagesPath = resolve(root, "src/_site/pages.json");
  const sitemapPath = resolve(dist, "sitemap.xml");

  const [page, article, discoveryRaw, pagesRaw, sitemap, implementationState] = await Promise.all([
    safeRead(pagePath, errors, "Web Forms guided setup route is missing"),
    safeRead(articlePath, errors, "Web Forms concept article is missing"),
    safeRead(discoveryPath, errors, "Web Forms discovery metadata is missing"),
    safeRead(pagesPath, errors, "Web Forms page registry is missing"),
    safeRead(sitemapPath, errors, "Web Forms sitemap is missing"),
    safeRead(implementationStatePath, errors, "Web Forms implementation-state boundary is missing")
  ]);
  if (!page) return;

  const visible = normalizeVisibleText(page);
  const activePage = stripHtmlComments(page);
  const publishedSurface = `${visible} ${normalizeAttributeValues(activePage)}`;
  const activePageLinks = activeAnchorHrefs(page);
  const activeArticleLinks = activeAnchorHrefs(article);
  requireIncludes(page, '<link rel="canonical" href="https://tracemap.tools/webforms/">', errors, "canonical metadata");
  requireIncludes(page, '<meta property="og:url" content="https://tracemap.tools/webforms/">', errors, "Open Graph URL");
  validateImplementationBoundary({ page, implementationState, repositoryRoot, errors });
  requireIncludes(page, "Public claim level:", errors, "public claim label");
  requireIncludes(page, "<strong>shipped</strong>", errors, "shipped workflow label");
  requireIncludes(page, "<strong>demo</strong>", errors, "demo proof label");
  requireIncludes(page, 'data-tm-boundary="claim-boundary"', errors, "claim boundary marker");

  for (const text of requiredText) {
    if (!visible.includes(text)) errors.push(`Web Forms guided setup is missing required concept: ${text}`);
  }
  for (const href of requiredLinks) {
    if (!activePageLinks.has(href)) errors.push(`Web Forms guided setup is missing active required link: ${href}`);
  }
  for (const pattern of [...forbiddenAffirmativeClaims, ...forbiddenMaterial, ...unavailableRepairClaims]) {
    if (pattern.test(publishedSurface)) errors.push(`Web Forms guided setup contains forbidden public material or claim: ${pattern}`);
  }

  if (article && !activeArticleLinks.has("/webforms/")) {
    errors.push("Web Forms concept article must link to the guided setup route");
  }
  if (!sitemap.includes(`${baseUrl}${route}`)) errors.push("Web Forms guided setup is missing from sitemap.xml");

  const discovery = safeJson(discoveryRaw, errors, "Web Forms discovery metadata");
  const pages = safeJson(pagesRaw, errors, "Web Forms page registry");
  const entry = Array.isArray(discovery) ? discovery.find((item) => item?.path === route) : null;
  if (!entry) {
    errors.push("Web Forms guided setup discovery entry is missing");
  } else {
    if (entry.publicClaimLevel !== "shipped") errors.push("Web Forms discovery claim level must be shipped");
    if (entry.sourceType !== "site-page") errors.push("Web Forms discovery source type must be site-page");
    if (!Array.isArray(entry.limitations) || entry.limitations.length < 2) errors.push("Web Forms discovery must retain explicit limitations");
    if (!Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) errors.push("Web Forms discovery must retain explicit non-claims");
    const discoveryText = normalizeVisibleText(JSON.stringify(entry));
    for (const phrase of ["terminal wizard", "windows-only", "external windows operator step", "no runtime execution", "cross-service tracing"]) {
      if (!discoveryText.includes(phrase)) errors.push(`Web Forms discovery is missing boundary text: ${phrase}`);
    }
    for (const pattern of forbiddenMaterial) {
      if (pattern.test(discoveryText)) errors.push(`Web Forms discovery contains forbidden public material: ${pattern}`);
    }
  }
  const pageEntry = Array.isArray(pages) ? pages.find((item) => item?.path === route) : null;
  if (!pageEntry) {
    errors.push("Web Forms guided setup page registry entry is missing");
  } else {
    const metadataSurface = normalizeVisibleText(JSON.stringify(pageEntry));
    for (const pattern of forbiddenMaterial) {
      if (pattern.test(metadataSurface)) errors.push(`Web Forms page registry contains forbidden public material: ${pattern}`);
    }
  }
}

function validateImplementationBoundary({ page, implementationState, repositoryRoot, errors }) {
  const pageMatch = page.match(/\bdata-main-boundary=["']([0-9a-f]{40})["']/i);
  const repairStateMatch = page.match(/\bdata-repairs-803=["'](shipped|not-shipped)["']/i);
  const stateMatch = implementationState.match(/^Exact base:\s*`([0-9a-f]{40})`\s*$/im);
  const recordedRepairStateMatch = implementationState.match(/^PR #803 ancestry at exact base:\s*`(shipped|not-shipped)`\s*$/im);
  if (!pageMatch) errors.push("Web Forms guided setup is missing exact implementation base");
  if (!repairStateMatch) errors.push("Web Forms guided setup is missing the #803 ancestry claim state");
  if (!stateMatch) errors.push("Web Forms implementation state is missing its independently recorded exact base");
  if (!recordedRepairStateMatch) errors.push("Web Forms implementation state is missing its independently recorded #803 ancestry state");
  if (!pageMatch || !repairStateMatch || !stateMatch || !recordedRepairStateMatch) return;

  const boundary = pageMatch[1].toLowerCase();
  if (boundary !== stateMatch[1].toLowerCase()) {
    errors.push("Web Forms page boundary does not match the independently recorded implementation base");
    return;
  }
  const fullHistoryAvailable = gitCommitExists(repositoryRoot, boundary);
  const boundaryVerified = fullHistoryAvailable && gitIsAncestor(repositoryRoot, boundary, "HEAD");
  if (!boundaryVerified) {
    errors.push("Web Forms implementation base is not a verified ancestor of the validation checkout");
    return;
  }
  const recordedRepairState = recordedRepairStateMatch[1].toLowerCase();
  let expectedState = recordedRepairState;
  if (fullHistoryAvailable && gitCommitExists(repositoryRoot, repair803Sha)) {
    expectedState = gitIsAncestor(repositoryRoot, repair803Sha, boundary) ? "shipped" : "not-shipped";
    if (recordedRepairState !== expectedState) {
      errors.push(`Web Forms independently recorded #803 ancestry must be ${expectedState} for the recorded implementation base`);
    }
  } else if (recordedRepairState !== "not-shipped") {
    errors.push("Web Forms validation cannot verify an affirmative #803 shipped claim without the repair commit");
    expectedState = "not-shipped";
  }
  if (repairStateMatch[1].toLowerCase() !== expectedState) {
    errors.push(`Web Forms #803 ancestry claim must be ${expectedState} for the recorded implementation base`);
  }
}

function gitCommitExists(repositoryRoot, sha) {
  return spawnSync("git", ["cat-file", "-e", `${sha}^{commit}`], { cwd: repositoryRoot, stdio: "ignore" }).status === 0;
}

function gitIsAncestor(repositoryRoot, ancestor, descendant) {
  return spawnSync("git", ["merge-base", "--is-ancestor", ancestor, descendant], { cwd: repositoryRoot, stdio: "ignore" }).status === 0;
}

function stripHtmlComments(value) {
  return String(value).replace(/<!--[\s\S]*?-->/g, " ");
}

function activeAnchorHrefs(value) {
  const hrefs = new Set();
  const html = stripHtmlComments(value);
  for (const match of html.matchAll(/<a\b[^>]*\bhref\s*=\s*(["'])(.*?)\1/gi)) hrefs.add(decodeEntities(match[2]));
  return hrefs;
}

function normalizeAttributeValues(value) {
  const values = [];
  for (const tag of stripHtmlComments(value).matchAll(/<[^>]+>/g)) {
    for (const attribute of tag[0].matchAll(/\b[\w:-]+\s*=\s*(["'])(.*?)\1/g)) values.push(decodeEntities(attribute[2]));
  }
  return values.join(" ").replace(/\s+/g, " ").trim().toLowerCase();
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

function requireIncludes(value, expected, errors, label) {
  if (!value.includes(expected)) errors.push(`Web Forms guided setup is missing ${label}`);
}

function normalizeVisibleText(value) {
  const text = stripHtmlComments(value)
    .replace(/<script[\s\S]*?<\/script>/gi, " ")
    .replace(/<style[\s\S]*?<\/style>/gi, " ")
    .replace(/<[^>]+>/g, "");
  return decodeEntities(text)
    .replace(/\s+/g, " ")
    .trim()
    .toLowerCase();
}

async function safeRead(path, errors, label) {
  try {
    return await readFile(path, "utf8");
  } catch (error) {
    errors.push(`${label}: ${error.code ?? error.message}`);
    return "";
  }
}

function safeJson(raw, errors, label) {
  if (!raw) return null;
  try {
    return JSON.parse(raw);
  } catch (error) {
    errors.push(`${label} is malformed: ${error.message}`);
    return null;
  }
}
