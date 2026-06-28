import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  swiftEvidenceLaneRequiredLinks,
  swiftEvidenceLaneRoute,
  validateSwiftEvidenceLaneDist
} from "./swift-evidence-lane.mjs";

test("validateSwiftEvidenceLaneDist accepts the Swift evidence lane source", async (t) => {
  const root = await createManagedSwiftDistFixture(t);
  const errors = [];

  await validateSwiftEvidenceLaneDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateSwiftEvidenceLaneDist requires all story rows", async (t) => {
  const source = await sourcePage();
  const root = await createManagedSwiftDistFixture(t, {
    pageHtml: source.replace('data-swift-story="surface-discovery"', 'data-swift-story="surface-discovery-removed"')
  });
  const errors = [];

  await validateSwiftEvidenceLaneDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing story row: surface-discovery/);
});

test("validateSwiftEvidenceLaneDist requires shipped route metadata", async (t) => {
  const root = await createManagedSwiftDistFixture(t, {
    discoveryRoutes: [
      {
        path: swiftEvidenceLaneRoute,
        title: "Swift Evidence Lane",
        summary: "Swift static evidence lane.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "evidence",
        preferredProofPath: "/proof-paths/",
        limitations: ["No runtime behavior."],
        nonClaims: ["No AI impact analysis."]
      }
    ]
  });
  const errors = [];

  await validateSwiftEvidenceLaneDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel shipped, got concept/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/validation\//);
});

test("validateSwiftEvidenceLaneDist rejects private paths and unsupported claims", async (t) => {
  const source = await sourcePage();
  const localPathLeak = `${String.fromCharCode(47)}Users/example/private.swift`;
  const cases = [
    [`<p>${localPathLeak}</p>`, /private or raw artifact text/],
    ["<p>facts.ndjson</p>", /private or raw artifact text/],
    ["<p>TraceMap proves Swift runtime behavior.</p>", /unsupported Swift claim wording/],
    ["<p>Swift v0 validates build success.</p>", /unsupported Swift claim wording/],
    ["<p>TraceMap uses vector database analysis.</p>", /unsupported Swift claim wording/]
  ];

  for (const [body, expected] of cases) {
    const root = await createManagedSwiftDistFixture(t, {
      pageHtml: source.replace("</main>", `${body}</main>`)
    });
    const errors = [];

    await validateSwiftEvidenceLaneDist({ dist: join(root, "dist"), errors });

    assert.match(errors.join("\n"), expected);
  }
});

async function createManagedSwiftDistFixture(t, options = {}) {
  const root = await createSwiftDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createSwiftDistFixture({
  discoveryRoutes = [swiftDiscoveryEntry()],
  sitemapRoutes = [swiftEvidenceLaneRoute],
  pageHtml
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-swift-evidence-test-"));
  const dist = join(root, "dist");
  const routes = new Set([swiftEvidenceLaneRoute, ...swiftEvidenceLaneRequiredLinks, ...sitemapRoutes]);

  await mkdir(join(dist, "swift"), { recursive: true });
  await writeFile(join(dist, "swift", "index.html"), pageHtml ?? (await sourcePage()), "utf8");
  await writeFile(join(dist, "sitemap.xml"), sitemap(routes), "utf8");
  const outputs = await createDiscoveryOutputs(discoveryRoutes, {
    dist,
    resolveInternalPaths: false
  });
  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");

  return root;
}

function swiftDiscoveryEntry() {
  return {
    path: swiftEvidenceLaneRoute,
    title: "Swift Evidence Lane",
    summary: "Shipped Swift v0 public evidence lane for static inventory, symbols and calls, surface discovery, storage/data surfaces, and evidence safety, anchored to PR #425.",
    publicClaimLevel: "shipped",
    sourceType: "site-page",
    hintCategory: "evidence",
    preferredProofPath: "/validation/",
    limitations: [
      "Swift v0 evidence is deterministic static evidence and does not prove runtime behavior, app navigation, build success, production usage, deployment state, or release safety.",
      "Surface discovery and storage/data rows remain bounded by syntax, structural, textual, and reduced-coverage evidence."
    ],
    nonClaims: [
      "No rendered UI proof, complete navigation proof, runtime network reachability proof, stored-value proof, query execution proof, live schema proof, dependency vulnerability/license/freshness proof, or build compatibility proof.",
      "No AI impact analysis, LLM analysis, prompt-based classification, embeddings, vector databases, raw source snippets, raw SQL, secrets, local absolute paths, raw remotes, credentials, stored values, analyzer logs, or hidden validation details are public Swift claims."
    ]
  };
}

async function sourcePage() {
  return readFile(new URL("../src/swift/index.html", import.meta.url), "utf8");
}

function sitemap(routes) {
  const urls = [...routes]
    .map((route) => `<url><loc>https://tracemap.tools${route}</loc></url>`)
    .join("");
  return `<?xml version="1.0" encoding="UTF-8"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">${urls}</urlset>`;
}
