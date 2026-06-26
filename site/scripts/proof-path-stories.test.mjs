import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { tmpdir } from "node:os";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  proofPathStoriesRequiredLinks,
  proofPathStoriesRoute,
  validateProofPathStoriesDist
} from "./proof-path-stories.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const siteRoot = resolve(scriptDir, "..");

test("validateProofPathStoriesDist accepts the proof-path story gallery route", async (t) => {
  const root = await createManagedProofPathStoriesFixture(t);
  const errors = [];

  await validateProofPathStoriesDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateProofPathStoriesDist reports missing required card field", async (t) => {
  const sourceHtml = await readSourcePage();
  const root = await createManagedProofPathStoriesFixture(t, {
    pageHtml: sourceHtml.replaceAll('data-story-field="supporting IDs"', 'data-story-field="supporting references"')
  });
  const errors = [];

  await validateProofPathStoriesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required field: supporting IDs/);
});

test("validateProofPathStoriesDist reports missing walkthrough ending", async (t) => {
  const sourceHtml = await readSourcePage();
  const root = await createManagedProofPathStoriesFixture(t, {
    pageHtml: sourceHtml.replace('data-walkthrough-ending="hidden"', 'data-walkthrough-ending="withheld"')
  });
  const errors = [];

  await validateProofPathStoriesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unsupported ending: withheld/);
  assert.match(errors.join("\n"), /missing walkthrough ending: hidden/);
});

test("validateProofPathStoriesDist reports route metadata regressions", async (t) => {
  const root = await createManagedProofPathStoriesFixture(t, {
    discoveryRoutes: [
      {
        ...routeEntry(),
        publicClaimLevel: "demo",
        preferredProofPath: "/demo/proof-upgrades/",
        summary: "Demo-level story gallery."
      }
    ]
  });
  const errors = [];

  await validateProofPathStoriesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/demo\/proof-upgrades\//);
  assert.match(errors.join("\n"), /summary must keep concept-level wording/);
});

test("validateProofPathStoriesDist rejects positive forbidden claims outside boundary context", async (t) => {
  const sourceHtml = await readSourcePage();
  const root = await createManagedProofPathStoriesFixture(t, {
    pageHtml: sourceHtml.replace("</main>", "<p>TraceMap proves runtime behavior for this endpoint.</p></main>")
  });
  const errors = [];

  await validateProofPathStoriesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateProofPathStoriesDist permits forbidden wording inside marked boundary context", async (t) => {
  const sourceHtml = await readSourcePage();
  const root = await createManagedProofPathStoriesFixture(t, {
    pageHtml: sourceHtml.replace(
      "</main>",
      '<section class="section rejected-example"><p>TraceMap proves runtime behavior and complete coverage.</p></section></main>'
    )
  });
  const errors = [];

  await validateProofPathStoriesDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateProofPathStoriesDist rejects raw material outside marked boundary context", async (t) => {
  const sourceHtml = await readSourcePage();
  const root = await createManagedProofPathStoriesFixture(t, {
    pageHtml: sourceHtml.replace("</main>", "<p>Publish raw facts and raw SQL with local paths.</p></main>")
  });
  const errors = [];

  await validateProofPathStoriesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden raw\/private material/);
});

test("validateProofPathStoriesDist rejects missing stop routing", async (t) => {
  const sourceHtml = await readSourcePage();
  const root = await createManagedProofPathStoriesFixture(t, {
    pageHtml: sourceHtml.replace(' data-owner-route="security owner"', "")
  });
  const errors = [];

  await validateProofPathStoriesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /stop condition is missing owner\/question routing/);
});

async function createManagedProofPathStoriesFixture(t, options = {}) {
  const root = await createProofPathStoriesFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createProofPathStoriesFixture({
  pageHtml,
  discoveryRoutes = [routeEntry(), ...proofPathStoriesRequiredLinks.map((route) => routeEntryFor(route))]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-proof-path-stories-test-"));
  const dist = join(root, "dist");
  const routes = new Set([proofPathStoriesRoute, ...proofPathStoriesRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    const body = route === proofPathStoriesRoute ? pageHtml ?? (await readSourcePage()) : page(route);
    await writeFile(join(path, "index.html"), body, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap([proofPathStoriesRoute]), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function readSourcePage() {
  return readFile(resolve(siteRoot, "src", "proof-path-stories", "index.html"), "utf8");
}

function page(body) {
  return `<!doctype html><html lang="en"><head><title>Fixture</title></head><body><main>${body}</main></body></html>`;
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${routes.map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`).join("\n")}
</urlset>
`;
}

async function writeDiscoveryFiles(dist, entries) {
  const outputs = await createDiscoveryOutputs(entries, {
    dist,
    resolveInternalPaths: false
  });

  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
}

function routeEntry() {
  return {
    path: proofPathStoriesRoute,
    title: "Proof-Path Story Gallery",
    summary:
      "Concept-level public-safe story cards for reading static proof paths with rule families, evidence tiers, coverage labels, limitations, stop conditions, and owner routing.",
    publicClaimLevel: "concept",
    sourceType: "site-page",
    hintCategory: "evidence",
    preferredProofPath: "/proof-paths/",
    limitations: [
      "The gallery is a concept-level reading aid over synthetic public-safe cards, not the canonical proof ledger or source catalog.",
      "Story cards remain concept-level until checked-in public-safe demo evidence supports a stricter card label."
    ],
    nonClaims: [
      "No runtime behavior, production traffic, endpoint performance, outage cause, release approval, release safety, operational safety, complete coverage, product behavior proof, or automated approval.",
      "No AI impact analysis, LLM analysis, embeddings, vector databases, prompt classification, raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, private labels, command output, hidden validation details, or credential-like values."
    ]
  };
}

function routeEntryFor(route) {
  return {
    path: route,
    title: `Fixture ${route}`,
    summary: "Fixture route for proof-path story gallery link validation.",
    publicClaimLevel: "concept",
    sourceType: "site-page",
    hintCategory: "evidence",
    preferredProofPath: "/proof-paths/",
    limitations: ["Fixture route limitations remain bounded."],
    nonClaims: ["No runtime behavior or production usage proof."]
  };
}
