import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  swiftRealWorldSmokeRequiredLinks,
  swiftRealWorldSmokeRoute,
  validateSwiftRealWorldSmokeDist
} from "./swift-real-world-smoke.mjs";

test("validateSwiftRealWorldSmokeDist accepts the Swift real-world smoke source", async (t) => {
  const root = await createManagedSwiftSmokeDistFixture(t);
  const errors = [];

  await validateSwiftRealWorldSmokeDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateSwiftRealWorldSmokeDist reports missing text and links", async (t) => {
  const root = await createManagedSwiftSmokeDistFixture(t, {
    pageHtml: page("<p>Swift smoke placeholder.</p>")
  });
  const errors = [];

  await validateSwiftRealWorldSmokeDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: shipped/);
  assert.match(errors.join("\n"), /missing required link: \/swift\//);
  assert.match(errors.join("\n"), /Evidence: swift\/real-world-smoke\/index\.html\./);
});

test("validateSwiftRealWorldSmokeDist reports missing route metadata", async (t) => {
  const root = await createManagedSwiftSmokeDistFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateSwiftRealWorldSmokeDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/swift\/real-world-smoke\//);
});

test("validateSwiftRealWorldSmokeDist reports route metadata regressions", async (t) => {
  const root = await createManagedSwiftSmokeDistFixture(t);
  await rewriteSwiftSmokeRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "start",
    sourceType: "repo-doc",
    preferredProofPath: "/swift/"
  });
  const errors = [];

  await validateSwiftRealWorldSmokeDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel shipped, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory evidence, got start/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/validation\//);
});

test("validateSwiftRealWorldSmokeDist reports invalid baseUrl values", async (t) => {
  const root = await createManagedSwiftSmokeDistFixture(t);
  const errors = [];

  await validateSwiftRealWorldSmokeDist({ baseUrl: "not a url", dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /baseUrl must be a valid absolute URL/);
});

test("validateSwiftRealWorldSmokeDist requires pinned sample rows", async (t) => {
  const source = await sourcePage();
  const root = await createManagedSwiftSmokeDistFixture(t, {
    pageHtml: source.replace('data-swift-smoke-sample="mastodon-ios"', 'data-swift-smoke-sample="mastodon-ios-removed"')
  });
  const errors = [];

  await validateSwiftRealWorldSmokeDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing sample row: mastodon-ios/);
});

test("validateSwiftRealWorldSmokeDist rejects private material and unsupported claims", async (t) => {
  const source = await sourcePage();
  const localPathLeak = `${String.fromCharCode(47)}Users/example/private.swift`;
  const cases = [
    [`<p>${localPathLeak}</p>`, /forbidden private material/],
    ["<p>/tmp/tracemap-swift-real-world-smoke</p>", /forbidden private material/],
    ["<p>facts.ndjson</p>", /forbidden raw artifact name/],
    ["<p>index.sqlite</p>", /forbidden raw artifact name/],
    ["<p>logs/analyzer.log</p>", /forbidden raw artifact name/],
    ["<p>TraceMap proves Swift runtime API correctness.</p>", /unsupported Swift claim wording/],
    ["<p>Swift validates package compatibility.</p>", /unsupported Swift claim wording/],
    ["<p>TraceMap performs AI impact analysis.</p>", /unsupported Swift claim wording/],
    ["<p>TraceMap uses embeddings.</p>", /unsupported Swift claim wording/],
    ["<p>TraceMap uses vector databases.</p>", /unsupported Swift claim wording/],
    ["<p>TraceMap uses vector database analysis.</p>", /unsupported Swift claim wording/],
    ["<p>Trace<span>Map</span> uses embeddings.</p>", /unsupported Swift claim wording/],
    ["<p>Trace<span>Map uses vector database analysis.</span></p>", /unsupported Swift claim wording/]
  ];

  for (const [body, expected] of cases) {
    const root = await createManagedSwiftSmokeDistFixture(t, {
      pageHtml: source.replace("</main>", `${body}</main>`)
    });
    const errors = [];

    await validateSwiftRealWorldSmokeDist({ dist: join(root, "dist"), errors });

    assert.match(errors.join("\n"), expected);
  }
});

