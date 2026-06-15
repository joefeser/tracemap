import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, stat, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { buildSite } from "./build.mjs";

test("buildSite publishes generated blog pages and keeps private blog folders out of dist", async () => {
  const root = await createSiteFixture({
    articles: [article("first-post")],
    bodies: { "first-post": "<p>Body content.</p>" }
  });

  await writeFile(join(root, "src", "_headers"), "/*\n  cache-control: public\n", "utf8");
  await mkdir(join(root, "src", "_blog", "private"));
  await writeFile(join(root, "src", "_blog", "private", "hidden.txt"), "hidden", "utf8");

  await buildSite({ log: () => {}, root });

  const index = await readFile(join(root, "dist", "blog", "index.html"), "utf8");
  const home = await readFile(join(root, "dist", "index.html"), "utf8");
  const post = await readFile(join(root, "dist", "blog", "first-post", "index.html"), "utf8");
  const headers = await readFile(join(root, "dist", "_headers"), "utf8");
  const sitemap = await readFile(join(root, "dist", "sitemap.xml"), "utf8");

  assert.match(index, /href="\/blog\/first-post\/"/);
  assert.match(index, /href="\/capabilities\/">Capabilities<\/a>/);
  assert.match(index, /href="\/docs\/">Docs<\/a>/);
  assert.match(home, /href="\/capabilities\/">Capabilities<\/a>/);
  assert.match(home, /href="\/docs\/">Docs<\/a>/);
  assert.doesNotMatch(home, /Old Nav/);
  assert.match(post, /<title>First Post \| TraceMap<\/title>/);
  assert.match(headers, /cache-control: public/);
  assert.match(sitemap, /<loc>https:\/\/tracemap\.tools\/<\/loc>/);
  assert.match(sitemap, /<loc>https:\/\/tracemap\.tools\/blog\/first-post\/<\/loc>/);
  await assert.rejects(stat(join(root, "dist", "_blog")), /ENOENT/);
  await assert.rejects(stat(join(root, "dist", "_site")), /ENOENT/);
});

test("buildSite replaces source headers with additional attributes and class order changes", async () => {
  const root = await createSiteFixture({
    articles: [article("first-post")],
    bodies: { "first-post": "<p>Body content.</p>" },
    indexHtml: `<!doctype html>
<html>
  <body>
    <header id="top" data-source="fixture" class="old site-header stale">
      <nav class="top-nav"><a href="/old/">Old Nav</a></nav>
    </header>
  </body>
</html>`
  });

  await buildSite({ log: () => {}, root });

  const home = await readFile(join(root, "dist", "index.html"), "utf8");

  assert.match(home, /<header class="site-header">/);
  assert.match(home, /href="\/capabilities\/">Capabilities<\/a>/);
  assert.doesNotMatch(home, /Old Nav/);
  assert.doesNotMatch(home, /data-source="fixture"/);
});

test("buildSite reports static HTML pages without replaceable site headers", async () => {
  const root = await createSiteFixture({
    articles: [article("first-post")],
    bodies: { "first-post": "<p>Body content.</p>" },
    indexHtml: "<!doctype html><html><body><main>Fixture</main></body></html>"
  });

  await assert.rejects(
    buildSite({ log: () => {}, root }),
    /Static HTML page is missing a replaceable site header: \//
  );
});

test("buildSite reports missing blog metadata with site-relative context", async () => {
  const root = await createSiteFixture({ skipMetadata: true });

  await assert.rejects(
    buildSite({ log: () => {}, root }),
    /Missing blog metadata file: src\/_blog\/articles\.json/
  );
});

test("buildSite reports missing article body with slug and expected path", async () => {
  const root = await createSiteFixture({
    articles: [article("missing-body")]
  });

  await assert.rejects(
    buildSite({ log: () => {}, root }),
    /Missing blog body for slug "missing-body": articles\/missing-body\.html/
  );
});

test("buildSite reports non-object article metadata with index context", async () => {
  const root = await createSiteFixture({
    articles: [null]
  });

  await assert.rejects(
    buildSite({ log: () => {}, root }),
    /Blog article at index 0 must be an object\./
  );
});

test("buildSite reports invalid article body path with the invalid value", async () => {
  const root = await createSiteFixture({
    articles: [
      {
        ...article("bad-body"),
        body: "articles/Bad Body.html"
      }
    ]
  });

  await assert.rejects(
    buildSite({ log: () => {}, root }),
    /Blog article body path is invalid for slug "bad-body": articles\/Bad Body\.html/
  );
});

test("buildSite reports missing site page metadata with site-relative context", async () => {
  const root = await createSiteFixture({
    articles: [article("first-post")],
    bodies: { "first-post": "<p>Body content.</p>" },
    skipSiteMetadata: true
  });

  await assert.rejects(
    buildSite({ log: () => {}, root }),
    /Missing site page metadata file: src\/_site\/pages\.json/
  );
});

test("buildSite rejects duplicated sitemap paths", async () => {
  const root = await createSiteFixture({
    articles: [article("first-post")],
    bodies: { "first-post": "<p>Body content.</p>" },
    sitePages: [
      sitemapPage("/"),
      sitemapPage("/")
    ]
  });

  await assert.rejects(
    buildSite({ log: () => {}, root }),
    /Sitemap path is duplicated: \//
  );
});

test("buildSite reports non-object sitemap page metadata with index context", async () => {
  const root = await createSiteFixture({
    articles: [article("first-post")],
    bodies: { "first-post": "<p>Body content.</p>" },
    sitePages: [null]
  });

  await assert.rejects(
    buildSite({ log: () => {}, root }),
    /Site page metadata entry at index 0 must be an object\./
  );
});

async function createSiteFixture({
  articles = [],
  bodies = {},
  indexHtml = `<!doctype html>
<html>
  <body>
    <header class="site-header"><nav class="top-nav"><a href="/old/">Old Nav</a></nav></header>
  </body>
</html>`,
  sitePages = [sitemapPage("/")],
  skipMetadata = false,
  skipSiteMetadata = false
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-site-test-"));
  const src = join(root, "src");
  const blog = join(src, "_blog");
  const articleDir = join(blog, "articles");
  const siteData = join(src, "_site");

  await mkdir(articleDir, { recursive: true });
  await mkdir(siteData, { recursive: true });
  await writeFile(join(src, "index.html"), indexHtml, "utf8");

  if (!skipMetadata) {
    await writeFile(join(blog, "articles.json"), JSON.stringify(articles, null, 2), "utf8");
  }

  if (!skipSiteMetadata) {
    await writeFile(join(siteData, "pages.json"), JSON.stringify(sitePages, null, 2), "utf8");
  }

  for (const [slug, body] of Object.entries(bodies)) {
    await writeFile(join(articleDir, `${slug}.html`), body, "utf8");
  }

  return root;
}

function article(slug) {
  const title = titleFromSlug(slug);

  return {
    body: `articles/${slug}.html`,
    calloutHeading: "Next step",
    calloutHtml: "Read <a href=\"/examples/\">examples</a>.",
    cardDescription: `${title} card.`,
    category: "Test",
    description: `${title} description.`,
    h1: title,
    hero: `${title} hero.`,
    ogDescription: `${title} Open Graph description.`,
    published: "2026-06-14",
    publishedDisplay: "June 14, 2026",
    slug,
    title
  };
}

function sitemapPage(path) {
  return {
    path,
    changefreq: "weekly",
    priority: "1.0"
  };
}

function titleFromSlug(slug) {
  return slug
    .split("-")
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}
