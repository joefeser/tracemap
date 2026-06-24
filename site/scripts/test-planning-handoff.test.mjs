import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  testPlanningHandoffRequiredLinks,
  testPlanningHandoffRoute,
  validateTestPlanningHandoffDist
} from "./test-planning-handoff.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePagePath = join(scriptDir, "..", "src", "test-planning", "index.html");

test("validateTestPlanningHandoffDist accepts the canonical test planning route", async (t) => {
  const root = await createManagedFixture(t);
  const errors = [];

  await validateTestPlanningHandoffDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateTestPlanningHandoffDist reports missing required field rows", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await canonicalPage()).replaceAll('data-test-planning-field="rule ID/family"', 'data-test-planning-field="rule family"')
  });
  const errors = [];

  await validateTestPlanningHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required field row: rule ID\/family/);
});

test("validateTestPlanningHandoffDist reports missing stop-condition markers", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await canonicalPage()).replaceAll('data-stop-condition="private-only evidence"', 'data-stop-condition="private evidence"')
  });
  const errors = [];

  await validateTestPlanningHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing stop condition marker: private-only evidence/);
});

test("validateTestPlanningHandoffDist reports route metadata regressions", async (t) => {
  const root = await createManagedFixture(t);
  await rewriteRouteEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/",
    nonClaims: ["No runtime behavior proof."]
  });
  const errors = [];

  await validateTestPlanningHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
  assert.match(errors.join("\n"), /nonClaims are missing required term: generated tests/);
});

test("validateTestPlanningHandoffDist reports missing required links", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await canonicalPage()).replaceAll('href="/proof-paths/tour/"', 'href="/proof-paths/tour-missing/"')
  });
  const errors = [];

  await validateTestPlanningHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/proof-paths\/tour\//);
});

test("validateTestPlanningHandoffDist rejects data-href in place of a required link", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await canonicalPage()).replaceAll('href="/review-claim-checklist/"', 'data-href="/review-claim-checklist/"')
  });
  const errors = [];

  await validateTestPlanningHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/review-claim-checklist\//);
});

test("validateTestPlanningHandoffDist reports missing neighbor distinction statements", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await canonicalPage()).replace(
      "Validation proof and quality signals; this page asks what validation evidence a human should seek.",
      "Validation context."
    )
  });
  const errors = [];

  await validateTestPlanningHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing neighbor distinction for \/validation\//);
});

test("validateTestPlanningHandoffDist rejects positive generated-test claims", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await canonicalPage()).replace("</main>", "<p>TraceMap generates tests for this handoff.</p></main>")
  });
  const errors = [];

  await validateTestPlanningHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateTestPlanningHandoffDist rejects forbidden claims split across tags", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await canonicalPage()).replace("</main>", "<p>TraceMap gen<em>erates</em> tests.</p></main>")
  });
  const errors = [];

  await validateTestPlanningHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateTestPlanningHandoffDist permits sanctioned boundary copy", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await canonicalPage()).replace(
      "</main>",
      `<section data-test-planning-boundary="non-claims">
        <p>TraceMap does not generate tests, prove test sufficiency, prove runtime behavior, prove production traffic, prove endpoint performance, approve releases, or provide complete coverage.</p>
        <p>Do not publish raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values.</p>
      </section></main>`
    )
  });
  const errors = [];

  await validateTestPlanningHandoffDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateTestPlanningHandoffDist rejects lookalike boundary copy", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await canonicalPage()).replace(
      "</main>",
      `<section data-test-planning-boundary="fixture">
        <p>TraceMap generates tests.</p>
      </section></main>`
    )
  });
  const errors = [];

  await validateTestPlanningHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateTestPlanningHandoffDist rejects raw material outside sanctioned sections", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await canonicalPage()).replace("</main>", "<p>Share raw facts in the public handoff.</p></main>")
  });
  const errors = [];

  await validateTestPlanningHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden raw\/private material/);
});

test("validateTestPlanningHandoffDist rejects encoded hard private text", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await canonicalPage()).replace("</main>", "<p>file&#58;//private/review</p></main>")
  });
  const errors = [];

  await validateTestPlanningHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /hard private material/);
});

test("validateTestPlanningHandoffDist reports word count outside bounds", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: testPlanningPage({ fillerWords: 0 })
  });
  const errors = [];

  await validateTestPlanningHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /word count must be between 450 and 1600 words/);
});

test("validateTestPlanningHandoffDist reports missing inbound links from adjacent routes", async (t) => {
  const root = await createManagedFixture(t, {
    includeInboundLinks: false
  });
  const errors = [];

  await validateTestPlanningHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing inbound links from live adjacent routes: \/reviewer-quickstart\/, \/packets\/assembly\//);
});

