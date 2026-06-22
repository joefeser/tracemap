import { readdir, readFile, stat } from "node:fs/promises";
import { dirname, extname, relative, resolve, sep } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

import { buildSite, topNavigationLinks } from "./build.mjs";
import { validateAdoptionPlaybookDist } from "./adoption-playbook.mjs";
import { validateBlogProofPathSeriesDist } from "./blog-proof-path-series.mjs";
import {
  validateDiscoveryDist,
  validateDiscoveryNotInSitemap,
  validateRobotsDiscoveryComment
} from "./discovery.mjs";
import { validateDeployAuditDist } from "./deploy-audit.mjs";
import { validateDemoEvidenceTrailDist } from "./demo-evidence-trail.mjs";
import { validateDemoRunbookDist } from "./demo-runbook.mjs";
import { validateEvidenceDecisionRecordDist } from "./evidence-decision-record.mjs";
import { validateEndpointReviewDist } from "./endpoint-review.mjs";
import { validateEvidencePacketExamplesDist } from "./evidence-packet-examples.mjs";
import { validateChangeReviewDist } from "./change-review.mjs";
import { validateGlossaryDist } from "./glossary.mjs";
import { validateIncidentCallDist } from "./incident-call.mjs";
import { validateIncidentEvidenceHandoffDist } from "./incident-evidence-handoff.mjs";
import { validateLegacyModernizationEvidenceMap } from "./legacy-modernization-evidence-map.mjs";
import { validateLegacyStorySafety } from "./legacy-story-safety.mjs";
import { validateManagerBriefDist } from "./manager-brief.mjs";
import { validateManagerDemoScriptDist } from "./manager-demo-script.mjs";
import { validateManagerFaqDist } from "./manager-faq.mjs";
import { validateOwnerFollowupMapDist } from "./owner-followup-map.mjs";
import { validateProofPathFaqDist } from "./proof-path-faq.mjs";
import { validateProofPathTourDist } from "./proof-path-tour.mjs";
import { validateProofSourceCatalogDist } from "./proof-source-catalog.mjs";
import { validateReviewerQuickstartDist } from "./reviewer-quickstart.mjs";
import { validateReviewPacketAssemblyDist } from "./review-packet-assembly.mjs";
import { validateReviewClaimChecklistDist } from "./review-claim-checklist.mjs";
import { validateReleaseReviewBoundaryDist } from "./release-review-boundary.mjs";
import { validateReviewRoomDist } from "./review-room.mjs";
import { validateRoadmapClaimLedgerDist } from "./roadmap-claim-ledger.mjs";
import { validateStaticTriageDist } from "./static-triage.mjs";
import { validateStaticVsRuntimeDist } from "./static-vs-runtime.mjs";
import { validateStakeholderObjectionGuideDist } from "./stakeholder-objection-guide.mjs";
import { validateStakeholderQuestionIndexDist } from "./stakeholder-question-index.mjs";
import { validateTeamEvidenceHandoffDist } from "./team-evidence-handoff.mjs";
import { validateTestPlanningHandoffDist } from "./test-planning-handoff.mjs";
import { validateDemoSummary } from "./validate-demo-summary.mjs";

const defaultRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const defaultBaseUrl = "https://tracemap.tools";
export async function validateSite(options = {}) {
  const { log = console.log, root = defaultRoot } = options;

  await buildSite({ log, root });
  await validateDemoSummary({ root });
  const legacyStoryResult = await validateLegacyStorySafety({ root });
  const legacyModernizationResult = await validateLegacyModernizationEvidenceMap({ root });
  const result = await validateDist({ root });

  log(
    `Validated ${result.htmlFileCount} HTML files, ${result.internalReferenceCount} internal references, ${result.sitemapUrlCount} sitemap URLs, ${legacyStoryResult.scannedFileCount} legacy story safety targets, and ${legacyModernizationResult.rowCount} legacy modernization evidence-map rows.`
  );

  return result;
}

