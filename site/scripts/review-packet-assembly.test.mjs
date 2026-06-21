import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  reviewPacketAssemblyRequiredLinks,
  reviewPacketAssemblyRoute,
  validateReviewPacketAssemblyDist
} from "./review-packet-assembly.mjs";

test("validateReviewPacketAssemblyDist accepts the review packet assembly route", async (t) => {
  const root = await createManagedReviewPacketAssemblyFixture(t);
  const errors = [];

  await validateReviewPacketAssemblyDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateReviewPacketAssemblyDist reports missing required ingredient copy", async (t) => {
  const root = await createManagedReviewPacketAssemblyFixture(t, {
    assemblyHtml: reviewPacketAssemblyPage().replaceAll("commit SHA", "commit context")
  });
  const errors = [];

  await validateReviewPacketAssemblyDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required ingredient: commit SHA/);
});

test("validateReviewPacketAssemblyDist reports missing route metadata", async (t) => {
  const root = await createManagedReviewPacketAssemblyFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateReviewPacketAssemblyDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/packets\/assembly\//);
});

test("validateReviewPacketAssemblyDist reports route metadata regressions", async (t) => {
  const root = await createManagedReviewPacketAssemblyFixture(t);
  await rewriteRouteEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/",
    nonClaims: ["No runtime behavior proof."]
  });
  const errors = [];

  await validateReviewPacketAssemblyDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
  assert.match(errors.join("\n"), /nonClaims are missing required term: production traffic/);
});

test("validateReviewPacketAssemblyDist reports missing required adjacent link", async (t) => {
  const root = await createManagedReviewPacketAssemblyFixture(t, {
    assemblyHtml: reviewPacketAssemblyPage().replaceAll('href="/manager-packet/"', 'href="/manager-packet-missing/"')
  });
  const errors = [];

  await validateReviewPacketAssemblyDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/manager-packet\//);
});

test("validateReviewPacketAssemblyDist rejects positive generated packet-builder claims", async (t) => {
  const root = await createManagedReviewPacketAssemblyFixture(t, {
    assemblyHtml: reviewPacketAssemblyPage("<p>TraceMap generated packet-builder output for the user.</p>")
  });
  const errors = [];

  await validateReviewPacketAssemblyDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateReviewPacketAssemblyDist rejects runtime proof wording in attributes", async (t) => {
  const root = await createManagedReviewPacketAssemblyFixture(t, {
    assemblyHtml: reviewPacketAssemblyPage('<img alt="TraceMap proves runtime behavior">')
  });
  const errors = [];

  await validateReviewPacketAssemblyDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateReviewPacketAssemblyDist permits sanctioned stop-condition and non-claim wording", async (t) => {
  const root = await createManagedReviewPacketAssemblyFixture(t, {
    assemblyHtml: reviewPacketAssemblyPage(`
      <section data-boundary-region>
        <p>TraceMap does not prove runtime behavior, production traffic, endpoint performance, outage cause, release approval or safety, operational safety, complete coverage, AI impact analysis, LLM analysis, autonomous review, or generated packet-builder behavior.</p>
        <p>Do not include raw facts, raw SQLite content, analyzer logs, source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private names, or hidden validation details.</p>
      </section>
    `)
  });
  const errors = [];

  await validateReviewPacketAssemblyDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateReviewPacketAssemblyDist rejects raw material outside sanctioned sections", async (t) => {
  const root = await createManagedReviewPacketAssemblyFixture(t, {
    assemblyHtml: reviewPacketAssemblyPage("<p>Share raw facts in the handoff.</p>")
  });
  const errors = [];

  await validateReviewPacketAssemblyDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden raw\/private material/);
});

test("validateReviewPacketAssemblyDist rejects encoded hard private text", async (t) => {
  const root = await createManagedReviewPacketAssemblyFixture(t, {
    assemblyHtml: reviewPacketAssemblyPage("<p>file&#58;//private/report</p>")
  });
  const errors = [];

  await validateReviewPacketAssemblyDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /hard private material/);
});

test("validateReviewPacketAssemblyDist reports word count outside bounds", async (t) => {
  const root = await createManagedReviewPacketAssemblyFixture(t, {
    assemblyHtml: reviewPacketAssemblyPage("", { fillerWords: 0 })
  });
  const errors = [];

  await validateReviewPacketAssemblyDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /word count must be between 400 and 1500 words/);
});

test("validateReviewPacketAssemblyDist reports missing inbound links from adjacent routes", async (t) => {
  const root = await createManagedReviewPacketAssemblyFixture(t, {
    includeInboundLinks: false
  });
  const errors = [];

  await validateReviewPacketAssemblyDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing inbound links from live adjacent routes: \/packets\/, \/review-room\//);
});

