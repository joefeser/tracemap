import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  adoptionPartialAnalysisSentence,
  adoptionPlaybookRequiredLinks,
  adoptionPlaybookRoute,
  validateAdoptionPlaybookDist
} from "./adoption-playbook.mjs";

test("validateAdoptionPlaybookDist accepts the adoption route", async (t) => {
  const root = await createManagedAdoptionDistFixture(t);
  const errors = [];

  await validateAdoptionPlaybookDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateAdoptionPlaybookDist accepts href spacing around assignment", async (t) => {
  const root = await createManagedAdoptionDistFixture(t, {
    adoptionHtml: adoptionPage("", { spacedHref: true })
  });
  const errors = [];

  await validateAdoptionPlaybookDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateAdoptionPlaybookDist reports missing required page text", async (t) => {
  const root = await createManagedAdoptionDistFixture(t, {
    adoptionHtml: page("<p>Adoption placeholder.</p>")
  });
  const errors = [];

  await validateAdoptionPlaybookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: concept/);
});

test("validateAdoptionPlaybookDist reports missing route metadata", async (t) => {
  const root = await createManagedAdoptionDistFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateAdoptionPlaybookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/adoption\//);
  assert.match(errors.join("\n"), /missing from the llms\.txt Limitations route section/);
});

test("validateAdoptionPlaybookDist reports route metadata regressions", async (t) => {
  const root = await createManagedAdoptionDistFixture(t);
  await rewriteAdoptionRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateAdoptionPlaybookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
});

test("validateAdoptionPlaybookDist checks the adoption llms claim line specifically", async (t) => {
  const root = await createManagedAdoptionDistFixture(t);
  await rewriteAdoptionLlmsLine(join(root, "dist"), (line) => line.replace("Public claim level: concept", "Public claim level: demo"));
  const errors = [];

  await validateAdoptionPlaybookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /llms\.txt route section must preserve concept claim level/);
});

test("validateAdoptionPlaybookDist reports word count outside bounds", async (t) => {
  const root = await createManagedAdoptionDistFixture(t, {
    adoptionHtml: adoptionPage("", { fillerWords: 0 })
  });
  const errors = [];

  await validateAdoptionPlaybookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /word count must be between 500 and 1500 words/);
});

test("validateAdoptionPlaybookDist rejects forbidden positioning", async (t) => {
  const root = await createManagedAdoptionDistFixture(t, {
    adoptionHtml: adoptionPage("<p>AI-powered adoption workflow.</p>")
  });
  const errors = [];

  await validateAdoptionPlaybookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden AI\/LLM positioning/);
});

test("validateAdoptionPlaybookDist rejects encoded private text", async (t) => {
  const root = await createManagedAdoptionDistFixture(t, {
    adoptionHtml: adoptionPage("<p>file&#58;//private/report</p>")
  });
  const errors = [];

  await validateAdoptionPlaybookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /contains forbidden public text: file:\/\//);
});

test("validateAdoptionPlaybookDist rejects raw artifact wording", async (t) => {
  const root = await createManagedAdoptionDistFixture(t, {
    adoptionHtml: adoptionPage("<p>facts.ndjson should not be published.</p>")
  });
  const errors = [];

  await validateAdoptionPlaybookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /contains forbidden public text: facts\.ndjson/);
});

async function createManagedAdoptionDistFixture(t, options = {}) {
  const root = await createAdoptionDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createAdoptionDistFixture({
  adoptionHtml = adoptionPage(),
  discoveryRoutes = [adoptionPlaybookRoute],
  sitemapRoutes = [adoptionPlaybookRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-adoption-test-"));
  const dist = join(root, "dist");
  const routes = new Set([adoptionPlaybookRoute, ...adoptionPlaybookRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(join(path, "index.html"), route === adoptionPlaybookRoute ? adoptionHtml : page(route), "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === adoptionPlaybookRoute ? "Adoption Playbook" : `Route ${route}`,
    summary: "Fixture route for adoption playbook validation.",
    publicClaimLevel: route === adoptionPlaybookRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === adoptionPlaybookRoute ? "use-case" : "evidence",
    ...(route === adoptionPlaybookRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: ["Fixture limitations remain bounded."],
    nonClaims: ["No runtime behavior, production usage, deployment state, endpoint performance, or release approval proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteAdoptionRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === adoptionPlaybookRoute
      ? {
          ...entry,
          ...fields
        }
      : entry
  );
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

async function rewriteAdoptionLlmsLine(dist, rewrite) {
  const path = join(dist, "llms.txt");
  const updated = (await readFile(path, "utf8"))
    .split(/\r?\n/)
    .map((line) => (line.includes("[Adoption Playbook](https://tracemap.tools/adoption/)") ? rewrite(line) : line))
    .join("\n");

  await writeFile(path, updated.endsWith("\n") ? updated : `${updated}\n`, "utf8");
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

function adoptionPage(extra = "", { fillerWords = 95, spacedHref = false } = {}) {
  const href = (route) => (spacedHref ? `<a href = "${route}">${route}</a>` : `<a href="${route}">${route}</a>`);
  const filler = Array.from({ length: fillerWords }, (_, index) => `adoption evidence workflow boundary ${index}`).join(" ");

  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>not a product promise or replacement for engineering judgment</p>
    <p>start with the public demo</p>
    <p>repository owners runtime owners test owners documentation owners future extractor work</p>
    <p>${adoptionPartialAnalysisSentence}</p>
    <p>The playbook is not runtime proof or release approval</p>
    ${adoptionPlaybookRequiredLinks.map((route) => href(route)).join("\n")}
    <p>${filler}</p>
    ${extra}
  `);
}
