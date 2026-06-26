import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  legacyModernizationReviewHandoffRequiredLinks,
  legacyModernizationReviewHandoffRoute,
  validateLegacyModernizationReviewHandoffDist
} from "./legacy-modernization-review-handoff.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));

test("validateLegacyModernizationReviewHandoffDist accepts the route fixture", async (t) => {
  const root = await createManagedFixture(t);
  const errors = [];

  await validateLegacyModernizationReviewHandoffDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateLegacyModernizationReviewHandoffDist reports missing required copy", async (t) => {
  const root = await createManagedFixture(t, {
    handoffHtml: (await sourcePage()).replace("Public claim level: concept", "Public claim level omitted")
  });
  const errors = [];

  await validateLegacyModernizationReviewHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: concept/);
});

test("validateLegacyModernizationReviewHandoffDist reports metadata regressions", async (t) => {
  const root = await createManagedFixture(t, {
    routeEntryPatch: {
      publicClaimLevel: "demo",
      sourceType: "repo-doc",
      hintCategory: "evidence",
      preferredProofPath: "/validation/",
      nonClaims: ["No runtime behavior proof."]
    }
  });
  const errors = [];

  await validateLegacyModernizationReviewHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/legacy-modernization\/evidence-map\//);
  assert.match(errors.join("\n"), /nonClaims are missing required term: production traffic/);
});

test("validateLegacyModernizationReviewHandoffDist rejects forbidden modernization and runtime claims", async (t) => {
  const cases = [
    ["<p>TraceMap proves runtime behavior.</p>", /forbidden modernization\/runtime claim/],
    ["<p>Migration success is guaranteed.</p>", /forbidden modernization\/runtime claim/],
    ["<p>Schema compatibility is validated.</p>", /forbidden modernization\/runtime claim/],
    ["<p>This uses embeddings for review.</p>", /forbidden modernization\/runtime claim/]
  ];

  for (const [extra, expected] of cases) {
    const root = await createManagedFixture(t, {
      handoffHtml: `${await sourcePage()}${extra}`
    });
    const errors = [];

    await validateLegacyModernizationReviewHandoffDist({ dist: join(root, "dist"), errors });

    assert.match(errors.join("\n"), expected);
  }
});

test("validateLegacyModernizationReviewHandoffDist rejects private and raw material", async (t) => {
  const localPathLeak = `${String.fromCharCode(47)}Users/example/private`;
  const cases = [
    [`<p>${localPathLeak}</p>`, /local-absolute-path/],
    ["<p>file:///tmp/private.html</p>", /local-absolute-path|private-url/],
    ["<p>secret=value</p>", /credential-like-value/],
    ["<p>Server=db;Database=orders;User ID=sa;Password=pw;</p>", /connection-string/],
    ["<p>git@github.com:private/repo.git</p>", /raw-remote/]
  ];

  for (const [extra, expected] of cases) {
    const root = await createManagedFixture(t, {
      handoffHtml: `${await sourcePage()}${extra}`
    });
    const errors = [];

    await validateLegacyModernizationReviewHandoffDist({ dist: join(root, "dist"), errors });

    assert.match(errors.join("\n"), expected);
    assert.doesNotMatch(errors.join("\n"), /Password=pw/);
  }
});

test("validateLegacyModernizationReviewHandoffDist rejects missing matrix rows and fields", async (t) => {
  const html = (await sourcePage())
    .replace('data-handoff-row="data-surface"', 'data-handoff-row="data-surface-removed"')
    .replace("<th scope=\"col\">Stop condition</th>", "<th scope=\"col\">End</th>");
  const root = await createManagedFixture(t, { handoffHtml: html });
  const errors = [];

  await validateLegacyModernizationReviewHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required row: data-surface/);
  assert.match(errors.join("\n"), /missing required header: Stop condition/);
});

test("validateLegacyModernizationReviewHandoffDist reports missing adjacent links", async (t) => {
  const root = await createManagedFixture(t, {
    handoffHtml: (await sourcePage()).replaceAll('href="/docs/"', 'href="/docs-removed/"')
  });
  const errors = [];

  await validateLegacyModernizationReviewHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required adjacent link: \/docs\//);
});

async function createManagedFixture(t, options = {}) {
  const root = await createFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createFixture({ handoffHtml, routeEntryPatch = {} } = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-legacy-modernization-review-handoff-test-"));
  const dist = join(root, "dist");
  const routes = new Set([legacyModernizationReviewHandoffRoute, ...legacyModernizationReviewHandoffRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(
      join(path, "index.html"),
      route === legacyModernizationReviewHandoffRoute ? handoffHtml ?? (await sourcePage()) : page(`<main><p>${route}</p></main>`),
      "utf8"
    );
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap([...routes]), "utf8");
  await writeDiscoveryFiles(dist, [...routes], routeEntryPatch);

  return root;
}

async function writeDiscoveryFiles(dist, routes, routeEntryPatch) {
  const entries = routes.map((route) => ({
    path: route,
    title: `Route ${route}`,
    summary: "Fixture route for legacy modernization review handoff validation.",
    publicClaimLevel: route === legacyModernizationReviewHandoffRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === legacyModernizationReviewHandoffRoute ? "use-case" : "evidence",
    preferredProofPath:
      route === legacyModernizationReviewHandoffRoute ? "/legacy-modernization/evidence-map/" : "/proof-paths/",
    limitations:
      route === legacyModernizationReviewHandoffRoute
        ? [
            "The route is a handoff checklist, not a modernization decision or new proof source.",
            "Every row must keep evidence, proof fields, limitations, owners, and stop conditions attached."
          ]
        : ["Fixture limitation."],
    nonClaims:
      route === legacyModernizationReviewHandoffRoute
        ? [
            "No runtime behavior, production traffic, endpoint performance, release safety, operational safety, migration success, database execution, AI impact analysis, LLM analysis, or complete coverage proof."
          ]
        : ["No runtime behavior proof."],
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), patchRoutesIndex(outputs.routesIndexJson, routeEntryPatch), "utf8");
}

function patchRoutesIndex(routesIndexJson, routeEntryPatch) {
  if (Object.keys(routeEntryPatch).length === 0) {
    return routesIndexJson;
  }

  const parsed = JSON.parse(routesIndexJson);
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === legacyModernizationReviewHandoffRoute ? { ...entry, ...routeEntryPatch } : entry
  );
  return JSON.stringify(parsed, null, 2);
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${routes
    .map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`)
    .join("\n")}\n</urlset>\n`;
}

function page(body) {
  return `<!doctype html><html lang="en"><head><title>Fixture</title></head><body>${body}</body></html>`;
}

async function sourcePage() {
  return readFile(join(scriptDir, "..", "src", "legacy-modernization", "review-handoff", "index.html"), "utf8");
}
