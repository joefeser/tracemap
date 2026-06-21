import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  proofPathTourRequiredLinks,
  proofPathTourRoute,
  validateProofPathTourDist
} from "./proof-path-tour.mjs";

test("validateProofPathTourDist accepts the guided proof-path tour route", async (t) => {
  const root = await createManagedProofPathTourFixture(t);
  const errors = [];

  await validateProofPathTourDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateProofPathTourDist reports missing required proof-step marker", async (t) => {
  const root = await createManagedProofPathTourFixture(t, {
    tourHtml: proofPathTourPage().replaceAll('data-proof-tour-step="commit SHA"', 'data-proof-tour-step="commit context"')
  });
  const errors = [];

  await validateProofPathTourDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing proof-step marker: commit SHA/);
});

test("validateProofPathTourDist reports missing route metadata", async (t) => {
  const root = await createManagedProofPathTourFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateProofPathTourDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/proof-paths\/tour\//);
});

test("validateProofPathTourDist reports route metadata regressions", async (t) => {
  const root = await createManagedProofPathTourFixture(t);
  await rewriteRouteEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "use-case",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/",
    nonClaims: ["No runtime behavior proof."]
  });
  const errors = [];

  await validateProofPathTourDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory evidence, got use-case/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
  assert.match(errors.join("\n"), /nonClaims are missing required term: production traffic/);
});

test("validateProofPathTourDist reports missing required adjacent link", async (t) => {
  const root = await createManagedProofPathTourFixture(t, {
    tourHtml: proofPathTourPage().replaceAll('href="/glossary/"', 'href="/missing-glossary/"')
  });
  const errors = [];

  await validateProofPathTourDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/glossary\//);
});

test("validateProofPathTourDist rejects data-href in place of a required link", async (t) => {
  const root = await createManagedProofPathTourFixture(t, {
    tourHtml: proofPathTourPage().replaceAll('href="/glossary/"', 'data-href="/glossary/"')
  });
  const errors = [];

  await validateProofPathTourDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/glossary\//);
});

test("validateProofPathTourDist rejects positive runtime proof claims", async (t) => {
  const root = await createManagedProofPathTourFixture(t, {
    tourHtml: proofPathTourPage("<p>TraceMap proves runtime behavior for this endpoint.</p>")
  });
  const errors = [];

  await validateProofPathTourDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateProofPathTourDist rejects forbidden claims split across tags", async (t) => {
  const root = await createManagedProofPathTourFixture(t, {
    tourHtml: proofPathTourPage("<p>TraceMap pro<em>ves</em> runtime behavior.</p>")
  });
  const errors = [];

  await validateProofPathTourDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateProofPathTourDist rejects runtime proof wording in attributes", async (t) => {
  const root = await createManagedProofPathTourFixture(t, {
    tourHtml: proofPathTourPage('<img alt="TraceMap proves runtime behavior">')
  });
  const errors = [];

  await validateProofPathTourDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateProofPathTourDist permits sanctioned boundary wording", async (t) => {
  const root = await createManagedProofPathTourFixture(t, {
    tourHtml: proofPathTourPage(`
      <section id="non-claims" data-tm-boundary="non-claims">
        <p>Do not claim runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, release approval, complete coverage, AI impact analysis, LLM analysis, embeddings, vector databases, prompt classification, autonomous approval, raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, hidden validation details, raw command output, or credential-like values.</p>
      </section>
    `)
  });
  const errors = [];

  await validateProofPathTourDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateProofPathTourDist rejects raw material outside sanctioned sections", async (t) => {
  const root = await createManagedProofPathTourFixture(t, {
    tourHtml: proofPathTourPage("<p>Publish raw facts with the claim.</p>")
  });
  const errors = [];

  await validateProofPathTourDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden raw\/private material/);
});

test("validateProofPathTourDist rejects encoded hard private text", async (t) => {
  const root = await createManagedProofPathTourFixture(t, {
    tourHtml: proofPathTourPage("<p>file&#58;//private/report</p>")
  });
  const errors = [];

  await validateProofPathTourDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /hard private material/);
});

test("validateProofPathTourDist reports missing worked example traversal", async (t) => {
  const root = await createManagedProofPathTourFixture(t, {
    tourHtml: proofPathTourPage().replaceAll('data-proof-tour-example-field="extractor version"', 'data-proof-tour-example-field="schema version"')
  });
  const errors = [];

  await validateProofPathTourDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /worked example is missing required field traversal: extractor version/);
});

test("validateProofPathTourDist reports word count outside bounds", async (t) => {
  const root = await createManagedProofPathTourFixture(t, {
    tourHtml: proofPathTourPage("", { fillerWords: 0 })
  });
  const errors = [];

  await validateProofPathTourDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /word count must be between 650 and 1600 words/);
});

test("validateProofPathTourDist reports missing inbound links from adjacent routes", async (t) => {
  const root = await createManagedProofPathTourFixture(t, {
    includeInboundLinks: false
  });
  const errors = [];

  await validateProofPathTourDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing inbound links from live adjacent routes: \/proof-paths\//);
});

