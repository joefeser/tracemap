import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  staticVsRuntimeRequiredLinks,
  staticVsRuntimeRoute,
  validateStaticVsRuntimeDist
} from "./static-vs-runtime.mjs";

test("validateStaticVsRuntimeDist accepts the static vs runtime route", async (t) => {
  const root = await createManagedStaticVsRuntimeDistFixture(t);
  const errors = [];

  await validateStaticVsRuntimeDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateStaticVsRuntimeDist reports missing required page text", async (t) => {
  const root = await createManagedStaticVsRuntimeDistFixture(t, {
    pageHtml: page("<p>Static vs runtime placeholder.</p>")
  });
  const errors = [];

  await validateStaticVsRuntimeDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: concept/);
});

test("validateStaticVsRuntimeDist reports missing route metadata", async (t) => {
  const root = await createManagedStaticVsRuntimeDistFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateStaticVsRuntimeDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/static-vs-runtime\//);
});

test("validateStaticVsRuntimeDist reports route metadata regressions", async (t) => {
  const root = await createManagedStaticVsRuntimeDistFixture(t);
  await rewriteStaticVsRuntimeRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateStaticVsRuntimeDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
});

test("validateStaticVsRuntimeDist rejects missing non-claim parity", async (t) => {
  const root = await createManagedStaticVsRuntimeDistFixture(t);
  await rewriteStaticVsRuntimeRoutesIndexEntry(join(root, "dist"), {
    nonClaims: ["No runtime behavior proof."]
  });
  const errors = [];

  await validateStaticVsRuntimeDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /nonClaims are missing boundary phrase: production traffic/);
});

test("validateStaticVsRuntimeDist rejects forbidden positioning", async (t) => {
  const root = await createManagedStaticVsRuntimeDistFixture(t, {
    pageHtml: staticVsRuntimePage("<p>TraceMap ships a runtime agent.</p>")
  });
  const errors = [];

  await validateStaticVsRuntimeDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden runtime or AI\/LLM positioning/);
});

test("validateStaticVsRuntimeDist rejects unsupported impacted wording", async (t) => {
  const root = await createManagedStaticVsRuntimeDistFixture(t, {
    pageHtml: staticVsRuntimePage("<p>The endpoint is impacted.</p>")
  });
  const errors = [];

  await validateStaticVsRuntimeDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unsupported impacted wording/);
});

test("validateStaticVsRuntimeDist rejects encoded private text", async (t) => {
  const root = await createManagedStaticVsRuntimeDistFixture(t, {
    pageHtml: staticVsRuntimePage("<p>file&#58;//private/report</p>")
  });
  const errors = [];

  await validateStaticVsRuntimeDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /contains forbidden public text: file:\/\//);
});

async function createManagedStaticVsRuntimeDistFixture(t, options = {}) {
  const root = await createStaticVsRuntimeDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createStaticVsRuntimeDistFixture({
  discoveryRoutes = [staticVsRuntimeRoute],
  sitemapRoutes = [staticVsRuntimeRoute],
  pageHtml = staticVsRuntimePage()
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-static-vs-runtime-test-"));
  const dist = join(root, "dist");
  const routes = new Set([staticVsRuntimeRoute, ...staticVsRuntimeRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(join(path, "index.html"), route === staticVsRuntimeRoute ? pageHtml : page(route), "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: `Route ${route}`,
    summary: "Concept-level static evidence and runtime observability boundary.",
    publicClaimLevel: route === staticVsRuntimeRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === staticVsRuntimeRoute ? "use-case" : "evidence",
    ...(route === staticVsRuntimeRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: ["Concept route keeps static evidence separate from operational conclusions."],
    nonClaims:
      route === staticVsRuntimeRoute
        ? [
            "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, complete product coverage, incident root cause, service ownership, or test sufficiency proof.",
            "No AI impact analysis, LLM analysis, prompt-based classification, embedding search, or vector database analysis."
          ]
        : ["No runtime behavior proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteStaticVsRuntimeRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === staticVsRuntimeRoute
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
  return `<!doctype html><html><body><main>${body}</main></body></html>`;
}

function staticVsRuntimePage(extra = "", { fillerWordCount = 700 } = {}) {
  const filler = Array.from({ length: fillerWordCount }, (_, index) => `boundary-evidence-${index}`).join(" ");

  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>deterministic static repository evidence</p>
    <p>runtime observability remains the source</p>
    <table><thead><tr><th scope="col">Static evidence question</th><th scope="col">TraceMap evidence shape</th><th scope="col">Runtime question</th><th scope="col">Runtime system owner</th></tr></thead></table>
    <section id="static-questions"></section>
    <section id="runtime-questions"></section>
    <section id="handoff-workflow"></section>
    <section id="proof-paths"></section>
    <section id="limitations"></section>
    <section id="non-claims"></section>
    <p>Before runtime review</p>
    <p>During handoff</p>
    <p>After runtime review</p>
    <p>TraceMap does not prove runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, incident root cause, service ownership, production dependency understanding, test sufficiency, or complete product coverage.</p>
    <p>TraceMap does not replace logs, traces, APM, telemetry, incident dashboards, production metrics, tests, service-owner review, incident response, release approval, governance, or human judgment.</p>
    <p>TraceMap does not perform AI impact analysis, LLM analysis, prompt-based classification, embedding search, or vector database analysis.</p>
    <p>TraceMap should not say a surface is impacted unless reducer-backed public-safe evidence supports that wording.</p>
    ${staticVsRuntimeRequiredLinks.map((route) => `<a href="${route}">${route}</a>`).join("\n")}
    <p>${filler}</p>
    ${extra}
  `);
}
