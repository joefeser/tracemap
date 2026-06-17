import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import { managerBriefRequiredLinks, managerBriefRoute, validateManagerBriefDist } from "./manager-brief.mjs";

test("validateManagerBriefDist accepts the manager brief route", async (t) => {
  const root = await createManagedManagerBriefDistFixture(t);
  const errors = [];

  await validateManagerBriefDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateManagerBriefDist reports missing required page text", async (t) => {
  const root = await createManagedManagerBriefDistFixture(t, {
    managerBriefHtml: page("<p>Manager brief placeholder.</p>")
  });
  const errors = [];

  await validateManagerBriefDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: concept/);
});

test("validateManagerBriefDist reports missing route metadata", async (t) => {
  const root = await createManagedManagerBriefDistFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateManagerBriefDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/manager-brief\//);
});

test("validateManagerBriefDist reports route metadata regressions", async (t) => {
  const root = await createManagedManagerBriefDistFixture(t);
  await rewriteManagerBriefRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateManagerBriefDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
});

test("validateManagerBriefDist rejects forbidden AI positioning", async (t) => {
  const root = await createManagedManagerBriefDistFixture(t, {
    managerBriefHtml: managerBriefPage("<p>AI-powered impact analysis.</p>")
  });
  const errors = [];

  await validateManagerBriefDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden AI\/LLM positioning/);
});

test("validateManagerBriefDist rejects encoded private text", async (t) => {
  const root = await createManagedManagerBriefDistFixture(t, {
    managerBriefHtml: managerBriefPage("<p>file&#58;//private/report</p>")
  });
  const errors = [];

  await validateManagerBriefDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /contains forbidden public text: file:\/\//);
});

async function createManagedManagerBriefDistFixture(t, options = {}) {
  const root = await createManagerBriefDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createManagerBriefDistFixture({
  discoveryRoutes = [managerBriefRoute],
  managerBriefHtml = managerBriefPage(),
  sitemapRoutes = [managerBriefRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-manager-brief-test-"));
  const dist = join(root, "dist");
  const routes = new Set([managerBriefRoute, ...managerBriefRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(join(path, "index.html"), route === managerBriefRoute ? managerBriefHtml : page(route), "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: `Route ${route}`,
    summary: "Fixture route for manager brief validation.",
    publicClaimLevel: route === managerBriefRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === managerBriefRoute ? "use-case" : "evidence",
    ...(route === managerBriefRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: ["Fixture limitations remain bounded."],
    nonClaims: ["No runtime behavior, production usage, deployment state, endpoint performance, or release approval proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteManagerBriefRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === managerBriefRoute
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

function managerBriefPage(extra = "") {
  const filler = Array.from({ length: 90 }, (_, index) => `evidence packet review boundary ${index}`).join(" ");
  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>Manual dependency indexing is expensive</p>
    <p>deterministic artifacts</p>
    <p>Static evidence is useful because its limits stay visible</p>
    ${managerBriefRequiredLinks.map((route) => `<a href="${route}">${route}</a>`).join("\n")}
    <p>${filler}</p>
    ${extra}
  `);
}
