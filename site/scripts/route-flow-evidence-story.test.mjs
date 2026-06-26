import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  routeFlowEvidenceStoryRequiredLinks,
  routeFlowEvidenceStoryRoute,
  validateRouteFlowEvidenceStoryDist
} from "./route-flow-evidence-story.mjs";

test("validateRouteFlowEvidenceStoryDist accepts the route-flow evidence story route", async (t) => {
  const root = await createManagedRouteFlowFixture(t);
  const errors = [];

  await validateRouteFlowEvidenceStoryDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateRouteFlowEvidenceStoryDist reports missing concept marker", async (t) => {
  const root = await createManagedRouteFlowFixture(t, {
    pageHtml: routeFlowPage().replace("Public claim level: concept", "Public claim level: demo")
  });
  const errors = [];

  await validateRouteFlowEvidenceStoryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: concept/);
});

test("validateRouteFlowEvidenceStoryDist reports missing proof-field marker", async (t) => {
  const root = await createManagedRouteFlowFixture(t, {
    pageHtml: routeFlowPage().replaceAll('data-route-flow-field="coverage label"', 'data-route-flow-field="coverage state"')
  });
  const errors = [];

  await validateRouteFlowEvidenceStoryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing proof-field marker: coverage label/);
});

test("validateRouteFlowEvidenceStoryDist reports route metadata regressions", async (t) => {
  const root = await createManagedRouteFlowFixture(t);
  await rewriteRouteEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "demo",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/",
    nonClaims: ["No runtime behavior proof."]
  });
  const errors = [];

  await validateRouteFlowEvidenceStoryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory evidence, got demo/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
  assert.match(errors.join("\n"), /nonClaims are missing required term: production traffic/);
});

test("validateRouteFlowEvidenceStoryDist reports missing adjacent link", async (t) => {
  const root = await createManagedRouteFlowFixture(t, {
    pageHtml: routeFlowPage().replaceAll('href="/static-vs-runtime/"', 'href="/missing-static-vs-runtime/"')
  });
  const errors = [];

  await validateRouteFlowEvidenceStoryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/static-vs-runtime\//);
});

test("validateRouteFlowEvidenceStoryDist rejects positive runtime proof claims", async (t) => {
  const root = await createManagedRouteFlowFixture(t, {
    pageHtml: routeFlowPage("<p>Route-flow proves runtime behavior for this endpoint.</p>")
  });
  const errors = [];

  await validateRouteFlowEvidenceStoryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateRouteFlowEvidenceStoryDist permits rejected runtime proof wording inside boundary", async (t) => {
  const root = await createManagedRouteFlowFixture(t, {
    pageHtml: routeFlowPage(`
      <section id="route-flow-story-rejected-patterns" data-tm-boundary="route-flow-rejected-patterns">
        <p>Rejected pattern: route-flow proves runtime behavior.</p>
      </section>
    `)
  });
  const errors = [];

  await validateRouteFlowEvidenceStoryDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateRouteFlowEvidenceStoryDist rejects raw material outside boundary", async (t) => {
  const root = await createManagedRouteFlowFixture(t, {
    pageHtml: routeFlowPage("<p>Publish facts.ndjson with raw SQL.</p>")
  });
  const errors = [];

  await validateRouteFlowEvidenceStoryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden raw\/private material/);
});

test("validateRouteFlowEvidenceStoryDist rejects unsupported boundary sections", async (t) => {
  const root = await createManagedRouteFlowFixture(t, {
    pageHtml: routeFlowPage(`
      <section id="extra-boundary" data-tm-boundary="route-flow-static-boundary">
        <p>Route-flow proves runtime behavior.</p>
      </section>
    `)
  });
  const errors = [];

  await validateRouteFlowEvidenceStoryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unsupported boundary section/);
});

test("validateRouteFlowEvidenceStoryDist reports missing illustrative framing", async (t) => {
  const root = await createManagedRouteFlowFixture(t, {
    pageHtml: routeFlowPage().replace("not a real TraceMap finding", "a TraceMap finding")
  });
  const errors = [];

  await validateRouteFlowEvidenceStoryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /must say it is not a real TraceMap finding/);
});

test("validateRouteFlowEvidenceStoryDist reports missing inbound link from proof paths", async (t) => {
  const root = await createManagedRouteFlowFixture(t, {
    includeInboundLinks: false
  });
  const errors = [];

  await validateRouteFlowEvidenceStoryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing inbound links from live adjacent routes: \/proof-paths\//);
});

test("validateRouteFlowEvidenceStoryDist reports missing implementation state when root is provided", async (t) => {
  const root = await createManagedRouteFlowFixture(t);
  const errors = [];

  await validateRouteFlowEvidenceStoryDist({ dist: join(root, "dist"), errors, root: join(root, "site") });

  assert.match(errors.join("\n"), /implementation-state file is missing/);
});

