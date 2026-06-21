import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  managerDemoScriptInboundLinkRoutes,
  managerDemoScriptRequiredLinks,
  managerDemoScriptRoute,
  validateManagerDemoScriptDist
} from "./manager-demo-script.mjs";

test("validateManagerDemoScriptDist accepts a complete manager demo script route", async (t) => {
  const root = await createManagedManagerDemoScriptDistFixture(t);
  const errors = [];

  await validateManagerDemoScriptDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateManagerDemoScriptDist reports missing required text", async (t) => {
  const root = await createManagedManagerDemoScriptDistFixture(t, {
    pageHtml: page("<p>Script placeholder.</p>")
  });
  const errors = [];

  await validateManagerDemoScriptDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: concept/);
});

test("validateManagerDemoScriptDist reports missing required link", async (t) => {
  const root = await createManagedManagerDemoScriptDistFixture(t, {
    pageHtml: managerDemoScriptPage().replace('href="/static-vs-runtime/"', 'href="/docs/"')
  });
  const errors = [];

  await validateManagerDemoScriptDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/static-vs-runtime\//);
});

test("validateManagerDemoScriptDist reports route metadata regressions", async (t) => {
  const root = await createManagedManagerDemoScriptDistFixture(t);
  await rewriteManagerDemoScriptRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateManagerDemoScriptDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory demo, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
});

test("validateManagerDemoScriptDist reports target route claim-level regressions", async (t) => {
  const root = await createManagedManagerDemoScriptDistFixture(t);
  await rewriteManagerDemoScriptRoutesIndexEntry(join(root, "dist"), { publicClaimLevel: "demo" }, "/questions/");
  const errors = [];

  await validateManagerDemoScriptDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected target \/questions\/ publicClaimLevel concept, got demo/);
});

test("validateManagerDemoScriptDist reports missing inbound discovery link", async (t) => {
  const root = await createManagedManagerDemoScriptDistFixture(t, {
    inboundHtmlByRoute: new Map([["/demo/", page("<p>Demo hub without manager script link.</p>")]])
  });
  const errors = [];

  await validateManagerDemoScriptDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /Required inbound route \/demo\/ does not link to \/demo\/manager-script\//);
});

test("validateManagerDemoScriptDist rejects unsupported positioning outside non-claims", async (t) => {
  const root = await createManagedManagerDemoScriptDistFixture(t, {
    pageHtml: managerDemoScriptPage("<p>TraceMap proves runtime behavior.</p><p>AI-powered impact shortcut.</p>")
  });
  const errors = [];

  await validateManagerDemoScriptDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden proof claim/);
  assert.match(errors.join("\n"), /forbidden AI\/LLM positioning/);
});

test("validateManagerDemoScriptDist rejects forbidden positioning in metadata", async (t) => {
  const root = await createManagedManagerDemoScriptDistFixture(t, {
    pageHtml: managerDemoScriptPage().replace(
      'content="Fixture description"',
      'content="AI-powered manager demo shortcut"'
    )
  });
  const errors = [];

  await validateManagerDemoScriptDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden AI\/LLM positioning/);
});

test("validateManagerDemoScriptDist allows non-claim warning vocabulary", async (t) => {
  const root = await createManagedManagerDemoScriptDistFixture(t, {
    pageHtml: managerDemoScriptPage(`
      <section data-manager-script-section="non-claims">
        <p>Do not claim AI-powered analysis, release safety, production proven status, or root cause.</p>
      </section>
    `)
  });
  const errors = [];

  await validateManagerDemoScriptDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateManagerDemoScriptDist rejects private and raw values composed at runtime", async (t) => {
  const slash = String.fromCharCode(47);
  const backslash = String.fromCharCode(92);
  const homePath = `${slash}Users${slash}demo${slash}scan`;
  const windowsPath = `C:${backslash}Users${backslash}demo${backslash}scan`;
  const connection = ["Server", "=", "demo;", "User Id", "=", "demo;", "Password", "=", "secret;"].join("");
  const rawStatement = ["SELECT", "*", "FROM", "DemoTable"].join(" ");
  const root = await createManagedManagerDemoScriptDistFixture(t, {
    pageHtml: managerDemoScriptPage(`<p>${homePath}</p><p>${windowsPath}</p><p>${connection}</p><p>${rawStatement}</p>`)
  });
  const errors = [];

  await validateManagerDemoScriptDist({ dist: join(root, "dist"), errors });

  const text = errors.join("\n");
  assert.match(text, /home directory path/);
  assert.match(text, /Windows user directory path/);
  assert.match(text, /connection string Server fragment/);
  assert.match(text, /connection string Password fragment/);
  assert.match(text, /connection string User Id fragment/);
  assert.match(text, /raw SQL statement/);
});

