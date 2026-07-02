import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  swiftApiClientWalkthroughRequiredLinks,
  swiftApiClientWalkthroughRoute,
  validateSwiftApiClientWalkthroughDist
} from "./swift-api-client-walkthrough.mjs";

test("validateSwiftApiClientWalkthroughDist accepts the Swift API-client walkthrough source", async (t) => {
  const root = await createManagedSwiftApiClientDistFixture(t);
  const errors = [];

  await validateSwiftApiClientWalkthroughDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateSwiftApiClientWalkthroughDist reports missing required text and links", async (t) => {
  const root = await createManagedSwiftApiClientDistFixture(t, {
    pageHtml: page("<p>Swift API-client placeholder.</p>")
  });
  const errors = [];

  await validateSwiftApiClientWalkthroughDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: demo/);
  assert.match(errors.join("\n"), /missing required text: URLSession/);
  assert.match(errors.join("\n"), /missing required link: \/swift\/surface-discovery\//);
  assert.match(errors.join("\n"), /Evidence: swift\/api-client-walkthrough\/index\.html\./);
});

test("validateSwiftApiClientWalkthroughDist reports missing route metadata", async (t) => {
  const root = await createManagedSwiftApiClientDistFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateSwiftApiClientWalkthroughDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/swift\/api-client-walkthrough\//);
  assert.match(errors.join("\n"), /sitemap is missing required route: https:\/\/tracemap\.tools\/swift\/api-client-walkthrough\//);
});

test("validateSwiftApiClientWalkthroughDist reports route metadata regressions", async (t) => {
  const root = await createManagedSwiftApiClientDistFixture(t);
  await rewriteSwiftApiClientRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "shipped",
    hintCategory: "start",
    sourceType: "repo-doc",
    preferredProofPath: "/swift/"
  });
  const errors = [];

  await validateSwiftApiClientWalkthroughDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel demo, got shipped/);
  assert.match(errors.join("\n"), /expected hintCategory evidence, got start/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/swift\/real-world-smoke\//);
});

test("validateSwiftApiClientWalkthroughDist normalizes baseUrl to the origin", async (t) => {
  const root = await createManagedSwiftApiClientDistFixture(t);
  const errors = [];

  await validateSwiftApiClientWalkthroughDist({
    baseUrl: "https://tracemap.tools/docs/?preview=true",
    dist: join(root, "dist"),
    errors
  });

  assert.deepEqual(errors, []);
});

test("validateSwiftApiClientWalkthroughDist requires every evidence shape row", async (t) => {
  const source = await sourcePage();
  const root = await createManagedSwiftApiClientDistFixture(t, {
    pageHtml: source.replace('data-swift-api-client-shape="moya-target"', 'data-swift-api-client-shape="moya-target-removed"')
  });
  const errors = [];

  await validateSwiftApiClientWalkthroughDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing evidence shape row: moya-target/);
});

test("validateSwiftApiClientWalkthroughDist requires rule IDs in evidence rows", async (t) => {
  const source = await sourcePage();
  const root = await createManagedSwiftApiClientDistFixture(t, {
    pageHtml: source.replace("<td><code>swift.http.client-library.v1</code></td>", "<td><code>swift.http.removed.v1</code></td>")
  });
  const errors = [];

  await validateSwiftApiClientWalkthroughDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing rule ID swift\.http\.client-library\.v1/);
});

test("validateSwiftApiClientWalkthroughDist rejects private material, raw artifacts, and unsupported claims", async (t) => {
  const source = await sourcePage();
  const localPathLeak = `${String.fromCharCode(47)}Users/example/private.swift`;
  const cases = [
    [`<p>${localPathLeak}</p>`, /forbidden private material/],
    ["<p>/tmp/tracemap-swift-api-client</p>", /forbidden private material/],
    ["<p>facts.ndjson</p>", /forbidden raw artifact name/],
    ["<p>index.sqlite</p>", /forbidden raw artifact name/],
    ["<p>logs/analyzer.log</p>", /forbidden raw artifact name/],
    ["<p>TraceMap proves Swift runtime API correctness.</p>", /unsupported Swift claim wording/],
    ["<p>Swift validates backend compatibility.</p>", /unsupported Swift claim wording/],
    ["<p>TraceMap performs AI impact analysis.</p>", /unsupported Swift claim wording/],
    ["<p>TraceMap uses vector database analysis.</p>", /unsupported Swift claim wording/],
    ["<p>TraceMap provides embedding-backed search.</p>", /unsupported Swift claim wording/]
  ];

  for (const [body, expected] of cases) {
    const root = await createManagedSwiftApiClientDistFixture(t, {
      pageHtml: source.replace("</main>", `${body}</main>`)
    });
    const errors = [];

    await validateSwiftApiClientWalkthroughDist({ dist: join(root, "dist"), errors });

    assert.match(errors.join("\n"), expected);
  }
});