export async function validateDist({ baseUrl = defaultBaseUrl, root = defaultRoot } = {}) {
  const dist = resolve(root, "dist");
  const errors = [];
  const normalizedBaseUrl = normalizeBaseUrl(baseUrl, errors);
  const files = await collectFiles(dist, errors);
  const htmlFiles = files.filter((file) => extname(file) === ".html");
  const sitemapPath = resolve(dist, "sitemap.xml");
  const robotsPath = resolve(dist, "robots.txt");

  await validateRequiredFile(sitemapPath, "sitemap.xml", errors);
  await validateRequiredFile(robotsPath, "robots.txt", errors);

  const sitemapUrls = await readSitemapUrls(sitemapPath, errors);
  validateDiscoveryNotInSitemap({ errors, sitemapUrls });
  if (normalizedBaseUrl) {
    await validateSitemapUrls({ baseUrl: normalizedBaseUrl, dist, errors, sitemapUrls });
  }

  const internalReferenceCount = normalizedBaseUrl
    ? await validateHtmlReferences({
        baseUrl: normalizedBaseUrl,
        dist,
        errors,
        htmlFiles
      })
    : 0;

  if (normalizedBaseUrl) {
    await validateRobotsSitemap({ baseUrl: normalizedBaseUrl, errors, robotsPath });
    await validateDiscoveryDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateDeployAuditDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateDemoEvidenceTrailDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateDemoRunbookDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateEvidenceDecisionRecordDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateEndpointReviewDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateEvidencePacketExamplesDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateChangeReviewDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateGlossaryDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateAdoptionPlaybookDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateBlogProofPathSeriesDist({ baseUrl: normalizedBaseUrl, dist, errors, root: resolve(root, "src") });
    await validateIncidentCallDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateIncidentEvidenceHandoffDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateManagerBriefDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateManagerDemoScriptDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateManagerFaqDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateOwnerFollowupMapDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateProofPathFaqDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateProofPathTourDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateProofSourceCatalogDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateReviewerQuickstartDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateReviewPacketAssemblyDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateReviewClaimChecklistDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateReleaseReviewBoundaryDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateReviewRoomDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateRoadmapClaimLedgerDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateStaticTriageDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateStaticVsRuntimeDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateStakeholderObjectionGuideDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateStakeholderQuestionIndexDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateTeamEvidenceHandoffDist({ baseUrl: normalizedBaseUrl, dist, errors });
    await validateTestPlanningHandoffDist({ baseUrl: normalizedBaseUrl, dist, errors });
  }

  await validateTopNavigation({ dist, errors, htmlFiles });

  if (errors.length > 0) {
    throw new Error(`Site validation failed:\n- ${errors.join("\n- ")}`);
  }

  return {
    htmlFileCount: htmlFiles.length,
    internalReferenceCount,
    sitemapUrlCount: sitemapUrls.length
  };
}

async function collectFiles(directory, errors) {
  const files = [];
  let entries;

  try {
    entries = await readdir(directory, { withFileTypes: true });
  } catch (error) {
    errors.push(`Unable to read generated output directory ${directory}: ${error.message}`);
    return files;
  }

  for (const entry of entries) {
    const path = resolve(directory, entry.name);

    if (entry.isDirectory()) {
      files.push(...(await collectFiles(path, errors)));
      continue;
    }

    if (entry.isFile()) {
      files.push(path);
    }
  }

  return files;
}

async function validateRequiredFile(path, label, errors) {
  if (!(await fileExists(path))) {
    errors.push(`Missing required generated file: ${label}`);
  }
}

async function readSitemapUrls(sitemapPath, errors) {
  if (!(await fileExists(sitemapPath))) {
    return [];
  }

  const sitemap = await readFile(sitemapPath, "utf8");
  const urls = [...sitemap.matchAll(/<loc>([^<]+)<\/loc>/g)].map((match) => decodeXml(match[1]));

  if (urls.length === 0) {
    errors.push("sitemap.xml does not contain any <loc> entries.");
  }

  return urls;
}

async function validateSitemapUrls({ baseUrl, dist, errors, sitemapUrls }) {
  const seen = new Set();

  for (const url of sitemapUrls) {
    if (seen.has(url)) {
      errors.push(`Duplicate sitemap URL: ${url}`);
      continue;
    }

    seen.add(url);

    const path = publicPathFromUrl(url, { baseUrl });
    if (!path) {
      errors.push(`Sitemap URL is not on ${baseUrl}: ${url}`);
      continue;
    }

    if (!(await publicPathExists(dist, path))) {
      errors.push(`Sitemap URL has no generated file: ${url}`);
    }
  }
}

async function validateHtmlReferences({ baseUrl, dist, errors, htmlFiles }) {
  let internalReferenceCount = 0;

  for (const file of htmlFiles) {
    const html = await readFile(file, "utf8");

    for (const reference of extractHtmlReferences(html)) {
      const path = resolveReference(reference, { baseUrl, file, dist });

      if (!path) {
        continue;
      }

      internalReferenceCount += 1;

      if (!(await publicPathExists(dist, path))) {
        errors.push(`${formatDistPath(dist, file)} references missing path: ${reference}`);
      }
    }
  }

  return internalReferenceCount;
}

