import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  adoptionPartialAnalysisSentence,
  adoptionPlaybookRoute
} from "./adoption-playbook.mjs";
import { createDiscoveryOutputs } from "./discovery.mjs";
import { demoEvidenceTrailRoute } from "./demo-evidence-trail.mjs";
import { demoRunbookInboundLinkRoutes, demoRunbookRoute } from "./demo-runbook.mjs";
import { deployAuditRequiredRoutes } from "./deploy-audit.mjs";
import { endpointReviewRoute } from "./endpoint-review.mjs";
import { incidentCallRoute } from "./incident-call.mjs";
import {
  incidentEvidenceHandoffRequiredLinks,
  incidentEvidenceHandoffRoute
} from "./incident-evidence-handoff.mjs";
import { managerBriefRoute } from "./manager-brief.mjs";
import { managerFaqRoute } from "./manager-faq.mjs";
import { proofSourceCatalogRoute } from "./proof-source-catalog.mjs";
import { reviewClaimChecklistInboundRoutes, reviewClaimChecklistRoute } from "./review-claim-checklist.mjs";
import { reviewRoomRoute } from "./review-room.mjs";
import { roadmapClaimLedgerRoute } from "./roadmap-claim-ledger.mjs";
import { staticTriageRoute } from "./static-triage.mjs";
import { staticVsRuntimeRoute } from "./static-vs-runtime.mjs";
import {
  teamEvidenceHandoffRequiredLinks,
  teamEvidenceHandoffRoute
} from "./team-evidence-handoff.mjs";
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
    ...new Set([
      ...deployAuditRequiredRoutes,
      adoptionPlaybookRoute,
      demoEvidenceTrailRoute,
      demoRunbookRoute,
      endpointReviewRoute,
      incidentCallRoute,
      incidentEvidenceHandoffRoute,
      ...incidentEvidenceHandoffRequiredLinks,
      teamEvidenceHandoffRoute,
      ...teamEvidenceHandoffRequiredLinks,
      managerBriefRoute,
      managerFaqRoute,
      proofSourceCatalogRoute,
      reviewClaimChecklistRoute,
      reviewRoomRoute,
      roadmapClaimLedgerRoute,
      staticTriageRoute,
      staticVsRuntimeRoute
    ])
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
    demoRunbookRoute,
    endpointReviewRoute,
    "/evidence/",
    "/examples/",
    incidentCallRoute,
    incidentEvidenceHandoffRoute,
    teamEvidenceHandoffRoute,
    "/legacy-validation/",
    managerBriefRoute,
    managerFaqRoute,
    proofSourceCatalogRoute,
    "/manager-packet/",
    "/packets/",
    reviewClaimChecklistRoute,
    reviewRoomRoute,
    roadmapClaimLedgerRoute,
    staticTriageRoute,
    staticVsRuntimeRoute,
    "/use-cases/",
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
            : route === demoRunbookRoute
              ? demoRunbookPage()
              : route === endpointReviewRoute
                ? endpointReviewPage()
              : route === incidentCallRoute
                ? incidentCallPage()
                : route === incidentEvidenceHandoffRoute
                  ? incidentEvidenceHandoffPage()
                  : route === teamEvidenceHandoffRoute
                    ? teamEvidenceHandoffPage()
                    : route === managerBriefRoute
                      ? managerBriefPage()
                      : route === managerFaqRoute
                        ? managerFaqPage()
                        : route === proofSourceCatalogRoute
                          ? await proofSourceCatalogPage()
                          : route === reviewClaimChecklistRoute
                            ? reviewClaimChecklistPage()
                            : route === reviewRoomRoute
                              ? reviewRoomPage()
                              : route === roadmapClaimLedgerRoute
                                ? roadmapClaimLedgerPage()
                                : route === staticTriageRoute
                                  ? staticTriagePage()
                                  : route === staticVsRuntimeRoute
                                    ? staticVsRuntimePage()
                                    : page(
                                      `<p>${path}</p>${demoRunbookInboundLinkRoutes.includes(route) ? `<a href="${demoRunbookRoute}">Public demo runbook</a>` : ""}${reviewClaimChecklistInboundRoutes.includes(route) ? `<a href="${reviewClaimChecklistRoute}">Review claim checklist</a>` : ""}`
                                    ),
      "utf8"
    );
  }

  await writeFile(join(dist, "index.html"), indexHtml, "utf8");
  await writeFile(join(dist, "docs", "index.html"), docsHtml, "utf8");
  await writeFile(join(dist, "favicon.svg"), "<svg></svg>", "utf8");
  await writeFile(join(dist, "styles.css"), "body { margin: 0; }\n", "utf8");
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
        path: demoRunbookRoute,
        title: "Public Demo Runbook",
        summary: "Fixture runbook route for validation.",
        publicClaimLevel: "demo",
        sourceType: "site-page",
        hintCategory: "demo",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture runbook limitations remain bounded."],
        nonClaims: ["No runtime behavior or production usage proof."]
      },
      {
        path: endpointReviewRoute,
        title: "Endpoint Review Playbook",
        summary: "Fixture endpoint review playbook route for validation.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture endpoint review limitations remain bounded."],
        nonClaims: [
          "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, AI impact analysis, LLM analysis, or complete product coverage proof.",
          "No facts.ndjson, index.sqlite, logs/analyzer.log, raw source snippets, raw SQL, config values, secrets, local absolute paths, raw remotes, generated scan directories, connection strings, credentials, table dumps, or database contents are public."
        ]
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
        path: incidentEvidenceHandoffRoute,
        title: "Incident Evidence Handoff",
        summary: "Fixture incident evidence handoff route for validation.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture incident evidence handoff limitations remain bounded."],
        nonClaims: ["No runtime behavior or production usage proof."]
      },
      {
        path: teamEvidenceHandoffRoute,
        title: "Team Evidence Handoff",
        summary: "Fixture team evidence handoff route for validation.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture team evidence handoff limitations remain bounded."],
        nonClaims: [
          "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, AI impact analysis, LLM analysis, or complete product coverage proof."
        ]
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
        path: proofSourceCatalogRoute,
        title: "Proof Source Catalog",
        summary: "Fixture proof source catalog route for validation.",
        publicClaimLevel: "demo",
        sourceType: "site-page",
        hintCategory: "evidence",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture proof source catalog limitations remain bounded."],
        nonClaims: ["No runtime behavior or production usage proof."]
      },
      {
        path: reviewClaimChecklistRoute,
        title: "Review Claim Checklist",
        summary: "Fixture review claim checklist route for validation.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture review claim checklist limitations remain bounded."],
        nonClaims: ["No runtime behavior, production usage, AI impact analysis, or LLM analysis proof."]
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
        path: staticVsRuntimeRoute,
        title: "Static Vs Runtime",
        summary: "Fixture static versus runtime concept route for validation.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture static versus runtime limitations remain bounded."],
        nonClaims: [
          "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, complete product coverage, incident root cause, service ownership, production dependency understanding, or test sufficiency proof.",
          "No AI impact analysis, LLM analysis, prompt-based classification, embedding search, or vector database analysis."
        ]
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