test("validateManagerDemoScriptDist reports word count outside bounds", async (t) => {
  const root = await createManagedManagerDemoScriptDistFixture(t, {
    pageHtml: managerDemoScriptPage("", { fillerWords: 0 })
  });
  const errors = [];

  await validateManagerDemoScriptDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /word count must be between 900 and 2400 words/);
});

async function createManagedManagerDemoScriptDistFixture(t, options = {}) {
  const root = await createManagerDemoScriptDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createManagerDemoScriptDistFixture({
  discoveryRoutes = [managerDemoScriptRoute],
  inboundHtmlByRoute = new Map(),
  pageHtml = managerDemoScriptPage(),
  sitemapRoutes = [managerDemoScriptRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-manager-demo-script-test-"));
  const dist = join(root, "dist");
  const routes = new Set([
    managerDemoScriptRoute,
    ...managerDemoScriptRequiredLinks,
    ...managerDemoScriptInboundLinkRoutes
  ]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    const html =
      route === managerDemoScriptRoute
        ? pageHtml
        : inboundHtmlByRoute.get(route) ?? page(`<a href="${managerDemoScriptRoute}">Manager demo script</a>`);
    await writeFile(join(path, "index.html"), html, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const allRoutes = new Set([...routes, ...managerDemoScriptRequiredLinks]);
  const entries = [...allRoutes].map((route) => ({
    path: route,
    title: route === managerDemoScriptRoute ? "Manager Demo Script" : `Route ${route}`,
    summary:
      route === managerDemoScriptRoute
        ? "Concept-level presenter script for showing static evidence routes."
        : "Fixture route for manager demo script validation.",
    publicClaimLevel: expectedClaimLevel(route),
    sourceType: "site-page",
    hintCategory: route === managerDemoScriptRoute ? "demo" : "evidence",
    ...(route === managerDemoScriptRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: ["Fixture limitations remain bounded."],
    nonClaims: ["No runtime behavior, production usage, deployment state, endpoint performance, or release approval proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteManagerDemoScriptRoutesIndexEntry(dist, fields, route = managerDemoScriptRoute) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === route
      ? {
          ...entry,
          ...fields
        }
      : entry
  );
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

function expectedClaimLevel(route) {
  return route === managerDemoScriptRoute || route === "/questions/" || route === "/static-vs-runtime/"
    ? "concept"
    : "demo";
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${routes.map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`).join("\n")}
</urlset>`;
}

function page(body) {
  return `<!doctype html><html><head><meta property="og:type" content="article"><meta property="og:title" content="Fixture"><meta property="og:description" content="Fixture description"></head><body><main>${body}</main></body></html>`;
}

function managerDemoScriptPage(extra = "", { fillerWords = 170 } = {}) {
  const filler = Array.from({ length: fillerWords }, (_, index) => `manager demo script evidence boundary ${index}`).join(" ");
  const links = managerDemoScriptRequiredLinks.map((route) => `<a href="${route}">${route}</a>`).join("\n");
  const families = [
    "value",
    "trust",
    "completeness",
    "release-decision",
    "production-behavior",
    "incident-use",
    "team-handoff",
    "next"
  ]
    .map((family) => `<article data-question-family="${family}"><p>${family}</p></article>`)
    .join("\n");

  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>bounded demo script, not a product capability proof</p>
    <h2>Opening context</h2>
    <h2>2-minute tour</h2>
    <h2>5-minute proof walkthrough</h2>
    <h2>Manager questions and safe answer shapes</h2>
    <h2>Engineer questions and proof routes</h2>
    <h2>Stop conditions</h2>
    <h2>Follow-up handoff</h2>
    <h2>Non-claims</h2>
    <p>rule ID or rule family, evidence tier, coverage label, proof path, limitation, raw facts, SQLite content, analyzer logs.</p>
    <p>Where are the rule IDs and evidence tiers?</p>
    <p>How does source mapping stay public-safe?</p>
    <p>What does the demo result status mean?</p>
    <p>Where do validation and static-versus-runtime boundaries live?</p>
    <p>What stays out of public copy?</p>
    ${links}
    ${families}
    <section data-manager-script-section="non-claims">
      <p>Do not claim runtime proof, release approval, operational safety, complete coverage, AI analysis, or root cause.</p>
    </section>
    <p>${filler}</p>
    ${extra}
  `);
}
