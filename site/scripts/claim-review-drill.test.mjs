import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  claimReviewDrillInboundRoutes,
  claimReviewDrillRequiredLinks,
  claimReviewDrillRoute,
  validateClaimReviewDrillDist
} from "./claim-review-drill.mjs";

test("validateClaimReviewDrillDist accepts the canonical drill route", async (t) => {
  const root = await createManagedDrillFixture(t);
  const errors = [];

  await validateClaimReviewDrillDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateClaimReviewDrillDist reports route metadata regressions", async (t) => {
  const root = await createManagedDrillFixture(t);
  await rewriteDrillRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/proof-paths/"
  });
  const errors = [];

  await validateClaimReviewDrillDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/review-claim-checklist\/, got \/proof-paths\//);
});

test("validateClaimReviewDrillDist reports missing required drill rows", async (t) => {
  const root = await createManagedDrillFixture(t, {
    drillHtml: drillPage().replace(/<tr data-drill-row data-drill-scenario="missing-proof claim"[\s\S]*?<\/tr>/, "")
  });
  const errors = [];

  await validateClaimReviewDrillDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected 7 drill rows, got 6/);
  assert.match(errors.join("\n"), /missing required scenario: missing-proof claim/);
});

test("validateClaimReviewDrillDist reports missing row fields and evidence fields", async (t) => {
  const root = await createManagedDrillFixture(t, {
    drillHtml: drillPage()
      .replace('data-row-field="next action"', 'data-row-field="next step"')
      .replace('data-evidence-field="coverage label"', 'data-evidence-field="coverage"')
  });
  const errors = [];

  await validateClaimReviewDrillDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required row field: next action/);
  assert.match(errors.join("\n"), /evidence fields do not enumerate: coverage label/);
});

test("validateClaimReviewDrillDist rejects invalid level and scenario outcome mapping", async (t) => {
  const root = await createManagedDrillFixture(t, {
    drillHtml: drillPage()
      .replace('data-expected-claim-level="demo"', 'data-expected-claim-level="validated"')
      .replace('data-correct-outcome="repeat with proof"', 'data-correct-outcome="do not repeat"')
  });
  const errors = [];

  await validateClaimReviewDrillDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /invalid expected claim level: validated/);
  assert.match(errors.join("\n"), /supported demo-level claim" has disallowed outcome: do not repeat/);
});

test("validateClaimReviewDrillDist rejects answer-key outcome regressions", async (t) => {
  const root = await createManagedDrillFixture(t, {
    drillHtml: drillPage().replace('data-answer-outcome="downgrade before repeating"', 'data-answer-outcome="repeat with proof"')
  });
  const errors = [];

  await validateClaimReviewDrillDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /answer key scenario "concept-only claim" has disallowed outcome: repeat with proof/);
});

test("validateClaimReviewDrillDist rejects raw proof links and private text", async (t) => {
  const privatePath = ["/", "Users", "/example"].join("");
  const root = await createManagedDrillFixture(t, {
    drillHtml: drillPage().replace('href="/demo/evidence-trail/"', 'href="/facts.ndjson"').replace("Public claim level", `${privatePath} Public claim level`)
  });
  const errors = [];

  await validateClaimReviewDrillDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /links to forbidden raw proof artifact: \/facts\.ndjson/);
  assert.match(errors.join("\n"), /contains forbidden private text: \/Users\//);
});

test("validateClaimReviewDrillDist rejects raw proof references outside anchor hrefs", async (t) => {
  const root = await createManagedDrillFixture(t, {
    drillHtml: drillPage('<iframe src="/logs/analyzer.log"></iframe>')
  });
  const errors = [];

  await validateClaimReviewDrillDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /links to forbidden raw proof artifact: \/logs\/analyzer\.log/);
});

