import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  proofSourceCatalogRequiredLinks,
  proofSourceCatalogRoute,
  validateProofSourceCatalogDist
} from "./proof-source-catalog.mjs";

test("validateProofSourceCatalogDist accepts a complete catalog route", async (t) => {
  const root = await createManagedProofSourceCatalogDistFixture(t);
  const errors = [];

  await validateProofSourceCatalogDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateProofSourceCatalogDist reports route metadata regressions", async (t) => {
  const root = await createManagedProofSourceCatalogDistFixture(t);
  await rewriteProofSourceCatalogRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "concept",
    hintCategory: "roadmap",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateProofSourceCatalogDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel demo, got concept/);
  assert.match(errors.join("\n"), /expected hintCategory evidence, got roadmap/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
});

test("validateProofSourceCatalogDist rejects invalid row anchors", async (t) => {
  const root = await createManagedProofSourceCatalogDistFixture(t, {
    catalogHtml: catalogPage().replace(
      'id="proof-source-docs-repository-doc-navigation"',
      'id="proof-source-docs-wrong-anchor"'
    )
  });
  const errors = [];

  await validateProofSourceCatalogDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /row anchor mismatch/);
});

test("validateProofSourceCatalogDist rejects not-yet-backed published rows", async (t) => {
  const root = await createManagedProofSourceCatalogDistFixture(t, {
    catalogHtml: catalogPage().replace(
      '<td data-field="evidenceStatus"><code>source-backed</code></td>',
      '<td data-field="evidenceStatus"><code>not-yet-backed</code></td>'
    )
  });
  const errors = [];

  await validateProofSourceCatalogDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /invalid published evidence status: not-yet-backed/);
});

test("validateProofSourceCatalogDist rejects hidden proof-path prose", async (t) => {
  const root = await createManagedProofSourceCatalogDistFixture(t, {
    catalogHtml: catalogPage().replace(
      '<td data-field="proofPath"><code>hidden</code></td>',
      '<td data-field="proofPath">hidden internal-only proof</td>'
    )
  });
  const errors = [];

  await validateProofSourceCatalogDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected proofPath hidden/);
});

test("validateProofSourceCatalogDist rejects non-link proofPath cells even when route cell links", async (t) => {
  const root = await createManagedProofSourceCatalogDistFixture(t, {
    catalogHtml: catalogPage().replace(
      '<td data-field="proofPath"><a href="/docs/">/docs/</a></td>',
      '<td data-field="proofPath">See docs</td>'
    )
  });
  const errors = [];

  await validateProofSourceCatalogDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /proofPath must be a public-safe link or allowed sentinel/);
});

test("validateProofSourceCatalogDist rejects forbidden affirmative wording", async (t) => {
  const root = await createManagedProofSourceCatalogDistFixture(t, {
    catalogHtml: catalogPage().replace(
      "Repository docs and validation routes are source references",
      "Repository docs are production-proven source references"
    )
  });
  const errors = [];

  await validateProofSourceCatalogDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden affirmative claim wording/);
});

