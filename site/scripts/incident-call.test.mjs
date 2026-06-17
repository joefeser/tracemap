import assert from "node:assert/strict";
import { mkdir, mkdtemp, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import { incidentCallRoute, validateIncidentCallDist } from "./incident-call.mjs";

test("validateIncidentCallDist accepts the incident call route", async () => {
  const root = await createIncidentCallDistFixture();
  const errors = [];

  await validateIncidentCallDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateIncidentCallDist reports missing required page text", async () => {
  const root = await createIncidentCallDistFixture({
    incidentCallHtml: page("<p>Incident call placeholder.</p>")
  });
  const errors = [];

  await validateIncidentCallDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: concept/);
});

test("validateIncidentCallDist reports missing route metadata", async () => {
  const root = await createIncidentCallDistFixture({
    sitemapRoutes: [],
    discoveryRoutes: []
  });
  const errors = [];

  await validateIncidentCallDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/incident-call\//);
});

test("validateIncidentCallDist rejects encoded private text", async () => {
  const root = await createIncidentCallDistFixture({
    incidentCallHtml: incidentCallPage("<p>file&#58;//private/report</p>")
  });
  const errors = [];

  await validateIncidentCallDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /contains forbidden public text: file:\/\//);
});

async function createIncidentCallDistFixture({
  discoveryRoutes = [incidentCallRoute],
  incidentCallHtml = incidentCallPage(),
  sitemapRoutes = [incidentCallRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-incident-call-test-"));
  const dist = join(root, "dist");
  const routes = new Set([incidentCallRoute, ...incidentRequiredLinks()]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(join(path, "index.html"), route === incidentCallRoute ? incidentCallHtml : page(route), "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: `Route ${route}`,
    summary: "Fixture route for incident call validation.",
    publicClaimLevel: route === incidentCallRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === incidentCallRoute ? "use-case" : "evidence",
    limitations: ["Fixture limitations remain bounded."],
    nonClaims: ["No runtime behavior, production usage, deployment state, endpoint performance, or release approval proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${routes.map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`).join("\n")}
</urlset>`;
}

function incidentRequiredLinks() {
  return ["/proof-paths/", "/validation/", "/docs/", "/limitations/", "/demo/result/", "/use-cases/incident-review/"];
}

function page(body) {
  return `<!doctype html><html><body><main>${body}</main></body></html>`;
}

function incidentCallPage(extra = "") {
  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>static dependency evidence and not runtime observability</p>
    <p>not operational approval</p>
    <p>P1-call orientation and incident review are related, not identical</p>
    ${incidentRequiredLinks().map((route) => `<a href="${route}">${route}</a>`).join("\n")}
    ${extra}
  `);
}
