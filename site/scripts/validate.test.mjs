import assert from "node:assert/strict";
import { mkdir, mkdtemp, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  adoptionPartialAnalysisSentence,
  adoptionPlaybookRoute
} from "./adoption-playbook.mjs";
import { createDiscoveryOutputs } from "./discovery.mjs";
import { demoEvidenceTrailRoute } from "./demo-evidence-trail.mjs";
import { deployAuditRequiredRoutes } from "./deploy-audit.mjs";
import { incidentCallRoute } from "./incident-call.mjs";
import { managerBriefRoute } from "./manager-brief.mjs";
import { managerFaqRoute } from "./manager-faq.mjs";
import { reviewRoomRoute } from "./review-room.mjs";
import { roadmapClaimLedgerRoute } from "./roadmap-claim-ledger.mjs";
import { staticTriageRoute } from "./static-triage.mjs";
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
  sitemapUrls = [
    ...deployAuditRequiredRoutes,
    adoptionPlaybookRoute,
    demoEvidenceTrailRoute,
    incidentCallRoute,
    managerBriefRoute,
    managerFaqRoute,
    reviewRoomRoute,
    roadmapClaimLedgerRoute,
    staticTriageRoute
  ].map((route) => `https://tracemap.tools${route}`)
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-site-validate-test-"));
  const dist = join(root, "dist");

  const fixtureRoutes = new Set([
    ...deployAuditRequiredRoutes,
    adoptionPlaybookRoute,
    "/blog/",
    "/capabilities/",
    "/demo/start-here/",
    "/demo/proof-upgrades/",
    "/demo/proof-assets/",
    demoEvidenceTrailRoute,
    "/evidence/",
    "/examples/",
    incidentCallRoute,
    managerBriefRoute,
    managerFaqRoute,
    "/manager-packet/",
    "/packets/",
    reviewRoomRoute,
    roadmapClaimLedgerRoute,
    staticTriageRoute,
    "/outputs/",
    "/use-cases/incident-review/",
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
      route === "/deploy-audit/"
        ? deployAuditPage()
        : route === adoptionPlaybookRoute
          ? adoptionPage()
          : route === demoEvidenceTrailRoute
            ? demoEvidenceTrailPage()
            : route === incidentCallRoute
              ? incidentCallPage()
              : route === managerBriefRoute
                ? managerBriefPage()
                : route === managerFaqRoute
                  ? managerFaqPage()
                  : route === reviewRoomRoute
                    ? reviewRoomPage()
                    : route === roadmapClaimLedgerRoute
                      ? roadmapClaimLedgerPage()
                      : route === staticTriageRoute
                        ? staticTriagePage()
                        : page(`<p>${path}</p>`),
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
        path: adoptionPlaybookRoute,
        title: "Adoption Playbook",
        summary: "Fixture adoption playbook route for validation.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture adoption limitations remain bounded."],
        nonClaims: ["No runtime behavior or production usage proof."]
      },
      {
        path: demoEvidenceTrailRoute,
        title: "Demo Evidence Trail",
        summary: "Fixture demo evidence trail route for validation.",
        publicClaimLevel: "demo",
        sourceType: "site-page",
        hintCategory: "demo",
        preferredProofPath: "/demo/proof-upgrades/",
        limitations: ["Fixture demo evidence trail limitations remain bounded."],
        nonClaims: ["No runtime behavior or production usage proof."]
      },
      {
        path: incidentCallRoute,
        title: "Incident Call",
        summary: "Fixture incident call route for validation.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture incident call limitations remain bounded."],
        nonClaims: ["No runtime behavior or production usage proof."]
      },
      {
        path: managerBriefRoute,
        title: "Manager Brief",
        summary: "Fixture manager brief route for validation.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture manager brief limitations remain bounded."],
        nonClaims: ["No runtime behavior or production usage proof."]
      },
      {
        path: managerFaqRoute,
        title: "Manager FAQ",
        summary: "Fixture manager FAQ route for validation.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture manager FAQ limitations remain bounded."],
        nonClaims: ["No runtime behavior or production usage proof."]
      },
      {
        path: reviewRoomRoute,
        title: "Review Room",
        summary: "Fixture review room route for validation.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture review room limitations remain bounded."],
        nonClaims: ["No runtime behavior or production usage proof."]
      },
      {
        path: roadmapClaimLedgerRoute,
        title: "Roadmap Claim Ledger",
        summary: "Fixture concept-level ledger for public claim wording and proof paths.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "roadmap",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture roadmap ledger limitations remain bounded."],
        nonClaims: ["No runtime behavior or production usage proof."]
      },
      {
        path: staticTriageRoute,
        title: "Static Triage",
        summary: "Fixture static triage route for validation.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture static triage limitations remain bounded."],
        nonClaims: ["No runtime behavior or production usage proof."]
      },
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