test("validateSwiftApiClientWalkthroughDist rejects private material and unsupported claims in route metadata", async (t) => {
  const localPathLeak = `${String.fromCharCode(47)}Users/example/private.swift`;
  const root = await createManagedSwiftApiClientDistFixture(t);
  const routesIndexPath = join(root, "dist", "routes-index.json");
  const routesIndex = JSON.parse(await readFile(routesIndexPath, "utf8"));
  routesIndex.entries[0].summary = `Swift API-client evidence from ${localPathLeak}.`;
  routesIndex.entries[0].nonClaims = [
    "No runtime behavior, endpoint reachability, backend compatibility, request success, auth flow, production traffic, AI impact analysis, or raw generated artifacts.",
    "TraceMap performs AI impact analysis.",
    "facts.ndjson"
  ];
  await writeFile(routesIndexPath, `${JSON.stringify(routesIndex, null, 2)}\n`, "utf8");
  const errors = [];

  await validateSwiftApiClientWalkthroughDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /route metadata contains forbidden private material/);
  assert.match(errors.join("\n"), /route metadata contains forbidden raw artifact name/);
  assert.match(errors.join("\n"), /route metadata contains unsupported Swift claim wording/);
});

async function createManagedSwiftApiClientDistFixture(t, options = {}) {
  const root = await createSwiftApiClientDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createSwiftApiClientDistFixture({
  discoveryRoutes = [swiftApiClientDiscoveryEntry()],
  sitemapRoutes = [swiftApiClientWalkthroughRoute],
  pageHtml
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-swift-api-client-test-"));
  const dist = join(root, "dist");
  const routes = new Set([
    ...swiftApiClientWalkthroughRequiredLinks.filter((route) => route.startsWith("/")),
    ...sitemapRoutes
  ]);

  await mkdir(join(dist, "swift", "api-client-walkthrough"), { recursive: true });
  await writeFile(join(dist, "swift", "api-client-walkthrough", "index.html"), pageHtml ?? (await sourcePage()), "utf8");
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

async function sourcePage() {
  return readFile(new URL("../src/swift/api-client-walkthrough/index.html", import.meta.url), "utf8");
}

function page(body) {
  return `<!doctype html>
<html lang="en">
  <head>
    <title>Swift API-Client Evidence Walkthrough | TraceMap</title>
    <meta property="og:type" content="article">
  </head>
  <body>
    <main data-swift-api-client-walkthrough>${body}</main>
  </body>
</html>`;
}

function swiftApiClientDiscoveryEntry(overrides = {}) {
  return {
    path: swiftApiClientWalkthroughRoute,
    title: "Swift API-Client Evidence Walkthrough",
    summary: "Demo-level Swift API-client evidence walkthrough for URLSession, URLRequest, Alamofire, and Moya-style static candidates.",
    publicClaimLevel: "demo",
    sourceType: "site-page",
    hintCategory: "evidence",
    preferredProofPath: "/swift/real-world-smoke/",
    limitations: [
      "Swift API-client evidence is deterministic static evidence and does not prove runtime behavior, endpoint reachability, backend compatibility, request success, auth flow, production traffic, or API correctness."
    ],
    nonClaims: [
      "No runtime behavior, endpoint reachability, backend compatibility, request success, auth flow, production traffic, API correctness, AI impact analysis, or raw generated artifacts."
    ],
    ...overrides
  };
}

async function rewriteSwiftApiClientRoutesIndexEntry(dist, overrides) {
  const routesIndexPath = join(dist, "routes-index.json");
  const routesIndex = JSON.parse(await readFile(routesIndexPath, "utf8"));
  routesIndex.entries = routesIndex.entries.map((entry) =>
    entry.path === swiftApiClientWalkthroughRoute ? { ...entry, ...overrides } : entry
  );
  await writeFile(routesIndexPath, `${JSON.stringify(routesIndex, null, 2)}\n`, "utf8");
}

function sitemap(routes) {
  const urls = [...routes]
    .sort()
    .map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`)
    .join("\n");
  return `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls}\n</urlset>\n`;
}
