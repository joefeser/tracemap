import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  siteClaimGuardrailsRequiredLinks,
  siteClaimGuardrailsRoute,
  validateSiteClaimGuardrailsDist
} from "./site-claim-guardrails.mjs";

const scriptsDir = dirname(fileURLToPath(import.meta.url));
const sourcePagePath = resolve(scriptsDir, "..", "src", "site-claim-guardrails", "index.html");

test("validateSiteClaimGuardrailsDist accepts the canonical guardrails route", async (t) => {
  const root = await createManagedFixture(t);
  const errors = [];

  await validateSiteClaimGuardrailsDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateSiteClaimGuardrailsDist reports route metadata regressions", async (t) => {
  const root = await createManagedFixture(t);
  await rewriteRouteEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "use-case",
    sourceType: "repo-doc",
    preferredProofPath: "/proof-source-catalog/",
    limitations: ["Only one limitation."],
    nonClaims: ["No runtime proof."]
  });
  const errors = [];

  await validateSiteClaimGuardrailsDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory evidence, got use-case/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/review-claim-checklist\/, got \/proof-source-catalog\//);
  assert.match(errors.join("\n"), /must include at least two limitations/);
  assert.match(errors.join("\n"), /must include nonClaims metadata/);
});

test("validateSiteClaimGuardrailsDist reports missing required sections and rows", async (t) => {
  const html = (await sourcePage())
    .replace('id="proof-path-requirements"', 'data-id="proof-path-requirements"')
    .replace('data-claim-guardrail-row="demo"', 'data-claim-guardrail-row="demo-hidden"');
  const root = await createManagedFixture(t, { pageHtml: html });
  const errors = [];

  await validateSiteClaimGuardrailsDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required section: proof-path-requirements/);
  assert.match(errors.join("\n"), /unexpected id: demo-hidden/);
  assert.match(errors.join("\n"), /missing required row: demo/);
});

test("validateSiteClaimGuardrailsDist reports missing row fields and invalid handoff states", async (t) => {
  const html = (await sourcePage())
    .replace('data-field="required proof path">Public demo proof', 'data-field-hidden="required proof path">Public demo proof')
    .replace('data-field="review handoff">downgrade before repeating', 'data-field="review handoff">publish now');
  const root = await createManagedFixture(t, { pageHtml: html });
  const errors = [];

  await validateSiteClaimGuardrailsDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /row demo is missing required field: required proof path/);
  assert.match(errors.join("\n"), /row demo has invalid review handoff: publish now/);
});

test("validateSiteClaimGuardrailsDist requires adjacent links and route metadata", async (t) => {
  const html = (await sourcePage()).replaceAll('href="/roadmap/"', 'href="/roadmap-missing/"');
  const root = await createManagedFixture(t, {
    pageHtml: html,
    discoveryRoutes: siteClaimGuardrailsRequiredLinks.filter((route) => route !== "/roadmap/")
  });
  const errors = [];

  await validateSiteClaimGuardrailsDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /adjacent route is absent from discovery output: \/roadmap\//);
});

test("validateSiteClaimGuardrailsDist rejects unmarked positive overclaims", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await sourcePage()).replace("</main>", "<p>TraceMap proves runtime behavior.</p></main>")
  });
  const errors = [];

  await validateSiteClaimGuardrailsDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim outside marked boundary copy: TraceMap proves runtime behavior/);
});

test("validateSiteClaimGuardrailsDist rejects private or credential-like material", async (t) => {
  const hardLeak = ["/", "Users", "/private"].join("");
  const root = await createManagedFixture(t, {
    pageHtml: (await sourcePage()).replace("</main>", `<p>${hardLeak}</p></main>`)
  });
  const errors = [];

  await validateSiteClaimGuardrailsDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /hard private or credential-like material/);
});

test("validateSiteClaimGuardrailsDist rejects private material in route metadata", async (t) => {
  const root = await createManagedFixture(t);
  await rewriteRouteEntry(join(root, "dist"), {
    limitations: [["/", "home", "/private-proof"].join("")]
  });
  const errors = [];

  await validateSiteClaimGuardrailsDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /hard private or credential-like material/);
  assert.match(errors.join("\n"), /routes-index\.json/);
});

test("validateSiteClaimGuardrailsDist keeps the route out of primary navigation", async (t) => {
  const root = await createManagedFixture(t, {
    pageHtml: (await sourcePage()).replace(
      '<a href="/demo/">Demo</a>',
      `<a href="/demo/">Demo</a><a href="${siteClaimGuardrailsRoute}">Guardrails</a>`
    )
  });
  const errors = [];

  await validateSiteClaimGuardrailsDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /must not be added to primary navigation/);
});

async function createManagedFixture(t, options = {}) {
  const root = await createFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createFixture({
  pageHtml,
  discoveryRoutes = siteClaimGuardrailsRequiredLinks,
  sitemapRoutes = [siteClaimGuardrailsRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-site-claim-guardrails-test-"));
  const dist = join(root, "dist");
  const routes = new Set([siteClaimGuardrailsRoute, ...siteClaimGuardrailsRequiredLinks]);

  for (const route of routes) {
    const routePath = route.replace(/^\/|\/$/g, "").replace(/#.*$/, "");
    await mkdir(join(dist, routePath), { recursive: true });
    const body = route === siteClaimGuardrailsRoute ? pageHtml ?? (await sourcePage()) : page(`<p>${route}</p>`);
    await writeFile(join(dist, routePath, "index.html"), body, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, [siteClaimGuardrailsRoute, ...discoveryRoutes]);

  return root;
}

async function sourcePage() {
  return readFile(sourcePagePath, "utf8");
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === siteClaimGuardrailsRoute ? "Site Claim Guardrails" : `Route ${route}`,
    summary:
      route === siteClaimGuardrailsRoute
        ? "Concept-level TraceMap site claim guardrails for public wording, proof paths, limitations, downgrade rules, hidden states, and review handoff."
        : "Fixture route for site claim guardrails validation.",
    publicClaimLevel: route === siteClaimGuardrailsRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === siteClaimGuardrailsRoute ? "evidence" : "use-case",
    ...(route === siteClaimGuardrailsRoute ? { preferredProofPath: "/review-claim-checklist/" } : {}),
    limitations:
      route === siteClaimGuardrailsRoute
        ? [
            "The route is copy-governance guidance, not scanner output, reducer output, validation success, or a new proof source.",
            "Claims still need their own public-safe proof path, rule basis, evidence tier when applicable, coverage label, limitation, and source context."
          ]
        : ["Fixture limitations remain bounded."],
    nonClaims:
      route === siteClaimGuardrailsRoute
        ? [
            "No runtime proof, production traffic proof, endpoint performance proof, outage-cause proof, release approval, release safety, operational safety, complete coverage, AI impact analysis, LLM analysis, autonomous approval, or replacement of human review.",
            "No raw facts, raw SQLite content, analyzer logs, source snippets, raw SQL, config values, secrets, local paths, remotes, generated scan directories, private sample names, command output, hidden validation details, or credential-like values are public guardrails material."
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
  parsed.entries = parsed.entries.map((entry) => (entry.path === siteClaimGuardrailsRoute ? { ...entry, ...fields } : entry));
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${routes.map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`).join("\n")}
</urlset>`;
}

function page(body) {
  return `<!doctype html><html><body>${body}</body></html>`;
}
