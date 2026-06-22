import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { tmpdir } from "node:os";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  ownerFollowupMapRequiredLinks,
  ownerFollowupMapRoute,
  validateOwnerFollowupMapDist
} from "./owner-followup-map.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const siteRoot = resolve(scriptDir, "..");
const sourcePagePath = resolve(siteRoot, "src", "owners", "follow-up", "index.html");

test("validateOwnerFollowupMapDist accepts the owner follow-up route", async (t) => {
  const root = await createManagedOwnerFollowupDistFixture(t);
  const errors = [];

  await validateOwnerFollowupMapDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateOwnerFollowupMapDist reports missing required row and field", async (t) => {
  const pageHtml = await ownerFollowupPage();
  const withoutRow = pageHtml.replace(/<article data-owner-row="runtime behavior question">[\s\S]*?<\/article>\n\n          /, "");
  const withoutField = withoutRow.replace(/<dd data-owner-field="stop condition">Stop if proof path, rule ID\/rule family[\s\S]*?<\/dd>/, "");
  const root = await createManagedOwnerFollowupDistFixture(t, { ownerHtml: withoutField });
  const errors = [];

  await validateOwnerFollowupMapDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required row: runtime behavior question/);
  assert.match(errors.join("\n"), /evidence gap question is missing required field: stop condition/);
});

test("validateOwnerFollowupMapDist reports route metadata regressions", async (t) => {
  const root = await createManagedOwnerFollowupDistFixture(t);
  await rewriteOwnerRouteEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/",
    nonClaims: ["No runtime behavior."]
  });
  const errors = [];

  await validateOwnerFollowupMapDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
  assert.match(errors.join("\n"), /nonClaims are missing required term: real org ownership/);
});

test("validateOwnerFollowupMapDist reports missing sitemap route", async (t) => {
  const root = await createManagedOwnerFollowupDistFixture(t, {
    sitemapRoutes: ownerFollowupMapRequiredLinks
  });
  const errors = [];

  await validateOwnerFollowupMapDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
});

test("validateOwnerFollowupMapDist reports missing and unresolved required links", async (t) => {
  const root = await createManagedOwnerFollowupDistFixture(t, {
    ownerHtml: (await ownerFollowupPage()).replaceAll('href="/manager-packet/"', 'href="/missing-manager-packet/"')
  });
  const errors = [];

  await validateOwnerFollowupMapDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/manager-packet\//);
});

test("validateOwnerFollowupMapDist reports required links missing from discovery", async (t) => {
  const root = await createManagedOwnerFollowupDistFixture(t, {
    discoveryRoutes: [ownerFollowupMapRoute, ...ownerFollowupMapRequiredLinks.filter((link) => link !== "/validation/")]
  });
  const errors = [];

  await validateOwnerFollowupMapDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /required link is not present in discovery route index: \/validation\//);
});

test("validateOwnerFollowupMapDist rejects forbidden claims, private values, and blame language", async (t) => {
  const root = await createManagedOwnerFollowupDistFixture(t, {
    ownerHtml: (await ownerFollowupPage()).replace(
      "</main>",
      "<p>This page is AI-powered. Use file:///tmp/private.html. Find who broke it.</p></main>"
    )
  });
  const errors = [];

  await validateOwnerFollowupMapDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden claim wording/);
  assert.match(errors.join("\n"), /forbidden private or credential-like material/);
  assert.match(errors.join("\n"), /blame-oriented wording/);
});

test("validateOwnerFollowupMapDist checks forbidden claims after negated occurrences", async (t) => {
  const root = await createManagedOwnerFollowupDistFixture(t, {
    ownerHtml: (await ownerFollowupPage()).replace(
      "</main>",
      "<p>TraceMap cannot show TraceMap proves runtime behavior. The bounded example stays separated by neutral routing filler around public-safe review context and evidence category wording. Later TraceMap proves runtime behavior.</p></main>"
    )
  });
  const errors = [];

  await validateOwnerFollowupMapDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden claim wording: TraceMap proves runtime behavior/);
});

