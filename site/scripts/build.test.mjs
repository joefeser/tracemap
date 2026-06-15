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
  const post = await readFile(join(root, "dist", "blog", "first-post", "index.html"), "utf8");
  const headers = await readFile(join(root, "dist", "_headers"), "utf8");

  assert.match(index, /href="\/blog\/first-post\/"/);
  assert.match(post, /<title>First Post \| TraceMap<\/title>/);
  assert.match(headers, /cache-control: public/);
  await assert.rejects(stat(join(root, "dist", "_blog")), /ENOENT/);
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

async function createSiteFixture({ articles = [], bodies = {}, skipMetadata = false } = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-site-test-"));
  const src = join(root, "src");
  const blog = join(src, "_blog");
  const articleDir = join(blog, "articles");

  await mkdir(articleDir, { recursive: true });
  await writeFile(join(src, "index.html"), "<!doctype html><title>Fixture</title>", "utf8");

  if (!skipMetadata) {
    await writeFile(join(blog, "articles.json"), JSON.stringify(articles, null, 2), "utf8");
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

function titleFromSlug(slug) {
  return slug
    .split("-")
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}
