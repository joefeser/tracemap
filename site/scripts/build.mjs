import { cp, mkdir, readdir, readFile, rm, writeFile } from "node:fs/promises";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const defaultRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

export async function buildSite(options = {}) {
  const context = createBuildContext(options);

  await rm(context.dist, { recursive: true, force: true });
  await mkdir(context.dist, { recursive: true });
  await copyPublicSource(context.src, context.dist);
  await generateBlog(context);

  context.log("Built static site to dist/");
}

function createBuildContext({ log = console.log, root = defaultRoot } = {}) {
  const siteRoot = resolve(root);
  const src = resolve(siteRoot, "src");
  const dist = resolve(siteRoot, "dist");

  return {
    blogSource: resolve(src, "_blog"),
    dist,
    log,
    root: siteRoot,
    src
  };
}

async function copyPublicSource(from, to) {
  await mkdir(to, { recursive: true });

  for (const entry of await readdir(from, { withFileTypes: true })) {
    if (entry.isDirectory() && entry.name.startsWith("_")) {
      continue;
    }

    const sourcePath = join(from, entry.name);
    const targetPath = join(to, entry.name);

    if (entry.isDirectory()) {
      await copyPublicSource(sourcePath, targetPath);
      continue;
    }

    if (entry.isFile()) {
      await cp(sourcePath, targetPath);
    }
  }
}

async function generateBlog(context) {
  const articles = await readArticles(context);

  await writeOutput(context, "blog/index.html", renderBlogIndex(articles));

  for (const article of articles) {
    const body = await readBlogSourceFile(
      context,
      article.body,
      `Missing blog body for slug "${article.slug}": ${article.body}`
    );
    await writeOutput(context, `blog/${article.slug}/index.html`, renderBlogArticle(article, body.trim()));
  }
}

async function readArticles(context) {
  const raw = await readBlogSourceFile(
    context,
    "articles.json",
    `Missing blog metadata file: ${formatSitePath(context, resolve(context.blogSource, "articles.json"))}`
  );
  let articles;

  try {
    articles = JSON.parse(raw);
  } catch (error) {
    throw new Error(`Blog articles metadata is not valid JSON: ${error.message}`);
  }

  if (!Array.isArray(articles) || articles.length === 0) {
    throw new Error("Blog articles must be a non-empty array.");
  }

  const slugs = new Set();
  for (const article of articles) {
    validateArticle(article, slugs);
    slugs.add(article.slug);
  }

  return articles;
}

async function readBlogSourceFile(context, relativePath, missingMessage) {
  try {
    return await readFile(resolve(context.blogSource, relativePath), "utf8");
  } catch (error) {
    if (error?.code === "ENOENT") {
      throw new Error(missingMessage, { cause: error });
    }

    throw new Error(`Unable to read blog source file: ${relativePath}`, { cause: error });
  }
}

function validateArticle(article, slugs) {
  const requiredFields = [
    "body",
    "calloutHeading",
    "calloutHtml",
    "cardDescription",
    "category",
    "description",
    "h1",
    "hero",
    "ogDescription",
    "published",
    "publishedDisplay",
    "slug",
    "title"
  ];

  for (const field of requiredFields) {
    if (typeof article[field] !== "string" || article[field].trim() === "") {
      throw new Error(`Blog article is missing required string field: ${field}`);
    }
  }

  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(article.slug)) {
    throw new Error(`Blog article slug is invalid: ${article.slug}`);
  }

  if (!/^articles\/[a-z0-9]+(?:-[a-z0-9]+)*\.html$/.test(article.body)) {
    throw new Error(`Blog article body path is invalid: ${article.slug}`);
  }

  if (slugs.has(article.slug)) {
    throw new Error(`Blog article slug is duplicated: ${article.slug}`);
  }

  if (!/^\d{4}-\d{2}-\d{2}$/.test(article.published)) {
    throw new Error(`Blog article published date must use YYYY-MM-DD: ${article.slug}`);
  }
}

async function writeOutput(context, pathname, html) {
  const outputPath = resolve(context.dist, pathname);
  await mkdir(dirname(outputPath), { recursive: true });
  await writeFile(outputPath, `${html}\n`, "utf8");
}

function formatSitePath(context, absolutePath) {
  return relative(context.root, absolutePath).split("\\").join("/");
}

