import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  swiftApiClientArticleRequiredLinks,
  swiftApiClientArticleRoute,
  swiftApiClientArticleSlug,
  validateSwiftApiClientArticleDist
} from "./swift-api-client-article.mjs";

test("validateSwiftApiClientArticleDist accepts the Swift API-client article", async (t) => {
  const root = await createManagedSwiftApiClientArticleFixture(t);
  const errors = [];

  await validateSwiftApiClientArticleDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateSwiftApiClientArticleDist reports missing blocks and links", async (t) => {
  const root = await createManagedSwiftApiClientArticleFixture(t, {
    pageHtml: pageShell("<main><p>Public claim level: demo.</p></main>")
  });
  const errors = [];

  await validateSwiftApiClientArticleDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required block: mobile-dependencies/);
  assert.match(errors.join("\n"), /missing required link: \/swift\/api-client-walkthrough\//);
});

test("validateSwiftApiClientArticleDist rejects unsupported claims outside boundary sections", async (t) => {
  const source = await sourcePage();
  const root = await createManagedSwiftApiClientArticleFixture(t, {
    pageHtml: source.replace("</main>", "<p>TraceMap proves endpoint reachability.</p></main>")
  });
  const errors = [];

  await validateSwiftApiClientArticleDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unsupported public claim/);
});

test("validateSwiftApiClientArticleDist permits non-claim wording inside boundary sections", async (t) => {
  const source = await sourcePage();
  const root = await createManagedSwiftApiClientArticleFixture(t, {
    pageHtml: source.replace(
      "</main>",
      '<section data-tm-boundary="claim-boundary"><p>TraceMap does not prove endpoint reachability, backend compatibility, request success, auth flow correctness, production traffic, API correctness, or runtime behavior.</p></section></main>'
    )
  });
  const errors = [];

  await validateSwiftApiClientArticleDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateSwiftApiClientArticleDist rejects affirmative claims inside boundary sections", async (t) => {
  const source = await sourcePage();
  const root = await createManagedSwiftApiClientArticleFixture(t, {
    pageHtml: source.replace(
      "</main>",
      '<section data-tm-boundary="claim-boundary"><p>TraceMap proves endpoint reachability.</p></section></main>'
    )
  });
  const errors = [];

  await validateSwiftApiClientArticleDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unsupported public claim/);
});

test("validateSwiftApiClientArticleDist strips nested boundary sections for raw-material checks", async (t) => {
  const source = await sourcePage();
  const root = await createManagedSwiftApiClientArticleFixture(t, {
    pageHtml: source.replace(
      "</main>",
      '<section data-tm-boundary="claim-boundary"><section><p>facts.ndjson</p></section></section></main>'
    )
  });
  const errors = [];

  await validateSwiftApiClientArticleDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateSwiftApiClientArticleDist accepts flexible metadata and block attributes", async (t) => {
  const source = await sourcePage();
  const flexibleHtml = source
    .replace('<meta property="og:type" content="article">', '<meta content = "article" data-test="ok" property = "og:type">')
    .replace(`<link rel="canonical" href="https://tracemap.tools${swiftApiClientArticleRoute}">`, `<link href = "https://tracemap.tools${swiftApiClientArticleRoute}" data-test="ok" rel = "canonical">`)
    .replaceAll("data-swift-api-blog-block=", "data-swift-api-blog-block = ");
  const root = await createManagedSwiftApiClientArticleFixture(t, {
    pageHtml: flexibleHtml
  });
  const errors = [];

  await validateSwiftApiClientArticleDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateSwiftApiClientArticleDist rejects raw material outside boundary sections", async (t) => {
  const source = await sourcePage();
  const root = await createManagedSwiftApiClientArticleFixture(t, {
    pageHtml: source.replace("</main>", "<p>facts.ndjson</p></main>")
  });
  const errors = [];

  await validateSwiftApiClientArticleDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /raw\/private material outside a boundary section/);
});

test("validateSwiftApiClientArticleDist reports missing sitemap route", async (t) => {
  const root = await createManagedSwiftApiClientArticleFixture(t, {
    sitemapRoutes: []
  });
  const errors = [];

  await validateSwiftApiClientArticleDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
});

async function createManagedSwiftApiClientArticleFixture(t, options = {}) {
  const root = await createSwiftApiClientArticleFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createSwiftApiClientArticleFixture({
  pageHtml,
  sitemapRoutes = [swiftApiClientArticleRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-swift-api-client-article-test-"));
  const dist = join(root, "dist");

  await mkdir(join(dist, "blog", swiftApiClientArticleSlug), { recursive: true });
  await mkdir(join(dist, "blog"), { recursive: true });
  await writeFile(join(dist, "blog", swiftApiClientArticleSlug, "index.html"), pageHtml ?? (await sourcePage()), "utf8");
  await writeFile(join(dist, "blog", "index.html"), pageShell(`<a href="${swiftApiClientArticleRoute}">Swift article</a>`), "utf8");
  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");

  return root;
}

async function sourcePage() {
  const body = await readFile(new URL("../src/_blog/articles/how-tracemap-reads-swift-api-clients.html", import.meta.url), "utf8");
  return page(body);
}

function page(body) {
  const links = swiftApiClientArticleRequiredLinks.map((link) => `<a href="${link}">${link}</a>`).join(" ");
  return pageShell(`
    <main>
      <article class="article">
        <header class="article-header">
          <h1>How TraceMap reads Swift API clients without pretending they ran</h1>
        </header>
        <div class="article-body">
          ${body}
          ${links}
        </div>
      </article>
    </main>`);
}

function pageShell(body) {
  return `<!doctype html><html lang="en"><head><title>How TraceMap Reads Swift API Clients Without Pretending They Ran | TraceMap</title><link rel="canonical" href="https://tracemap.tools${swiftApiClientArticleRoute}"><meta property="og:type" content="article"></head><body>${body}</body></html>`;
}

function renderSitemap(routes) {
  const urls = routes
    .map((route) => `<url><loc>https://tracemap.tools${route}</loc></url>`)
    .join("");
  return `<?xml version="1.0" encoding="UTF-8"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">${urls}</urlset>`;
}
