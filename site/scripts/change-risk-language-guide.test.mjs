import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  changeRiskLanguageGuideInboundRoutes,
  changeRiskLanguageGuideRequiredLinks,
  changeRiskLanguageGuideRoute,
  validateChangeRiskLanguageGuideDist
} from "./change-risk-language-guide.mjs";

const scriptsDir = dirname(fileURLToPath(import.meta.url));
const sourcePagePath = resolve(scriptsDir, "..", "src", "language", "change-risk", "index.html");

test("validateChangeRiskLanguageGuideDist accepts the canonical language guide route", async (t) => {
  const root = await createManagedFixture(t);
  const errors = [];

  await validateChangeRiskLanguageGuideDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateChangeRiskLanguageGuideDist reports route metadata regressions", async (t) => {
  const root = await createManagedFixture(t);
  await rewriteRouteEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "use-case",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/",
    nonClaims: ["No runtime proof."]
  });
  const errors = [];

  await validateChangeRiskLanguageGuideDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory evidence, got use-case/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
  assert.match(errors.join("\n"), /must include nonClaims metadata/);
});

test("validateChangeRiskLanguageGuideDist reports missing required sections and tables", async (t) => {
  const html = (await sourcePage()).replace('id="reduced-coverage-wording"', 'data-id="reduced-coverage-wording"').replace(
    'data-language-table="coverage-reduced"',
    'data-language-table="coverage-hidden"'
  );
  const root = await createManagedFixture(t, { pageHtml: html });
  const errors = [];

  await validateChangeRiskLanguageGuideDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required section: reduced-coverage-wording/);
  assert.match(errors.join("\n"), /missing required table: coverage-reduced/);
});

test("validateChangeRiskLanguageGuideDist requires machine-marked blocked phrases", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await sourcePage()).replace("<span data-blocked-phrase>Safe to release.</span>", "Safe to release.")
  });
  const errors = [];

  await validateChangeRiskLanguageGuideDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing blocked marked phrase: Safe to release/);
});

test("validateChangeRiskLanguageGuideDist rejects positive overclaims outside sanctioned boundary copy", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await sourcePage()).replace("</main>", "<p>TraceMap proves runtime behavior.</p></main>")
  });
  const errors = [];

  await validateChangeRiskLanguageGuideDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim: TraceMap proves runtime behavior/);
});

test("validateChangeRiskLanguageGuideDist rejects unmarked positive overclaims inside boundary sections", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await sourcePage()).replace(
      '<section class="section boundary-section" id="non-claims">',
      '<section class="section boundary-section" id="non-claims"><p>TraceMap proves runtime behavior.</p>'
    )
  });
  const errors = [];

  await validateChangeRiskLanguageGuideDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim: TraceMap proves runtime behavior/);
});

test("validateChangeRiskLanguageGuideDist rejects private or credential-like material", async (t) => {
  const privatePath = ["/", "Users", "/example/private"].join("");
  const root = await createManagedFixture(t, {
    pageHtml: (await sourcePage()).replace("</main>", `<p>${privatePath}</p></main>`)
  });
  const errors = [];

  await validateChangeRiskLanguageGuideDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /hard private or credential-like material: \/Users\//);
});

test("validateChangeRiskLanguageGuideDist requires adjacent links to be anchors", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await sourcePage())
      .replaceAll('href="/manager-faq/"', 'href="/manager-faq-missing/"')
      .replace("</head>", '<link rel="canonical" href="/manager-faq/"></head>')
  });
  const errors = [];

  await validateChangeRiskLanguageGuideDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required adjacent link: \/manager-faq\//);
});

test("validateChangeRiskLanguageGuideDist reports missing adjacent and inbound links", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await sourcePage()).replaceAll('href="/manager-faq/"', 'href="/manager-faq-missing/"'),
    includeInboundLinks: false
  });
  const errors = [];

  await validateChangeRiskLanguageGuideDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required adjacent link: \/manager-faq\//);
  assert.match(errors.join("\n"), /missing inbound links from live adjacent routes: \/review-claim-checklist\/, \/questions\/objections\/, \/manager-faq\//);
});

async function createManagedFixture(t, options = {}) {
  const root = await createFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createFixture({
  pageHtml,
  includeInboundLinks = true,
  discoveryRoutes = [changeRiskLanguageGuideRoute, ...changeRiskLanguageGuideRequiredLinks, "/proof-paths/"],
  sitemapRoutes = [changeRiskLanguageGuideRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-change-risk-language-test-"));
  const dist = join(root, "dist");
  const routes = new Set([changeRiskLanguageGuideRoute, ...changeRiskLanguageGuideRequiredLinks, ...changeRiskLanguageGuideInboundRoutes, "/proof-paths/"]);
  const html = pageHtml ?? (await sourcePage());

  for (const route of routes) {
    const routePath = route.replace(/^\/|\/$/g, "").replace(/#.*$/, "");
    const path = route === "/" ? dist : join(dist, routePath);
    await mkdir(path, { recursive: true });
    const body =
      route === changeRiskLanguageGuideRoute
        ? html
        : page(includeInboundLinks && changeRiskLanguageGuideInboundRoutes.includes(route) ? `<a href="${changeRiskLanguageGuideRoute}">Change-risk language</a>` : `<p>${route}</p>`);
    await writeFile(join(path, "index.html"), body, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function sourcePage() {
  return readFile(sourcePagePath, "utf8");
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === changeRiskLanguageGuideRoute ? "Change-Risk Language Guide" : `Route ${route}`,
    summary:
      route === changeRiskLanguageGuideRoute
        ? "Concept-level wording guide for choosing bounded public language around deterministic static change evidence, reduced coverage, owner handoffs, and stop conditions."
        : "Fixture route for change-risk language guide validation.",
    publicClaimLevel: route === changeRiskLanguageGuideRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === changeRiskLanguageGuideRoute ? "evidence" : "use-case",
    ...(route === changeRiskLanguageGuideRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations:
      route === changeRiskLanguageGuideRoute
        ? [
            "The guide teaches public-safe wording and cannot upgrade static evidence into stronger product, runtime, release, or safety conclusions.",
            "Evidence-bearing scanner facts, reducer findings, rule catalog entries, coverage labels, and documented limitations remain the source of support."
          ]
        : ["Fixture limitations remain bounded."],
    nonClaims:
      route === changeRiskLanguageGuideRoute
        ? [
            "No impact proof, absence-of-impact proof, release approval, release safety, operational safety, runtime proof, production traffic proof, endpoint performance proof, complete coverage, AI impact analysis, LLM analysis, autonomous approval, or replacement of human judgment.",
            "No raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values are public language-guide material."
          ]
        : ["No runtime behavior, production usage, deployment state, endpoint performance, or release approval proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteRouteEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) => (entry.path === changeRiskLanguageGuideRoute ? { ...entry, ...fields } : entry));
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${routes.map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`).join("\n")}
</urlset>`;
}

function page(body) {
  return `<!doctype html><html><head>
    <title>Fixture</title>
    <meta property="og:type" content="article">
  </head><body><main>${body}</main></body></html>`;
}
