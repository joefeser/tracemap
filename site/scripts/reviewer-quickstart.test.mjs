import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  reviewerQuickstartRequiredLinks,
  reviewerQuickstartRoute,
  validateReviewerQuickstartDist
} from "./reviewer-quickstart.mjs";

test("validateReviewerQuickstartDist accepts the reviewer quickstart route", async (t) => {
  const root = await createManagedReviewerQuickstartFixture(t);
  const errors = [];

  await validateReviewerQuickstartDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateReviewerQuickstartDist reports missing quickstart step copy", async (t) => {
  const root = await createManagedReviewerQuickstartFixture(t, {
    reviewerHtml: reviewerQuickstartPage().replaceAll("read rule ID/family", "read rule family")
  });
  const errors = [];

  await validateReviewerQuickstartDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing quickstart step: read rule ID\/family/);
});

test("validateReviewerQuickstartDist reports missing route metadata", async (t) => {
  const root = await createManagedReviewerQuickstartFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateReviewerQuickstartDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/reviewer-quickstart\//);
});

test("validateReviewerQuickstartDist reports route metadata regressions", async (t) => {
  const root = await createManagedReviewerQuickstartFixture(t);
  await rewriteRouteEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/",
    nonClaims: ["No runtime behavior proof."]
  });
  const errors = [];

  await validateReviewerQuickstartDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
  assert.match(errors.join("\n"), /nonClaims are missing required term: production traffic/);
});

test("validateReviewerQuickstartDist reports malformed metadata without crashing", async (t) => {
  const root = await createManagedReviewerQuickstartFixture(t);
  await rewriteRouteEntry(join(root, "dist"), {
    limitations: 42,
    summary: "TraceMap proves runtime behavior."
  });
  const errors = [];

  await validateReviewerQuickstartDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /must include limitations metadata/);
  assert.match(errors.join("\n"), /forbidden public claim in metadata/);
  assert.match(errors.join("\n"), /\[routes-index\.json\]/);
});

test("validateReviewerQuickstartDist reports missing required adjacent link", async (t) => {
  const root = await createManagedReviewerQuickstartFixture(t, {
    reviewerHtml: reviewerQuickstartPage().replaceAll('href="/demo/manager-script/"', 'href="/demo/manager-script-missing/"')
  });
  const errors = [];

  await validateReviewerQuickstartDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/demo\/manager-script\//);
});

test("validateReviewerQuickstartDist rejects data-href in place of a required link", async (t) => {
  const root = await createManagedReviewerQuickstartFixture(t, {
    reviewerHtml: reviewerQuickstartPage().replaceAll('href="/review-room/"', 'data-href="/review-room/"')
  });
  const errors = [];

  await validateReviewerQuickstartDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/review-room\//);
});

test("validateReviewerQuickstartDist rejects positive runtime proof claims", async (t) => {
  const root = await createManagedReviewerQuickstartFixture(t, {
    reviewerHtml: reviewerQuickstartPage("<p>TraceMap proves runtime behavior for reviewers.</p>")
  });
  const errors = [];

  await validateReviewerQuickstartDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateReviewerQuickstartDist rejects forbidden claims split across tags", async (t) => {
  const root = await createManagedReviewerQuickstartFixture(t, {
    reviewerHtml: reviewerQuickstartPage("<p>TraceMap pro<em>ves</em> runtime behavior.</p>")
  });
  const errors = [];

  await validateReviewerQuickstartDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateReviewerQuickstartDist permits sanctioned boundary copy", async (t) => {
  const root = await createManagedReviewerQuickstartFixture(t, {
    reviewerHtml: reviewerQuickstartPage(`
      <section data-reviewer-boundary="test">
        <p>TraceMap does not prove runtime behavior, production traffic, endpoint performance, outage cause, release approval, release safety, operational safety, complete coverage, AI impact analysis, LLM analysis, embeddings, vector database analysis, prompt classification, or autonomous approval.</p>
        <p>Do not publish raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values.</p>
      </section>
    `)
  });
  const errors = [];

  await validateReviewerQuickstartDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateReviewerQuickstartDist rejects raw material outside sanctioned sections", async (t) => {
  const root = await createManagedReviewerQuickstartFixture(t, {
    reviewerHtml: reviewerQuickstartPage("<p>Share raw facts in the public handoff.</p>")
  });
  const errors = [];

  await validateReviewerQuickstartDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden raw\/private material/);
});

test("validateReviewerQuickstartDist rejects encoded hard private text", async (t) => {
  const root = await createManagedReviewerQuickstartFixture(t, {
    reviewerHtml: reviewerQuickstartPage("<p>file&#58;//private/review</p>")
  });
  const errors = [];

  await validateReviewerQuickstartDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /hard private material/);
});

test("validateReviewerQuickstartDist reports word count outside bounds", async (t) => {
  const root = await createManagedReviewerQuickstartFixture(t, {
    reviewerHtml: reviewerQuickstartPage("", { fillerWords: 0 })
  });
  const errors = [];

  await validateReviewerQuickstartDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /word count must be between 500 and 1400 words/);
});

test("validateReviewerQuickstartDist reports missing inbound links from adjacent routes", async (t) => {
  const root = await createManagedReviewerQuickstartFixture(t, {
    includeInboundLinks: false
  });
  const errors = [];

  await validateReviewerQuickstartDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing inbound links from live adjacent routes: \/review-room\/, \/packets\/assembly\//);
});

