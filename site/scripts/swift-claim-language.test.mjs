import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  swiftClaimLanguageRequiredLinks,
  swiftClaimLanguageRoute,
  validateSwiftClaimLanguageDist
} from "./swift-claim-language.mjs";

test("validateSwiftClaimLanguageDist accepts the Swift claim-language source", async (t) => {
  const root = await createManagedSwiftClaimDistFixture(t);
  const errors = [];

  await validateSwiftClaimLanguageDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateSwiftClaimLanguageDist reports missing text and links", async (t) => {
  const root = await createManagedSwiftClaimDistFixture(t, {
    pageHtml: page("<p>Swift claim placeholder.</p>")
  });
  const errors = [];

  await validateSwiftClaimLanguageDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: shipped/);
  assert.match(errors.join("\n"), /missing required link: \/swift\//);
  assert.match(errors.join("\n"), /Evidence: swift\/claim-language\/index\.html\./);
});

test("validateSwiftClaimLanguageDist reports missing route metadata", async (t) => {
  const root = await createManagedSwiftClaimDistFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateSwiftClaimLanguageDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/swift\/claim-language\//);
  assert.match(errors.join("\n"), /sitemap is missing required route: https:\/\/tracemap\.tools\/swift\/claim-language\//);
});

test("validateSwiftClaimLanguageDist reports route metadata regressions", async (t) => {
  const root = await createManagedSwiftClaimDistFixture(t);
  await rewriteSwiftClaimRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "start",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateSwiftClaimLanguageDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel shipped, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory evidence, got start/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/swift\//);
});

test("validateSwiftClaimLanguageDist reports invalid baseUrl values", async (t) => {
  const root = await createManagedSwiftClaimDistFixture(t);
  const errors = [];

  await validateSwiftClaimLanguageDist({ baseUrl: "not a url", dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /baseUrl must be a valid absolute URL/);
});

test("validateSwiftClaimLanguageDist normalizes baseUrl to the origin", async (t) => {
  const root = await createManagedSwiftClaimDistFixture(t);
  const errors = [];

  await validateSwiftClaimLanguageDist({
    baseUrl: "https://tracemap.tools/docs/?preview=true",
    dist: join(root, "dist"),
    errors
  });

  assert.deepEqual(errors, []);
});

test("validateSwiftClaimLanguageDist requires checklist rows and safe examples", async (t) => {
  const source = await sourcePage();
  const root = await createManagedSwiftClaimDistFixture(t, {
    pageHtml: source
      .replace('data-swift-claim-check="proof-path"', 'data-swift-claim-check="proof-path-removed"')
      .replace('data-swift-claim-example="safe-demo"', 'data-swift-claim-example="safe-demo-removed"')
  });
  const errors = [];

  await validateSwiftClaimLanguageDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required check row: proof-path/);
  assert.match(errors.join("\n"), /missing safe example: safe-demo/);
});

test("validateSwiftClaimLanguageDist rejects private material, raw artifact names, and unsupported claims", async (t) => {
  const source = await sourcePage();
  const localPathLeak = `${String.fromCharCode(47)}Users/example/private.swift`;
  const cases = [
    [`<p>${localPathLeak}</p>`, /forbidden private material/],
    ["<p>/tmp/tracemap-swift-claim-language</p>", /forbidden private material/],
    ["<p>facts.ndjson</p>", /forbidden raw artifact name/],
    ["<p>index.sqlite</p>", /forbidden raw artifact name/],
    ["<p>logs/analyzer.log</p>", /forbidden raw artifact name/],
    ["<p>TraceMap proves Swift runtime API correctness.</p>", /unsupported Swift claim wording/],
    ["<p>Swift validates package compatibility.</p>", /unsupported Swift claim wording/],
    ["<p>TraceMap performs AI impact analysis.</p>", /unsupported Swift claim wording/],
    ["<p>TraceMap uses vector database analysis.</p>", /unsupported Swift claim wording/],
    ["<p>TraceMap provides embedding-backed search.</p>", /unsupported Swift claim wording/],
    ["<p>TraceMap uses vector databases.</p>", /unsupported Swift claim wording/]
  ];

  for (const [body, expected] of cases) {
    const root = await createManagedSwiftClaimDistFixture(t, {
      pageHtml: source.replace("</main>", `${body}</main>`)
    });
    const errors = [];

    await validateSwiftClaimLanguageDist({ dist: join(root, "dist"), errors });

    assert.match(errors.join("\n"), expected);
  }
});

test("validateSwiftClaimLanguageDist rejects tag-split private material, artifacts, and unsupported claims", async (t) => {
  const source = await sourcePage();
  const cases = [
    ["<p>/Us<span>ers/example/private.swift</span></p>", /forbidden private material/],
    ["<p>facts.<span>ndjson</span></p>", /forbidden raw artifact name/],
    ["<p>Trace<span>Map uses vector databases.</span></p>", /unsupported Swift claim wording/],
    ["<p>TraceMap provides embedding<span>-backed search.</span></p>", /unsupported Swift claim wording/]
  ];

  for (const [body, expected] of cases) {
    const root = await createManagedSwiftClaimDistFixture(t, {
      pageHtml: source.replace("</main>", `${body}</main>`)
    });
    const errors = [];

    await validateSwiftClaimLanguageDist({ dist: join(root, "dist"), errors });

    assert.match(errors.join("\n"), expected);
  }
});

test("validateSwiftClaimLanguageDist rejects private material and unsupported claims in route metadata", async (t) => {
  const localPathLeak = `${String.fromCharCode(47)}Users/example/private.swift`;
  const root = await createManagedSwiftClaimDistFixture(t);
  const routesIndexPath = join(root, "dist", "routes-index.json");
  const routesIndex = JSON.parse(await readFile(routesIndexPath, "utf8"));
  routesIndex.entries[0].summary = `Swift checklist from ${localPathLeak}.`;
  routesIndex.entries[0].nonClaims = [
    "No runtime behavior, API correctness, complete Swift semantic analysis, AI impact analysis, or raw generated artifacts.",
    "TraceMap performs AI impact analysis.",
    "facts.ndjson"
  ];
  await writeFile(routesIndexPath, `${JSON.stringify(routesIndex, null, 2)}\n`, "utf8");
  const errors = [];

  await validateSwiftClaimLanguageDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /route metadata contains forbidden private material/);
  assert.match(errors.join("\n"), /route metadata contains forbidden raw artifact name/);
  assert.match(errors.join("\n"), /route metadata contains unsupported Swift claim wording/);
});

