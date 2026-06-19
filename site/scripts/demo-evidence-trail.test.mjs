import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  demoEvidenceTrailRequiredLinks,
  demoEvidenceTrailRoute,
  validateDemoEvidenceTrailDist
} from "./demo-evidence-trail.mjs";

test("validateDemoEvidenceTrailDist accepts the demo evidence trail route", async (t) => {
  const root = await createManagedDemoEvidenceTrailDistFixture(t);
  const errors = [];

  await validateDemoEvidenceTrailDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateDemoEvidenceTrailDist reports missing required text", async (t) => {
  const root = await createManagedDemoEvidenceTrailDistFixture(t, {
    pageHtml: page("<p>Evidence trail placeholder.</p>")
  });
  const errors = [];

  await validateDemoEvidenceTrailDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: demo/);
});

test("validateDemoEvidenceTrailDist reports missing route metadata", async (t) => {
  const root = await createManagedDemoEvidenceTrailDistFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateDemoEvidenceTrailDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/demo\/evidence-trail\//);
});

test("validateDemoEvidenceTrailDist reports route metadata regressions", async (t) => {
  const root = await createManagedDemoEvidenceTrailDistFixture(t);
  await rewriteDemoEvidenceTrailRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "concept",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateDemoEvidenceTrailDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel demo, got concept/);
  assert.match(errors.join("\n"), /expected hintCategory demo, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/demo\/proof-upgrades\/, got \/validation\//);
});

test("validateDemoEvidenceTrailDist rejects forbidden positioning", async (t) => {
  const root = await createManagedDemoEvidenceTrailDistFixture(t, {
    pageHtml: demoEvidenceTrailPage("<p>AI-powered evidence trail.</p>")
  });
  const errors = [];

  await validateDemoEvidenceTrailDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden AI\/LLM positioning/);
});

test("validateDemoEvidenceTrailDist rejects banned impacted wording", async (t) => {
  const root = await createManagedDemoEvidenceTrailDistFixture(t, {
    pageHtml: demoEvidenceTrailPage("<p>This changed surface is impacted.</p>")
  });
  const errors = [];

  await validateDemoEvidenceTrailDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /contains banned word: impacted/);
});

test("validateDemoEvidenceTrailDist rejects banned impacted wording in metadata", async (t) => {
  const root = await createManagedDemoEvidenceTrailDistFixture(t, {
    pageHtml: demoEvidenceTrailPage('<meta name="description" content="impacted surface">')
  });
  const errors = [];

  await validateDemoEvidenceTrailDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /contains banned word: impacted/);
});

test("validateDemoEvidenceTrailDist rejects missing downstream markers", async (t) => {
  const root = await createManagedDemoEvidenceTrailDistFixture(t, {
    pageHtml: demoEvidenceTrailPage("", { includeSqlMarker: false })
  });
  const errors = [];

  await validateDemoEvidenceTrailDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing surface marker: sql-facing/);
  assert.match(errors.join("\n"), /missing coverage-gap marker: sql-facing/);
});

test("validateDemoEvidenceTrailDist rejects encoded private text", async (t) => {
  const root = await createManagedDemoEvidenceTrailDistFixture(t, {
    pageHtml: demoEvidenceTrailPage("<p>file&#58;//private/report</p>")
  });
  const errors = [];

  await validateDemoEvidenceTrailDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /contains forbidden public text: file:\/\//);
});

test("validateDemoEvidenceTrailDist rejects HTML5 named entity private text", async (t) => {
  const root = await createManagedDemoEvidenceTrailDistFixture(t, {
    pageHtml: demoEvidenceTrailPage("<p>file&colon;&sol;&sol;private/report</p>")
  });
  const errors = [];

  await validateDemoEvidenceTrailDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /contains forbidden public text: file:\/\//);
});

async function createManagedDemoEvidenceTrailDistFixture(t, options = {}) {
  const root = await createDemoEvidenceTrailDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createDemoEvidenceTrailDistFixture({
  discoveryRoutes = [demoEvidenceTrailRoute],
  pageHtml = demoEvidenceTrailPage(),
  sitemapRoutes = [demoEvidenceTrailRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-demo-evidence-trail-test-"));
  const dist = join(root, "dist");
  const routes = new Set([demoEvidenceTrailRoute, ...demoEvidenceTrailRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(join(path, "index.html"), route === demoEvidenceTrailRoute ? pageHtml : page(route), "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === demoEvidenceTrailRoute ? "Demo Evidence Trail" : `Route ${route}`,
    summary: "Fixture route for demo evidence trail validation.",
    publicClaimLevel: "demo",
    sourceType: "site-page",
    hintCategory: "demo",
    ...(route === demoEvidenceTrailRoute ? { preferredProofPath: "/demo/proof-upgrades/" } : {}),
    limitations: ["Fixture limitations remain bounded."],
    nonClaims: ["No runtime behavior, production usage, deployment state, endpoint performance, or release approval proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteDemoEvidenceTrailRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === demoEvidenceTrailRoute
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

function page(body) {
  return `<!doctype html><html><head><meta property="og:type" content="article"></head><body><main>${body}</main></body></html>`;
}

function demoEvidenceTrailPage(extra = "", { includeSqlMarker = true } = {}) {
  const links = demoEvidenceTrailRequiredLinks.map((route) => `<a href="${route}">${route}</a>`).join("\n");
  const sqlMarker = includeSqlMarker
    ? '<article data-trail-surface-type="sql-facing" data-trail-gap="sql-facing"><h3>SQL-facing evidence</h3></article>'
    : "";
  const filler = Array.from({ length: 75 }, (_, index) => `demo evidence trail boundary ${index}`).join(" ");

  return page(`
    <p>Public claim level: demo</p>
    <p>No public conclusion without evidence</p>
    <p>What static evidence connects a changed demo surface to a route and downstream surfaces?</p>
    <p>This is the same evidence packet made easier to follow, not stronger.</p>
    <p>site/src/_data/demo-public-summary.json public.demo.summary.v1 Tier2Structural Tier4Unknown PartialAnalysis</p>
    <p>12 changed demo surfaces 14 endpoint findings 12 paths 25 reverse paths 37 path gaps</p>
    <article data-trail-surface-type="package" data-trail-gap="package"><h3>Package evidence</h3><p>missing-public-item</p></article>
    <article data-trail-surface-type="config" data-trail-gap="config"><h3>Configuration evidence</h3><p>missing-public-item</p></article>
    ${sqlMarker}
    <p>runtime proof production proof release approval complete product coverage</p>
    ${links}
    <p>${filler}</p>
    ${extra}
  `);
}
