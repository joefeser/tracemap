import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  blogProofPathRequiredLinks,
  blogProofPathSeriesRoute,
  blogProofPathSeriesSlug,
  validateBlogProofPathSeriesDist
} from "./blog-proof-path-series.mjs";

test("validateBlogProofPathSeriesDist accepts the proof-path blog article", async (t) => {
  const root = await createManagedBlogFixture(t);
  const errors = [];

  await validateBlogProofPathSeriesDist({ dist: join(root, "dist"), errors, root: join(root, "src") });

  assert.deepEqual(errors, []);
});

test("validateBlogProofPathSeriesDist reports missing required content block", async (t) => {
  const root = await createManagedBlogFixture(t, {
    articleHtml: articlePage().replace(
      'data-proof-blog-block="proof-path-reading-steps"',
      'data-proof-blog-block="proof-path-walkthrough"'
    )
  });
  const errors = [];

  await validateBlogProofPathSeriesDist({ dist: join(root, "dist"), errors, root: join(root, "src") });

  assert.match(errors.join("\n"), /missing required block: proof-path-reading-steps/);
});

test("validateBlogProofPathSeriesDist reports missing required proof-surface link", async (t) => {
  const root = await createManagedBlogFixture(t, {
    articleHtml: articlePage().replaceAll('href="/questions/"', 'href="/missing-questions/"')
  });
  const errors = [];

  await validateBlogProofPathSeriesDist({ dist: join(root, "dist"), errors, root: join(root, "src") });

  assert.match(errors.join("\n"), /missing required link: \/questions\//);
});

test("validateBlogProofPathSeriesDist rejects forbidden claims outside boundary sections", async (t) => {
  const root = await createManagedBlogFixture(t, {
    articleHtml: articlePage('<p>TraceMap proves runtime behavior for this endpoint.</p>')
  });
  const errors = [];

  await validateBlogProofPathSeriesDist({ dist: join(root, "dist"), errors, root: join(root, "src") });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateBlogProofPathSeriesDist permits wording-to-avoid examples inside boundary sections", async (t) => {
  const root = await createManagedBlogFixture(t, {
    articleHtml: articlePage(`
      <section data-proof-blog-block="unsafe-language-examples" data-tm-boundary="wording-to-avoid">
        <h2>Unsafe language examples, framed as wording to avoid</h2>
        <ul>
          <li>"TraceMap proves this endpoint is safe in production."</li>
          <li>"The proof path confirms release safety."</li>
          <li>"The scan identifies the root cause of the outage."</li>
          <li>"AI impact analysis determines the affected services."</li>
          <li>"Static evidence replaces telemetry and tests."</li>
        </ul>
      </section>
    `)
  });
  const errors = [];

  await validateBlogProofPathSeriesDist({ dist: join(root, "dist"), errors, root: join(root, "src") });

  assert.deepEqual(errors, []);
});

test("validateBlogProofPathSeriesDist rejects raw material outside boundary sections", async (t) => {
  const root = await createManagedBlogFixture(t, {
    articleHtml: articlePage("<p>Publish raw facts beside the public claim.</p>")
  });
  const errors = [];

  await validateBlogProofPathSeriesDist({ dist: join(root, "dist"), errors, root: join(root, "src") });

  assert.match(errors.join("\n"), /forbidden raw\/private material/);
});

test("validateBlogProofPathSeriesDist reports metadata regressions", async (t) => {
  const root = await createManagedBlogFixture(t, {
    articles: [
      articleMetadata({
        body: "articles/other.html",
        description:
          "This description is intentionally too long for the proof path blog metadata validator because it should remain concise and conservative in public previews and avoid bloated discovery text."
      })
    ]
  });
  const errors = [];

  await validateBlogProofPathSeriesDist({ dist: join(root, "dist"), errors, root: join(root, "src") });

  assert.match(errors.join("\n"), /unexpected body path/);
  assert.match(errors.join("\n"), /description must be 160 characters or fewer/);
});

async function createManagedBlogFixture(t, { articleHtml = articlePage(), articles = [articleMetadata()] } = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-blog-proof-path-test-"));
  const dist = join(root, "dist");
  const src = join(root, "src");
  const articleDir = join(dist, "blog", blogProofPathSeriesSlug);
  const sourceArticleDir = join(src, "_blog", "articles");

  t.after(() => rmrf(root));

  await mkdir(articleDir, { recursive: true });
  await mkdir(sourceArticleDir, { recursive: true });
  await mkdir(join(dist, "blog"), { recursive: true });
  await writeFile(join(articleDir, "index.html"), articleHtml, "utf8");
  await writeFile(
    join(dist, "blog", "index.html"),
    pageShell(`<a href="${blogProofPathSeriesRoute}">What a Proof Path Is</a>`),
    "utf8"
  );
  await writeFile(join(dist, "sitemap.xml"), renderSitemap([blogProofPathSeriesRoute]), "utf8");
  await writeFile(join(src, "_blog", "articles.json"), JSON.stringify(articles, null, 2), "utf8");
  await writeFile(join(sourceArticleDir, `${blogProofPathSeriesSlug}.html`), "<p>Source body.</p>", "utf8");

  return root;
}

async function rmrf(path) {
  const { rm } = await import("node:fs/promises");
  await rm(path, { recursive: true, force: true });
}

function articleMetadata(overrides = {}) {
  return {
    slug: blogProofPathSeriesSlug,
    category: "Proof paths",
    title: "What a Proof Path Is",
    h1: "What a proof path is",
    description: "A practical guide to reading TraceMap proof paths, static evidence, limitations, and claim boundaries.",
    ogDescription: "Learn how to read a TraceMap proof path without turning static evidence into runtime claims.",
    cardDescription: "How to follow a claim through proof surfaces, evidence tiers, limitations, and handoff language.",
    hero: "A proof path keeps a public claim attached to its evidence, limitation, and next question.",
    published: "2026-06-21",
    publishedDisplay: "June 21, 2026",
    body: `articles/${blogProofPathSeriesSlug}.html`,
    calloutHeading: "Ready to inspect the surfaces?",
    calloutHtml: "Open the <a href=\"/proof-paths/\">proof path index</a>.",
    ...overrides
  };
}

function articlePage(extraHtml = "") {
  const links = blogProofPathRequiredLinks
    .map((link) => `<a href="${link}">${link}</a>`)
    .join(" ");
  const filler = Array.from(
    { length: 55 },
    () =>
      "The reviewer follows static evidence, checks rule family, reads coverage label, records limitation, and hands unresolved runtime questions to owners."
  ).join(" ");

  return pageShell(`
    <article>
      <header>
        <h1>What a proof path is</h1>
        <p>Public claim level: concept</p>
      </header>
      <section data-proof-blog-block="opening-problem">
        <h2>The problem is claim drift.</h2>
        <p>${filler}</p>
      </section>
      <section data-proof-blog-block="evidence-backed-claim-example">
        <h2>An evidence-backed claim names what would support it.</h2>
        <p>${links}</p>
      </section>
      <section data-proof-blog-block="proof-path-reading-steps">
        <h2>Read a proof path in order.</h2>
        <p>Start with the claim, check the public claim level, open the proof surface, read the tier, keep the limitation visible, and use the checklist.</p>
      </section>
      <section data-proof-blog-block="proof-surfaces">
        <h2>The supporting surfaces each have a job.</h2>
        <p>${links}</p>
      </section>
      <section data-proof-blog-block="limitations-and-non-claims" data-tm-boundary="claim-boundary">
        <h2>Limitations and non-claims are part of the proof path.</h2>
        <p>TraceMap does not prove runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, release approval, complete coverage, or product behavior.</p>
        <p>Do not publish raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, hidden validation details, raw command output, or credential-like values.</p>
      </section>
      <section data-proof-blog-block="safe-language-examples">
        <h2>Safe language examples</h2>
        <p>This proof path shows where the public claim is supported and what limits still apply.</p>
      </section>
      <section data-proof-blog-block="unsafe-language-examples" data-tm-boundary="wording-to-avoid">
        <h2>Unsafe language examples, framed as wording to avoid</h2>
        <p>TraceMap proves this endpoint is safe in production.</p>
      </section>
      <section data-proof-blog-block="closing-handoff-action">
        <h2>Close the loop with a handoff, not a bigger claim.</h2>
        <p>Repeat the sentence with the same limits or take the static evidence to the owner, telemetry, logs, traces, tests, or release review.</p>
      </section>
      ${extraHtml}
    </article>
  `);
}

function pageShell(body) {
  return `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8">
    <meta name="description" content="Fixture">
    <meta property="og:type" content="article">
    <link rel="canonical" href="https://tracemap.tools${blogProofPathSeriesRoute}">
    <title>Fixture</title>
  </head>
  <body>${body}</body>
</html>`;
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${routes.map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`).join("\n")}
</urlset>`;
}