test("validateSwiftRealWorldSmokeDist rejects private material and unsupported claims in route metadata", async (t) => {
  const localPathLeak = `${String.fromCharCode(47)}Users/example/private.swift`;
  const root = await createManagedSwiftSmokeDistFixture(t);
  const routesIndexPath = join(root, "dist", "routes-index.json");
  const routesIndex = JSON.parse(await readFile(routesIndexPath, "utf8"));
  routesIndex.entries[0].summary = `Swift smoke proof from ${localPathLeak}.`;
  routesIndex.entries[0].nonClaims = [
    "No runtime behavior, API correctness, complete Swift semantic analysis, AI impact analysis, or raw generated artifacts.",
    "TraceMap performs AI impact analysis.",
    "facts.ndjson"
  ];
  await writeFile(routesIndexPath, `${JSON.stringify(routesIndex, null, 2)}\n`, "utf8");
  const errors = [];

  await validateSwiftRealWorldSmokeDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /route metadata contains forbidden private material/);
  assert.match(errors.join("\n"), /route metadata contains forbidden raw artifact name/);
  assert.match(errors.join("\n"), /route metadata contains unsupported Swift claim wording/);
});

async function createManagedSwiftSmokeDistFixture(t, options = {}) {
  const root = await createSwiftSmokeDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createSwiftSmokeDistFixture({
  discoveryRoutes = [swiftSmokeDiscoveryEntry()],
  sitemapRoutes = [swiftRealWorldSmokeRoute],
  pageHtml
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-swift-smoke-test-"));
  const dist = join(root, "dist");
  const routes = new Set([swiftRealWorldSmokeRoute, ...swiftRealWorldSmokeRequiredLinks, ...sitemapRoutes]);

  await mkdir(join(dist, "swift", "real-world-smoke"), { recursive: true });
  await writeFile(join(dist, "swift", "real-world-smoke", "index.html"), pageHtml ?? (await sourcePage()), "utf8");
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

function swiftSmokeDiscoveryEntry() {
  return {
    path: swiftRealWorldSmokeRoute,
    title: "Swift Real-World Smoke Proof",
    summary:
      "Shipped Swift v0 validation story for the pinned real-world API-client smoke harness, public samples, sanitized generated summaries, and static evidence boundaries.",
    publicClaimLevel: "shipped",
    sourceType: "site-page",
    hintCategory: "evidence",
    preferredProofPath: "/validation/",
    limitations: [
      "The smoke validates static artifact generation and sanitized summary metadata over pinned public Swift app samples; it is not raw scanner output and raw generated artifacts stay local.",
      "Swift real-world smoke evidence remains deterministic static evidence and does not prove runtime behavior, API correctness, app navigation, build success, production usage, deployment state, package compatibility, or release safety."
    ],
    nonClaims: [
      "No Xcode build proof, SwiftPM restore proof, simulator or device execution proof, network-call proof, credential proof, auth-flow proof, production telemetry proof, endpoint reachability proof, backend compatibility proof, complete app navigation proof, package compatibility proof, production-use proof, impact proof, complete Swift semantic analysis, stored-value proof, query execution proof, or live schema proof.",
      "No AI impact analysis, LLM analysis, prompt-based classification, embeddings, vector databases, raw generated artifacts, raw source snippets, raw SQL, secrets, local absolute paths, clone URLs, raw remotes, credentials, hostnames, config values, private labels, runtime observations, analyzer logs, or hidden validation details are public Swift smoke claims."
    ]
  };
}

async function rewriteSwiftSmokeRoutesIndexEntry(dist, patch) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  const entry = parsed.entries.find((item) => item.path === swiftRealWorldSmokeRoute);

  for (const [key, value] of Object.entries(patch)) {
    if (value === undefined) {
      delete entry[key];
    } else {
      entry[key] = value;
    }
  }

  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

async function sourcePage() {
  return readFile(new URL("../src/swift/real-world-smoke/index.html", import.meta.url), "utf8");
}

function page(body) {
  return `<!doctype html><html lang="en"><head><title>Swift Real-World Smoke Proof | TraceMap</title><meta property="og:type" content="article"></head><body><main>${body}</main></body></html>`;
}

function sitemap(routes) {
  const urls = [...routes]
    .sort()
    .map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`)
    .join("\n");
  return `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls}\n</urlset>\n`;
}
