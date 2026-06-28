import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  swiftAdapterStoryRequiredLinks,
  swiftAdapterStoryRoute,
  validateSwiftAdapterStoryDist
} from "./swift-adapter-story.mjs";

test("validateSwiftAdapterStoryDist accepts the Swift adapter story source", async (t) => {
  const root = await createManagedSwiftStoryDistFixture(t);
  const errors = [];

  await validateSwiftAdapterStoryDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateSwiftAdapterStoryDist reports missing required page text and links", async (t) => {
  const root = await createManagedSwiftStoryDistFixture(t, {
    pageHtml: page("<p>Swift story placeholder.</p>")
  });
  const errors = [];

  await validateSwiftAdapterStoryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: shipped/);
  assert.match(errors.join("\n"), /missing required link: \/swift\//);
  assert.match(errors.join("\n"), /Evidence: swift\/story\/index\.html\./);
});

test("validateSwiftAdapterStoryDist reports missing route metadata", async (t) => {
  const root = await createManagedSwiftStoryDistFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateSwiftAdapterStoryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/swift\/story\//);
});

test("validateSwiftAdapterStoryDist reports route metadata regressions", async (t) => {
  const root = await createManagedSwiftStoryDistFixture(t);
  await rewriteSwiftStoryRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "concept",
    hintCategory: "use-case",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateSwiftAdapterStoryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel shipped, got concept/);
  assert.match(errors.join("\n"), /expected hintCategory evidence, got use-case/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/swift\//);
});

test("validateSwiftAdapterStoryDist requires real anchor hrefs and tolerates href spacing", async (t) => {
  const source = await sourcePage();
  const spacedHref = source.replace('href="/swift/"', 'href = "/swift/"');
  const spacedRoot = await createManagedSwiftStoryDistFixture(t, { pageHtml: spacedHref });
  const spacedErrors = [];

  await validateSwiftAdapterStoryDist({ dist: join(spacedRoot, "dist"), errors: spacedErrors });

  assert.deepEqual(spacedErrors, []);

  const nonAnchorHref = source.replaceAll(/<a\b([^>]*)href="\/swift\/"([^>]*)>/g, '<span$1href="/swift/"$2>');
  const nonAnchorRoot = await createManagedSwiftStoryDistFixture(t, { pageHtml: nonAnchorHref });
  const nonAnchorErrors = [];

  await validateSwiftAdapterStoryDist({ dist: join(nonAnchorRoot, "dist"), errors: nonAnchorErrors });

  assert.match(nonAnchorErrors.join("\n"), /missing required link: \/swift\//);
});

test("validateSwiftAdapterStoryDist rejects private material and unsupported claims", async (t) => {
  const source = await sourcePage();
  const localPathLeak = `${String.fromCharCode(47)}Users/example/private.swift`;
  const cases = [
    [`<p>${localPathLeak}</p>`, /forbidden private material/],
    ["<p>TraceMap proves Swift runtime behavior.</p>", /unsupported Swift claim wording/],
    ["<p>Swift v0 validates build success.</p>", /unsupported Swift claim wording/],
    ["<p>TraceMap performs AI impact analysis.</p>", /unsupported Swift claim wording/],
    ["<p>TraceMap uses vector database analysis.</p>", /unsupported Swift claim wording/]
  ];

  for (const [body, expected] of cases) {
    const root = await createManagedSwiftStoryDistFixture(t, {
      pageHtml: source.replace("</main>", `${body}</main>`)
    });
    const errors = [];

    await validateSwiftAdapterStoryDist({ dist: join(root, "dist"), errors });

    assert.match(errors.join("\n"), expected);
  }
});

test("validateSwiftAdapterStoryDist rejects private material and unsupported claims in route metadata", async (t) => {
  const localPathLeak = `${String.fromCharCode(47)}Users/example/private.swift`;
  const root = await createManagedSwiftStoryDistFixture(t);
  const routesIndexPath = join(root, "dist", "routes-index.json");
  const routesIndex = JSON.parse(await readFile(routesIndexPath, "utf8"));
  routesIndex.entries[0].summary = `Swift adapter story from ${localPathLeak}.`;
  routesIndex.entries[0].nonClaims = [
    "No runtime behavior, app navigation, build success, release safety, raw source snippets, or hidden validation details are public Swift story claims.",
    "TraceMap performs AI impact analysis."
  ];
  await writeFile(routesIndexPath, `${JSON.stringify(routesIndex, null, 2)}\n`, "utf8");
  const errors = [];

  await validateSwiftAdapterStoryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /route metadata contains forbidden private material/);
  assert.match(errors.join("\n"), /route metadata contains unsupported Swift claim wording/);
});

async function createManagedSwiftStoryDistFixture(t, options = {}) {
  const root = await createSwiftStoryDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createSwiftStoryDistFixture({
  discoveryRoutes = [swiftStoryDiscoveryEntry()],
  sitemapRoutes = [swiftAdapterStoryRoute],
  pageHtml
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-swift-story-test-"));
  const dist = join(root, "dist");
  const routes = new Set([swiftAdapterStoryRoute, ...swiftAdapterStoryRequiredLinks, ...sitemapRoutes]);

  await mkdir(join(dist, "swift", "story"), { recursive: true });
  await writeFile(join(dist, "swift", "story", "index.html"), pageHtml ?? (await sourcePage()), "utf8");
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

function swiftStoryDiscoveryEntry() {
  return {
    path: swiftAdapterStoryRoute,
    title: "Why Swift Evidence Matters",
    summary:
      "Shipped story layer explaining why TraceMap Swift v0 matters and how to read its static evidence without upgrading it into runtime, build, production, release, or AI-analysis proof.",
    publicClaimLevel: "shipped",
    sourceType: "site-page",
    hintCategory: "evidence",
    preferredProofPath: "/swift/",
    limitations: [
      "The story orients readers to shipped Swift v0 evidence and links to the proof matrix; it is not itself raw scanner output.",
      "Swift v0 evidence remains deterministic static evidence with documented reduced-coverage and unsupported-surface gaps."
    ],
    nonClaims: [
      "No runtime behavior, app navigation, rendered UI, complete navigation, user action, production usage, endpoint performance, deployment state, release safety, stored-value proof, query execution proof, live schema proof, build success proof, or complete Swift understanding.",
      "No AI impact analysis, LLM analysis, prompt-based classification, embeddings, vector databases, raw source snippets, raw SQL, secrets, local absolute paths, raw remotes, credentials, stored values, private scan artifacts, analyzer logs, or hidden validation details are public Swift story claims."
    ]
  };
}

async function rewriteSwiftStoryRoutesIndexEntry(dist, patch) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  const entry = parsed.entries.find((item) => item.path === swiftAdapterStoryRoute);

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
  return readFile(new URL("../src/swift/story/index.html", import.meta.url), "utf8");
}

function page(body) {
  return `<!doctype html><html lang="en"><head><title>Why Swift Evidence Matters | TraceMap</title><meta property="og:type" content="article"></head><body><main>${body}</main></body></html>`;
}

function sitemap(routes) {
  const urls = [...routes]
    .sort()
    .map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`)
    .join("\n");
  return `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls}\n</urlset>\n`;
}
