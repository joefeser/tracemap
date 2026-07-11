import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  ragVsAgenticRetrievalArticleRequiredLinks,
  ragVsAgenticRetrievalArticleRoute,
  ragVsAgenticRetrievalArticleSlug,
  validateRagVsAgenticRetrievalArticleDist
} from "./rag-vs-agentic-retrieval-article.mjs";

test("validateRagVsAgenticRetrievalArticleDist accepts the article", async (t) => {
  const root = await createManagedArticleFixture(t);
  const errors = [];

  await validateRagVsAgenticRetrievalArticleDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateRagVsAgenticRetrievalArticleDist reports missing blocks and links", async (t) => {
  const root = await createManagedArticleFixture(t, {
    pageHtml: pageShell("<main><p>Public claim level: concept.</p></main>")
  });
  const errors = [];

  await validateRagVsAgenticRetrievalArticleDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required block: classic-rag/);
  assert.match(errors.join("\n"), /missing required link: \/vault-export\//);
});

test("validateRagVsAgenticRetrievalArticleDist rejects unsupported impacted wording", async (t) => {
  const source = await sourceBody();
  const root = await createManagedArticleFixture(t, {
    pageHtml: page(source.replace("related static surfaces", "impacted static surfaces"))
  });
  const errors = [];

  await validateRagVsAgenticRetrievalArticleDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unsupported public claim/);
});

test("validateRagVsAgenticRetrievalArticleDist rejects Qodo replacement claims", async (t) => {
  const source = await sourceBody();
  const root = await createManagedArticleFixture(t, {
    pageHtml: page(`${source}<p>TraceMap replaces Qodo.</p>`)
  });
  const errors = [];

  await validateRagVsAgenticRetrievalArticleDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unsupported public claim/);
});

test("validateRagVsAgenticRetrievalArticleDist rejects raw material outside boundary sections", async (t) => {
  const source = await sourceBody();
  const root = await createManagedArticleFixture(t, {
    pageHtml: page(`${source}<p>facts.ndjson</p>`)
  });
  const errors = [];

  await validateRagVsAgenticRetrievalArticleDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /raw\/private material outside a boundary section/);
});

test("validateRagVsAgenticRetrievalArticleDist permits boundary non-claim examples", async (t) => {
  const source = await sourceBody();
  const root = await createManagedArticleFixture(t, {
    pageHtml: page(`${source}<section data-tm-boundary="claim-boundary"><p>facts.ndjson raw SQL raw source snippets</p></section>`)
  });
  const errors = [];

  await validateRagVsAgenticRetrievalArticleDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateRagVsAgenticRetrievalArticleDist reports missing sitemap route", async (t) => {
  const root = await createManagedArticleFixture(t, {
    sitemapRoutes: []
  });
  const errors = [];

  await validateRagVsAgenticRetrievalArticleDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
});

async function createManagedArticleFixture(t, options = {}) {
  const root = await createArticleFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createArticleFixture({
  pageHtml,
  sitemapRoutes = [ragVsAgenticRetrievalArticleRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-rag-agentic-article-test-"));
  const dist = join(root, "dist");

  await mkdir(join(dist, "blog", ragVsAgenticRetrievalArticleSlug), { recursive: true });
  await mkdir(join(dist, "blog"), { recursive: true });
  await writeFile(join(dist, "blog", ragVsAgenticRetrievalArticleSlug, "index.html"), pageHtml ?? page(await sourceBody()), "utf8");
  await writeFile(join(dist, "blog", "index.html"), pageShell(`<a href="${ragVsAgenticRetrievalArticleRoute}">RAG article</a>`), "utf8");
  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");

  return root;
}

async function sourceBody() {
  return readFile(new URL("../src/_blog/articles/rag-vs-agentic-retrieval-for-code-review.html", import.meta.url), "utf8");
}

function page(body) {
  const links = ragVsAgenticRetrievalArticleRequiredLinks.map((link) => `<a href="${link}">${link}</a>`).join(" ");
  return pageShell(`
    <main>
      <article class="article">
        <header class="article-header">
          <h1>RAG vs agentic retrieval for code review</h1>
        </header>
        <div class="article-body">
          ${body}
          ${links}
        </div>
      </article>
    </main>`);
}

function pageShell(body) {
  return `<!doctype html><html lang="en"><head><title>RAG vs Agentic Retrieval for Code Review: A TraceMap Perspective | TraceMap</title><link rel="canonical" href="https://tracemap.tools${ragVsAgenticRetrievalArticleRoute}"><meta property="og:type" content="article"></head><body>${body}</body></html>`;
}

function renderSitemap(routes) {
  const urls = routes
    .map((route) => `<url><loc>https://tracemap.tools${route}</loc></url>`)
    .join("");
  return `<?xml version="1.0" encoding="UTF-8"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">${urls}</urlset>`;
}
