import assert from "node:assert/strict";
import { mkdir, mkdtemp, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import { roadmapClaimLedgerRoute, validateRoadmapClaimLedgerDist } from "./roadmap-claim-ledger.mjs";

test("validateRoadmapClaimLedgerDist accepts the canonical roadmap claim ledger", async () => {
  const root = await createRoadmapFixture();
  const errors = [];

  await validateRoadmapClaimLedgerDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateRoadmapClaimLedgerDist reports route metadata regressions", async () => {
  const root = await createRoadmapFixture({
    routeEntry: {
      path: roadmapClaimLedgerRoute,
      title: "Roadmap Claim Ledger",
      summary: "Fixture concept-level ledger for public claim wording and proof paths.",
      publicClaimLevel: "demo",
      sourceType: "site-page",
      hintCategory: "roadmap",
      preferredProofPath: "/limitations/",
      limitations: ["Fixture ledger limitations remain bounded."],
      nonClaims: ["No runtime behavior proof."]
    }
  });
  const errors = [];

  await validateRoadmapClaimLedgerDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/limitations\//);
});

test("validateRoadmapClaimLedgerDist requires every evidence status mapping", async () => {
  const root = await createRoadmapFixture({
    roadmapHtml: validRoadmapHtml().replace('data-ledger-label="hidden/internal"', 'data-ledger-label="future-only"')
  });
  const errors = [];

  await validateRoadmapClaimLedgerDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing mapped evidence status: hidden\/internal/);
});

test("validateRoadmapClaimLedgerDist rejects raw artifact proof links", async () => {
  const root = await createRoadmapFixture({
    roadmapHtml: validRoadmapHtml().replace("/proof-paths/", "/facts.ndjson")
  });
  const errors = [];

  await validateRoadmapClaimLedgerDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /links to forbidden raw proof artifact: \/facts\.ndjson/);
});

async function createRoadmapFixture({ roadmapHtml = validRoadmapHtml(), routeEntry = discoveryRoute() } = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-roadmap-ledger-test-"));
  const dist = join(root, "dist");

  await mkdir(join(dist, "roadmap"), { recursive: true });
  await mkdir(join(dist, "proof-paths"), { recursive: true });
  await mkdir(join(dist, "limitations"), { recursive: true });
  await writeFile(join(dist, "roadmap", "index.html"), roadmapHtml, "utf8");
  await writeFile(join(dist, "proof-paths", "index.html"), "<p>Proof paths</p>", "utf8");
  await writeFile(join(dist, "limitations", "index.html"), "<p>Limitations</p>", "utf8");
  await writeFile(
    join(dist, "sitemap.xml"),
    `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <url><loc>https://tracemap.tools/roadmap/</loc></url>
  <url><loc>https://tracemap.tools/proof-paths/</loc></url>
</urlset>`,
    "utf8"
  );

  const outputs = await createDiscoveryOutputs([routeEntry], { dist, resolveInternalPaths: true });
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");

  return root;
}

function discoveryRoute() {
  return {
    path: roadmapClaimLedgerRoute,
    title: "Roadmap Claim Ledger",
    summary: "Fixture concept-level ledger for public claim wording and proof paths.",
    publicClaimLevel: "concept",
    sourceType: "site-page",
    hintCategory: "roadmap",
    preferredProofPath: "/proof-paths/",
    limitations: ["Fixture ledger limitations remain bounded."],
    nonClaims: ["No runtime behavior proof."]
  };
}

function validRoadmapHtml() {
  return `<!doctype html>
<html>
  <body>
    <main>
      <p>Public claim level: concept</p>
      <p>No public conclusion without evidence</p>
      <h2>Claim ledger</h2>
      <p>Public wording status</p>
      <p>Source-of-truth artifact family</p>
      <p>SQLite indexes, fact streams, reports, rule catalog entries, commit metadata, coverage labels, and documented limitations</p>
      <p>presentation and governance layer</p>
      <p>runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, AI impact analysis, LLM analysis, and complete product coverage wording is forbidden</p>
      <a href="/proof-paths/">Proof paths</a>
      <a href="/capabilities/">Capabilities</a>
      <a href="/demo/proof-upgrades/">Demo proof upgrades</a>
      <a href="/limitations/">Limitations</a>
      <table>
        <tbody>
          <tr id="claim-shipped" data-claim-row data-claim-level="shipped" data-evidence-status="evidence-backed" data-wording-status="live"><td>shipped</td></tr>
          <tr id="claim-demo-partial" data-claim-row data-claim-level="demo" data-evidence-status="partial/reduced coverage" data-wording-status="demo-only"><td>demo</td></tr>
          <tr id="claim-demo-gap" data-claim-row data-claim-level="demo" data-evidence-status="gap-labeled demo evidence" data-wording-status="demo-only"><td>gap</td></tr>
          <tr id="claim-concept-future" data-claim-row data-claim-level="concept" data-evidence-status="future-only" data-wording-status="future-facing"><td>concept</td></tr>
          <tr id="claim-hidden-internal" data-claim-row data-claim-level="hidden" data-evidence-status="hidden/internal" data-wording-status="hidden-from-public-navigation"><td>hidden</td></tr>
          <tr id="claim-forbidden" data-claim-row data-claim-level="hidden" data-evidence-status="not-yet-backed" data-wording-status="forbidden"><td>forbidden</td></tr>
        </tbody>
      </table>
      <table>
        <tbody>
          <tr data-mapping-row data-mapping-axis="claim-level" data-ledger-label="shipped"><td>shipped</td></tr>
          <tr data-mapping-row data-mapping-axis="claim-level" data-ledger-label="demo"><td>demo</td></tr>
          <tr data-mapping-row data-mapping-axis="claim-level" data-ledger-label="concept"><td>concept</td></tr>
          <tr data-mapping-row data-mapping-axis="claim-level" data-ledger-label="hidden"><td>hidden</td></tr>
          <tr data-mapping-row data-mapping-axis="evidence-status" data-ledger-label="evidence-backed"><td>evidence-backed</td></tr>
          <tr data-mapping-row data-mapping-axis="evidence-status" data-ledger-label="partial/reduced coverage"><td>partial</td></tr>
          <tr data-mapping-row data-mapping-axis="evidence-status" data-ledger-label="gap-labeled demo evidence"><td>gap</td></tr>
          <tr data-mapping-row data-mapping-axis="evidence-status" data-ledger-label="future-only"><td>future</td></tr>
          <tr data-mapping-row data-mapping-axis="evidence-status" data-ledger-label="hidden/internal"><td>hidden/internal</td></tr>
          <tr data-mapping-row data-mapping-axis="evidence-status" data-ledger-label="not-yet-backed"><td>not-yet</td></tr>
        </tbody>
      </table>
    </main>
  </body>
</html>`;
}
