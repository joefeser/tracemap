import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  swiftStoryPageRequiredLinks,
  swiftStoryPageRoutes,
  swiftStoryPages,
  validateSwiftStoryPagesDist
} from "./swift-story-pages.mjs";

test("validateSwiftStoryPagesDist accepts the Swift story source pages", async (t) => {
  const root = await createManagedSwiftStoryPagesDistFixture(t);
  const errors = [];

  await validateSwiftStoryPagesDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateSwiftStoryPagesDist reports missing page text and links", async (t) => {
  const root = await createManagedSwiftStoryPagesDistFixture(t, {
    pages: {
      "/swift/static-inventory/": page("<p>Swift placeholder.</p>")
    }
  });
  const errors = [];

  await validateSwiftStoryPagesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: shipped/);
  assert.match(errors.join("\n"), /missing required link: \/swift\/story\//);
  assert.match(errors.join("\n"), /Evidence: swift\/static-inventory\/index\.html\./);
});

test("validateSwiftStoryPagesDist reports missing route metadata", async (t) => {
  const root = await createManagedSwiftStoryPagesDistFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateSwiftStoryPagesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/swift\/static-inventory\//);
  assert.match(errors.join("\n"), /sitemap is missing required route: https:\/\/tracemap\.tools\/swift\/static-inventory\//);
});

test("validateSwiftStoryPagesDist reports route metadata regressions", async (t) => {
  const root = await createManagedSwiftStoryPagesDistFixture(t);
  await rewriteSwiftStoryRoutesIndexEntry(join(root, "dist"), "/swift/surface-discovery/", {
    publicClaimLevel: "shipped",
    hintCategory: "start",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateSwiftStoryPagesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected \/swift\/surface-discovery\/ publicClaimLevel demo, got shipped/);
  assert.match(errors.join("\n"), /expected \/swift\/surface-discovery\/ hintCategory evidence, got start/);
  assert.match(errors.join("\n"), /expected \/swift\/surface-discovery\/ sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected \/swift\/surface-discovery\/ preferredProofPath \/swift\//);
});

test("validateSwiftStoryPagesDist rejects private material and unsupported claims", async (t) => {
  const source = await sourcePage("/swift/evidence-safety/");
  const localPathLeak = `${String.fromCharCode(47)}Users/example/private.swift`;
  const cases = [
    [`<p>${localPathLeak}</p>`, /forbidden private material/],
    ["<p>TraceMap proves Swift runtime behavior.</p>", /unsupported Swift claim wording/],
    ["<p>Swift validates build success.</p>", /unsupported Swift claim wording/],
    ["<p>TraceMap performs AI impact analysis.</p>", /unsupported Swift claim wording/],
    ["<p>TraceMap uses vector database analysis.</p>", /unsupported Swift claim wording/]
  ];

  for (const [body, expected] of cases) {
    const root = await createManagedSwiftStoryPagesDistFixture(t, {
      pages: {
        "/swift/evidence-safety/": source.replace("</main>", `${body}</main>`)
      }
    });
    const errors = [];

    await validateSwiftStoryPagesDist({ dist: join(root, "dist"), errors });

    assert.match(errors.join("\n"), expected);
  }
});

test("validateSwiftStoryPagesDist rejects private material and unsupported claims in metadata", async (t) => {
  const localPathLeak = `${String.fromCharCode(47)}Users/example/private.swift`;
  const root = await createManagedSwiftStoryPagesDistFixture(t);
  const routesIndexPath = join(root, "dist", "routes-index.json");
  const routesIndex = JSON.parse(await readFile(routesIndexPath, "utf8"));
  const entry = routesIndex.entries.find((item) => item.path === "/swift/storage-data/");
  entry.summary = `Swift storage story from ${localPathLeak}.`;
  entry.nonClaims = [
    "No runtime behavior, build success, release safety, raw source snippets, or hidden validation details are public Swift storage claims.",
    "TraceMap performs AI impact analysis."
  ];
  await writeFile(routesIndexPath, `${JSON.stringify(routesIndex, null, 2)}\n`, "utf8");
  const errors = [];

  await validateSwiftStoryPagesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /route metadata contains forbidden private material/);
  assert.match(errors.join("\n"), /route metadata contains unsupported Swift claim wording/);
});

async function createManagedSwiftStoryPagesDistFixture(t, options = {}) {
  const root = await createSwiftStoryPagesDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createSwiftStoryPagesDistFixture({
  discoveryRoutes = swiftStoryPages.map(swiftStoryDiscoveryEntry),
  sitemapRoutes = swiftStoryPageRoutes,
  pages = {}
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-swift-story-pages-test-"));
  const dist = join(root, "dist");
  const routes = new Set([
    ...swiftStoryPageRequiredLinks,
    ...sitemapRoutes
  ]);

  for (const route of swiftStoryPageRoutes) {
    const pathParts = route.replace(/^\/|\/$/g, "").split("/");
    await mkdir(join(dist, ...pathParts), { recursive: true });
    await writeFile(join(dist, ...pathParts, "index.html"), pages[route] ?? (await sourcePage(route)), "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), sitemap(routes), "utf8");
  const outputs = await createDiscoveryOutputs(discoveryRoutes, {
    dist,
    resolveInternalPaths: false
  });
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");

  return root;
}

function swiftStoryDiscoveryEntry(pageConfig) {
  return {
    path: pageConfig.route,
    title: pageConfig.title,
    summary: `${pageConfig.title} story page with public-safe Swift v0 evidence boundaries.`,
    publicClaimLevel: pageConfig.claimLevel,
    sourceType: "site-page",
    hintCategory: "evidence",
    preferredProofPath: "/swift/",
    limitations: [
      "Swift story pages orient readers to shipped Swift v0 evidence and link to the proof matrix; they are not raw scanner output.",
      "Swift v0 evidence remains deterministic static evidence with documented reduced-coverage and unsupported-surface gaps."
    ],
    nonClaims: [
      "No runtime behavior, app navigation, rendered UI, complete navigation, user action, production usage, endpoint performance, deployment state, release safety, stored-value proof, query execution proof, live schema proof, build success proof, or complete Swift understanding.",
      "No AI impact analysis, LLM analysis, prompt-based classification, embeddings, vector databases, raw source snippets, raw SQL, secrets, local absolute paths, raw remotes, credentials, stored values, private scan artifacts, analyzer logs, or hidden validation details are public Swift story claims."
    ]
  };
}

async function rewriteSwiftStoryRoutesIndexEntry(dist, route, patch) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  const entry = parsed.entries.find((item) => item.path === route);

  for (const [key, value] of Object.entries(patch)) {
    if (value === undefined) {
      delete entry[key];
    } else {
      entry[key] = value;
    }
  }

  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

async function sourcePage(route) {
  const pathParts = route.replace(/^\/|\/$/g, "").split("/");
  return readFile(new URL(`../src/${pathParts.join("/")}/index.html`, import.meta.url), "utf8");
}

function page(body) {
  return `<!doctype html><html lang="en"><head><title>Swift Static Inventory | TraceMap</title><meta property="og:type" content="article"></head><body><main data-swift-story-page>${body}</main></body></html>`;
}

function sitemap(routes) {
  const urls = [...routes]
    .sort()
    .map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`)
    .join("\n");
  return `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls}\n</urlset>\n`;
}
