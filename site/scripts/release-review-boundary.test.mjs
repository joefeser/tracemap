import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { tmpdir } from "node:os";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  releaseReviewBoundaryInboundRoutes,
  releaseReviewBoundaryRequiredLinks,
  releaseReviewBoundaryRoute,
  validateReleaseReviewBoundaryDist
} from "./release-review-boundary.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const siteRoot = resolve(scriptDir, "..");
const sourcePagePath = resolve(siteRoot, "src", "release-review-boundary", "index.html");
const proofPathsRoute = "/proof-paths/";

test("validateReleaseReviewBoundaryDist accepts the release boundary route", async (t) => {
  const root = await createManagedReleaseBoundaryFixture(t);
  const errors = [];

  await validateReleaseReviewBoundaryDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateReleaseReviewBoundaryDist reports missing required row and field", async (t) => {
  const pageHtml = await releaseBoundaryPage();
  const withoutRow = replaceRequiredFixtureFragment(
    pageHtml,
    /<tr\b[^>]*\bdata-release-boundary-row="runtime telemetry need"[^>]*>[\s\S]*?<\/tr>/,
    "runtime telemetry need row"
  );
  const withoutField = replaceRequiredFixtureFragment(
    withoutRow,
    /(<tr\b[^>]*\bdata-release-boundary-row="coverage gap"[^>]*>[\s\S]*?)<td\b[^>]*\bdata-field="stop condition"[^>]*>[\s\S]*?<\/td>/,
    "coverage gap stop condition field",
    "$1"
  );
  const root = await createManagedReleaseBoundaryFixture(t, { pageHtml: withoutField });
  const errors = [];

  await validateReleaseReviewBoundaryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required row: runtime telemetry need/);
  assert.match(errors.join("\n"), /coverage gap is missing required field: stop condition/);
});

test("validateReleaseReviewBoundaryDist accepts spaced row attributes", async (t) => {
  const pageHtml = (await releaseBoundaryPage())
    .replaceAll("data-release-boundary-row=", "data-release-boundary-row = ")
    .replaceAll("data-field=", "data-field = ");
  const root = await createManagedReleaseBoundaryFixture(t, { pageHtml });
  const errors = [];

  await validateReleaseReviewBoundaryDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateReleaseReviewBoundaryDist reports route metadata regressions", async (t) => {
  const root = await createManagedReleaseBoundaryFixture(t);
  await rewriteReleaseBoundaryRouteEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/",
    nonClaims: ["No release approval."]
  });
  const errors = [];

  await validateReleaseReviewBoundaryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
  assert.match(errors.join("\n"), /nonClaims are missing required term: runtime behavior proof/);
});

test("validateReleaseReviewBoundaryDist reports missing sitemap and unresolved supporting route", async (t) => {
  const root = await createManagedReleaseBoundaryFixture(t, {
    discoveryRoutes: [releaseReviewBoundaryRoute, ...releaseReviewBoundaryRequiredLinks.filter((link) => link !== "/deploy-audit/")],
    sitemapRoutes: releaseReviewBoundaryRequiredLinks
  });
  const errors = [];

  await validateReleaseReviewBoundaryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
  assert.match(errors.join("\n"), /required link is not present in discovery route index: \/deploy-audit\//);
});

test("validateReleaseReviewBoundaryDist rejects forbidden positive claims and private material", async (t) => {
  const root = await createManagedReleaseBoundaryFixture(t, {
    pageHtml: (await releaseBoundaryPage()).replace(
      "</main>",
      "<p>TraceMap approves releases. file&#58;//private/report.html. Who broke it?</p></main>"
    )
  });
  const errors = [];

  await validateReleaseReviewBoundaryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden positive claim wording/);
  assert.match(errors.join("\n"), /forbidden private or raw material/);
  assert.match(errors.join("\n"), /blame-oriented wording/);
});

test("validateReleaseReviewBoundaryDist rejects metadata content regardless of attribute order", async (t) => {
  const pageHtml = (await releaseBoundaryPage()).replace(
    '<meta property="og:title" content="TraceMap Release Review Boundary">',
    '<meta property="og:title" content="TraceMap approves releases">'
  );
  const root = await createManagedReleaseBoundaryFixture(t, { pageHtml });
  const errors = [];

  await validateReleaseReviewBoundaryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden positive claim wording/);
});

test("validateReleaseReviewBoundaryDist rejects forbidden head title text", async (t) => {
  const pageHtml = (await releaseBoundaryPage()).replace(
    "<title>Release Review Boundary | TraceMap</title>",
    "<title>TraceMap approves releases</title>"
  );
  const root = await createManagedReleaseBoundaryFixture(t, { pageHtml });
  const errors = [];

  await validateReleaseReviewBoundaryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden positive claim wording/);
});

test("validateReleaseReviewBoundaryDist rejects hard private material inside boundary sections", async (t) => {
  const privatePath = ["/", "Users", "/private"].join("");
  const pageHtml = (await releaseBoundaryPage()).replace(
    "Stop when proof path, rule ID or rule family",
    `Stop when ${privatePath} appears near proof path, rule ID or rule family`
  );
  const root = await createManagedReleaseBoundaryFixture(t, { pageHtml });
  const errors = [];

  await validateReleaseReviewBoundaryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden private or raw material/);
});

