import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import { reviewRoomRequiredLinks, reviewRoomRoute, validateReviewRoomDist } from "./review-room.mjs";

test("validateReviewRoomDist accepts the review room route", async (t) => {
  const root = await createManagedReviewRoomDistFixture(t);
  const errors = [];

  await validateReviewRoomDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateReviewRoomDist accepts href spacing around assignment", async (t) => {
  const root = await createManagedReviewRoomDistFixture(t, {
    reviewRoomHtml: reviewRoomPage("", { spacedHref: true })
  });
  const errors = [];

  await validateReviewRoomDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateReviewRoomDist reports missing required page text", async (t) => {
  const root = await createManagedReviewRoomDistFixture(t, {
    reviewRoomHtml: page("<p>Review room placeholder.</p>")
  });
  const errors = [];

  await validateReviewRoomDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: concept/);
});

test("validateReviewRoomDist reports missing route metadata", async (t) => {
  const root = await createManagedReviewRoomDistFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateReviewRoomDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/review-room\//);
});

test("validateReviewRoomDist reports route metadata regressions", async (t) => {
  const root = await createManagedReviewRoomDistFixture(t);
  await rewriteReviewRoomRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateReviewRoomDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
});

test("validateReviewRoomDist rejects forbidden AI positioning", async (t) => {
  const root = await createManagedReviewRoomDistFixture(t, {
    reviewRoomHtml: reviewRoomPage("<p>smart impact for review rooms.</p>")
  });
  const errors = [];

  await validateReviewRoomDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden AI\/LLM positioning/);
});

test("validateReviewRoomDist rejects forbidden AI positioning in attributes", async (t) => {
  const root = await createManagedReviewRoomDistFixture(t, {
    reviewRoomHtml: reviewRoomPage('<img alt="LLM-powered review room">')
  });
  const errors = [];

  await validateReviewRoomDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden AI\/LLM positioning/);
});

test("validateReviewRoomDist rejects encoded private text", async (t) => {
  const root = await createManagedReviewRoomDistFixture(t, {
    reviewRoomHtml: reviewRoomPage("<p>file&#58;//private/report</p>")
  });
  const errors = [];

  await validateReviewRoomDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /contains forbidden public text: file:\/\//);
});

test("validateReviewRoomDist rejects raw-remotes and secrets text", async (t) => {
  const root = await createManagedReviewRoomDistFixture(t, {
    reviewRoomHtml: reviewRoomPage("<p>Raw Remotes and Secrets stay private.</p>")
  });
  const errors = [];

  await validateReviewRoomDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /contains forbidden public text: raw remotes/);
  assert.match(errors.join("\n"), /contains forbidden public text: secrets/);
});

test("validateReviewRoomDist reports invalid base urls clearly", async (t) => {
  const root = await createManagedReviewRoomDistFixture(t);
  const errors = [];

  await validateReviewRoomDist({ baseUrl: "not a url", dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /Review room baseUrl must be a valid absolute URL: not a url/);
  assert.doesNotMatch(errors.join("\n"), /undefined\/review-room/);
});

test("validateReviewRoomDist rejects missing og type article", async (t) => {
  const root = await createManagedReviewRoomDistFixture(t, {
    reviewRoomHtml: reviewRoomPage().replace('<meta property="og:type" content="article">', "")
  });
  const errors = [];

  await validateReviewRoomDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /must include <meta property="og:type" content="article">/);
});

test("validateReviewRoomDist reports under word-count bound", async (t) => {
  const root = await createManagedReviewRoomDistFixture(t, {
    reviewRoomHtml: reviewRoomPage("", { fillerWords: 0 })
  });
  const errors = [];

  await validateReviewRoomDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /word count must be between 400 and 1500 words/);
});

test("validateReviewRoomDist reports over word-count bound", async (t) => {
  const root = await createManagedReviewRoomDistFixture(t, {
    reviewRoomHtml: reviewRoomPage("", { fillerWords: 1700 })
  });
  const errors = [];

  await validateReviewRoomDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /word count must be between 400 and 1500 words/);
});

async function createManagedReviewRoomDistFixture(t, options = {}) {
  const root = await createReviewRoomDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createReviewRoomDistFixture({
  discoveryRoutes = [reviewRoomRoute],
  reviewRoomHtml = reviewRoomPage(),
  sitemapRoutes = [reviewRoomRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-review-room-test-"));
  const dist = join(root, "dist");
  const routes = new Set([reviewRoomRoute, ...reviewRoomRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(join(path, "index.html"), route === reviewRoomRoute ? reviewRoomHtml : page(route), "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: `Route ${route}`,
    summary: "Fixture route for review room validation.",
    publicClaimLevel: route === reviewRoomRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === reviewRoomRoute ? "use-case" : "evidence",
    ...(route === reviewRoomRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: ["Fixture limitations remain bounded."],
    nonClaims: ["No runtime behavior, production usage, deployment state, endpoint performance, or release approval proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteReviewRoomRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === reviewRoomRoute
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
  return `<!doctype html><html><head><meta property="og:type" content="article"></head><body><main>${body}</main></body></html>`;
}

function reviewRoomPage(extra = "", { fillerWords = 100, spacedHref = false } = {}) {
  const href = (route) => (spacedHref ? `<a href = "${route}">${route}</a>` : `<a href="${route}">${route}</a>`);
  const filler = Array.from({ length: fillerWords }, (_, index) => `evidence review agenda boundary ${index}`).join(" ");

  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>claim proof path rule ID/evidence tier coverage label limitation owner decision gap</p>
    <p>Known evidence is reducer-backed and public-safe; partial evidence is reduced-coverage and labeled; missing evidence is an explicit gap for human review.</p>
    ${reviewRoomRequiredLinks.map((route) => href(route)).join("\n")}
    <p>${filler}</p>
    ${extra}
  `);
}
