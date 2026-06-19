import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  demoRunbookInboundLinkRoutes,
  demoRunbookRequiredLinks,
  demoRunbookRoute,
  validateDemoRunbookDist
} from "./demo-runbook.mjs";

test("validateDemoRunbookDist accepts a complete public demo runbook route", async (t) => {
  const root = await createManagedDemoRunbookDistFixture(t);
  const errors = [];

  await validateDemoRunbookDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateDemoRunbookDist reports missing required label", async (t) => {
  const root = await createManagedDemoRunbookDistFixture(t, {
    pageHtml: demoRunbookPage().replace("Public claim level: demo", "Public claim level: concept")
  });
  const errors = [];

  await validateDemoRunbookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: demo/);
});

test("validateDemoRunbookDist reports missing required link", async (t) => {
  const root = await createManagedDemoRunbookDistFixture(t, {
    pageHtml: demoRunbookPage().replace('href="/validation/"', 'href="/docs/"')
  });
  const errors = [];

  await validateDemoRunbookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/validation\//);
});

test("validateDemoRunbookDist reports route metadata regressions", async (t) => {
  const root = await createManagedDemoRunbookDistFixture(t);
  await rewriteDemoRunbookRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "concept",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateDemoRunbookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel demo, got concept/);
  assert.match(errors.join("\n"), /expected hintCategory demo, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
});

test("validateDemoRunbookDist rejects artifact vocabulary outside sanctioned sections", async (t) => {
  const root = await createManagedDemoRunbookDistFixture(t, {
    pageHtml: demoRunbookPage("<p>facts.ndjson should not be here.</p>")
  });
  const errors = [];

  await validateDemoRunbookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /artifact-boundary vocabulary outside sanctioned sections: facts\.ndjson/);
});

test("validateDemoRunbookDist allows sanctioned warning vocabulary on non-section elements", async (t) => {
  const root = await createManagedDemoRunbookDistFixture(t, {
    pageHtml: demoRunbookPage(`
      <article data-runbook-section="red-flag">
        <p>AI-powered smart impact and complete product coverage are red flags, not positioning.</p>
      </article>
    `)
  });
  const errors = [];

  await validateDemoRunbookDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateDemoRunbookDist rejects forbidden private and raw values composed at runtime", async (t) => {
  const slash = String.fromCharCode(47);
  const backslash = String.fromCharCode(92);
  const homePath = `${slash}Users${slash}demo${slash}scan`;
  const windowsPath = `C:${backslash}Users${backslash}demo${backslash}scan`;
  const connection = ["Server", "=", "demo;", "User Id", "=", "demo;", "Password", "=", "secret;"].join("");
  const rawStatement = ["SELECT", "*", "FROM", "DemoTable"].join(" ");
  const root = await createManagedDemoRunbookDistFixture(t, {
    pageHtml: demoRunbookPage(`<p>${homePath}</p><p>${windowsPath}</p><p>${connection}</p><p>${rawStatement}</p>`)
  });
  const errors = [];

  await validateDemoRunbookDist({ dist: join(root, "dist"), errors });

  const text = errors.join("\n");
  assert.match(text, /home directory path/);
  assert.match(text, /Windows user directory path/);
  assert.match(text, /connection string Server fragment/);
  assert.match(text, /connection string Password fragment/);
  assert.match(text, /connection string User Id fragment/);
  assert.match(text, /raw SQL statement/);
});

test("validateDemoRunbookDist rejects unsupported overclaim positioning", async (t) => {
  const root = await createManagedDemoRunbookDistFixture(t, {
    pageHtml: demoRunbookPage("<p>This demo proves runtime behavior.</p><p>AI-powered impact shortcut.</p>")
  });
  const errors = [];

  await validateDemoRunbookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden operational overclaim/);
  assert.match(errors.join("\n"), /forbidden AI\/LLM positioning/);
});

test("validateDemoRunbookDist reports missing inbound route links", async (t) => {
  const root = await createManagedDemoRunbookDistFixture(t, {
    inboundHtmlByRoute: new Map([["/validation/", page("<p>Validation fixture without runbook link.</p>")]])
  });
  const errors = [];

  await validateDemoRunbookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /Required inbound route \/validation\/ does not link to \/demo\/runbook\//);
});

async function createManagedDemoRunbookDistFixture(t, options = {}) {
  const root = await createDemoRunbookDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createDemoRunbookDistFixture({
  discoveryRoutes = [demoRunbookRoute],
  inboundHtmlByRoute = new Map(),
  pageHtml = demoRunbookPage(),
  sitemapRoutes = [demoRunbookRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-demo-runbook-test-"));
  const dist = join(root, "dist");
  const routes = new Set([demoRunbookRoute, ...demoRunbookRequiredLinks, ...demoRunbookInboundLinkRoutes]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    const html =
      route === demoRunbookRoute
        ? pageHtml
        : inboundHtmlByRoute.get(route) ?? page(`<a href="${demoRunbookRoute}">Public demo runbook</a>`);
    await writeFile(join(path, "index.html"), html, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === demoRunbookRoute ? "Public Demo Runbook" : `Route ${route}`,
    summary:
      route === demoRunbookRoute
        ? "Operator checklist for running the public demo and keeping demo claims bounded."
        : "Fixture route for demo runbook validation.",
    publicClaimLevel: "demo",
    sourceType: "site-page",
    hintCategory: "demo",
    ...(route === demoRunbookRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: ["Fixture limitations remain bounded."],
    nonClaims: ["No runtime behavior, production usage, deployment state, endpoint performance, or release approval proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteDemoRunbookRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === demoRunbookRoute
      ? {
          ...entry,
          ...fields
        }
      : entry
  );
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${routes.map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`).join("\n")}
</urlset>`;
}

function page(body) {
  return `<!doctype html><html><head><meta property="og:type" content="article"></head><body><main>${body}</main></body></html>`;
}

function demoRunbookPage(extra = "") {
  const links = [
    ...demoRunbookRequiredLinks.map((route) => `<a href="${route}">${route}</a>`),
    '<a href="https://github.com/joefeser/tracemap/blob/main/scripts/demo-public.sh">scripts/demo-public.sh</a>'
  ].join("\n");

  return page(`
    <p>Public claim level: demo</p>
    <p>No public conclusion without evidence</p>
    <p>operator checklist</p>
    <h3>Follow the evidence</h3>
    <p>&lt;ignored-output-dir&gt; ./scripts/check-private-paths.sh public.demo.summary.v1</p>
    <p>Tier1Semantic Tier2Structural Tier3SyntaxOrTextual Tier4Unknown PartialAnalysis not_requested unavailable</p>
    <p>gap-labeled row: partial coverage, no clean reducer conclusion</p>
    ${links}
    <section data-runbook-section="artifact-boundary">
      <p>scan-manifest.json facts.ndjson index.sqlite report.md logs/analyzer.log analyzer.log raw SQL config values secrets generated scan directories private sample names raw source snippets raw repository remotes local absolute paths</p>
    </section>
    <section data-runbook-section="sharing-guidance">
      <p>Use static evidence and avoid unsupported impacted wording.</p>
    </section>
    <section data-runbook-section="red-flag">
      <p>AI impact analysis, LLM analysis, runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, and complete product coverage are red flags.</p>
    </section>
    ${extra}
  `);
}