async function createManagedProofSourceCatalogDistFixture(t, options = {}) {
  const root = await createProofSourceCatalogDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createProofSourceCatalogDistFixture({
  catalogHtml = catalogPage(),
  discoveryRoutes = [proofSourceCatalogRoute],
  sitemapRoutes = [proofSourceCatalogRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-proof-source-catalog-test-"));
  const dist = join(root, "dist");
  const routes = new Set([proofSourceCatalogRoute, ...proofSourceCatalogRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(join(path, "index.html"), route === proofSourceCatalogRoute ? catalogHtml : page(route), "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: `Route ${route}`,
    summary: "Fixture route for proof source catalog validation.",
    publicClaimLevel: route === proofSourceCatalogRoute ? "demo" : "demo",
    sourceType: "site-page",
    hintCategory: "evidence",
    ...(route === proofSourceCatalogRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: ["Fixture limitations remain bounded."],
    nonClaims: ["No runtime behavior, production usage, deployment state, endpoint performance, or release approval proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteProofSourceCatalogRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === proofSourceCatalogRoute
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

function catalogPage() {
  return page(`
    <p>Public claim level: demo</p>
    <p>Public claim level</p>
    <p>route-to-source map</p>
    <p>site-tracemap-tools-claim-ledger</p>
    <p>SQLite indexes, fact streams, reports, rule catalog entries, route metadata, source docs, coverage labels, and documented limitations remain authoritative</p>
    <p>Concept rows are not shipped capabilities</p>
    <p>No public conclusion without evidence</p>
    ${proofSourceCatalogRequiredLinks.map((route) => `<a href="${route}">${route}</a>`).join("\n")}
    <table>
      <tbody>
        ${catalogRow({
          id: "proof-source-docs-repository-doc-navigation",
          route: "/docs/",
          claimLabel: "Repository doc navigation",
          allowedPublicWording: "Repository docs and validation routes are source references for CLI and artifact boundaries.",
          publicClaimLevel: "shipped",
          evidenceStatus: "source-backed",
          proofPath: '<a href="/docs/">/docs/</a>',
          sourceArtifactOrDoc: "Repository docs on main.",
          ruleIdOrFamily: "Repository documentation family.",
          evidenceTierOrCoverage: "Source document on main.",
          limitation: "Docs describe expected behavior and boundaries.",
          nonClaims: "No runtime behavior proof."
        })}
        ${catalogRow({
          id: "proof-source-proof-paths-public-evidence-trails",
          route: "/proof-paths/",
          claimLabel: "Public evidence trails",
          allowedPublicWording: "Public pages can route readers to artifact families, rule IDs, tiers, coverage labels, proof paths, and limitations.",
          publicClaimLevel: "demo",
          evidenceStatus: "demo-evidence-backed",
          proofPath: '<a href="/proof-paths/">/proof-paths/</a>',
          sourceArtifactOrDoc: "Public-safe demo summary.",
          ruleIdOrFamily: "public.demo.summary.v1",
          evidenceTierOrCoverage: "Tier2Structural.",
          limitation: "Generated artifacts and rule docs remain source material.",
          nonClaims: "No runtime behavior proof."
        })}
        ${catalogRow({
          id: "proof-source-demo-proof-upgrades-reduced-demo-coverage",
          route: "/demo/proof-upgrades/",
          claimLabel: "Reduced demo coverage",
          allowedPublicWording: "Demo proof-upgrade rows can show public-demo report families with explicit partial or gap labels.",
          publicClaimLevel: "demo",
          evidenceStatus: "partial-or-reduced",
          proofPath: '<a href="/demo/proof-upgrades/">/demo/proof-upgrades/</a> and <a href="/proof-paths/">/proof-paths/</a>',
          sourceArtifactOrDoc: "Public-safe generated summary rows.",
          ruleIdOrFamily: "public.demo.summary.v1",
          evidenceTierOrCoverage: "PartialAnalysis.",
          limitation: "Partial rows remain partial evidence.",
          nonClaims: "No release approval proof."
        })}
        ${catalogRow({
          id: "proof-source-roadmap-public-claim-gates",
          route: "/roadmap/",
          claimLabel: "Public claim gates",
          allowedPublicWording: "Roadmap rows can explain shipped, demo, concept, and hidden gates without promoting future-facing wording.",
          publicClaimLevel: "concept",
          evidenceStatus: "future-only",
          proofPath: '<a href="/roadmap/">/roadmap/</a>',
          sourceArtifactOrDoc: "Roadmap route copy.",
          ruleIdOrFamily: "Public claim gate family.",
          evidenceTierOrCoverage: "Concept route metadata.",
          limitation: "Concept rows need public-safe proof before promotion.",
          nonClaims: "No shipped capability proof."
        })}
        ${catalogRow({
          id: "proof-source-hidden-aggregate-placeholder",
          route: "hidden",
          claimLabel: "Internal-only aggregate placeholder",
          allowedPublicWording: "none",
          publicClaimLevel: "hidden",
          evidenceStatus: "hidden-or-internal",
          proofPath: "<code>hidden</code>",
          sourceArtifactOrDoc: "hidden",
          ruleIdOrFamily: "none",
          evidenceTierOrCoverage: "hidden",
          limitation: "Details are not disclosed publicly.",
          nonClaims: "This row does not represent any specific capability, route, private sample, count, cadence, sequence, or in-flight work."
        })}
      </tbody>
    </table>
    ${claimLevelMappings()}
    ${evidenceStatusMappings()}
  `);
}

function catalogRow(fields) {
  return `<tr id="${fields.id}" data-proof-source-row>
    <td data-field="route">${fields.route.startsWith("/") ? `<a href="${fields.route}">${fields.route}</a>` : fields.route}</td>
    <td data-field="claimLabel">${fields.claimLabel}</td>
    <td data-field="allowedPublicWording">${fields.allowedPublicWording}</td>
    <td data-field="publicClaimLevel"><code>${fields.publicClaimLevel}</code></td>
    <td data-field="evidenceStatus"><code>${fields.evidenceStatus}</code></td>
    <td data-field="proofPath">${fields.proofPath}</td>
    <td data-field="sourceArtifactOrDoc">${fields.sourceArtifactOrDoc}</td>
    <td data-field="ruleIdOrFamily">${fields.ruleIdOrFamily}</td>
    <td data-field="evidenceTierOrCoverage">${fields.evidenceTierOrCoverage}</td>
    <td data-field="limitation">${fields.limitation}</td>
    <td data-field="nonClaims">${fields.nonClaims}</td>
  </tr>`;
}

function claimLevelMappings() {
  return `
    <tr data-proof-source-claim-level-map data-catalog-level="shipped" data-source-vocabulary="main|shipped|shipped navigation|repository docs on main|main with maturity caveats"></tr>
    <tr data-proof-source-claim-level-map data-catalog-level="demo" data-source-vocabulary="demo|demo guidance|main/demo|public-demo|checked-in public-safe demo summary|route metadata publicClaimLevel: demo|proof-path public status demo"></tr>
    <tr data-proof-source-claim-level-map data-catalog-level="concept" data-source-vocabulary="concept|concept-only|future|future-only|dev|dev-only|route metadata publicClaimLevel: concept|proof-path public status future"></tr>
    <tr data-proof-source-claim-level-map data-catalog-level="hidden" data-source-vocabulary="hidden|hidden pending validation|no public capability row|no public proof-path counterpart|internal-only aggregate placeholder"></tr>
  `;
}

function evidenceStatusMappings() {
  return [
    "source-backed",
    "demo-evidence-backed",
    "partial-or-reduced",
    "gap-labeled-demo",
    "future-only",
    "hidden-or-internal",
    "not-yet-backed"
  ]
    .map((status) => `<tr data-proof-source-evidence-status-map data-evidence-status="${status}"></tr>`)
    .join("\n");
}