async function createManagedReviewerQuickstartFixture(t, options = {}) {
  const root = await createReviewerQuickstartFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createReviewerQuickstartFixture({
  discoveryRoutes = null,
  includeInboundLinks = true,
  reviewerHtml = reviewerQuickstartPage(),
  sitemapRoutes = [reviewerQuickstartRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-reviewer-quickstart-test-"));
  const dist = join(root, "dist");
  const routes = new Set([
    reviewerQuickstartRoute,
    ...reviewerQuickstartRequiredLinks,
    "/review-room/",
    "/packets/assembly/"
  ]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    const html =
      route === reviewerQuickstartRoute
        ? reviewerHtml
        : page(includeInboundLinks && ["/review-room/", "/packets/assembly/"].includes(route) ? `<a href="${reviewerQuickstartRoute}">quickstart</a>` : route);
    await writeFile(join(path, "index.html"), html, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes ?? [...routes]);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === reviewerQuickstartRoute ? "Reviewer Quickstart" : `Route ${route}`,
    summary:
      route === reviewerQuickstartRoute
        ? "Five-minute concept-level guide for inspecting a public-safe TraceMap evidence packet before repeating or routing a claim."
        : "Fixture route for reviewer quickstart validation.",
    publicClaimLevel: route === reviewerQuickstartRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === reviewerQuickstartRoute ? "use-case" : "evidence",
    ...(route === reviewerQuickstartRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: [
      route === reviewerQuickstartRoute
        ? "The route is first-stop reviewer orientation over existing public-safe packet and proof-path surfaces, not a complete review workflow or new evidence source."
        : "Fixture limitations remain bounded."
    ],
    nonClaims:
      route === reviewerQuickstartRoute
        ? [
            "No runtime behavior, production traffic, endpoint performance, outage cause, release approval, release safety, operational safety, complete coverage, AI impact analysis, LLM analysis, embeddings, vector database analysis, prompt classification, autonomous approval, or replacement of tests proof."
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
    entry.path === reviewerQuickstartRoute
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

function reviewerQuickstartPage(extra = "", { fillerWords = 90 } = {}) {
  const filler = Array.from({ length: fillerWords }, (_, index) => `reviewer quickstart evidence boundary ${index}`).join(" ");
  const links = reviewerQuickstartRequiredLinks.map((route) => `<a href="${route}">${route}</a>`).join("\n");
  const steps = [
    "identify the claim",
    "find the proof path",
    "check public claim level",
    "read rule ID/family",
    "inspect evidence tier and coverage label",
    "check commit/extractor context",
    "read limitations/non-claims",
    "name next owner",
    "stop on missing evidence"
  ];
  const fields = [
    "claim",
    "proof path",
    "public claim level",
    "rule ID or rule family",
    "evidence tier",
    "coverage label",
    "commit SHA or source revision context",
    "extractor version or extractor family",
    "file path and line span when public-safe",
    "limitation",
    "non-claim",
    "validation evidence",
    "unresolved gap",
    "next owner"
  ];

  return page(`
    <title>Reviewer Quickstart | TraceMap</title>
    <meta name="description" content="Reviewer quickstart fixture">
    <link rel="canonical" href="https://tracemap.tools/reviewer-quickstart/">
    <meta property="og:type" content="article">
    <meta property="og:title" content="TraceMap Reviewer Quickstart">
    <meta property="og:description" content="Reviewer quickstart fixture">
    <meta property="og:url" content="https://tracemap.tools/reviewer-quickstart/">
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>This route is a five minutes reviewer guide. Missing evidence creates a follow-up owner question, not a clean conclusion.</p>
    <h2>Start Here</h2>
    <h2>Five-Minute Review</h2>
    ${steps.map((step) => `<article data-quickstart-step="${step}"><h3>${step}</h3><p>${step} guidance.</p></article>`).join("\n")}
    <h2>Evidence Fields</h2>
    <table>${fields.map((field) => `<tr data-evidence-field="${field}"><td>${field}</td><td>${field} reviewer check.</td></tr>`).join("\n")}</table>
    <p>Tier1Semantic Tier2Structural Tier3SyntaxOrTextual Tier4Unknown</p>
    <section data-reviewer-boundary="stop-conditions">
      <h2>Stop Conditions</h2>
      <p>missing proof path missing rule ID or rule family missing evidence tier missing coverage label missing limitation missing public claim level missing commit or extractor context without explicit limitation no validation evidence no next owner private-only support presented as public proof raw artifact leakage unsupported wording.</p>
    </section>
    <h2>Safe Review Language</h2>
    <p>inspect check follow review compare label record route escalate cannot conclude from this packet.</p>
    <h2>Escalation Owners</h2>
    <p>reviewer owner source review owner code owner service owner database owner test owner validation owner telemetry or runtime owner release owner manager or decision owner.</p>
    <section data-reviewer-boundary="non-claims">
      <h2>Non-Claims</h2>
      <p>This quickstart does not prove runtime behavior, production traffic, endpoint performance, outage cause, release approval, release safety, operational safety, complete coverage, AI impact analysis, LLM analysis, embeddings, vector database analysis, prompt classification, autonomous approval, or replacement of tests.</p>
    </section>
    ${links}
    <p>${filler}</p>
    ${extra}
  `);
}
