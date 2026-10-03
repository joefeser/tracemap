import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

const route = "/webforms/";
const requiredText = [
  "terminal wizard",
  "not a gui",
  "website folder",
  "solution",
  ".csproj",
  ".vbproj",
  "projectless web site",
  "explicit selection",
  "forms.txt",
  "code 2",
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
  "https://github.com/joefeser/tracemap/blob/main/docs/WEBFORMS_NATIVE_WORKFLOW.md"
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

export async function validateWebformsGuidedSetupDist({ baseUrl, dist, errors, root }) {
  const pagePath = resolve(dist, "webforms/index.html");
  const articlePath = resolve(dist, "blog/modernizing-web-forms-without-running-it/index.html");
  const discoveryPath = resolve(root, "src/_site/discovery.json");
  const pagesPath = resolve(root, "src/_site/pages.json");
  const sitemapPath = resolve(dist, "sitemap.xml");

  const [page, article, discoveryRaw, pagesRaw, sitemap] = await Promise.all([
    safeRead(pagePath, errors, "Web Forms guided setup route is missing"),
    safeRead(articlePath, errors, "Web Forms concept article is missing"),
    safeRead(discoveryPath, errors, "Web Forms discovery metadata is missing"),
    safeRead(pagesPath, errors, "Web Forms page registry is missing"),
    safeRead(sitemapPath, errors, "Web Forms sitemap is missing")
  ]);
  if (!page) return;

  const visible = normalizeVisibleText(page);
  requireIncludes(page, '<link rel="canonical" href="https://tracemap.tools/webforms/">', errors, "canonical metadata");
  requireIncludes(page, '<meta property="og:url" content="https://tracemap.tools/webforms/">', errors, "Open Graph URL");
  requireIncludes(page, 'data-main-boundary="5ffd4a54176c002e4c6d41ce0133eab5963ad79b"', errors, "exact implementation base");
  requireIncludes(page, "Public claim level:", errors, "public claim label");
  requireIncludes(page, "<strong>shipped</strong>", errors, "shipped workflow label");
  requireIncludes(page, "<strong>demo</strong>", errors, "demo proof label");
  requireIncludes(page, 'data-tm-boundary="claim-boundary"', errors, "claim boundary marker");

  for (const text of requiredText) {
    if (!visible.includes(text)) errors.push(`Web Forms guided setup is missing required concept: ${text}`);
  }
  for (const href of requiredLinks) {
    if (!page.includes(`href="${href}"`)) errors.push(`Web Forms guided setup is missing required link: ${href}`);
  }
  for (const pattern of [...forbiddenAffirmativeClaims, ...forbiddenMaterial]) {
    if (pattern.test(visible)) errors.push(`Web Forms guided setup contains forbidden public material or claim: ${pattern}`);
  }

  if (article && !article.includes('href="/webforms/"')) {
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
  if (!Array.isArray(pages) || !pages.some((item) => item?.path === route)) {
    errors.push("Web Forms guided setup page registry entry is missing");
  }
}

function requireIncludes(value, expected, errors, label) {
  if (!value.includes(expected)) errors.push(`Web Forms guided setup is missing ${label}`);
}

function normalizeVisibleText(value) {
  return String(value)
    .replace(/<!--[\s\S]*?-->/g, " ")
    .replace(/<script[\s\S]*?<\/script>/gi, " ")
    .replace(/<style[\s\S]*?<\/style>/gi, " ")
    .replace(/<[^>]+>/g, "")
    .replace(/&#(\d+);?/g, (_, code) => String.fromCodePoint(Number(code)))
    .replace(/&#x([0-9a-f]+);?/gi, (_, code) => String.fromCodePoint(Number.parseInt(code, 16)))
    .replace(/&(?:nbsp|Tab|NewLine);?/gi, " ")
    .replace(/&amp;/gi, "&")
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