function renderBlogIndex(articles) {
  const cards = articles
    .map(
      (article) => `          <a href="/blog/${escapeHtml(article.slug)}/">
            <span>${escapeHtml(article.category)}</span>
            <strong>${escapeHtml(article.title)}</strong>
            <p>${escapeHtml(article.cardDescription)}</p>
          </a>`
    )
    .join("\n");

  return renderPage({
    title: "Blog | TraceMap",
    description:
      "TraceMap articles on deterministic evidence, review handoff, contract-impact analysis, and the project workflow.",
    canonicalPath: "/blog/",
    ogType: "website",
    ogTitle: "TraceMap Blog",
    ogDescription: "Articles about evidence-backed repository review and TraceMap project workflow.",
    blogCurrent: "page",
    main: `<section class="page-hero">
        <p class="eyebrow">Blog</p>
        <h1>Notes on evidence-backed review.</h1>
        <p class="hero-subhead">
          Articles here explain why TraceMap exists, how the project is being
          built, and where deterministic static evidence helps teams review
          contracts, dependencies, gaps, and release risk.
        </p>
      </section>

      <section class="section">
        <div class="article-grid">
${cards}
        </div>
      </section>

      <section class="section callout-section">
        <h2>The blog follows the same boundaries as the tool.</h2>
        <p>
          TraceMap articles can describe review workflows, static evidence, and
          project coordination. They do not claim runtime proof, production usage,
          release approval, or AI impact analysis in the core scanner or reducer.
        </p>
      </section>`,
    footer: `<p>
        Start with the
        <a href="/examples/">examples</a>
        or run the
        <a href="/demo/">public demo</a>.
      </p>`
  });
}

function renderBlogArticle(article, body) {
  return renderPage({
    title: `${article.title} | TraceMap`,
    description: article.description,
    canonicalPath: `/blog/${article.slug}/`,
    ogType: "article",
    ogTitle: article.title,
    ogDescription: article.ogDescription,
    articlePublished: article.published,
    blogCurrent: "location",
    main: `<article class="article">
        <header class="article-header">
          <p class="eyebrow">${escapeHtml(article.category)}</p>
          <h1>${escapeHtml(article.h1)}</h1>
          <p class="hero-subhead">
            ${escapeHtml(article.hero)}
          </p>
          <p class="article-meta">Published ${escapeHtml(article.publishedDisplay)}</p>
        </header>

        <div class="article-body">
${indent(body, 10)}
        </div>
      </article>

      <section class="section callout-section">
        <h2>${escapeHtml(article.calloutHeading)}</h2>
        <p>
          ${article.calloutHtml}
        </p>
      </section>`,
    footer: `<p>
        Blog index:
        <a href="/blog/">TraceMap Blog</a>.
      </p>`
  });
}

function renderPage({
  articlePublished,
  blogCurrent,
  canonicalPath,
  description,
  footer,
  main,
  ogDescription,
  ogTitle,
  ogType,
  title
}) {
  const canonicalUrl = `https://tracemap.tools${canonicalPath}`;
  const articleMeta = articlePublished
    ? `    <meta property="article:published_time" content="${escapeHtml(articlePublished)}">\n`
    : "";

  return `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta
      name="description"
      content="${escapeHtml(description)}"
    >
    <meta name="robots" content="index,follow">
    <meta name="author" content="Joe Feser">
    <title>${escapeHtml(title)}</title>
    <link rel="canonical" href="${escapeHtml(canonicalUrl)}">
    <meta property="og:type" content="${escapeHtml(ogType)}">
    <meta property="og:site_name" content="TraceMap">
    <meta property="og:title" content="${escapeHtml(ogTitle)}">
    <meta
      property="og:description"
      content="${escapeHtml(ogDescription)}"
    >
    <meta property="og:url" content="${escapeHtml(canonicalUrl)}">
${articleMeta}    <link rel="icon" href="/favicon.svg" type="image/svg+xml">
    <link rel="stylesheet" href="/styles.css">
  </head>
  <body>
    <header class="site-header">
      <a class="brand" href="/" aria-label="TraceMap home">
        <span class="brand-mark" aria-hidden="true">T</span>
        <span>TraceMap</span>
      </a>
      <nav class="top-nav" aria-label="Primary navigation">
        <a href="/evidence/">Evidence</a>
        <a href="/outputs/">Outputs</a>
        <a href="/workflows/">Workflows</a>
        <a href="/examples/">Examples</a>
        <a href="/blog/" aria-current="${escapeHtml(blogCurrent)}">Blog</a>
        <a href="/validation/">Validation</a>
        <a href="/limitations/">Limitations</a>
        <a href="/demo/">Demo</a>
        <a href="https://github.com/joefeser/tracemap">GitHub</a>
      </nav>
    </header>

    <main>
      ${main}
    </main>

    <footer class="site-footer">
      ${footer}
      <p class="copyright">&copy; 2026 Joe Feser.</p>
    </footer>
  </body>
</html>`;
}

function escapeHtml(value) {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function indent(value, spaces) {
  const padding = " ".repeat(spaces);
  return value
    .split("\n")
    .map((line) => (line.length > 0 ? `${padding}${line}` : ""))
    .join("\n");
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  await buildSite();
}
