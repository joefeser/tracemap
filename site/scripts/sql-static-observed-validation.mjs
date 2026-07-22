import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import { decodeHtmlEntities, escapeRegExp, fileExists, normalizeRenderedText, readSitemapLocSet, stripTagsQuoteAware } from "./validate-utils.mjs";

export const sqlStaticObservedValidationRoute = "/sql/operator-handoff/validation/";

const requiredText = [
  "Public claim level: demo",
  "Static repository evidence",
  "Observed validation summary",
  "sql-validation-summary/v1",
  "observed-pass",
  "observed-fail",
  "observed-indeterminate",
  "not-run",
  "repository and commit",
  "canonical digest",
  "rule-backed gaps",
  "Human decision"
];

const forbiddenPatterns = [
  /(?:\/Users\/|\/home\/|[A-Z]:\\Users\\)/i,
  /\b(?:Server|Password|User Id)\s*=/i,
  /\b(?:SELECT\s+.+\s+FROM|INSERT\s+INTO|UPDATE\s+\w+\s+SET|DELETE\s+FROM|CREATE\s+(?:SERVER|DATABASE|EXTENSION))\b/i,
  /\b(?:safe to run|validation passed|migration succeeded|job ran|permissions are effective|release approved)\b/i,
  /\b(?:private-host|private-password|ticket-[0-9]+)\b/i
];

export async function validateSqlStaticObservedValidationDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const pagePath = resolve(dist, "sql", "operator-handoff", "validation", "index.html");
  if (!(await fileExists(pagePath))) {
    errors.push(`SQL static/observed validation page is missing required route: ${sqlStaticObservedValidationRoute}`);
    return;
  }
  const html = await readFile(pagePath, "utf8");
  const decoded = decodeHtmlEntities(html);
  const text = normalizeRenderedText(html);
  const collapsed = decodeHtmlEntities(stripTagsQuoteAware(html)).replace(/\s+/g, "");
  for (const phrase of requiredText) {
    if (!text.includes(phrase) && !decoded.includes(phrase)) errors.push(`SQL static/observed validation page is missing required text: ${phrase}`);
  }
  for (const pattern of forbiddenPatterns) {
    if (pattern.test(`${decoded} ${text} ${collapsed}`)) errors.push(`SQL static/observed validation page contains forbidden private, executable, or overclaim text: ${pattern}`);
  }
  if (!/<link\b(?=[^>]*\brel\s*=\s*["']canonical["'])(?=[^>]*\bhref\s*=\s*["']https:\/\/tracemap\.tools\/sql\/operator-handoff\/validation\/["'])[^>]*>/i.test(html)) {
    errors.push("SQL static/observed validation canonical URL is missing or incorrect.");
  }
  for (const relative of ["sql/operator-handoff/index.html", "sql/operator-handoff/proof-packet/index.html"]) {
    const inbound = resolve(dist, relative);
    if (!(await fileExists(inbound)) || !hasHref(await readFile(inbound, "utf8"), sqlStaticObservedValidationRoute)) {
      errors.push(`Required inbound page ${relative} does not link to ${sqlStaticObservedValidationRoute}.`);
    }
  }
  const sitemap = await readSitemapLocSet(resolve(dist, "sitemap.xml"));
  if (!sitemap.has(`${new URL(baseUrl).origin}${sqlStaticObservedValidationRoute}`)) errors.push(`Sitemap is missing ${sqlStaticObservedValidationRoute}.`);
  try {
    const routes = JSON.parse(await readFile(resolve(dist, "routes-index.json"), "utf8"));
    const entry = Array.isArray(routes.entries) ? routes.entries.find((item) => item.path === sqlStaticObservedValidationRoute) : undefined;
    if (!entry) errors.push(`routes-index.json is missing ${sqlStaticObservedValidationRoute}.`);
    else {
      if (entry.publicClaimLevel !== "demo") errors.push("SQL static/observed validation publicClaimLevel must be demo.");
      if (entry.preferredProofPath !== "/sql/operator-handoff/proof-packet/") errors.push("SQL static/observed validation preferredProofPath is incorrect.");
    }
  } catch (error) {
    errors.push(`SQL static/observed validation could not parse routes-index.json: ${error.message}`);
  }
}

function hasHref(html, href) {
  const escaped = escapeRegExp(href);
  return new RegExp(`<a\\b[^>]*\\bhref\\s*=\\s*["']${escaped}["']`, "i").test(html);
}
