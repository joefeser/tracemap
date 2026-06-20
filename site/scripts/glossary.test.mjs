import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import { glossaryRequiredLinks, glossaryRoute, validateGlossaryDist } from "./glossary.mjs";

test("validateGlossaryDist accepts a complete glossary route", async (t) => {
  const root = await createManagedGlossaryDistFixture(t);
  const errors = [];

  await validateGlossaryDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateGlossaryDist reports route metadata regressions", async (t) => {
  const root = await createManagedGlossaryDistFixture(t);
  await rewriteGlossaryRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "roadmap",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateGlossaryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory evidence, got roadmap/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
});

test("validateGlossaryDist accepts page metadata attributes in any order", async (t) => {
  const glossaryHtml = (await sourceGlossaryPage())
    .replace(
      '<link rel="canonical" href="https://tracemap.tools/glossary/">',
      '<link href="https://tracemap.tools/glossary/" rel="canonical">'
    )
    .replace(
      '<meta property="og:title" content="TraceMap Evidence Glossary">',
      '<meta content="TraceMap Evidence Glossary" property="og:title">'
    )
    .replace(
      '<meta name="tracemap:public-claim-level" content="concept">',
      '<meta content="concept" name="tracemap:public-claim-level">'
    );
  const root = await createManagedGlossaryDistFixture(t, { glossaryHtml });
  const errors = [];

  await validateGlossaryDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateGlossaryDist rejects missing required terms", async (t) => {
  const glossaryHtml = (await sourceGlossaryPage()).replace('id="rule-id"', 'id="rule-id-missing"');
  const root = await createManagedGlossaryDistFixture(t, { glossaryHtml });
  const errors = [];

  await validateGlossaryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required term anchor: rule-id/);
});

test("validateGlossaryDist rejects forbidden affirmative AI positioning outside sanctioned sections", async (t) => {
  const glossaryHtml = (await sourceGlossaryPage()).replace(
    "Stable words for bounded TraceMap claims.",
    "TraceMap provides AI impact analysis for bounded claims."
  );
  const root = await createManagedGlossaryDistFixture(t, { glossaryHtml });
  const errors = [];

  await validateGlossaryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden affirmative positioning outside sanctioned sections: AI impact analysis/);
});

test("validateGlossaryDist rejects private and raw material outside sanctioned sections", async (t) => {
  const glossaryHtml = (await sourceGlossaryPage()).replace(
    "The glossary is a vocabulary map, not a new source of proof.",
    "The glossary is a vocabulary map with facts.ndjson, not a new source of proof."
  );
  const root = await createManagedGlossaryDistFixture(t, { glossaryHtml });
  const errors = [];

  await validateGlossaryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden private\/raw material outside sanctioned sections: facts\.ndjson/);
});

test("validateGlossaryDist rejects missing required public-safe links", async (t) => {
  const glossaryHtml = (await sourceGlossaryPage()).replaceAll('href="/proof-source-catalog/"', 'href="/proof-source-catalog-missing/"');
  const root = await createManagedGlossaryDistFixture(t, { glossaryHtml });
  const errors = [];

  await validateGlossaryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/proof-source-catalog\//);
});

async function createManagedGlossaryDistFixture(t, options = {}) {
  const root = await createGlossaryDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createGlossaryDistFixture({
  glossaryHtml,
  discoveryRoutes = [glossaryRoute],
  sitemapRoutes = [glossaryRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-glossary-test-"));
  const dist = join(root, "dist");
  const routes = new Set([glossaryRoute, ...glossaryRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(join(path, "index.html"), route === glossaryRoute ? glossaryHtml ?? (await sourceGlossaryPage()) : page(route), "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === glossaryRoute ? "Evidence Glossary" : `Route ${route}`,
    summary:
      route === glossaryRoute
        ? "Concept-level vocabulary for public-safe TraceMap evidence terms before readers repeat public claims."
        : "Fixture route for glossary validation.",
    publicClaimLevel: route === glossaryRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: "evidence",
    ...(route === glossaryRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations:
      route === glossaryRoute
        ? [
            "Glossary definitions are vocabulary guidance, not scanner or reducer coverage evidence.",
            "Terms must stay attached to route-specific proof paths, coverage labels, and limitations."
          ]
        : ["Fixture limitations remain bounded."],
    nonClaims:
      route === glossaryRoute
        ? [
            "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, AI impact analysis, LLM analysis, or complete product coverage proof.",
            "No raw artifact publication, raw facts, raw SQLite indexes, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, or hidden validation details."
          ]
        : ["No runtime behavior or production usage proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteGlossaryRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === glossaryRoute
      ? {
          ...entry,
          ...fields
        }
      : entry
  );
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

async function sourceGlossaryPage() {
  return readFile(new URL("../src/glossary/index.html", import.meta.url), "utf8");
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
