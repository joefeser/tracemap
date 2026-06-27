import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  propertyFlowSchemaGapRequiredLinks,
  propertyFlowSchemaGapRoute,
  validatePropertyFlowSchemaGapDist
} from "./property-flow-schema-gap.mjs";

test("validatePropertyFlowSchemaGapDist accepts the property-flow schema gap route", async (t) => {
  const root = await createManagedFixture(t);
  const errors = [];

  await validatePropertyFlowSchemaGapDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validatePropertyFlowSchemaGapDist reports missing unsupported schema wording", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: propertyFlowSchemaPage().replaceAll("UnsupportedRouteFlowSchema", "RouteFlowSchemaGap")
  });
  const errors = [];

  await validatePropertyFlowSchemaGapDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: UnsupportedRouteFlowSchema/);
});

test("validatePropertyFlowSchemaGapDist reports route metadata regressions", async (t) => {
  const root = await createManagedFixture(t);
  const path = join(root, "dist", "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === propertyFlowSchemaGapRoute
      ? { ...entry, publicClaimLevel: "demo", preferredProofPath: "/validation/", nonClaims: ["No runtime behavior."] }
      : entry
  );
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
  const errors = [];

  await validatePropertyFlowSchemaGapDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\//);
  assert.match(errors.join("\n"), /nonClaims are missing required term: production traffic/);
});

test("validatePropertyFlowSchemaGapDist reports missing inbound link", async (t) => {
  const root = await createManagedFixture(t, { includeInboundLinks: false });
  const errors = [];

  await validatePropertyFlowSchemaGapDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing inbound links from live adjacent routes: \/proof-paths\//);
});

test("validatePropertyFlowSchemaGapDist rejects forbidden runtime proof claim", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: propertyFlowSchemaPage("<p>TraceMap proves runtime behavior for this property.</p>")
  });
  const errors = [];

  await validatePropertyFlowSchemaGapDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validatePropertyFlowSchemaGapDist rejects hard private material", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: propertyFlowSchemaPage("<p>Review /Users/example/private-output.</p>")
  });
  const errors = [];

  await validatePropertyFlowSchemaGapDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /hard private material/);
});

async function createManagedFixture(t, options = {}) {
  const root = await createFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createFixture({
  includeInboundLinks = true,
  pageHtml = propertyFlowSchemaPage(),
  sitemapRoutes = [propertyFlowSchemaGapRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-property-flow-schema-gap-test-"));
  const dist = join(root, "dist");
  const routes = new Set([propertyFlowSchemaGapRoute, ...propertyFlowSchemaGapRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    const body =
      route === propertyFlowSchemaGapRoute
        ? pageHtml
        : page(includeInboundLinks && route === "/proof-paths/" ? `<a href="${propertyFlowSchemaGapRoute}">property schema</a>` : route);
    await writeFile(join(path, "index.html"), body, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeFile(join(dist, "routes-index.json"), JSON.stringify({ entries: [routeEntry(), ...propertyFlowSchemaGapRequiredLinks.map(genericRouteEntry)] }, null, 2), "utf8");

  return root;
}

function propertyFlowSchemaPage(extra = "") {
  const links = propertyFlowSchemaGapRequiredLinks.map((link) => `<a href="${link}">${link}</a>`).join("\n");
  return page(`
    <title>Property-Flow Schema Gap | TraceMap</title>
    <meta name="description" content="Property-flow schema fixture.">
    <link rel="canonical" href="https://tracemap.tools/proof-paths/property-flow-schema/">
    <meta property="og:type" content="article">
    <meta property="og:title" content="Property-Flow Schema Gap">
    <meta property="og:description" content="Fixture">
    <meta property="og:url" content="https://tracemap.tools/proof-paths/property-flow-schema/">
    <section id="property-flow-schema-purpose"><p>Public claim level: concept. No public conclusion without evidence. UnsupportedRouteFlowSchema does not prove route-flow evidence is absent.</p></section>
    <section id="property-flow-schema-statuses"><article data-property-schema-status="unavailable">RouteFlowUnavailable</article><article data-property-schema-status="empty">empty</article><article data-property-schema-status="unsupported">unsupported</article><article data-property-schema-status="available">available</article></section>
    <section id="property-flow-schema-gap-fields"><p>property-flow.schema.v1 Tier4Unknown UnknownAnalysisGap supporting IDs commit evidence observed schema context extractor versions owner follow-up. existing combined path evidence may still be shown.</p></section>
    <section id="property-flow-schema-review-language"><p>route-flow-specific endpoint context was not promoted.</p></section>
    <section id="property-flow-schema-boundaries" data-tm-boundary="property-flow-schema-boundaries"><p>It does not prove runtime behavior, UI behavior, impact proof, release approval, complete coverage, AI impact analysis, LLM analysis, or replacement.</p></section>
    <section id="property-flow-schema-source-evidence"><p>PropertyFlowReport.cs PropertyFlowTests.cs rule catalog acceptance notes.</p></section>
    <section id="property-flow-schema-next">${links}</section>
    ${extra}
  `);
}

function page(body) {
  return `<!doctype html><html><head></head><body><main>${body}</main></body></html>`;
}

function routeEntry() {
  return {
    path: propertyFlowSchemaGapRoute,
    title: "Property-Flow Schema Gap",
    summary: "Concept-level proof-path page.",
    publicClaimLevel: "concept",
    sourceType: "site-page",
    hintCategory: "evidence",
    preferredProofPath: "/proof-paths/",
    limitations: ["Unsupported route-flow schema is a schema compatibility gap."],
    nonClaims: [
      "No runtime behavior, production traffic, endpoint performance, impact proof, UI behavior proof, release approval, complete coverage, AI impact analysis, LLM analysis, or replacement proof."
    ]
  };
}

function genericRouteEntry(path) {
  return {
    path,
    title: path,
    summary: path,
    publicClaimLevel: "concept",
    sourceType: "site-page",
    hintCategory: "evidence",
    preferredProofPath: "/proof-paths/",
    limitations: ["fixture"],
    nonClaims: ["fixture"]
  };
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${routes.map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`).join("\n")}
</urlset>`;
}
