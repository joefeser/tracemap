import assert from "node:assert/strict";
import { mkdir, mkdtemp, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { validateDist } from "./validate.mjs";

test("validateDist accepts generated public sitemap and internal links", async () => {
  const root = await createDistFixture();

  await validateDist({ root });
});

test("validateDist reports missing dist directory through validation errors", async () => {
  const root = await mkdtemp(join(tmpdir(), "tracemap-site-validate-test-"));

  await assert.rejects(
    validateDist({ root }),
    /Site validation failed:\n- Unable to read generated output directory .*dist/
  );
});

test("validateDist normalizes trailing slash baseUrl values", async () => {
  const root = await createDistFixture();

  await validateDist({ baseUrl: "https://tracemap.tools/", root });
});

test("validateDist accepts directory links without trailing slashes", async () => {
  const root = await createDistFixture({
    indexHtml: page('<a href="/docs">Docs</a>')
  });

  await validateDist({ root });
});

test("validateDist rejects sitemap URLs without generated files", async () => {
  const root = await createDistFixture({
    sitemapUrls: ["https://tracemap.tools/", "https://tracemap.tools/missing/"]
  });

  await assert.rejects(
    validateDist({ root }),
    /Sitemap URL has no generated file: https:\/\/tracemap\.tools\/missing\//
  );
});

test("validateDist rejects broken internal HTML links", async () => {
  const root = await createDistFixture({
    indexHtml: page('<a href="/missing/">Missing</a>')
  });

  await assert.rejects(
    validateDist({ root }),
    /index\.html references missing path: \/missing\//
  );
});

test("validateDist rejects generated HTML without top navigation", async () => {
  const root = await createDistFixture({
    docsHtml: "<p>Docs</p>"
  });

  await assert.rejects(validateDist({ root }), /docs\/index\.html is missing <nav class="top-nav">/);
});

test("validateDist rejects stale top navigation", async () => {
  const root = await createDistFixture({
    docsHtml: page("<p>Docs</p>", {
      nav: topNav({ omitHref: "/capabilities/" })
    })
  });

  await assert.rejects(
    validateDist({ root }),
    /docs\/index\.html top navigation does not match the canonical links/
  );
});

test("validateDist requires robots sitemap directive", async () => {
  const root = await createDistFixture({
    robots: "User-agent: *\nAllow: /\n"
  });

  await assert.rejects(
    validateDist({ root }),
    /robots\.txt must include "Sitemap: https:\/\/tracemap\.tools\/sitemap\.xml"/
  );
});

async function createDistFixture({
  docsHtml = page("<p>Docs</p>"),
  indexHtml = page('<a href="/docs/">Docs</a><link rel="canonical" href="https://tracemap.tools/">'),
  robots = "User-agent: *\nAllow: /\n\nSitemap: https://tracemap.tools/sitemap.xml\n",
  sitemapUrls = ["https://tracemap.tools/", "https://tracemap.tools/docs/"]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-site-validate-test-"));
  const dist = join(root, "dist");

  for (const path of [
    "blog",
    "capabilities",
    "demo",
    "docs",
    "evidence",
    "examples",
    "limitations",
    "outputs",
    "validation",
    "workflows"
  ]) {
    await mkdir(join(dist, path), { recursive: true });
    await writeFile(join(dist, path, "index.html"), page(`<p>${path}</p>`), "utf8");
  }

  await writeFile(join(dist, "index.html"), indexHtml, "utf8");
  await writeFile(join(dist, "docs", "index.html"), docsHtml, "utf8");
  await writeFile(join(dist, "robots.txt"), robots, "utf8");
  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapUrls), "utf8");

  return root;
}

function renderSitemap(urls) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${urls.map((url) => `  <url><loc>${url}</loc></url>`).join("\n")}
</urlset>`;
}

function page(body, { nav = topNav() } = {}) {
  return `<!doctype html><html><body>${nav}<main>${body}</main></body></html>`;
}

function topNav({ omitHref } = {}) {
  const links = [
    ["/evidence/", "Evidence"],
    ["/outputs/", "Outputs"],
    ["/workflows/", "Workflows"],
    ["/examples/", "Examples"],
    ["/blog/", "Blog"],
    ["/capabilities/", "Capabilities"],
    ["/docs/", "Docs"],
    ["/validation/", "Validation"],
    ["/limitations/", "Limitations"],
    ["/demo/", "Demo"],
    ["https://github.com/joefeser/tracemap", "GitHub"]
  ];

  return `<nav class="top-nav" aria-label="Primary navigation">${links
    .filter(([href]) => href !== omitHref)
    .map(([href, text]) => `<a href="${href}">${text}</a>`)
    .join("")}</nav>`;
}