function incidentCallPage() {
  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>static dependency evidence and not runtime observability</p>
    <p>not operational approval</p>
    <p>P1-call orientation and incident review are related, not identical</p>
    <p>static triage checklist</p>
    <a href="/proof-paths/">Proof paths</a>
    <a href="/validation/">Validation</a>
    <a href="/docs/">Docs</a>
    <a href="/limitations/">Limitations</a>
    <a href="/demo/result/">Demo result</a>
    <a href="/use-cases/incident-review/">Incident review orientation</a>
    <a href="/static-triage/">static triage checklist</a>
  `);
}

function adoptionPage() {
  const filler = Array.from({ length: 95 }, (_, index) => `adoption evidence workflow boundary ${index}`).join(" ");
  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>not a product promise or replacement for engineering judgment</p>
    <p>start with the public demo</p>
    <p>repository owners runtime owners test owners documentation owners future extractor work</p>
    <p>${adoptionPartialAnalysisSentence}</p>
    <p>The playbook is not runtime proof or release approval</p>
    <meta property="og:type" content="article">
    <a href="/demo/">Public demo</a>
    <a href="/demo/result/">Demo result</a>
    <a href="/docs/">Docs</a>
    <a href="/validation/">Validation</a>
    <a href="/limitations/">Limitations</a>
    <a href="/proof-paths/">Proof paths</a>
    <a href="/review-room/">Review room</a>
    <a href="/static-triage/">Static triage</a>
    <p>${filler}</p>
  `);
}

function demoEvidenceTrailPage() {
  const filler = Array.from({ length: 80 }, (_, index) => `demo evidence trail boundary ${index}`).join(" ");
  return page(`
    <p>Public claim level: demo</p>
    <p>No public conclusion without evidence</p>
    <p>What static evidence connects a changed demo surface to a route and downstream surfaces?</p>
    <p>This is the same evidence packet made easier to follow, not stronger.</p>
    <p>site/src/_data/demo-public-summary.json public.demo.summary.v1 Tier2Structural Tier4Unknown PartialAnalysis</p>
    <p>12 changed demo surfaces 14 endpoint findings 12 paths 25 reverse paths 37 path gaps</p>
    <article data-trail-surface-type="package" data-trail-gap="package"><h3>Package evidence</h3><p>missing-public-item</p></article>
    <article data-trail-surface-type="config" data-trail-gap="config"><h3>Configuration evidence</h3><p>missing-public-item</p></article>
    <article data-trail-surface-type="sql-facing" data-trail-gap="sql-facing"><h3>SQL-facing evidence</h3><p>missing-public-item</p></article>
    <p>runtime proof production proof release approval complete product coverage</p>
    <meta property="og:type" content="article">
    <a href="/demo/result/">Demo result</a>
    <a href="/demo/proof-upgrades/">Demo proof upgrades</a>
    <a href="/demo/proof-assets/">Demo proof assets</a>
    <a href="/proof-paths/">Proof paths</a>
    <a href="/evidence/">Evidence</a>
    <a href="/validation/">Validation</a>
    <a href="/limitations/">Limitations</a>
    <a href="/packets/">Packets</a>
    <p>${filler}</p>
  `);
}

function managerBriefPage() {
  const filler = Array.from({ length: 90 }, (_, index) => `evidence packet review boundary ${index}`).join(" ");
  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>Manual dependency indexing is expensive</p>
    <p>deterministic artifacts</p>
    <p>Static evidence is useful because its limits stay visible</p>
    <a href="/proof-paths/">Proof paths</a>
    <a href="/validation/">Validation</a>
    <a href="/limitations/">Limitations</a>
    <a href="/demo/">Demo</a>
    <a href="/docs/">Docs</a>
    <p>${filler}</p>
  `);
}

function managerFaqPage() {
  const filler = Array.from({ length: 100 }, (_, index) => `manager faq evidence boundary ${index}`).join(" ");
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
    <meta property="og:type" content="article">
    <a href="/manager-brief/">Manager brief</a>
    <a href="/manager-packet/">Manager packet</a>
    <a href="/review-room/">Review room</a>
    <a href="/limitations/">Limitations</a>
    <a href="/validation/">Validation</a>
    <a href="/proof-paths/">Proof paths</a>
    <p>${filler}</p>
  `);
}

