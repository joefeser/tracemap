import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import { decodeHtmlEntities, escapeRegExp, fileExists, normalizeRenderedText, readSitemapLocSet } from "./validate-utils.mjs";

export const sqlOperatorHandoffRoute = "/sql/operator-handoff/";
export const sqlOperatorHandoffInboundRoutes = [
  "/manager-packet/",
  "/outputs/",
  "/limitations/",
  "/proof-paths/for-managers/",
  "/packets/"
];

const requiredText = [
  "Public claim level: demo",
  "Before anyone runs the scripts",
  "wrong-tab and wrong-database review checkpoint",
  "postgres_fdw",
  "dblink",
  "Logical replication",
  "pg_cron",
  "present-in-scripts",
  "missing-evidence",
  "conflicting-evidence",
  "needs-owner-review",
  "validation-step-present",
  "validation-evidence-not-provided",
  "Stop conditions and owner questions",
  "database.sql.context.declaration.v1",
  "database.sql.secret-bearing-step.v1",
  "database.postgres.permission.coverage.v1",
  "database.postgres.archive-link.v1",
  "Tier2Structural",
  "sql-execution-context/0.1.0"
];

const forbiddenPatterns = [
  /(?:\/Users\/|\/home\/|[A-Z]:\\Users\\)/i,
  /\b(?:Server|Password|User Id)\s*=/i,
  /\b(?:SELECT\s+.+\s+FROM|INSERT\s+INTO|UPDATE\s+\w+\s+SET|DELETE\s+FROM|CREATE\s+(?:SERVER|USER\s+MAPPING|SUBSCRIPTION))\b/i,
  /\b(?:safe to run|setup succeeded|permissions are effective|replication is healthy|validation passed|rollback worked)\b/i,
  /\b(?:private-host|private-password|ticket-[0-9]+)\b/i
];

export async function validateSqlOperatorHandoffDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const pagePath = resolve(dist, "sql", "operator-handoff", "index.html");
  if (!(await fileExists(pagePath))) {
    errors.push("SQL operator handoff page is missing required route: /sql/operator-handoff/");
    return;
  }

  const html = await readFile(pagePath, "utf8");
  const text = normalizeRenderedText(html);
  const decoded = decodeHtmlEntities(html);
  const tagCollapsedText = decoded.replace(/<[^>]*>/g, "");
  for (const phrase of requiredText) {
    if (!text.includes(phrase) && !decoded.includes(phrase)) errors.push(`SQL operator handoff page is missing required text: ${phrase}`);
  }
  for (const pattern of forbiddenPatterns) {
    if (pattern.test(`${decoded} ${text} ${tagCollapsedText}`)) errors.push(`SQL operator handoff page contains forbidden private, executable, or overclaim text: ${pattern}`);
  }
  if (!/<meta\b(?=[^>]*\bproperty\s*=\s*["']og:type["'])(?=[^>]*\bcontent\s*=\s*["']article["'])[^>]*>/i.test(html)) {
    errors.push("SQL operator handoff page must use article social metadata.");
  }
  if (!/<link\b(?=[^>]*\brel\s*=\s*["']canonical["'])(?=[^>]*\bhref\s*=\s*["']https:\/\/tracemap\.tools\/sql\/operator-handoff\/["'])[^>]*>/i.test(html)) {
    errors.push("SQL operator handoff canonical URL is missing or incorrect.");
  }

  for (const route of sqlOperatorHandoffInboundRoutes) {
    const inboundPath = resolve(dist, route.slice(1), "index.html");
    if (!(await fileExists(inboundPath)) || !hasHref(await readFile(inboundPath, "utf8"), sqlOperatorHandoffRoute)) {
      errors.push(`Required inbound route ${route} does not link to /sql/operator-handoff/.`);
    }
  }

  const sitemapPath = resolve(dist, "sitemap.xml");
  if (!(await fileExists(sitemapPath))) errors.push("SQL operator handoff validation requires sitemap.xml.");
  else {
    const sitemap = await readSitemapLocSet(sitemapPath);
    if (!sitemap.has(`${new URL(baseUrl).origin}/sql/operator-handoff/`)) errors.push("Sitemap is missing /sql/operator-handoff/.");
  }

  const routesPath = resolve(dist, "routes-index.json");
  let routes = {};
  if (!(await fileExists(routesPath))) errors.push("SQL operator handoff validation requires routes-index.json.");
  else {
    try {
      routes = JSON.parse(await readFile(routesPath, "utf8"));
    } catch (error) {
      errors.push(`SQL operator handoff could not parse routes-index.json: ${error.message}`);
    }
  }
  const entry = Array.isArray(routes.entries)
    ? routes.entries.find((item) => item.path === sqlOperatorHandoffRoute)
    : undefined;
  if (!entry) errors.push("routes-index.json is missing /sql/operator-handoff/.");
  else {
    if (entry.publicClaimLevel !== "demo") errors.push(`SQL operator handoff publicClaimLevel must be demo, got ${entry.publicClaimLevel}.`);
    if (entry.sourceType !== "site-page") errors.push(`SQL operator handoff sourceType must be site-page, got ${entry.sourceType}.`);
    if (entry.preferredProofPath !== "/manager-packet/") errors.push(`SQL operator handoff preferredProofPath must be /manager-packet/, got ${entry.preferredProofPath}.`);
  }
}

function hasHref(html, route) {
  return new RegExp(`\\bhref\\s*=\\s*(["'])${escapeRegExp(route)}\\1`, "i").test(html);
}
