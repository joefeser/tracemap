import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import { managerFaqRequiredLinks, managerFaqRoute, validateManagerFaqDist } from "./manager-faq.mjs";

test("validateManagerFaqDist accepts the manager FAQ route", async (t) => {
  const root = await createManagedManagerFaqDistFixture(t);
  const errors = [];

  await validateManagerFaqDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateManagerFaqDist reports missing required text", async (t) => {
  const root = await createManagedManagerFaqDistFixture(t, {
    managerFaqHtml: page("<p>FAQ placeholder.</p>")
  });
  const errors = [];

  await validateManagerFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: concept/);
});

test("validateManagerFaqDist reports missing route metadata", async (t) => {
  const root = await createManagedManagerFaqDistFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateManagerFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/manager-faq\//);
});

test("validateManagerFaqDist reports route metadata regressions", async (t) => {
  const root = await createManagedManagerFaqDistFixture(t);
  await rewriteManagerFaqRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateManagerFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
});

test("validateManagerFaqDist rejects forbidden positioning", async (t) => {
  const root = await createManagedManagerFaqDistFixture(t, {
    managerFaqHtml: managerFaqPage("<p>AI-powered stakeholder FAQ.</p>")
  });
  const errors = [];

  await validateManagerFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden AI\/LLM positioning/);
});

test("validateManagerFaqDist rejects overclaims outside boundary copy", async (t) => {
  const root = await createManagedManagerFaqDistFixture(t, {
    managerFaqHtml: managerFaqPage("<p>This result is approved for release.</p>")
  });
  const errors = [];

  await validateManagerFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /overclaim wording outside sanctioned boundary copy/);
});

test("validateManagerFaqDist rejects affirmative runtime proof claims", async (t) => {
  const root = await createManagedManagerFaqDistFixture(t, {
    managerFaqHtml: managerFaqPage("<p>TraceMap proves runtime behavior and proves production traffic.</p>")
  });
  const errors = [];

  await validateManagerFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /affirmative runtime, production, or release proof wording/);
});

test("validateManagerFaqDist allows negated proof-boundary wording", async (t) => {
  const root = await createManagedManagerFaqDistFixture(t, {
    managerFaqHtml: managerFaqPage("<p>TraceMap cannot prove runtime behavior and does not prove production traffic.</p>")
  });
  const errors = [];

  await validateManagerFaqDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateManagerFaqDist allows overclaim examples inside boundary copy", async (t) => {
  const root = await createManagedManagerFaqDistFixture(t, {
    managerFaqHtml: managerFaqPage("", {
      boundaryText: "Do not say a finding is impacted, safe, unsafe, approved, blocked, root cause, validated for release, or production proven."
    })
  });
  const errors = [];

  await validateManagerFaqDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateManagerFaqDist rejects encoded private text", async (t) => {
  const root = await createManagedManagerFaqDistFixture(t, {
    managerFaqHtml: managerFaqPage("<p>file&#58;//private/report</p>")
  });
  const errors = [];

  await validateManagerFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /contains forbidden public text: file:\/\//);
});

test("validateManagerFaqDist reports word count outside bounds", async (t) => {
  const root = await createManagedManagerFaqDistFixture(t, {
    managerFaqHtml: managerFaqPage("", { fillerWords: 0 })
  });
  const errors = [];

  await validateManagerFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /word count must be between 500 and 1500 words/);
});

async function createManagedManagerFaqDistFixture(t, options = {}) {
  const root = await createManagerFaqDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createManagerFaqDistFixture({
  discoveryRoutes = [managerFaqRoute],
  managerFaqHtml = managerFaqPage(),
  sitemapRoutes = [managerFaqRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-manager-faq-test-"));
  const dist = join(root, "dist");
  const routes = new Set([managerFaqRoute, ...managerFaqRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(join(path, "index.html"), route === managerFaqRoute ? managerFaqHtml : page(route), "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === managerFaqRoute ? "Manager FAQ" : `Route ${route}`,
    summary: "Fixture route for manager FAQ validation.",
    publicClaimLevel: route === managerFaqRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === managerFaqRoute ? "use-case" : "evidence",
    ...(route === managerFaqRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: ["Fixture limitations remain bounded."],
    nonClaims: ["No runtime behavior, production usage, deployment state, endpoint performance, or release approval proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteManagerFaqRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === managerFaqRoute
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

function managerFaqPage(extra = "", { boundaryText = "Do not use TraceMap as proof of runtime behavior.", fillerWords = 100 } = {}) {
  const filler = Array.from({ length: fillerWords }, (_, index) => `manager faq evidence boundary ${index}`).join(" ");

  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <h3>What can TraceMap say from static evidence?</h3>
    <h3>What can it not prove by itself?</h3>
    <h3>Does TraceMap replace telemetry or tests?</h3>
    <h3>What do rule IDs mean for a manager?</h3>
    <h3>What are evidence tiers?</h3>
    <h3>What does partial or reduced coverage mean?</h3>
    <h3>How should managers use TraceMap in review?</h3>
    <h3>How should it support prioritization?</h3>
    <h3>How should it help incident follow-up?</h3>
    <h3>What should be escalated?</h3>
    <h3>Why no model-driven scanner claim?</h3>
    <h3>What is a proof path?</h3>
    <section class="boundary-section"><p>${boundaryText}</p></section>
    ${managerFaqRequiredLinks.map((route) => `<a href="${route}">${route}</a>`).join("\n")}
    <p>${filler}</p>
    ${extra}
  `);
}