test("validateReleaseReviewBoundaryDist rejects tag-split hard private material", async (t) => {
  const tokenPrefix = ["s", "k"].join("");
  const tokenBody = "abcdefghijklmn";
  const pageHtml = (await releaseBoundaryPage()).replace(
    "</main>",
    `<p>${tokenPrefix[0]}<span>${tokenPrefix[1]}-${tokenBody}</span></p></main>`
  );
  const root = await createManagedReleaseBoundaryFixture(t, { pageHtml });
  const errors = [];

  await validateReleaseReviewBoundaryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden private or raw material/);
});

test("validateReleaseReviewBoundaryDist keeps negation inside the current sentence", async (t) => {
  const pageHtml = (await releaseBoundaryPage()).replace(
    "</main>",
    "<p>This page is not release approval. TraceMap approves releases.</p></main>"
  );
  const root = await createManagedReleaseBoundaryFixture(t, { pageHtml });
  const errors = [];

  await validateReleaseReviewBoundaryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden positive claim wording/);
});

test("validateReleaseReviewBoundaryDist reports under word-count bound", async (t) => {
  const root = await createManagedReleaseBoundaryFixture(t, {
    pageHtml: releaseBoundaryFixturePage("<p>Public claim level: concept. No public conclusion without evidence.</p>")
  });
  const errors = [];

  await validateReleaseReviewBoundaryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /word count must be between 900 and 2400 words/);
});

test("validateReleaseReviewBoundaryDist reports missing inbound links", async (t) => {
  const root = await createManagedReleaseBoundaryFixture(t, { includeInboundLinks: false });
  const errors = [];

  await validateReleaseReviewBoundaryDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing inbound links from live adjacent routes/);
});

async function createManagedReleaseBoundaryFixture(t, options = {}) {
  const root = await createReleaseBoundaryFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createReleaseBoundaryFixture({
  discoveryRoutes = [releaseReviewBoundaryRoute, proofPathsRoute, ...releaseReviewBoundaryRequiredLinks],
  includeInboundLinks = true,
  pageHtml = null,
  sitemapRoutes = [releaseReviewBoundaryRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-release-boundary-test-"));
  const dist = join(root, "dist");
  const routes = new Set([
    releaseReviewBoundaryRoute,
    proofPathsRoute,
    ...releaseReviewBoundaryRequiredLinks,
    ...releaseReviewBoundaryInboundRoutes
  ]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    const html =
      route === releaseReviewBoundaryRoute
        ? pageHtml ?? (await releaseBoundaryPage())
        : includeInboundLinks && releaseReviewBoundaryInboundRoutes.includes(route)
          ? page(`<a href="${releaseReviewBoundaryRoute}">Release boundary</a>`)
          : page(`<p>${route}</p>`);
    await writeFile(join(path, "index.html"), html, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === releaseReviewBoundaryRoute ? "Release Review Boundary" : `Route ${route}`,
    summary:
      route === releaseReviewBoundaryRoute
        ? "Concept-level handoff for deterministic static evidence during release review while release-control decisions remain owner-owned."
        : "Fixture route for release boundary validation.",
    publicClaimLevel: route === releaseReviewBoundaryRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === releaseReviewBoundaryRoute ? "use-case" : "evidence",
    ...(route === releaseReviewBoundaryRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations:
      route === releaseReviewBoundaryRoute
        ? [
            "This is a static-evidence release-review handoff, not a release gate, approval system, runtime workflow, deploy audit, validation proof, manager packet, or objection guide.",
            "Static repository evidence can orient questions and gaps but cannot replace release owners, release controls, tests, source review, service-owner judgment, runtime observability, or human judgment."
          ]
        : ["Fixture limitations remain bounded."],
    nonClaims:
      route === releaseReviewBoundaryRoute
        ? [
            "No release approval, release safety, operational safety, production proof, runtime behavior proof, endpoint performance proof, deployment success proof, absence-of-impact proof, complete coverage, AI impact analysis, LLM analysis, embeddings, vector databases, prompt classification, or replacement of release controls.",
            "No raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values are public release-boundary material."
          ]
        : ["No runtime behavior, production traffic, endpoint performance, deployment state, or release approval proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteReleaseBoundaryRouteEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === releaseReviewBoundaryRoute
      ? {
          ...entry,
          ...fields
        }
      : entry
  );
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

async function releaseBoundaryPage() {
  return readFile(sourcePagePath, "utf8");
}

function releaseBoundaryFixturePage(body) {
  return `<!doctype html><html><head><meta property="og:type" content="article"></head><body><main>${body}</main></body></html>`;
}

function replaceRequiredFixtureFragment(value, pattern, label, replacement = "") {
  assert.match(value, pattern, `fixture must include ${label}`);
  return value.replace(pattern, replacement);
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