async function createManagedFixture(t, options = {}) {
  const root = await createFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createFixture({
  discoveryRoutes = null,
  includeInboundLinks = true,
  pageHtml = null,
  sitemapRoutes = [testPlanningHandoffRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-test-planning-handoff-test-"));
  const dist = join(root, "dist");
  const routes = new Set([
    testPlanningHandoffRoute,
    "/proof-paths/",
    ...testPlanningHandoffRequiredLinks,
    "/reviewer-quickstart/",
    "/packets/assembly/"
  ]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    const html =
      route === testPlanningHandoffRoute
        ? pageHtml ?? (await canonicalPage())
        : page(
            includeInboundLinks && ["/reviewer-quickstart/", "/packets/assembly/"].includes(route)
              ? `<a href="${testPlanningHandoffRoute}">test planning handoff</a>`
              : route
          );
    await writeFile(join(path, "index.html"), html, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes ?? [...routes]);

  return root;
}

async function canonicalPage() {
  return readFile(sourcePagePath, "utf8");
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === testPlanningHandoffRoute ? "Test Planning Handoff" : `Route ${route}`,
    summary:
      route === testPlanningHandoffRoute
        ? "Concept-level handoff for turning TraceMap deterministic static evidence into human-owned test-planning questions."
        : "Fixture route for test planning handoff validation.",
    publicClaimLevel: route === testPlanningHandoffRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === testPlanningHandoffRoute ? "use-case" : "evidence",
    ...(route === testPlanningHandoffRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: [
      route === testPlanningHandoffRoute
        ? "The route translates static evidence into test-planning questions; it is not a generated test workflow, validation result, or release gate."
        : "Fixture limitations remain bounded."
    ],
    nonClaims:
      route === testPlanningHandoffRoute
        ? [
            "No generated tests, test sufficiency, runtime behavior, production traffic, endpoint performance, release safety, release approval, complete coverage, AI impact analysis, LLM analysis, or replacement of QA proof."
          ]
        : ["No runtime behavior, production usage, deployment state, endpoint performance, or release approval proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteRouteEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === testPlanningHandoffRoute
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

function testPlanningPage({ fillerWords = 80 } = {}) {
  const filler = Array.from({ length: fillerWords }, (_, index) => `test planning evidence boundary ${index}`).join(" ");
  const links = testPlanningHandoffRequiredLinks.map((route) => `<a href="${route}">${route}</a>`).join("\n");
  const fields = [
    "claim label",
    "proof path",
    "rule ID/family",
    "evidence tier",
    "coverage label",
    "changed surface",
    "limitation",
    "suggested test question",
    "next owner",
    "validation evidence",
    "non-claim"
  ];
  const stops = [
    "missing proof path",
    "private-only evidence",
    "reduced coverage",
    "concept-only or demo-only evidence",
    "no validation evidence",
    "uncertain owner",
    "a question requiring runtime observability"
  ];

  return page(`
    <title>Test Planning Handoff | TraceMap</title>
    <meta name="description" content="Test planning handoff fixture">
    <link rel="canonical" href="https://tracemap.tools/test-planning/">
    <meta property="og:type" content="article">
    <meta property="og:title" content="TraceMap Test Planning Handoff">
    <meta property="og:description" content="Test planning handoff fixture">
    <meta property="og:url" content="https://tracemap.tools/test-planning/">
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>Humans still choose, write, run, review, and interpret tests.</p>
    <h2>Static Evidence Input</h2>
    <table>${fields.map((field) => `<tr data-test-planning-field="${field}"><td>${field}</td><td>${field} use.</td></tr>`).join("\n")}</table>
    <p>Tier1Semantic Tier2Structural Tier3SyntaxOrTextual Tier4Unknown</p>
    <h2>Test-Planning Questions</h2>
    <h2>Coverage Caveats</h2>
    <p>partial reduced gap syntax-only demo-only concept-only.</p>
    <h2>Examples Of Safe Handoff Language</h2>
    <article data-safe-handoff-example><p>Proof path. Limitation. Next owner. Validation evidence. Non-claim.</p></article>
    <article data-safe-handoff-example><p>Proof path. Limitation. Next owner. Validation evidence. Non-claim.</p></article>
    <article data-safe-handoff-example><p>Proof path. Limitation. Next owner. Validation evidence. Non-claim.</p></article>
    <section data-test-planning-boundary="stop-conditions">
      <h2>Stop Conditions</h2>
      <ul>${stops.map((stop) => `<li data-stop-condition="${stop}">${stop}</li>`).join("\n")}</ul>
    </section>
    <h2>Test Owner Handoff</h2>
    <p>test owner service owner database owner source reviewer validation owner release owner security review.</p>
    <h2>Neighbor Boundaries</h2>
    <p data-neighbor-route="/reviewer-quickstart/">General reviewer orientation; this page is the narrower test-planning handoff aid.</p>
    <p data-neighbor-route="/packets/assembly/">Packet assembly instructions; this page uses assembled evidence as input for questions.</p>
    <p data-neighbor-route="/review-claim-checklist/">Public claim review; this page frames test-owner conversations.</p>
    <p data-neighbor-route="/validation/">Validation proof and quality signals; this page asks what validation evidence a human should seek.</p>
    <p data-neighbor-route="/proof-paths/tour/">Proof-path walkthrough; this page applies proof paths to test conversations.</p>
    <p data-neighbor-route="/questions/objections/">Objection handling; this page gives practical handoff language for the next test-owner question.</p>
    <section data-test-planning-boundary="non-claims">
      <h2>Non-Claims</h2>
      <p>TraceMap does not generate tests, prove test sufficiency, prove runtime behavior, prove production traffic, prove endpoint performance, approve releases, provide complete coverage, perform AI impact analysis, LLM analysis, or replace QA.</p>
    </section>
    ${links}
    <p>${filler}</p>
  `);
}