async function createManagedSwiftClaimDistFixture(t, options = {}) {
  const root = await createSwiftClaimDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createSwiftClaimDistFixture({
  discoveryRoutes = [swiftClaimDiscoveryEntry()],
  sitemapRoutes = [swiftClaimLanguageRoute],
  pageHtml
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-swift-claim-test-"));
  const dist = join(root, "dist");
  const routes = new Set([
    ...swiftClaimLanguageRequiredLinks.filter((route) => route.startsWith("/")),
    ...sitemapRoutes
  ]);

  await mkdir(join(dist, "swift", "claim-language"), { recursive: true });
  await writeFile(join(dist, "swift", "claim-language", "index.html"), pageHtml ?? (await sourcePage()), "utf8");
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

function swiftClaimDiscoveryEntry() {
  return {
    path: swiftClaimLanguageRoute,
    title: "Swift Claim Language Checklist",
    summary:
      "Shipped Swift v0 reviewer checklist for repeating public Swift claims with proof paths, claim levels, evidence tiers, coverage labels, limitations, and non-claim boundaries attached.",
    publicClaimLevel: "shipped",
    sourceType: "site-page",
    hintCategory: "evidence",
    preferredProofPath: "/swift/",
    limitations: [
      "The checklist is a public copy review aid for shipped Swift v0 evidence; it is not raw scanner output, a complete Swift semantic analysis, or runtime validation.",
      "Every repeated Swift claim still needs a proof path, claim level, evidence tier, coverage label, and limitation before publication."
    ],
    nonClaims: [
      "No runtime behavior proof, API correctness proof, app navigation proof, rendered UI proof, Xcode build proof, SwiftPM restore proof, simulator/device execution proof, production usage proof, deployment proof, package compatibility proof, release safety proof, stored-value proof, query execution proof, live schema proof, or complete Swift semantic analysis.",
      "No AI impact analysis, LLM analysis, prompt-based classification, embeddings, vector databases, raw generated artifacts, raw source snippets, raw SQL, secrets, local absolute paths, raw remotes, credentials, stored values, private scan artifacts, analyzer logs, or hidden validation details are public Swift checklist claims."
    ]
  };
}

async function rewriteSwiftClaimRoutesIndexEntry(dist, patch) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  const entry = parsed.entries.find((item) => item.path === swiftClaimLanguageRoute);

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
  return readFile(new URL("../src/swift/claim-language/index.html", import.meta.url), "utf8");
}

function page(body) {
  return `<!doctype html><html lang="en"><head><title>Swift Claim Language Checklist | TraceMap</title><meta property="og:type" content="article"></head><body><main>${body}</main></body></html>`;
}

function sitemap(routes) {
  const urls = [...routes]
    .sort()
    .map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`)
    .join("\n");
  return `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls}\n</urlset>\n`;
}
