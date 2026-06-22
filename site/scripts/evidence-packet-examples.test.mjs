import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { tmpdir } from "node:os";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  evidencePacketExamplesRequiredLinks,
  evidencePacketExamplesRoute,
  validateEvidencePacketExamplesDist
} from "./evidence-packet-examples.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const siteRoot = resolve(scriptDir, "..");

test("validateEvidencePacketExamplesDist accepts the evidence packet examples route", async (t) => {
  const root = await createManagedEvidencePacketExamplesFixture(t);
  const errors = [];

  await validateEvidencePacketExamplesDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateEvidencePacketExamplesDist reports missing schema field copy", async (t) => {
  const html = await sourcePage();
  const root = await createManagedEvidencePacketExamplesFixture(t, {
    examplesHtml: html.replaceAll("commit or extractor placeholder", "extractor context")
  });
  const errors = [];

  await validateEvidencePacketExamplesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing field row: commit or extractor placeholder/);
});

test("validateEvidencePacketExamplesDist reports missing required example category", async (t) => {
  const html = await sourcePage();
  const root = await createManagedEvidencePacketExamplesFixture(t, {
    examplesHtml: html.replaceAll("gap-labeled packet", "unknown packet")
  });
  const errors = [];

  await validateEvidencePacketExamplesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing category marker: gap-labeled packet/);
});

test("validateEvidencePacketExamplesDist reports missing route metadata", async (t) => {
  const root = await createManagedEvidencePacketExamplesFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateEvidencePacketExamplesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/packets\/examples\//);
});

test("validateEvidencePacketExamplesDist reports route metadata regressions", async (t) => {
  const root = await createManagedEvidencePacketExamplesFixture(t);
  await rewriteRouteEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/",
    nonClaims: ["No runtime behavior proof."]
  });
  const errors = [];

  await validateEvidencePacketExamplesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/packets\/, got \/validation\//);
  assert.match(errors.join("\n"), /nonClaims are missing required term: production traffic/);
});

test("validateEvidencePacketExamplesDist reports missing adjacent link", async (t) => {
  const html = await sourcePage();
  const root = await createManagedEvidencePacketExamplesFixture(t, {
    examplesHtml: html.replaceAll('href="/examples/scan-packet/"', 'href="/examples/scan-packet-missing/"')
  });
  const errors = [];

  await validateEvidencePacketExamplesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/examples\/scan-packet\//);
});

test("validateEvidencePacketExamplesDist rejects data-href in place of a required link", async (t) => {
  const html = await sourcePage();
  const root = await createManagedEvidencePacketExamplesFixture(t, {
    examplesHtml: html.replaceAll('href="/demo/result/"', 'data-href="/demo/result/"')
  });
  const errors = [];

  await validateEvidencePacketExamplesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/demo\/result\//);
});

test("validateEvidencePacketExamplesDist reports missing synthetic labels", async (t) => {
  const html = await sourcePage();
  const root = await createManagedEvidencePacketExamplesFixture(t, {
    examplesHtml: html.replaceAll("synthetic public-safe example", "public-safe teaching shape")
  });
  const errors = [];

  await validateEvidencePacketExamplesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: synthetic public-safe example/);
});

test("validateEvidencePacketExamplesDist reports missing stop blocked marker", async (t) => {
  const html = await sourcePage();
  const root = await createManagedEvidencePacketExamplesFixture(t, {
    examplesHtml: html.replaceAll("blocked: missing public-safe proof trail", "pending public-safe proof trail")
  });
  const errors = [];

  await validateEvidencePacketExamplesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /must include a blocked proof-path marker/);
});

test("validateEvidencePacketExamplesDist rejects forbidden claims split across tags", async (t) => {
  const html = await sourcePage();
  const root = await createManagedEvidencePacketExamplesFixture(t, {
    examplesHtml: html.replace("</main>", "<p>TraceMap pro<em>ves</em> runtime behavior.</p></main>")
  });
  const errors = [];

  await validateEvidencePacketExamplesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateEvidencePacketExamplesDist rejects raw material outside sanctioned boundaries", async (t) => {
  const html = await sourcePage();
  const root = await createManagedEvidencePacketExamplesFixture(t, {
    examplesHtml: html.replace("</main>", "<p>Share raw facts on this route.</p></main>")
  });
  const errors = [];

  await validateEvidencePacketExamplesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden raw\/private material/);
});

test("validateEvidencePacketExamplesDist rejects raw artifact filename tokens", async (t) => {
  const html = await sourcePage();
  const root = await createManagedEvidencePacketExamplesFixture(t, {
    examplesHtml: html.replace("</main>", "<p>Publish facts.ndjson and logs/analyzer.log with the example.</p></main>")
  });
  const errors = [];

  await validateEvidencePacketExamplesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden raw\/private material/);
});

