import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import { incidentCallRequiredLinks, incidentCallRoute, validateIncidentCallDist } from "./incident-call.mjs";

test("validateIncidentCallDist accepts the incident call route", async (t) => {
  const root = await createManagedIncidentCallDistFixture(t);
  const errors = [];

  await validateIncidentCallDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateIncidentCallDist accepts href spacing around assignment", async (t) => {
  const root = await createManagedIncidentCallDistFixture(t, {
    incidentCallHtml: incidentCallPage("", { spacedHref: true })
  });
  const errors = [];

  await validateIncidentCallDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateIncidentCallDist reports missing required page text", async (t) => {
  const root = await createManagedIncidentCallDistFixture(t, {
    incidentCallHtml: page("<p>Incident call placeholder.</p>")
  });
  const errors = [];

  await validateIncidentCallDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: concept/);
});

test("validateIncidentCallDist reports missing route metadata", async (t) => {
  const root = await createManagedIncidentCallDistFixture(t, {
    sitemapRoutes: [],
    discoveryRoutes: []
  });
  const errors = [];

  await validateIncidentCallDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/incident-call\//);
});

test("validateIncidentCallDist reports route metadata regressions", async (t) => {
  const root = await createManagedIncidentCallDistFixture(t);
  await rewriteIncidentCallRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateIncidentCallDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
});

test("validateIncidentCallDist rejects encoded private text", async (t) => {
  const root = await createManagedIncidentCallDistFixture(t, {
    incidentCallHtml: incidentCallPage("<p>file&#58;//private/report</p>")
  });
  const errors = [];

  await validateIncidentCallDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /contains forbidden public text: file:\/\//);
});

async function createManagedIncidentCallDistFixture(t, options = {}) {
  const root = await createIncidentCallDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

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
    ...(route === incidentCallRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: ["Fixture limitations remain bounded."],
    nonClaims: ["No runtime behavior, production usage, deployment state, endpoint performance, or release approval proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteIncidentCallRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === incidentCallRoute
      ? {
          ...entry,
          ...fields
        }
      : entry
  );
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${routes.map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`).join("\n")}
</urlset>`;
}

function incidentRequiredLinks() {
  return incidentCallRequiredLinks;
}

function page(body) {
  return `<!doctype html><html><body><main>${body}</main></body></html>`;
}

function incidentCallPage(extra = "", { spacedHref = false } = {}) {
  const href = (route) => (spacedHref ? `<a href = "${route}">${route}</a>` : `<a href="${route}">${route}</a>`);

  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>static dependency evidence and not runtime observability</p>
    <p>not operational approval</p>
    <p>P1-call orientation and incident review are related, not identical</p>
    <p>static triage checklist</p>
    ${incidentRequiredLinks().map((route) => href(route)).join("\n")}
    ${extra}
  `);
}