test("validateClaimReviewDrillDist reports missing adjacent and inbound links", async (t) => {
  const root = await createManagedDrillFixture(t, {
    drillHtml: drillPage().replace('href="/proof-paths/faq/"', 'data-href="/proof-paths/faq/"'),
    inboundRoutesWithLink: ["/review-claim-checklist/"]
  });
  const errors = [];

  await validateClaimReviewDrillDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required adjacent link: \/proof-paths\/faq\//);
  assert.match(errors.join("\n"), /missing inbound links from live adjacent routes: \/proof-paths\/tour\/, \/proof-paths\/faq\/, \/questions\/objections\/, \/packets\/examples\/, \/language\/change-risk\//);
});

test("validateClaimReviewDrillDist rejects forbidden positioning outside boundary regions", async (t) => {
  const root = await createManagedDrillFixture(t, {
    drillHtml: drillPage('<p>This page is an AI-powered automated grading system.</p>')
  });
  const errors = [];

  await validateClaimReviewDrillDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden AI, release, or production positioning/);
});

test("validateClaimReviewDrillDist rejects proof-claim wording with intervening words", async (t) => {
  const root = await createManagedDrillFixture(t, {
    drillHtml: drillPage('<p>TraceMap proves the sample endpoint stayed fast in production traffic.</p>')
  });
  const errors = [];

  await validateClaimReviewDrillDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /affirmative runtime, release, safety, or complete-coverage proof wording/);
});

test("validateClaimReviewDrillDist allows negated proof wording with intervening words", async (t) => {
  const root = await createManagedDrillFixture(t, {
    drillHtml: drillPage("<p>This drill cannot fully prove runtime behavior.</p>")
  });
  const errors = [];

  await validateClaimReviewDrillDist({ dist: join(root, "dist"), errors });

  assert.doesNotMatch(errors.join("\n"), /affirmative runtime, release, safety, or complete-coverage proof wording/);
});