test("validateEvidencePacketExamplesDist rejects hard private material in attributes", async (t) => {
  const html = await sourcePage();
  const root = await createManagedEvidencePacketExamplesFixture(t, {
    examplesHtml: html.replace("</main>", '<img alt="file&#58;//private/report"></main>')
  });
  const errors = [];

  await validateEvidencePacketExamplesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /hard private material/);
});

test("validateEvidencePacketExamplesDist reports word count outside bounds", async (t) => {
  const html = await sourcePage();
  const root = await createManagedEvidencePacketExamplesFixture(t, {
    examplesHtml: html.replace(/<section class="section" id="examples">[\s\S]*?<\/section>/, "<section><p>too short</p></section>")
  });
  const errors = [];

  await validateEvidencePacketExamplesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /word count must be between 450 and 1300 words/);
});

test("validateEvidencePacketExamplesDist reports missing inbound links from adjacent routes", async (t) => {
  const root = await createManagedEvidencePacketExamplesFixture(t, {
    includeInboundLinks: false
  });
  const errors = [];

  await validateEvidencePacketExamplesDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing inbound links from live adjacent routes: \/packets\/, \/packets\/assembly\//);
});

async function createManagedEvidencePacketExamplesFixture(t, options = {}) {
  const root = await createEvidencePacketExamplesFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createEvidencePacketExamplesFixture({
  discoveryRoutes = [evidencePacketExamplesRoute, ...evidencePacketExamplesRequiredLinks],
  examplesHtml = null,
  includeInboundLinks = true,
  sitemapRoutes = [evidencePacketExamplesRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-evidence-packet-examples-test-"));
  const dist = join(root, "dist");
  const routes = new Set([
    evidencePacketExamplesRoute,
    ...evidencePacketExamplesRequiredLinks,
    "/packets/assembly/"
  ]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    const html =
      route === evidencePacketExamplesRoute
        ? examplesHtml ?? (await sourcePage())
        : page(
            includeInboundLinks && ["/packets/", "/packets/assembly/"].includes(route)
              ? `<a href="${evidencePacketExamplesRoute}">packet examples</a>`
              : route
          );
    await writeFile(join(path, "index.html"), html, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function sourcePage() {
  return readFile(resolve(siteRoot, "src", "packets", "examples", "index.html"), "utf8");
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === evidencePacketExamplesRoute ? "Evidence Packet Examples" : `Route ${route}`,
    summary:
      route === evidencePacketExamplesRoute
        ? "Concept-level gallery of synthetic public-safe packet shapes showing claims, proof paths, tiers, coverage labels, limitations, non-claims, owners, and validation evidence."
        : "Fixture route for evidence packet examples validation.",
    publicClaimLevel: route === evidencePacketExamplesRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === evidencePacketExamplesRoute ? "use-case" : "evidence",
    ...(route === evidencePacketExamplesRoute ? { preferredProofPath: "/packets/" } : {}),
    limitations: [
      route === evidencePacketExamplesRoute
        ? "The route teaches synthetic public-safe packet shapes, not real customer, private repository, production, or raw artifact evidence."
        : "Fixture limitations remain bounded."
    ],
    nonClaims: [
      route === evidencePacketExamplesRoute
        ? "No runtime behavior, production traffic, endpoint performance, outage cause, release approval, release safety, operational safety, complete coverage, AI impact analysis, LLM analysis, autonomous approval, autonomous review, or replacement of human review."
        : "No runtime behavior proof."
    ]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: false });
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteRouteEntry(dist, patch) {
  const routesIndexPath = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(routesIndexPath, "utf8"));
  const routeEntry = parsed.entries.find((entry) => entry.path === evidencePacketExamplesRoute);
  Object.assign(routeEntry, patch);
  await writeFile(routesIndexPath, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

function page(body) {
  return `<!doctype html>
<html lang="en">
  <head><title>Fixture</title><meta name="description" content="Fixture page"></head>
  <body>
    <header><nav class="top-nav"><a href="/evidence/">Evidence</a><a href="/outputs/">Outputs</a><a href="/workflows/">Workflows</a><a href="/examples/">Examples</a><a href="/blog/">Blog</a><a href="/capabilities/">Capabilities</a><a href="/docs/">Docs</a><a href="/validation/">Validation</a><a href="/limitations/">Limitations</a><a href="/demo/">Demo</a><a href="https://github.com/joefeser/tracemap">GitHub</a></nav></header>
    <main>${body}</main>
  </body>
</html>`;
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${routes.map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`).join("\n")}
</urlset>
`;
}
