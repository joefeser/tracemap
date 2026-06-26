import assert from "node:assert/strict";
import { mkdir, mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  buildReviewWorkflowRequiredLinks,
  buildReviewWorkflowStoryRoute,
  buildReviewWorkflowStorySlug,
  validateBuildReviewWorkflowStoryDist
} from "./build-review-workflow-story.mjs";

test("validateBuildReviewWorkflowStoryDist accepts the build-review workflow article", async (t) => {
  const root = await createBuildReviewWorkflowFixture(t);
  const errors = [];

  await validateBuildReviewWorkflowStoryDist({ dist: join(root, "dist"), errors, root: join(root, "src") });

  assert.deepEqual(errors, []);
});

test("validateBuildReviewWorkflowStoryDist reports missing concept label", async (t) => {
  const root = await createBuildReviewWorkflowFixture(t, {
    articleHtml: articlePage().replace("Public claim level: concept", "Public claim level: demo")
  });
  const errors = [];

  await validateBuildReviewWorkflowStoryDist({ dist: join(root, "dist"), errors, root: join(root, "src") });

  assert.match(errors.join("\n"), /missing required text: Public claim level: concept/);
});

test("validateBuildReviewWorkflowStoryDist requires non-claim marker", async (t) => {
  const root = await createBuildReviewWorkflowFixture(t, {
    articleHtml: articlePage().replace(' data-non-claim-region="workflow-does-not-prove"', "")
  });
  const errors = [];

  await validateBuildReviewWorkflowStoryDist({ dist: join(root, "dist"), errors, root: join(root, "src") });

  assert.match(errors.join("\n"), /non-claims section must use data-non-claim-region/);
});

test("validateBuildReviewWorkflowStoryDist rejects forbidden claims outside marked regions", async (t) => {
  const root = await createBuildReviewWorkflowFixture(t, {
    articleHtml: articlePage("<p>TraceMap uses AI to inspect product changes.</p>")
  });
  const errors = [];

  await validateBuildReviewWorkflowStoryDist({ dist: join(root, "dist"), errors, root: join(root, "src") });

  assert.match(errors.join("\n"), /forbidden claim outside marked regions: TraceMap uses AI/);
});

test("validateBuildReviewWorkflowStoryDist rejects private material outside marked regions", async (t) => {
  const root = await createBuildReviewWorkflowFixture(t, {
    articleHtml: articlePage("<p>The story can publish raw review logs.</p>")
  });
  const errors = [];

  await validateBuildReviewWorkflowStoryDist({ dist: join(root, "dist"), errors, root: join(root, "src") });

  assert.match(errors.join("\n"), /forbidden private\/raw material outside marked regions: raw review logs/);
});

test("validateBuildReviewWorkflowStoryDist reports metadata regressions", async (t) => {
  const root = await createBuildReviewWorkflowFixture(t, {
    articles: [
      articleMetadata({
        body: "articles/other.html",
        category: "Project workflow",
        description: "TraceMap uses AI to approve releases."
      })
    ]
  });
  const errors = [];

  await validateBuildReviewWorkflowStoryDist({ dist: join(root, "dist"), errors, root: join(root, "src") });

  assert.match(errors.join("\n"), /expected body articles\/building-tracemap-under-review-pressure\.html/);
  assert.match(errors.join("\n"), /expected category Workflow governance/);
  assert.match(errors.join("\n"), /forbidden claim outside marked regions: TraceMap uses AI/);
});

test("validateBuildReviewWorkflowStoryDist reports missing reciprocal link", async (t) => {
  const root = await createBuildReviewWorkflowFixture(t, {
    oldWorkflowHtml: pageShell("<article><p>Older workflow article.</p></article>", {
      route: "/blog/building-tracemap-with-codex-kiro-qodo/"
    })
  });
  const errors = [];

  await validateBuildReviewWorkflowStoryDist({ dist: join(root, "dist"), errors, root: join(root, "src") });

  assert.match(errors.join("\n"), /Earlier workflow article must link to/);
});

async function createBuildReviewWorkflowFixture(
  t,
  {
    articleHtml = articlePage(),
    articles = [articleMetadata()],
    oldWorkflowHtml = pageShell(`<article><a href="${buildReviewWorkflowStoryRoute}">New companion article</a></article>`, {
      route: "/blog/building-tracemap-with-codex-kiro-qodo/"
    })
  } = {}
) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-build-review-workflow-test-"));
  const dist = join(root, "dist");
  const src = join(root, "src");
  const articleDir = join(dist, "blog", buildReviewWorkflowStorySlug);
  const oldArticleDir = join(dist, "blog", "building-tracemap-with-codex-kiro-qodo");
  const sourceArticleDir = join(src, "_blog", "articles");

  t.after(() => rm(root, { recursive: true, force: true }));

  await mkdir(articleDir, { recursive: true });
  await mkdir(oldArticleDir, { recursive: true });
  await mkdir(join(dist, "blog"), { recursive: true });
  await mkdir(sourceArticleDir, { recursive: true });
  await writeFile(join(articleDir, "index.html"), articleHtml, "utf8");
  await writeFile(join(oldArticleDir, "index.html"), oldWorkflowHtml, "utf8");
  await writeFile(
    join(dist, "blog", "index.html"),
    pageShell(`<a href="${buildReviewWorkflowStoryRoute}"><span>Workflow governance</span>Building TraceMap Under Review Pressure</a>`, {
      route: "/blog/"
    }),
    "utf8"
  );
  await writeFile(join(dist, "sitemap.xml"), renderSitemap([buildReviewWorkflowStoryRoute]), "utf8");
  await writeFile(join(src, "_blog", "articles.json"), JSON.stringify(articles, null, 2), "utf8");
  await writeFile(join(sourceArticleDir, `${buildReviewWorkflowStorySlug}.html`), "<p>Source body.</p>", "utf8");

  return root;
}