async function createManagedReviewPacketAssemblyFixture(t, options = {}) {
  const root = await createReviewPacketAssemblyFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createReviewPacketAssemblyFixture({
  assemblyHtml = reviewPacketAssemblyPage(),
  discoveryRoutes = [reviewPacketAssemblyRoute, ...reviewPacketAssemblyRequiredLinks],
  includeInboundLinks = true,
  sitemapRoutes = [reviewPacketAssemblyRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-review-packet-assembly-test-"));
  const dist = join(root, "dist");
  const routes = new Set([reviewPacketAssemblyRoute, ...reviewPacketAssemblyRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    const body =
      route === reviewPacketAssemblyRoute
        ? assemblyHtml
        : page(includeInboundLinks && ["/packets/", "/review-room/"].includes(route) ? `<a href="${reviewPacketAssemblyRoute}">assembly</a>` : route);
    await writeFile(join(path, "index.html"), body, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === reviewPacketAssemblyRoute ? "Review Packet Assembly" : `Route ${route}`,
    summary:
      route === reviewPacketAssemblyRoute
        ? "Concept-level checklist for assembling public-safe review handoff material from existing TraceMap evidence surfaces."
        : "Fixture route for review packet assembly validation.",
    publicClaimLevel: route === reviewPacketAssemblyRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === reviewPacketAssemblyRoute ? "use-case" : "evidence",
    ...(route === reviewPacketAssemblyRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: ["Fixture limitations remain bounded."],
    nonClaims:
      route === reviewPacketAssemblyRoute
        ? [
            "No runtime behavior, production traffic, endpoint performance, outage cause, release approval or safety, operational safety, AI impact analysis, LLM analysis, autonomous review, generated packet-builder behavior, or complete coverage proof."
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
  parsed.entries = parsed.entries.map((entry) => (entry.path === reviewPacketAssemblyRoute ? { ...entry, ...fields } : entry));
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

function reviewPacketAssemblyPage(extra = "", { fillerWords = 80 } = {}) {
  const filler = Array.from({ length: fillerWords }, (_, index) => `bounded evidence assembly ${index}`).join(" ");

  return page(`
    <p>Public claim level: concept. No public conclusion without evidence. This is not a generated packet-builder feature.</p>
    <table>
      <tr data-packet-ingredient="claim being reviewed"><td>claim being reviewed</td></tr>
      <tr data-packet-ingredient="audience"><td>audience</td></tr>
      <tr data-packet-ingredient="proof path"><td>proof path</td><td>A public-safe trail or named private review location.</td></tr>
      <tr data-packet-ingredient="public claim level"><td>public claim level</td></tr>
      <tr data-packet-ingredient="rule ID or rule family"><td>rule ID or rule family</td></tr>
      <tr data-packet-ingredient="evidence tier"><td>evidence tier Tier1Semantic Tier2Structural Tier3SyntaxOrTextual Tier4Unknown</td></tr>
      <tr data-packet-ingredient="coverage label"><td>coverage label</td></tr>
      <tr data-packet-ingredient="commit SHA"><td>commit SHA</td></tr>
      <tr data-packet-ingredient="extractor version"><td>extractor version</td></tr>
      <tr data-packet-ingredient="public-safe file path and line span"><td>public-safe file path and line span</td></tr>
      <tr data-packet-ingredient="limitations"><td>limitations</td></tr>
      <tr data-packet-ingredient="non-claims"><td>non-claims</td></tr>
      <tr data-packet-ingredient="next owner"><td>next owner</td></tr>
      <tr data-packet-ingredient="validation evidence"><td>validation evidence</td></tr>
      <tr data-packet-ingredient="unresolved gaps"><td>unresolved gaps</td></tr>
    </table>
    <p>Missing fields stay visible as limitations.</p>
    <h2>Choose the question</h2>
    <h2>Collect public-safe evidence</h2>
    <h2>Attach limitations</h2>
    <h2>Name next owners</h2>
    <h2>Run claim checklist</h2>
    <h2>Stop conditions</h2>
    <h2>Handoff notes</h2>
    <section data-boundary-region>
      <p>missing proof path private-only support raw artifact leakage unknown or reduced coverage without label unsupported runtime, release, or safety wording no next owner no validation evidence</p>
    </section>
    ${reviewPacketAssemblyRequiredLinks.map((route) => `<a href="${route}">${route}</a>`).join("\n")}
    <link rel="canonical" href="https://tracemap.tools/packets/assembly/">
    <meta name="description" content="Concept checklist">
    <meta property="og:type" content="article">
    <meta property="og:title" content="Review Packet Assembly">
    <meta property="og:description" content="Concept checklist">
    <meta property="og:url" content="https://tracemap.tools/packets/assembly/">
    <title>Review Packet Assembly | TraceMap</title>
    <p>${filler}</p>
    ${extra}
  `);
}
