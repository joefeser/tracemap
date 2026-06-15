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
    indexHtml: '<a href="/missing/">Missing</a>'
  });

  await assert.rejects(
    validateDist({ root }),
    /index\.html references missing path: \/missing\//
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
  indexHtml = '<a href="/docs/">Docs</a><link rel="canonical" href="https://tracemap.tools/">',
  robots = "User-agent: *\nAllow: /\n\nSitemap: https://tracemap.tools/sitemap.xml\n",
  sitemapUrls = ["https://tracemap.tools/", "https://tracemap.tools/docs/"]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-site-validate-test-"));
  const dist = join(root, "dist");

  await mkdir(join(dist, "docs"), { recursive: true });
  await writeFile(join(dist, "index.html"), indexHtml, "utf8");
  await writeFile(join(dist, "docs", "index.html"), "<p>Docs</p>", "utf8");
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