async function createManagedProofPathTourFixture(t, options = {}) {
  const root = await createProofPathTourFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createProofPathTourFixture({
  discoveryRoutes = [proofPathTourRoute, ...proofPathTourRequiredLinks],
  includeInboundLinks = true,
  sitemapRoutes = [proofPathTourRoute],
  tourHtml = proofPathTourPage()
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-proof-path-tour-test-"));
  const dist = join(root, "dist");
  const routes = new Set([proofPathTourRoute, ...proofPathTourRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    const body =
      route === proofPathTourRoute
        ? tourHtml
        : page(includeInboundLinks && route === "/proof-paths/" ? `<a href="${proofPathTourRoute}">tour</a>` : route);
    await writeFile(join(path, "index.html"), body, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === proofPathTourRoute ? "Guided Proof-Path Tour" : `Route ${route}`,
    summary:
      route === proofPathTourRoute
        ? "Concept-level guided reading flow for inspecting one public claim through proof path, rule family, evidence tier, coverage label, source context, limitation, non-claim, and next owner."
        : "Fixture route for proof-path tour validation.",
    publicClaimLevel: route === proofPathTourRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: "evidence",
    ...(route === proofPathTourRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: [
      route === proofPathTourRoute
        ? "The tour is concept-level reading guidance over existing public-safe evidence surfaces, not a proof engine or approval workflow."
        : "Fixture limitations remain bounded."
    ],
    nonClaims:
      route === proofPathTourRoute
        ? [
            "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, release approval, complete coverage, AI impact analysis, LLM analysis, embeddings, vector databases, prompt classification, autonomous approval, or replacement for tests, code review, source review, runtime observability, or human judgment."
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
  parsed.entries = parsed.entries.map((entry) => (entry.path === proofPathTourRoute ? { ...entry, ...fields } : entry));
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${routes.map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`).join("\n")}
</urlset>`;
}

function page(body) {
  return `<!doctype html><html><head>
    <title>Fixture</title>
    <meta property="og:type" content="article">
  </head><body><main>${body}</main></body></html>`;
}

function proofPathTourPage(extra = "", { fillerWords = 210 } = {}) {
  const stepRows = [
    "claim label",
    "public claim level",
    "proof path",
    "rule ID/family",
    "evidence tier",
    "coverage label",
    "commit SHA",
    "extractor version",
    "supporting public route/artifact",
    "limitation",
    "non-claim",
    "next owner"
  ]
    .map((field) => `<article id="${anchorFor(field)}" data-proof-tour-step="${field}"><h3>${field}</h3><p>${field} check and stop condition.</p></article>`)
    .join("\n");
  const exampleRows = [
    "claim label",
    "public claim level",
    "proof path",
    "rule ID/family",
    "evidence tier",
    "coverage label",
    "commit SHA",
    "extractor version",
    "supporting public route/artifact",
    "limitation",
    "non-claim",
    "next owner",
    "bounded non-claim conclusion"
  ]
    .map((field) => `<div data-proof-tour-example-field="${field}"><strong>${field}</strong><span>${field} value.</span></div>`)
    .join("\n");
  const filler = Array.from({ length: fillerWords }, (_, index) => `guided evidence tour ${index}`).join(" ");

  return page(`
    <title>Guided Proof-Path Tour | TraceMap</title>
    <meta name="description" content="Guided proof-path tour fixture.">
    <link rel="canonical" href="https://tracemap.tools/proof-paths/tour/">
    <meta property="og:type" content="article">
    <meta property="og:title" content="Guided Proof-Path Tour">
    <meta property="og:description" content="Fixture">
    <meta property="og:url" content="https://tracemap.tools/proof-paths/tour/">
    <p>Public claim level: concept. No public conclusion without evidence. This is a guided explanation, not a proof engine.</p>
    <p>not a real product claim. Tier1Semantic Tier2Structural Tier3SyntaxOrTextual Tier4Unknown. full partial reduced unknown gap-labeled.</p>
    <p>rule ID/family evidence tier coverage label commit/source context extractor version limitation supporting public route/artifact.</p>
    ${stepRows}
    <section id="supporting-public-route-artifact"><p>supporting public route/artifact anchor.</p></section>
    <section id="worked-example" data-worked-example="illustrative-not-real">
      <p>Illustrative worked example; not a real product claim.</p>
      ${exampleRows}
      <p>Bounded non-claim conclusion: stop before repeating it as a real TraceMap finding.</p>
    </section>
    <section id="where-to-stop" data-tm-boundary="where-to-stop"><p>Stop when rule ID/family, evidence tier, coverage label, commit/source context, extractor version, limitation, or supporting public route/artifact is missing.</p></section>
    <section id="non-claims" data-tm-boundary="non-claims"><p>Do not claim runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, release approval, complete coverage, AI impact analysis, LLM analysis, embeddings, vector databases, prompt classification, autonomous approval, raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, hidden validation details, raw command output, or credential-like values.</p></section>
    ${proofPathTourRequiredLinks.map((link) => `<a href="${link}">${link}</a>`).join("\n")}
    <p>${filler}</p>
    ${extra}
  `);
}

function anchorFor(field) {
  return (
    {
      "rule ID/family": "rule-id-family",
      "commit SHA": "commit-source-context",
      "supporting public route/artifact": "supporting-public-route-artifact",
      "non-claim": "step-non-claim"
    }[field] ?? field.replaceAll(" ", "-")
  );
}
