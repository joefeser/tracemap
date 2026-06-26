import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  demoTroubleshootingAdjacentRoutes,
  demoTroubleshootingRoute,
  validateDemoTroubleshootingDist
} from "./demo-troubleshooting.mjs";

test("validateDemoTroubleshootingDist accepts the public demo troubleshooting route", async (t) => {
  const root = await createManagedDemoTroubleshootingDistFixture(t);
  const errors = [];

  await validateDemoTroubleshootingDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateDemoTroubleshootingDist reports missing concept label", async (t) => {
  const pageHtml = (await demoTroubleshootingPage()).replace("Public claim level: concept", "Public claim level: demo");
  const root = await createManagedDemoTroubleshootingDistFixture(t, { pageHtml });
  const errors = [];

  await validateDemoTroubleshootingDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: concept/);
});

test("validateDemoTroubleshootingDist reports missing required row field", async (t) => {
  const pageHtml = (await demoTroubleshootingPage()).replace('data-field="likely public-safe cause"', 'data-field="cause"');
  const root = await createManagedDemoTroubleshootingDistFixture(t, { pageHtml });
  const errors = [];

  await validateDemoTroubleshootingDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /row missing route is missing required field: likely public-safe cause/);
});

test("validateDemoTroubleshootingDist requires non-claim field markers", async (t) => {
  const pageHtml = (await demoTroubleshootingPage()).replace(' data-non-claim-region="matrix-not-conclude"', "");
  const root = await createManagedDemoTroubleshootingDistFixture(t, { pageHtml });
  const errors = [];

  await validateDemoTroubleshootingDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /field what not to conclude is missing data-non-claim-region/);
});

test("validateDemoTroubleshootingDist requires rejected examples to stay marked", async (t) => {
  const pageHtml = (await demoTroubleshootingPage()).replace(' data-rejected-pattern-region="demo-troubleshooting-rejected"', "");
  const root = await createManagedDemoTroubleshootingDistFixture(t, { pageHtml });
  const errors = [];

  await validateDemoTroubleshootingDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /Rejected wording must be inside a data-rejected-pattern-region region/);
});

test("validateDemoTroubleshootingDist rejects unsupported claims outside marked regions", async (t) => {
  const pageHtml = (await demoTroubleshootingPage()).replace(
    "</main>",
    "<p>The page diagnoses runtime behavior or endpoint performance.</p></main>"
  );
  const root = await createManagedDemoTroubleshootingDistFixture(t, { pageHtml });
  const errors = [];

  await validateDemoTroubleshootingDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unsupported affirmative claim outside marked regions/);
});

test("validateDemoTroubleshootingDist rejects tag-split unsupported claims", async (t) => {
  const pageHtml = (await demoTroubleshootingPage()).replace(
    "</main>",
    "<p>The release <span>is</span> approved.</p></main>"
  );
  const root = await createManagedDemoTroubleshootingDistFixture(t, { pageHtml });
  const errors = [];

  await validateDemoTroubleshootingDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unsupported affirmative claim outside marked regions/);
});

test("validateDemoTroubleshootingDist rejects explicit support service wording outside marked regions", async (t) => {
  const pageHtml = (await demoTroubleshootingPage()).replace(
    "</main>",
    "<p>This route provides a support SLA and ticketing channel.</p></main>"
  );
  const root = await createManagedDemoTroubleshootingDistFixture(t, { pageHtml });
  const errors = [];

  await validateDemoTroubleshootingDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unsupported affirmative claim outside marked regions/);
});

test("validateDemoTroubleshootingDist rejects internal spec artifact row links by path segment", async (t) => {
  const pageHtml = (await demoTroubleshootingPage()).replace(
    'Check the <a href="/demo/runbook/">demo runbook</a>',
    'Check the <a href="/public/specs/demo/">demo runbook</a>'
  );
  const root = await createManagedDemoTroubleshootingDistFixture(t, { pageHtml });
  const errors = [];

  await validateDemoTroubleshootingDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /row directs visitors to internal artifact: \/public\/specs\/demo\//);
});