async function proofSourceCatalogPage() {
  return readFile(new URL("../src/proof-source-catalog/index.html", import.meta.url), "utf8");
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

function incidentEvidenceHandoffPage() {
  const filler = Array.from({ length: 110 }, (_, index) => `incident evidence handoff boundary ${index}`).join(" ");
  return `<!doctype html>
<html>
  <head>
    <title>Incident Evidence Handoff Packet | TraceMap</title>
    <meta name="description" content="Fixture incident evidence handoff packet.">
    <link rel="canonical" href="https://tracemap.tools/incident-evidence-handoff/">
    <meta property="og:type" content="article">
    <meta property="og:title" content="TraceMap Incident Evidence Handoff Packet">
    <meta property="og:description" content="Fixture static evidence handoff packet.">
    <meta property="og:url" content="https://tracemap.tools/incident-evidence-handoff/">
  </head>
  <body>
    ${topNav()}
    <main>
      <p>Public claim level: concept</p>
      <p>No public conclusion without evidence</p>
      <p>Incident evidence handoff is the packet of static evidence, proof paths, limits, and next owners; it is not runtime proof or incident command.</p>
      <p>Static triage frames the question; the incident evidence handoff packet carries the already-framed evidence, proof paths, limits, and next owners into the next conversation.</p>
      <p>static evidence proof path rule ID/evidence tier coverage label limitation next owner</p>
      <p>route existence DTO shape package reference dependency edge SQL-facing reference</p>
      <p>telemetry logs traces APM release controls tests database ownership service ownership incident command</p>
      <a href="/proof-paths/">Proof paths</a>
      <a href="/validation/">Validation</a>
      <a href="/limitations/">Limitations</a>
      <a href="/demo/result/">Demo result</a>
      <a href="/incident-call/">Incident call</a>
      <a href="/static-triage/">Static triage</a>
      <a href="/review-room/">Review room</a>
      <a href="/manager-faq/">Manager FAQ</a>
      <a href="/packets/">Packets</a>
      <a href="/manager-packet/">Manager packet</a>
      <a href="/manager-brief/">Manager brief</a>
      <a href="/use-cases/incident-review/">Incident review</a>
      <a href="/docs/">Docs</a>
      <p>${filler}</p>
    </main>
  </body>
</html>`;
}

function teamEvidenceHandoffPage() {
  const filler = Array.from({ length: 110 }, (_, index) => `team evidence handoff boundary ${index}`).join(" ");
  return `<!doctype html>
<html>
  <head>
    <title>Team Evidence Handoff | TraceMap</title>
    <meta name="description" content="Fixture team evidence handoff route.">
    <link rel="canonical" href="https://tracemap.tools/team-evidence-handoff/">
    <meta property="og:type" content="article">
    <meta property="og:title" content="TraceMap Team Evidence Handoff">
    <meta property="og:description" content="Fixture team evidence handoff route.">
    <meta property="og:url" content="https://tracemap.tools/team-evidence-handoff/">
  </head>
  <body>
    ${topNav()}
    <main>
      <p>Public claim level: concept</p>
      <p>No public conclusion without evidence</p>
      <p>A handoff is complete only when the summary, proof path, rule ID/rule family, evidence tier, coverage label, limitations, non-claims, local-only artifacts, and next owner/action travel together.</p>
      <p>The summary is a bounded statement of what static evidence supports, and the proof path points to public-safe proof surfaces or private review locations, not private scanner output on the public site.</p>
      <p>summary proof path rule ID/rule family evidence tier coverage label limitations non-claims local-only artifacts next owner/action.</p>
      <p>Teammate Reviewer Manager Agent teammate reviewer manager agent.</p>
      <p>Use /packets/ for packet artifact families. Use /manager-packet/ for manager-ready summaries. Use /review-room/ for a shared agenda. Use /manager-faq/ for stakeholder questions. Use /proof-source-catalog/ for proof-source families.</p>
      <a href="/proof-paths/">Proof paths</a>
      <a href="/packets/">Packets</a>
      <a href="/manager-packet/">Manager packet</a>
      <a href="/review-room/">Review room</a>
      <a href="/manager-faq/">Manager FAQ</a>
      <a href="/proof-source-catalog/">Proof-source catalog</a>
      <a href="/limitations/">Limitations</a>
      <a href="/validation/">Validation</a>
      <section data-boundary-region>
        <p>It does not claim runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, AI impact analysis, LLM analysis, autonomous approval, or complete product coverage.</p>
        <p>It does not replace human ownership, tests, telemetry, release review, code review, source review, logs, traces, incident response, or manager judgment.</p>
        <p>Do not publish raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, credential-like values, or private URLs. Private repository evidence needs private review before any public-safe summary is written.</p>
      </section>
      <p>${filler}</p>
    </main>
  </body>
</html>`;
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
    <a href="/demo/runbook/">Demo runbook</a>
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

function demoRunbookPage() {
  return page(`
    <p>Public claim level: demo</p>
    <p>No public conclusion without evidence</p>
    <p>operator checklist</p>
    <h3>Follow the evidence</h3>
    <p>&lt;ignored-output-dir&gt; ./scripts/check-private-paths.sh public.demo.summary.v1</p>
    <p>Tier1Semantic Tier2Structural Tier3SyntaxOrTextual Tier4Unknown PartialAnalysis not_requested unavailable</p>
    <p>gap-labeled row: partial coverage, no clean reducer conclusion</p>
    <meta property="og:type" content="article">
    <a href="/demo/start-here/">Demo walkthrough</a>
    <a href="/demo/result/">Demo result</a>
    <a href="/demo/evidence-trail/">Demo evidence trail</a>
    <a href="/demo/proof-upgrades/">Demo proof upgrades</a>
    <a href="/proof-paths/">Proof paths</a>
    <a href="/validation/">Validation</a>
    <a href="/limitations/">Limitations</a>
    <a href="https://github.com/joefeser/tracemap/blob/main/scripts/demo-public.sh">scripts/demo-public.sh</a>
    <section data-runbook-section="artifact-boundary">
      <p>scan-manifest.json facts.ndjson index.sqlite report.md logs/analyzer.log analyzer.log raw SQL config values secrets generated scan directories private sample names raw source snippets raw repository remotes local absolute paths</p>
    </section>
    <section data-runbook-section="sharing-guidance">
      <p>Use static evidence and avoid unsupported impacted wording.</p>
    </section>
    <section data-runbook-section="red-flag">
      <p>AI impact analysis, LLM analysis, runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, and complete product coverage are red flags.</p>
    </section>
  `);
}

function endpointReviewPage() {
  const filler = Array.from({ length: 150 }, (_, index) => `endpoint review boundary ${index}`).join(" ");

  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>Endpoint review starts with static evidence, not certainty.</p>
    <meta property="og:type" content="article">
    <section id="evidence-packet">
      <p>endpoint-adjacent static paths packages config surfaces SQL-facing surfaces coverage labels limitations</p>
      <p>rule IDs evidence tiers file paths line spans commit/source context extractor versions gap labels</p>
    </section>
    <section id="workflow">
      <p>Static paths direct structural syntax-only evidence package framework surfaces config surfaces SQL-facing surfaces coverage and limitations.</p>
    </section>
    <section id="decisions">
      <p>deeper code review targeted tests telemetry question owner follow-up</p>
    </section>
    <section id="concept-example">
      <p>static evidence suggests a review candidate</p>
      <p>rule ID &lt;rule-id&gt;, Tier2Structural, partial coverage</p>
      <p>gap-labeled packet: review question remains open</p>
    </section>
    <section id="artifact-boundary">
      <p>facts.ndjson index.sqlite report.md scan-manifest.json logs/analyzer.log raw source snippets raw SQL config values secrets local paths raw remotes generated scan directories private sample names connection strings credentials table dumps database contents</p>
    </section>
    <section id="claim-safe-language">
      <p>runtime behavior production traffic endpoint performance outage cause release safety operational safety AI impact analysis LLM analysis complete product coverage team blame vendor blame scare framing</p>
    </section>
    <a href="/use-cases/">Use cases</a>
    <a href="/evidence/">Evidence</a>
    <a href="/proof-paths/">Proof paths</a>
    <a href="/validation/">Validation</a>
    <a href="/limitations/">Limitations</a>
    <a href="/review-room/">Review room</a>
    <a href="/static-triage/">Static triage</a>
    <a href="/demo/runbook/">Demo runbook</a>
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
    <a href="/review-claim-checklist/">Review claim checklist</a>
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
    <a href="/review-claim-checklist/">Review claim checklist</a>
    <p>${filler}</p>
  `);
}

function reviewClaimChecklistPage() {
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
    <meta property="og:type" content="article">
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
    <a href="/review-claim-checklist/">Review claim checklist</a>
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

function staticVsRuntimePage() {
  const filler = Array.from({ length: 115 }, (_, index) => `static runtime evidence boundary ${index}`).join(" ");
  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>deterministic static repository evidence</p>
    <p>runtime observability remains the source</p>
    <table>
      <thead>
        <tr>
          <th scope="col">Static evidence question</th>
          <th scope="col">TraceMap evidence shape</th>
          <th scope="col">Runtime question</th>
          <th scope="col">Runtime system owner</th>
        </tr>
      </thead>
    </table>
    <section id="static-questions"></section>
    <section id="runtime-questions"></section>
    <section id="handoff-workflow"></section>
    <section id="proof-paths"></section>
    <section id="limitations"></section>
    <section id="non-claims"></section>
    <p>Before runtime review</p>
    <p>During handoff</p>
    <p>After runtime review</p>
    <p>TraceMap does not prove runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, incident root cause, service ownership, production dependency understanding, test sufficiency, or complete product coverage.</p>
    <p>TraceMap does not replace logs, traces, APM, telemetry, incident dashboards, production metrics, tests, service-owner review, incident response, release approval, governance, or human judgment.</p>
    <p>TraceMap does not perform AI impact analysis, LLM analysis, prompt-based classification, embedding search, or vector database analysis.</p>
    <p>TraceMap should not use impact wording for a surface unless reducer-backed public-safe evidence supports that wording.</p>
    <a href="/docs/">Docs</a>
    <a href="/validation/">Validation</a>
    <a href="/limitations/">Limitations</a>
    <a href="/outputs/">Outputs</a>
    <a href="/proof-paths/">Proof paths</a>
    <a href="/capabilities/">Capabilities</a>
    <a href="/demo/">Demo</a>
    <a href="/demo/result/">Demo result</a>
    <a href="/static-triage/">Static triage</a>
    <a href="/incident-call/">Incident call</a>
    <a href="/use-cases/incident-review/">Incident review</a>
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
