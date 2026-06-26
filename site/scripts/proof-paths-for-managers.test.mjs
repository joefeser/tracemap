import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  proofPathsForManagersRequiredLinks,
  proofPathsForManagersRoute,
  validateProofPathsForManagersDist
} from "./proof-paths-for-managers.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePagePath = resolve(scriptDir, "..", "src", "proof-paths", "for-managers", "index.html");

test("validateProofPathsForManagersDist accepts the manager proof-path route", async (t) => {
  const root = await createManagedFixture(t);
  const errors = [];

  await validateProofPathsForManagersDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateProofPathsForManagersDist reports missing concept marker", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await page()).replace("Public claim level: concept", "Public claim level: demo")
  });
  const errors = [];

  await validateProofPathsForManagersDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /required text: Public claim level: concept/);
});

test("validateProofPathsForManagersDist reports missing matrix question", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await page()).replace('id="question-public-sharing"', 'id="question-sharing"')
  });
  const errors = [];

  await validateProofPathsForManagersDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required matrix anchor: #question-public-sharing/);
});

test("validateProofPathsForManagersDist reports missing anatomy field", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await page()).replace('data-proof-manager-anatomy="coverage label"', 'data-proof-manager-anatomy="coverage state"')
  });
  const errors = [];

  await validateProofPathsForManagersDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /anatomy is missing field marker: coverage label/);
});

test("validateProofPathsForManagersDist reports route metadata regressions", async (t) => {
  const root = await createManagedFixture(t);
  await rewriteRouteEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "use-case",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/",
    nonClaims: ["No runtime behavior proof."]
  });
  const errors = [];

  await validateProofPathsForManagersDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory evidence, got use-case/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
  assert.match(errors.join("\n"), /nonClaims do not include required term: production traffic/);
});

test("validateProofPathsForManagersDist rejects unsupported positive runtime claims", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await page()).replace("</main>", "<p>TraceMap proves runtime behavior for this endpoint.</p></main>")
  });
  const errors = [];

  await validateProofPathsForManagersDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateProofPathsForManagersDist rejects hard private material", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await page()).replace("</main>", '<img alt="file&#58;//private/report"></main>')
  });
  const errors = [];

  await validateProofPathsForManagersDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /hard private material/);
});

test("validateProofPathsForManagersDist reports missing inbound links", async (t) => {
  const root = await createManagedFixture(t, {
    includeInboundLinks: false
  });
  const errors = [];

  await validateProofPathsForManagersDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /does not include inbound links from live adjacent routes: \/proof-paths\//);
});

test("validateProofPathsForManagersDist reports unresolved adjacent link", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await page()).replaceAll('href="/static-vs-runtime/"', 'href="/missing-static-vs-runtime/"')
  });
  const errors = [];

  await validateProofPathsForManagersDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /does not include required adjacent link: \/static-vs-runtime\//);
  assert.match(errors.join("\n"), /links to unresolved internal route: \/missing-static-vs-runtime\//);
});

async function createManagedFixture(t, options = {}) {
  const root = await createFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createFixture({
  discoveryRoutes = [routeEntry(proofPathsForManagersRoute), ...proofPathsForManagersRequiredLinks.map(routeEntry)],
  includeInboundLinks = true,
  pageHtml,
  sitemapRoutes = [proofPathsForManagersRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-proof-paths-for-managers-test-"));
  const dist = join(root, "dist");
  const routes = new Set([proofPathsForManagersRoute, ...proofPathsForManagersRequiredLinks]);
  const source = pageHtml ?? (await page());

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    const body =
      route === proofPathsForManagersRoute
        ? source
        : fixturePage(includeInboundLinks && isInboundFixtureRoute(route) ? `<a href="${proofPathsForManagersRoute}">manager proof-path guide</a>` : route);
    await writeFile(join(path, "index.html"), body, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function page() {
  return readFile(sourcePagePath, "utf8");
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
    entry.path === proofPathsForManagersRoute
      ? {
          ...entry,
          ...patch
        }
      : entry
  );
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

function routeEntry(route) {
  if (route === proofPathsForManagersRoute) {
    return {
      path: proofPathsForManagersRoute,
      title: "Proof Paths for Managers",
      summary: "Concept-level manager and reviewer guide for deterministic static proof paths, evidence packets, coverage labels, limitations, stop conditions, and next-owner routing.",
      publicClaimLevel: "concept",
      sourceType: "site-page",
      hintCategory: "evidence",
      preferredProofPath: "/proof-paths/",
      limitations: [
        "The route translates proof paths into decision questions and owner routing; it is not a new scanner result, reducer result, proof source, validation result, packet generator, or approval workflow."
      ],
      nonClaims: [
        "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, release approval, complete coverage, autonomous approval, automated management decision, AI impact analysis, LLM analysis, embeddings, vector databases, prompt classification, or replacement for tests.",
        "No raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values."
      ]
    };
  }

  return {
    path: route,
    title: `Route ${route}`,
    summary: "Fixture route for internal link validation.",
    publicClaimLevel: "concept",
    sourceType: "site-page",
    hintCategory: "evidence",
    limitations: ["Fixture page only."],
    nonClaims: ["No runtime behavior proof."]
  };
}

function isInboundFixtureRoute(route) {
  return ["/proof-paths/", "/proof-paths/faq/", "/proof-paths/tour/", "/manager-packet/", "/manager-faq/"].includes(route);
}

function fixturePage(body) {
  return `<!doctype html><html><head></head><body><main>${body}</main></body></html>`;
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${routes.map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`).join("\n")}
</urlset>
`;
}
