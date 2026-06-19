import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  reviewClaimChecklistInboundRoutes,
  reviewClaimChecklistRequiredLinks,
  reviewClaimChecklistRoute,
  validateReviewClaimChecklistDist
} from "./review-claim-checklist.mjs";

test("validateReviewClaimChecklistDist accepts the canonical checklist route", async (t) => {
  const root = await createManagedChecklistFixture(t);
  const errors = [];

  await validateReviewClaimChecklistDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateReviewClaimChecklistDist reports route metadata regressions", async (t) => {
  const root = await createManagedChecklistFixture(t);
  await rewriteChecklistRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateReviewClaimChecklistDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
});

test("validateReviewClaimChecklistDist reports missing checklist fields", async (t) => {
  const root = await createManagedChecklistFixture(t, {
    checklistHtml: checklistPage().replace('data-checklist-field="limitation"', 'data-checklist-field="limitations"')
  });
  const errors = [];

  await validateReviewClaimChecklistDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unexpected field: limitations/);
  assert.match(errors.join("\n"), /missing required field row: limitation/);
});

test("validateReviewClaimChecklistDist rejects invalid example outcomes", async (t) => {
  const root = await createManagedChecklistFixture(t, {
    checklistHtml: checklistPage().replace('data-review-outcome="owner follow-up needed"', 'data-review-outcome="needs review"')
  });
  const errors = [];

  await validateReviewClaimChecklistDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /invalid review outcome: needs review/);
});

test("validateReviewClaimChecklistDist rejects raw proof links", async (t) => {
  const root = await createManagedChecklistFixture(t, {
    checklistHtml: checklistPage().replace('href="/proof-paths/"', 'href="/facts.ndjson"')
  });
  const errors = [];

  await validateReviewClaimChecklistDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /links to forbidden raw proof artifact: \/facts\.ndjson/);
});

test("validateReviewClaimChecklistDist reports missing inbound links", async (t) => {
  const root = await createManagedChecklistFixture(t, {
    inboundRoutesWithLink: ["/review-room/"]
  });
  const errors = [];

  await validateReviewClaimChecklistDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing inbound links from live adjacent routes: \/manager-faq\/, \/proof-paths\/, \/roadmap\//);
});

test("validateReviewClaimChecklistDist rejects overclaims outside boundary copy", async (t) => {
  const root = await createManagedChecklistFixture(t, {
    checklistHtml: checklistPage("<p>This checklist says the release is safe.</p>")
  });
  const errors = [];

  await validateReviewClaimChecklistDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /overclaim wording outside sanctioned boundary copy/);
});

test("validateReviewClaimChecklistDist rejects encoded forbidden positioning in attributes", async (t) => {
  const root = await createManagedChecklistFixture(t, {
    checklistHtml: checklistPage('<span data-note="AI&#45;powered release&#45;safe"></span>')
  });
  const errors = [];

  await validateReviewClaimChecklistDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden AI, release, or production positioning/);
});

async function createManagedChecklistFixture(t, options = {}) {
  const root = await createChecklistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createChecklistFixture({
  checklistHtml = checklistPage(),
  discoveryRoutes = [reviewClaimChecklistRoute],
  inboundRoutesWithLink = reviewClaimChecklistInboundRoutes,
  sitemapRoutes = [reviewClaimChecklistRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-review-claim-checklist-test-"));
  const dist = join(root, "dist");
  const routes = new Set([reviewClaimChecklistRoute, ...reviewClaimChecklistRequiredLinks, ...reviewClaimChecklistInboundRoutes]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, "").replace(/#.*$/, ""));
    await mkdir(path, { recursive: true });
    const routeHtml =
      route === reviewClaimChecklistRoute
        ? checklistHtml
        : page(inboundRoutesWithLink.includes(route) ? `<a href="${reviewClaimChecklistRoute}">Review claim checklist</a>` : `<p>${route}</p>`);
    await writeFile(join(path, "index.html"), routeHtml, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === reviewClaimChecklistRoute ? "Review Claim Checklist" : `Route ${route}`,
    summary: "Fixture route for review claim checklist validation.",
    publicClaimLevel: route === reviewClaimChecklistRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === reviewClaimChecklistRoute ? "use-case" : "evidence",
    ...(route === reviewClaimChecklistRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: ["Fixture limitations remain bounded."],
    nonClaims: ["No runtime behavior, production usage, deployment state, endpoint performance, release approval, AI impact analysis, or LLM analysis proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteChecklistRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === reviewClaimChecklistRoute
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

function checklistPage(extra = "") {
  const fieldRows = [
    "claim statement",
    "public claim level",
    "proof path",
    "rule ID or rule family",
    "evidence tier",
    "coverage label",
    "limitation",
    "non-claims",
    "source branch or main-dev status",
    "owner follow-up",
    "reviewer",
    "review date",
    "decision"
  ]
    .map((field) => `<tr data-checklist-field="${field}"><td>${field}</td></tr>`)
    .join("\n");
  const filler = Array.from({ length: 140 }, (_, index) => `claim proof limitation boundary ${index}`).join(" ");

  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    ${extra}
    <a href="/review-room/">Review-room agenda</a>
    <a href="/manager-faq/">Manager FAQ</a>
    <a href="/proof-paths/">Proof path index</a>
    <a href="/roadmap/#claim-ledger">Claim ledger</a>
    <section id="claim-row-template">
      <table><tbody>${fieldRows}</tbody></table>
      <p>shipped demo concept hidden main maps to shipped</p>
      <p>Tier1Semantic Tier2Structural Tier3SyntaxOrTextual Tier4Unknown</p>
      <p>repeat with proof downgrade before repeating owner follow-up needed do not repeat internal only</p>
      <p>claim statement public claim level proof path rule ID or rule family evidence tier coverage label limitation non-claims source branch or main-dev status owner follow-up reviewer review date decision</p>
    </section>
    <section id="stop-conditions">
      <p>missing proof path private-only artifact hidden claim detail unsupported demo claim forbidden wording</p>
    </section>
    <section id="illustrative-examples">
      <h2>Illustrative examples</h2>
      <table>
        <tbody>
          <tr data-example-row data-review-outcome="repeat with proof" data-public-claim-level="demo"><td>Tier2Structural Reviewer role Example date repeat with proof</td></tr>
          <tr data-example-row data-review-outcome="owner follow-up needed" data-public-claim-level="concept"><td>Tier2Structural Reviewer role Example date owner follow-up needed</td></tr>
        </tbody>
      </table>
    </section>
    <section id="non-claims">
      <p>TraceMap does not prove runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, AI impact analysis, LLM analysis, or complete product coverage.</p>
      <p>TraceMap does not replace telemetry, logs, traces, tests, source review, ownership decisions, incident response, or release approval.</p>
      <p>A successful checklist does not say a system is impacted, safe, unsafe, approved, blocked, root cause, validated for release, production proven, or complete.</p>
    </section>
    <section id="private-material">
      <p>Raw facts.ndjson, raw index.sqlite, analyzer logs, raw source snippets, raw SQL, config values, secrets, local absolute paths, raw repository remotes, generated scan directories, and private sample names stay out of public proof links.</p>
    </section>
    <p>${filler}</p>
  `);
}