async function createManagedDrillFixture(t, options = {}) {
  const root = await createDrillFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createDrillFixture({
  drillHtml = drillPage(),
  discoveryRoutes = [claimReviewDrillRoute],
  inboundRoutesWithLink = claimReviewDrillInboundRoutes,
  sitemapRoutes = [claimReviewDrillRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-claim-review-drill-test-"));
  const dist = join(root, "dist");
  const routes = new Set([claimReviewDrillRoute, ...claimReviewDrillRequiredLinks, ...claimReviewDrillInboundRoutes]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, "").replace(/#.*$/, ""));
    await mkdir(path, { recursive: true });
    const routeHtml =
      route === claimReviewDrillRoute
        ? drillHtml
        : page(inboundRoutesWithLink.includes(route) ? `<a href="${claimReviewDrillRoute}">Claim review drill</a>` : `<p>${route}</p>`);
    await writeFile(join(path, "index.html"), routeHtml, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === claimReviewDrillRoute ? "Claim Review Drill" : `Route ${route}`,
    summary: "Fixture route for claim review drill validation.",
    publicClaimLevel: route === claimReviewDrillRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === claimReviewDrillRoute ? "use-case" : "evidence",
    ...(route === claimReviewDrillRoute ? { preferredProofPath: "/review-claim-checklist/" } : {}),
    limitations: ["Fixture limitations remain bounded."],
    nonClaims: ["No runtime behavior, production usage, deployment state, endpoint performance, release approval, AI impact analysis, or LLM analysis proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteDrillRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === claimReviewDrillRoute
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

function drillPage(extra = "") {
  const scenarios = [
    ["supported demo-level claim", "demo", "repeat with proof", "A public demo route shows partial static evidence labels.", "/demo/evidence-trail/"],
    ["concept-only claim", "concept", "downgrade before repeating", "The checklist ships a claim-approval workflow.", "/review-claim-checklist/"],
    ["reduced-coverage claim", "demo", "owner follow-up needed", "The demo evidence covers every sample route.", "/limitations/reduced-coverage/"],
    ["unsafe runtime claim", "shipped", "do not repeat", "TraceMap proves the sample endpoint stayed fast in production traffic.", "/static-vs-runtime/"],
    ["unsafe release claim", "shipped", "do not repeat", "The static evidence checklist approves the release as operationally safe.", "/release-review-boundary/"],
    ["private-evidence-only claim", "hidden", "internal only", "An internal scan note supports the sample dependency claim.", "/review-claim-checklist/"],
    ["missing-proof claim", "concept", "do not repeat", "Reviewers remember seeing support for this public claim.", "/review-claim-checklist/"]
  ];
  const rows = scenarios
    .map(
      ([scenario, level, outcome, claim, href]) => `
        <tr data-drill-row data-drill-scenario="${scenario}" data-expected-claim-level="${level}" data-correct-outcome="${outcome}">
          <td data-row-field="claim text">${claim}</td>
          <td data-row-field="expected claim level"><code>${level}</code></td>
          <td data-row-field="proof path needed"><a href="${href}">Proof path</a></td>
          <td data-row-field="evidence fields to check">
            <span data-evidence-field="proof path">Proof path: public-safe route.</span>
            <span data-evidence-field="rule ID or rule family">Rule family: drill-fixture.</span>
            <span data-evidence-field="evidence tier">Evidence tier: <code>Tier2Structural</code>.</span>
            <span data-evidence-field="coverage label">Coverage label: <code>fixture</code>.</span>
            <span data-evidence-field="limitation">Limitation: fixture scope.</span>
            <span data-evidence-field="non-claim">Non-claim: no runtime or release proof.</span>
            <span data-evidence-field="source context">Source context: public page.</span>
            <span data-evidence-field="public/private status">Public/private status: public-safe.</span>
          </td>
          <td data-row-field="limitation or non-claim">Fixture limitation.</td>
          <td data-row-field="correct outcome"><code>${outcome}</code></td>
          <td data-row-field="next action">Fixture next action.</td>
        </tr>`
    )
    .join("\n");
  const answerRows = scenarios
    .map(
      ([scenario, , outcome]) =>
        `<tr data-answer-key-row data-answer-scenario="${scenario}" data-answer-outcome="${outcome}"><td>${scenario}</td><td><code>${outcome}</code></td><td>Next action.</td></tr>`
    )
    .join("\n");
  const filler = Array.from({ length: 80 }, (_, index) => `claim proof limitation boundary ${index}`).join(" ");

  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>This is a learning exercise, not an automated grader.</p>
    ${extra}
    ${claimReviewDrillRequiredLinks.map((link) => `<a href="${link}">${link}</a>`).join("\n")}
    <section id="drill-setup"><p>source context public/private status rule ID or rule family evidence tier coverage label limitation non-claim</p></section>
    <section id="sample-public-safe-claims"><table><tbody>${rows}</tbody></table></section>
    <section id="evidence-checklist"><p>proof path rule ID or rule family evidence tier coverage label limitation non-claim source context public/private status Tier1Semantic Tier2Structural Tier3SyntaxOrTextual Tier4Unknown</p></section>
    <section id="answer-key"><p>repeat with proof downgrade before repeating owner follow-up needed do not repeat internal only</p><table><tbody>${answerRows}</tbody></table></section>
    <section id="unsafe-answer-examples"><p>Rejected examples mention runtime behavior, production traffic, endpoint performance, release approval, release safety, operational safety, AI/LLM analysis, autonomous grading, and replacement of human review only as non-claims.</p></section>
    <section id="stop-conditions"><p>No proof path, no rule ID or rule family, private-only support, missing tier, missing coverage label, runtime wording, and release wording are stop conditions.</p></section>
    <section id="non-claims"><p>No raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values.</p><p>The drill does not prove runtime behavior, production traffic, endpoint performance, outage cause, release approval, release safety, operational safety, absence of impact, complete coverage, AI impact analysis, LLM analysis, or replacement of human review.</p></section>
    <p>${filler}</p>
  `);
}