async function validateRobotsSitemap({ baseUrl, errors, robotsPath }) {
  if (!(await fileExists(robotsPath))) {
    return;
  }

  const robots = await readFile(robotsPath, "utf8");
  const expected = `Sitemap: ${baseUrl}/sitemap.xml`;

  if (!robots.split(/\r?\n/).some((line) => line.trim() === expected)) {
    errors.push(`robots.txt must include "${expected}".`);
  }

  validateRobotsDiscoveryComment({ baseUrl, errors, robots });
}

async function validateTopNavigation({ dist, errors, htmlFiles }) {
  for (const file of htmlFiles) {
    const html = await readFile(file, "utf8");
    const links = extractTopNavLinks(html);
    const label = formatDistPath(dist, file);

    if (!links) {
      errors.push(`${label} is missing <nav class="top-nav">.`);
      continue;
    }

    if (!sameNavLinks(links, topNavigationLinks)) {
      errors.push(
        `${label} top navigation does not match the canonical links. Expected: ${formatNavLinks(
          topNavigationLinks
        )}. Found: ${formatNavLinks(links)}.`
      );
    }
  }
}

function extractTopNavLinks(html) {
  const nav = html.match(/<nav\b[^>]*class=["'][^"']*\btop-nav\b[^"']*["'][^>]*>([\s\S]*?)<\/nav>/);

  if (!nav) {
    return null;
  }

  return [...nav[1].matchAll(/<a\b([^>]*)>([\s\S]*?)<\/a>/g)].map((match) => ({
    href: getAttribute(match[1], "href") ?? "",
    text: normalizeHtmlText(match[2])
  }));
}

function getAttribute(attributes, name) {
  const match = attributes.match(new RegExp(`\\b${name}=["']([^"']*)["']`));
  return match ? decodeXml(match[1]) : null;
}

function normalizeHtmlText(value) {
  return decodeXml(value.replace(/<[^>]+>/g, " ").replace(/\s+/g, " ").trim());
}

function sameNavLinks(actual, expected) {
  if (actual.length !== expected.length) {
    return false;
  }

  return actual.every((link, index) => link.href === expected[index].href && link.text === expected[index].text);
}

function formatNavLinks(links) {
  return links.map((link) => `${link.text} (${link.href})`).join(", ");
}

function extractHtmlReferences(html) {
  return [...html.matchAll(/\b(?:href|src)=["']([^"']+)["']/g)].map((match) => match[1]);
}

function resolveReference(reference, { baseUrl, dist, file }) {
  if (
    reference === "" ||
    reference.startsWith("#") ||
    reference.startsWith("data:") ||
    reference.startsWith("mailto:") ||
    reference.startsWith("tel:") ||
    reference.startsWith("javascript:")
  ) {
    return null;
  }

  if (/^https?:\/\//.test(reference)) {
    return publicPathFromUrl(reference, { baseUrl });
  }

  if (reference.startsWith("//")) {
    return null;
  }

  if (reference.startsWith("/")) {
    return stripQueryAndHash(reference);
  }

  const fileRoute = `/${relative(dist, file).split(sep).join("/")}`;
  const url = new URL(reference, `${baseUrl}${fileRoute}`);
  return stripQueryAndHash(url.pathname);
}

function publicPathFromUrl(value, { baseUrl }) {
  let url;

  try {
    url = new URL(value);
  } catch {
    return null;
  }

  if (url.origin !== baseUrl) {
    return null;
  }

  return stripQueryAndHash(`${url.pathname}${url.search}${url.hash}`);
}

async function publicPathExists(dist, pathname) {
  const publicPath = stripQueryAndHash(pathname);
  const resolved = resolvePublicPath(dist, publicPath);

  if (resolved && (await fileExists(resolved))) {
    return true;
  }

  if (!publicPath.endsWith("/")) {
    const indexPath = resolvePublicPath(dist, `${publicPath}/`);
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

  const filePath = decoded.endsWith("/")
    ? resolve(dist, `.${decoded}`, "index.html")
    : resolve(dist, `.${decoded}`);
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

function stripQueryAndHash(value) {
  return value.split("#", 1)[0].split("?", 1)[0];
}

function normalizeBaseUrl(value, errors) {
  let url;

  try {
    url = new URL(value);
  } catch {
    errors.push(`baseUrl must be a valid absolute URL: ${value}`);
    return null;
  }

  if (url.protocol !== "https:" && url.protocol !== "http:") {
    errors.push(`baseUrl must use http or https: ${value}`);
    return null;
  }

  return url.origin;
}

function decodeXml(value) {
  return value
    .replaceAll("&amp;", "&")
    .replaceAll("&lt;", "<")
    .replaceAll("&gt;", ">")
    .replaceAll("&quot;", '"')
    .replaceAll("&apos;", "'");
}

function formatDistPath(dist, file) {
  return relative(dist, file).split(sep).join("/");
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  await validateSite();
}