async function createManagedRouteFlowFixture(t, options = {}) {
  const root = await createRouteFlowFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createRouteFlowFixture({
  discoveryRoutes = [routeFlowRouteEntry(), ...routeFlowEvidenceStoryRequiredLinks.map((path) => routeEntry(path))],
  includeInboundLinks = true,
  pageHtml = routeFlowPage(),
  sitemapRoutes = [routeFlowEvidenceStoryRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-route-flow-story-test-"));
  const dist = join(root, "dist");
  const routes = new Set([routeFlowEvidenceStoryRoute, ...routeFlowEvidenceStoryRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    const body =
      route === routeFlowEvidenceStoryRoute
        ? pageHtml
        : page(includeInboundLinks && route === "/proof-paths/" ? `<a href="${routeFlowEvidenceStoryRoute}">route flow</a>` : route);
    await writeFile(join(path, "index.html"), body, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, entries) {
  const outputs = await createDiscoveryOutputs(entries, {
    dist,
    resolveInternalPaths: false
  });

  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
}

async function rewriteRouteEntry(dist, patch) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === routeFlowEvidenceStoryRoute
      ? {
          ...entry,
          ...patch
        }
      : entry
  );
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

function routeFlowPage(extra = "", { fillerWords = 260 } = {}) {
  const proofFields = [
    "static question",
    "evidence path",
    "rule ID or rule family",
    "evidence tier",
    "coverage label",
    "supporting IDs",
    "source context",
    "limitation",
    "next owner"
  ]
    .map((field) => `<article data-route-flow-field="${field}"><h3>${field}</h3><p>${field} check and stop condition.</p></article>`)
    .join("\n");
  const filler = Array.from({ length: fillerWords }, (_, index) => `route flow static evidence ${index}`).join(" ");
  const links = routeFlowEvidenceStoryRequiredLinks.map((link) => `<a href="${link}">${link}</a>`).join("\n");

  return page(`
    <title>Route-Flow Evidence Story | TraceMap</title>
    <meta name="description" content="Route-flow evidence story fixture.">
    <link rel="canonical" href="https://tracemap.tools/proof-paths/route-flow/">
    <meta property="og:type" content="article">
    <meta property="og:title" content="Route-Flow Evidence Story">
    <meta property="og:description" content="Fixture">
    <meta property="og:url" content="https://tracemap.tools/proof-paths/route-flow/">
    <section id="route-flow-story-positioning"><p>Public claim level: concept. No public conclusion without evidence. This concept page explains how a reader can inspect route-centered static evidence. This is not a public demo result.</p></section>
    <section id="route-flow-story-anatomy"><h2>Every public row keeps the evidence fields attached.</h2>${proofFields}</section>
    <section id="route-flow-story-current-evidence"><p>Checked-in route-flow evidence supports vocabulary, not broad public completion.</p></section>
    <section id="route-flow-story-rows"><p>Selected context must join through the proof path. selector endpoint/root route/root evidence bridge state static flow row context group service/helper repository/data query or SQL shape dependency surface value origin implementation candidate gap limitation owner follow-up.</p></section>
    <section id="route-flow-story-static-boundary" data-tm-boundary="route-flow-static-boundary"><p>StrongStaticRouteFlow ProbableStaticRouteFlow NeedsReviewStaticRouteFlow NoRouteFlowEvidence UnknownAnalysisGap. It does not prove runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, release approval, complete coverage, business impact, AI impact analysis, LLM analysis, autonomous approval, or replacement.</p><p>Tier1Semantic Tier2Structural Tier3SyntaxOrTextual Tier4Unknown full partial reduced unknown unavailable future-only gap-labeled.</p></section>
    <section id="route-flow-story-review-language"><p>Safe verbs: inspect follow compare record label downgrade hold hand off escalate. Outcomes: show as static evidence, show as context, label the gap, downgrade, keep internal, owner follow-up, do not repeat.</p><p>Illustrative safe pattern: This route-flow row shows static evidence under Rule combined.route-flow.* with Tier2Structural, partial coverage, supporting IDs, and limitations. Illustrative only; not a real TraceMap finding.</p><p>Illustrative gap pattern: adjacent context is not joined. Illustrative only.</p></section>
    <section id="route-flow-story-rejected-patterns" data-tm-boundary="route-flow-rejected-patterns"><p>Rejected pattern: route-flow proves runtime behavior. Rejected pattern: safe to release. Rejected pattern: AI impact analysis and LLM impact analysis.</p></section>
    <section id="route-flow-story-stop-conditions" data-tm-boundary="route-flow-stop-conditions"><p>missing proof path, missing rule ID or rule family, missing evidence tier, missing coverage label, missing limitation, missing supporting public-safe source context, private-only evidence, hidden detail, unjoined adjacent context, ambiguous endpoint/root, runtime-only binding, reduced coverage, schema/extractor gap, unsupported demo claim, forbidden runtime wording.</p></section>
    <section id="route-flow-story-continue">${links}</section>
    <p>${filler}</p>
    ${extra}
  `);
}

function page(body) {
  return `<!doctype html><html><head></head><body><main>${body}</main></body></html>`;
}

function routeFlowRouteEntry() {
  return {
    path: routeFlowEvidenceStoryRoute,
    title: "Route-Flow Evidence Story",
    summary: "Concept-level route-flow proof-path explanation for reading selected static route evidence, context rows, gaps, limitations, and owner handoffs.",
    publicClaimLevel: "concept",
    sourceType: "site-page",
    hintCategory: "evidence",
    preferredProofPath: "/proof-paths/",
    limitations: [
      "The route is a concept-level reading model over checked-in route-flow vocabulary, rule families, classifications, and tests; it is not a public demo result or new evidence source."
    ],
    nonClaims: [
      "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, release approval, complete coverage, business impact, AI impact analysis, LLM analysis, autonomous approval, or replacement."
    ]
  };
}

function routeEntry(path) {
  return {
    path,
    title: path,
    summary: "Fixture route.",
    publicClaimLevel: "concept",
    sourceType: "site-page",
    hintCategory: "evidence",
    preferredProofPath: "/proof-paths/",
    limitations: ["Fixture route."],
    nonClaims: ["No runtime behavior proof."]
  };
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${routes
    .map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`)
    .join("\n")}\n</urlset>\n`;
}