test("validateDemoTroubleshootingDist reports route metadata regressions", async (t) => {
  const root = await createManagedDemoTroubleshootingDistFixture(t);
  await rewriteDemoTroubleshootingRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateDemoTroubleshootingDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory demo, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/demo\/runbook\/, got \/validation\//);
});

test("validateDemoTroubleshootingDist rejects hard private values inside marked regions", async (t) => {
  const hardLeak = ["/", "Users", "/private-demo"].join("");
  const pageHtml = (await demoTroubleshootingPage()).replace(
    "Private-only evidence is enough public proof.",
    `${hardLeak} is enough public proof.`
  );
  const root = await createManagedDemoTroubleshootingDistFixture(t, { pageHtml });
  const errors = [];

  await validateDemoTroubleshootingDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden private or credential-like text: home directory path/);
});

test("validateDemoTroubleshootingDist rejects tag-split hard private values", async (t) => {
  const pageHtml = (await demoTroubleshootingPage()).replace(
    "Private-only evidence is enough public proof.",
    ["/", "Us", "<span>ers</span>", "/private-demo is enough public proof."].join("")
  );
  const root = await createManagedDemoTroubleshootingDistFixture(t, { pageHtml });
  const errors = [];

  await validateDemoTroubleshootingDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden private or credential-like text: home directory path/);
});

test("validateDemoTroubleshootingDist reads metadata content regardless of attribute order", async (t) => {
  const pageHtml = (await demoTroubleshootingPage()).replace(
    /<meta\s+name="description"\s+content="[^"]+"\s+>/s,
    '<meta content="Concept-level TraceMap public demo troubleshooting guidance for route, summary, proof, coverage, evidence, wording, validation, and owner handoff questions." name="description">'
  );
  const root = await createManagedDemoTroubleshootingDistFixture(t, { pageHtml });
  const errors = [];

  await validateDemoTroubleshootingDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateDemoTroubleshootingDist scans route metadata for hard-private material", async (t) => {
  const root = await createManagedDemoTroubleshootingDistFixture(t);
  await rewriteDemoTroubleshootingRoutesIndexEntry(join(root, "dist"), {
    limitations: [["/", "home", "/private-demo"].join("")]
  });
  const errors = [];

  await validateDemoTroubleshootingDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden private or credential-like text: home directory path/);
  assert.match(errors.join("\n"), /routes-index\.json/);
});

async function createManagedDemoTroubleshootingDistFixture(t, options = {}) {
  const root = await createDemoTroubleshootingDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createDemoTroubleshootingDistFixture({
  discoveryRoutes = [demoTroubleshootingRoute],
  pageHtml,
  sitemapRoutes = [demoTroubleshootingRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-demo-troubleshooting-test-"));
  const dist = join(root, "dist");
  const routes = new Set([demoTroubleshootingRoute, ...demoTroubleshootingAdjacentRoutes]);

  for (const route of routes) {
    const path = join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(join(path, "index.html"), route === demoTroubleshootingRoute ? pageHtml ?? (await demoTroubleshootingPage()) : page(`<p>${route}</p>`), "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === demoTroubleshootingRoute ? "Public Demo Troubleshooting" : `Route ${route}`,
    summary:
      route === demoTroubleshootingRoute
        ? "Concept-level guidance for routing public demo confusion to visible checks, labels, owner roles, stop conditions, and non-claims."
        : "Fixture route for demo troubleshooting validation.",
    publicClaimLevel: route === demoTroubleshootingRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: "demo",
    ...(route === demoTroubleshootingRoute ? { preferredProofPath: "/demo/runbook/" } : {}),
    limitations: ["Fixture limitations remain bounded."],
    nonClaims: ["No runtime behavior, production usage, deployment state, endpoint performance, or release approval proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteDemoTroubleshootingRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === demoTroubleshootingRoute
      ? {
          ...entry,
          ...fields
        }
      : entry
  );
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

async function demoTroubleshootingPage() {
  return readFile(new URL("../src/demo/troubleshooting/index.html", import.meta.url), "utf8");
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