test("validateOwnerFollowupMapDist rejects unsubstituted placeholders", async (t) => {
  const root = await createManagedOwnerFollowupDistFixture(t, {
    ownerHtml: (await ownerFollowupPage()).replace("Stop if no proof path exists.", "Stop if [stop condition].")
  });
  const errors = [];

  await validateOwnerFollowupMapDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unsubstituted handoff placeholder token: \[stop condition\]/);
});

test("validateOwnerFollowupMapDist rejects unsupported next owner categories", async (t) => {
  const root = await createManagedOwnerFollowupDistFixture(t, {
    ownerHtml: (await ownerFollowupPage()).replace(
      '<dd data-owner-field="next owner">code owner or reviewer</dd>',
      '<dd data-owner-field="next owner">code owner or incident commander</dd>'
    )
  });
  const errors = [];

  await validateOwnerFollowupMapDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unsupported next owner category: incident commander/);
});

test("validateOwnerFollowupMapDist rejects word count drift", async (t) => {
  const root = await createManagedOwnerFollowupDistFixture(t, {
    ownerHtml: page("<main><p>Public claim level: concept. No public conclusion without evidence.</p></main>")
  });
  const errors = [];

  await validateOwnerFollowupMapDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /word count must be between 600 and 1700 words/);
});

async function createManagedOwnerFollowupDistFixture(t, options = {}) {
  const root = await createOwnerFollowupDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createOwnerFollowupDistFixture({
  discoveryRoutes = [ownerFollowupMapRoute, ...ownerFollowupMapRequiredLinks],
  sitemapRoutes = [ownerFollowupMapRoute, ...ownerFollowupMapRequiredLinks],
  ownerHtml = ownerFollowupPage()
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-owner-followup-test-"));
  const dist = join(root, "dist");
  const ownerPage = await ownerHtml;
  const routeSet = new Set([
    ownerFollowupMapRoute,
    ...ownerFollowupMapRequiredLinks,
    "/static-vs-runtime/",
    "/review-claim-checklist/"
  ]);

  for (const route of routeSet) {
    const path = join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(join(path, "index.html"), route === ownerFollowupMapRoute ? ownerPage : page(`<main><p>${route}</p></main>`), "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function ownerFollowupPage() {
  return readFile(sourcePagePath, "utf8");
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === ownerFollowupMapRoute ? "Owner Follow-Up Map" : `Route ${route}`,
    summary:
      route === ownerFollowupMapRoute
        ? "Concept-level map for routing static-evidence questions to human owner categories while preserving proof paths, limitations, handoff wording, and stop conditions."
        : "Fixture route for owner follow-up validation.",
    publicClaimLevel: route === ownerFollowupMapRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === ownerFollowupMapRoute ? "use-case" : "evidence",
    ...(route === ownerFollowupMapRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations:
      route === ownerFollowupMapRoute
        ? [
            "The map routes questions to owner categories, not real teams, people, approval chains, on-call rotations, service catalogs, database stewardship, or production ownership records.",
            "Every row must keep the static evidence trigger, what TraceMap can and cannot show, proof path, limitation, handoff wording, and stop condition attached."
          ]
        : ["Fixture limitations remain bounded."],
    nonClaims:
      route === ownerFollowupMapRoute
        ? [
            "No real org ownership claim, production ownership proof, runtime behavior, production traffic, endpoint performance, release approval, release safety, operational safety, complete coverage, or replacement of human judgment.",
            "No AI impact analysis, LLM analysis, embeddings, vector databases, prompt classification, automated ownership detection, automated release approval, raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values are public owner follow-up material."
          ]
        : ["No runtime behavior or production usage proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteOwnerRouteEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === ownerFollowupMapRoute
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
</urlset>
`;
}

function page(body) {
  return `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8">
    <meta name="description" content="Fixture page.">
    <title>Fixture</title>
    <link rel="canonical" href="https://tracemap.tools/owners/follow-up/">
    <meta property="og:type" content="article">
    <meta property="og:title" content="Fixture">
    <meta property="og:description" content="Fixture page.">
    <meta property="og:url" content="https://tracemap.tools/owners/follow-up/">
  </head>
  <body>${body}</body>
</html>`;
}