function articleMetadata(overrides = {}) {
  return {
    slug: buildReviewWorkflowStorySlug,
    category: "Workflow governance",
    title: "Building TraceMap Under Review Pressure",
    h1: "Building TraceMap Under Review Pressure",
    description:
      "A concept-level workflow story about using specs, review loops, and deterministic validation to keep TraceMap public claims attached to evidence.",
    ogDescription:
      "How TraceMap site work uses specs, review pressure, claim levels, and validation evidence without turning workflow pressure into product proof.",
    cardDescription:
      "A process story about review pressure, claim levels, validation evidence, non-claims, and human ownership in TraceMap site work.",
    hero:
      "A concept-level note on using specs, review loops, and deterministic validation to keep public claims attached to evidence.",
    published: "2026-06-26",
    publishedDisplay: "June 26, 2026",
    body: `articles/${buildReviewWorkflowStorySlug}.html`,
    calloutHeading: "Want the baseline workflow first?",
    calloutHtml:
      "Read <a href=\"/blog/building-tracemap-with-codex-kiro-qodo/\">Building TraceMap With Codex, Kiro, and Qodo</a>.",
    ...overrides
  };
}

function articlePage(extraHtml = "") {
  const links = buildReviewWorkflowRequiredLinks.map((link) => `<a href="${link}">${link}</a>`).join(" ");
  const filler = Array.from(
    { length: 42 },
    () =>
      "The workflow keeps claim level, review context, validation evidence, limitation, and human ownership visible before public wording grows stronger."
  ).join(" ");

  return pageShell(`
    <article>
      <header>
        <h1>Building TraceMap Under Review Pressure</h1>
      </header>
      <div class="article-body">
        <p>Public claim level: concept</p>
        <p>No public conclusion without evidence</p>
        <section data-build-review-block="claim-level-note">
          <h2>Claim-level note</h2>
          <p>${filler}</p>
          <p>${links}</p>
        </section>
        <section data-build-review-block="pressure-shaped-workflow">
          <h2>The pressure that shaped the workflow</h2>
          <p>Review pressure asks for evidence, limitations, and partial-state labels.</p>
        </section>
        <section data-build-review-block="specs-before-implementation">
          <h2>Specs before implementation</h2>
          <p>Kiro reviews spec packets as pressure, not certification or endorsement.</p>
        </section>
        <section data-build-review-block="reviewable-diffs">
          <h2>Implementation with reviewable diffs</h2>
          <p>Codex assists implementation in the build workflow, not the scanner or reducer.</p>
        </section>
        <section data-build-review-block="review-loop-coordination">
          <h2>Kiro, Qodo, and review-loop coordination</h2>
          <p>Qodo may surface PR findings. A review-loop coordination layer organizes stop reasons and validation evidence.</p>
          <p>Human ownership remains necessary for merge, publication, product claims, and unresolved judgment calls.</p>
        </section>
        <section data-build-review-block="workflow-does-not-prove" data-non-claim-region="workflow-does-not-prove">
          <h2>What the workflow does not prove</h2>
          <p>This workflow does not prove production traffic, endpoint performance, outage cause, release safe status, safe to release status, approved by Codex, approved by Kiro, approved by Qodo, certified by anyone, endorsed by anyone, autonomous merge authority, complete coverage, AI impact analysis, LLM impact analysis, embeddings, vector database, prompt classification, or that tools consume TraceMap output.</p>
          <p>It does not publish raw review logs, raw bot transcripts, raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, configuration values, generated scan directories, secrets, credential-like values, local paths, raw remotes, private sample names, hidden run IDs, private session IDs, or hidden validation details.</p>
        </section>
        <section data-build-review-block="lessons-evidence-led-specs">
          <h2>Lessons for evidence-led specs</h2>
          <p>State the level early, keep acceptance criteria observable, and attach limitations.</p>
        </section>
        <section data-build-review-block="validation-publication-checklist">
          <h2>Validation and publication checklist</h2>
          <p>Check metadata, sitemap, route output, internal links, word count, and private-material filters before publication.</p>
        </section>
        ${extraHtml}
      </div>
    </article>
  `);
}

function pageShell(body, { route = buildReviewWorkflowStoryRoute } = {}) {
  return `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8">
    <meta name="description" content="Concept-level fixture">
    <meta property="og:type" content="article">
    <meta property="og:description" content="Concept-level fixture">
    <link rel="canonical" href="https://tracemap.tools${route}">
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