function reviewRoomPage() {
  const filler = Array.from({ length: 100 }, (_, index) => `evidence review agenda boundary ${index}`).join(" ");
  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>claim proof path rule ID/evidence tier coverage label limitation owner decision gap</p>
    <p>Known evidence is reducer-backed and public-safe; partial evidence is reduced-coverage and labeled; missing evidence is an explicit gap for human review.</p>
    <meta property="og:type" content="article">
    <a href="/proof-paths/">Proof paths</a>
    <a href="/evidence/">Evidence model</a>
    <a href="/validation/">Validation</a>
    <a href="/limitations/">Limitations</a>
    <a href="/manager-brief/">Manager brief</a>
    <a href="/manager-packet/">Manager packet</a>
    <a href="/incident-call/">Incident call</a>
    <a href="/use-cases/incident-review/">Incident review</a>
    <p>${filler}</p>
  `);
}

function roadmapClaimLedgerPage() {
  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <h2>Claim ledger</h2>
    <p>Public wording status</p>
    <p>Source-of-truth artifact family</p>
    <p>SQLite indexes, fact streams, reports, rule catalog entries, commit metadata, coverage labels, and documented limitations</p>
    <p>presentation and governance layer</p>
    <p>runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, AI impact analysis, LLM analysis, and complete product coverage wording is forbidden</p>
    <a href="/proof-paths/">Proof paths</a>
    <a href="/capabilities/">Capabilities</a>
    <a href="/demo/proof-upgrades/">Demo proof upgrades</a>
    <a href="/limitations/">Limitations</a>
    <table>
      <tbody>
        <tr id="claim-shipped" data-claim-row data-claim-level="shipped" data-evidence-status="evidence-backed" data-wording-status="live"><td>shipped</td></tr>
        <tr id="claim-demo-partial" data-claim-row data-claim-level="demo" data-evidence-status="partial/reduced coverage" data-wording-status="demo-only"><td>demo</td></tr>
        <tr id="claim-demo-gap" data-claim-row data-claim-level="demo" data-evidence-status="gap-labeled demo evidence" data-wording-status="demo-only"><td>gap</td></tr>
        <tr id="claim-concept-future" data-claim-row data-claim-level="concept" data-evidence-status="future-only" data-wording-status="future-facing"><td>concept</td></tr>
        <tr id="claim-hidden-internal" data-claim-row data-claim-level="hidden" data-evidence-status="hidden/internal" data-wording-status="hidden-from-public-navigation"><td>hidden</td></tr>
        <tr id="claim-forbidden" data-claim-row data-claim-level="hidden" data-evidence-status="not-yet-backed" data-wording-status="forbidden"><td>forbidden</td></tr>
      </tbody>
    </table>
    <table>
      <tbody>
        <tr data-mapping-row data-mapping-axis="claim-level" data-ledger-label="shipped"><td>shipped</td></tr>
        <tr data-mapping-row data-mapping-axis="claim-level" data-ledger-label="demo"><td>demo</td></tr>
        <tr data-mapping-row data-mapping-axis="claim-level" data-ledger-label="concept"><td>concept</td></tr>
        <tr data-mapping-row data-mapping-axis="claim-level" data-ledger-label="hidden"><td>hidden</td></tr>
        <tr data-mapping-row data-mapping-axis="evidence-status" data-ledger-label="evidence-backed"><td>evidence-backed</td></tr>
        <tr data-mapping-row data-mapping-axis="evidence-status" data-ledger-label="partial/reduced coverage"><td>partial</td></tr>
        <tr data-mapping-row data-mapping-axis="evidence-status" data-ledger-label="gap-labeled demo evidence"><td>gap</td></tr>
        <tr data-mapping-row data-mapping-axis="evidence-status" data-ledger-label="future-only"><td>future</td></tr>
        <tr data-mapping-row data-mapping-axis="evidence-status" data-ledger-label="hidden/internal"><td>hidden/internal</td></tr>
        <tr data-mapping-row data-mapping-axis="evidence-status" data-ledger-label="not-yet-backed"><td>not-yet</td></tr>
      </tbody>
    </table>
  `);
}

function staticTriagePage() {
  const filler = Array.from({ length: 90 }, (_, index) => `static triage evidence boundary ${index}`).join(" ");
  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>static evidence checklist</p>
    <p>evidence tier</p>
    <p>handoff questions</p>
    <p>Partial static evidence is useful when labeled as partial</p>
    <p>Static triage is the engineer checklist and handoff page, distinct from the incident-call orientation page.</p>
    <p>The checklist is not telemetry, diagnosis, or approval.</p>
    <a href="/proof-paths/">Proof paths</a>
    <a href="/validation/">Validation</a>
    <a href="/docs/">Docs</a>
    <a href="/limitations/">Limitations</a>
    <a href="/demo/result/">Demo result</a>
    <a href="/incident-call/">Incident-call orientation</a>
    <p>${filler}</p>
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
