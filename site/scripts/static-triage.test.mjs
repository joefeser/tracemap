import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import { staticTriageRequiredLinks, staticTriageRoute, validateStaticTriageDist } from "./static-triage.mjs";

test("validateStaticTriageDist accepts the static triage route", async (t) => {
  const root = await createManagedStaticTriageDistFixture(t);
  const errors = [];

  await validateStaticTriageDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateStaticTriageDist accepts href spacing around assignment", async (t) => {
  const root = await createManagedStaticTriageDistFixture(t, {
    staticTriageHtml: staticTriagePage("", { spacedHref: true })
  });
  const errors = [];

  await validateStaticTriageDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateStaticTriageDist reports missing required page text", async (t) => {
  const root = await createManagedStaticTriageDistFixture(t, {
    staticTriageHtml: page("<p>Static triage placeholder.</p>")
  });
  const errors = [];

  await validateStaticTriageDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: concept/);
});

test("validateStaticTriageDist reports missing route metadata", async (t) => {
  const root = await createManagedStaticTriageDistFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateStaticTriageDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/static-triage\//);
});

test("validateStaticTriageDist reports route metadata regressions", async (t) => {
  const root = await createManagedStaticTriageDistFixture(t);
  await rewriteStaticTriageRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateStaticTriageDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
});

test("validateStaticTriageDist reports word count outside bounds", async (t) => {
  const root = await createManagedStaticTriageDistFixture(t, {
    staticTriageHtml: staticTriagePage("", { fillerWordCount: 20 })
  });
  const errors = [];

  await validateStaticTriageDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /word count must be between 400 and 1500 words/);
});

test("validateStaticTriageDist rejects forbidden positioning", async (t) => {
  const root = await createManagedStaticTriageDistFixture(t, {
    staticTriageHtml: staticTriagePage("<p>AI-powered static triage.</p>")
  });
  const errors = [];

  await validateStaticTriageDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden AI\/LLM positioning/);
});

test("validateStaticTriageDist rejects encoded private text", async (t) => {
  const root = await createManagedStaticTriageDistFixture(t, {
    staticTriageHtml: staticTriagePage("<p>file&#58;//private/report</p>")
  });
  const errors = [];

  await validateStaticTriageDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /contains forbidden public text: file:\/\//);
});

test("validateStaticTriageDist rejects case variants of forbidden public text", async (t) => {
  const root = await createManagedStaticTriageDistFixture(t, {
    staticTriageHtml: staticTriagePage("<p>Connection String details stay private.</p>")
  });
  const errors = [];

  await validateStaticTriageDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /contains forbidden public text: connection string/);
});

async function createManagedStaticTriageDistFixture(t, options = {}) {
  const root = await createStaticTriageDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createStaticTriageDistFixture({
  discoveryRoutes = [staticTriageRoute],
  sitemapRoutes = [staticTriageRoute],
  staticTriageHtml = staticTriagePage()
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-static-triage-test-"));
  const dist = join(root, "dist");
  const routes = new Set([staticTriageRoute, ...staticTriageRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(join(path, "index.html"), route === staticTriageRoute ? staticTriageHtml : page(route), "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: `Route ${route}`,
    summary: "Fixture route for static triage validation.",
    publicClaimLevel: route === staticTriageRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === staticTriageRoute ? "use-case" : "evidence",
    ...(route === staticTriageRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: ["Fixture limitations remain bounded."],
    nonClaims: ["No runtime behavior, production usage, deployment state, endpoint performance, or release approval proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteStaticTriageRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === staticTriageRoute
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

function staticTriagePage(extra = "", { fillerWordCount = 420, spacedHref = false } = {}) {
  const href = (route) => (spacedHref ? `<a href = "${route}">${route}</a>` : `<a href="${route}">${route}</a>`);
  const filler = Array.from({ length: fillerWordCount }, (_, index) => `evidence-check-${index}`).join(" ");

  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>static evidence checklist and evidence tier</p>
    <p>handoff questions</p>
    <p>Partial static evidence is useful when labeled as partial</p>
    <p>Static triage is the engineer checklist and handoff page, distinct from the incident-call orientation page.</p>
    <p>The checklist is not telemetry, diagnosis, or approval.</p>
    ${staticTriageRequiredLinks.map((route) => href(route)).join("\n")}
    <p>${filler}</p>
    ${extra}
  `);
}
