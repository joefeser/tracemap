import assert from "node:assert/strict";
import { mkdir, mkdtemp, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import { deployAuditRequiredRoutes } from "./deploy-audit.mjs";
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
    robots: "User-agent: *\nAllow: /\n# LLM discovery: https://tracemap.tools/llms.txt\n"
  });

  await assert.rejects(
    validateDist({ root }),
    /robots\.txt must include "Sitemap: https:\/\/tracemap\.tools\/sitemap\.xml"/
  );
});

async function createDistFixture({
  docsHtml = page("<p>Docs</p>"),
  indexHtml = page('<a href="/docs/">Docs</a><link rel="canonical" href="https://tracemap.tools/">'),
  robots = "User-agent: *\nAllow: /\n\n# LLM discovery: https://tracemap.tools/llms.txt\nSitemap: https://tracemap.tools/sitemap.xml\n",
  sitemapUrls = deployAuditRequiredRoutes.map((route) => `https://tracemap.tools${route}`)
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-site-validate-test-"));
  const dist = join(root, "dist");

  const fixtureRoutes = new Set([
    ...deployAuditRequiredRoutes,
    "/blog/",
    "/capabilities/",
    "/demo/start-here/",
    "/demo/proof-upgrades/",
    "/demo/proof-assets/",
    "/evidence/",
    "/examples/",
    "/outputs/",
    "/workflows/"
  ]);

  for (const route of fixtureRoutes) {
    if (route === "/") {
      continue;
    }

    const path = route.replace(/^\/|\/$/g, "");
    await mkdir(join(dist, path), { recursive: true });
    await writeFile(
      join(dist, path, "index.html"),
      route === "/deploy-audit/" ? deployAuditPage() : page(`<p>${path}</p>`),
      "utf8"
    );
  }

  await writeFile(join(dist, "index.html"), indexHtml, "utf8");
  await writeFile(join(dist, "docs", "index.html"), docsHtml, "utf8");
  await writeFile(join(dist, "robots.txt"), robots, "utf8");
  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapUrls), "utf8");
  await writeDiscoveryFiles(dist);

  return root;
}

async function writeDiscoveryFiles(dist) {
  const outputs = await createDiscoveryOutputs(
    [
      ...deployAuditRequiredRoutes.map((route) => ({
        path: route,
        title: "Fixture Home",
        summary: "Fixture route for deterministic static evidence validation.",
        publicClaimLevel: "demo",
        sourceType: "site-page",
        hintCategory: route === "/limitations/" ? "limitations" : "evidence",
        preferredProofPath: "/docs/",
        limitations: ["Fixture limitations remain bounded."],
        nonClaims: ["No runtime behavior or production usage proof."]
      })),
      {
        url: "https://github.com/joefeser/tracemap/blob/main/README.md",
        title: "Fixture README",
        summary: "Fixture source document for validation.",
        publicClaimLevel: "main",
        sourceType: "repo-doc",
        hintCategory: "repo-doc",
        limitations: ["Fixture docs require validation context."],
        nonClaims: ["No release approval proof."]
      }
    ],
    { dist, resolveInternalPaths: true }
  );

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
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

function deployAuditPage() {
  return page(`
    <p>Public claim level: demo</p>
    <p>No public conclusion without evidence</p>
    <p>This is not live AWS state, not runtime behavior proof, and not deployment success proof.</p>
    <p>sitemap.xml robots.txt llms.txt docs-index.json routes-index.json</p>
  `);
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
