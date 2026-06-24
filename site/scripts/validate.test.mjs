import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  adoptionPartialAnalysisSentence,
  adoptionPlaybookRoute
} from "./adoption-playbook.mjs";
import {
  blogProofPathRequiredLinks,
  blogProofPathSeriesRoute
} from "./blog-proof-path-series.mjs";
import { changeRiskLanguageGuideRoute } from "./change-risk-language-guide.mjs";
import { claimReviewDrillRoute } from "./claim-review-drill.mjs";
import { createDiscoveryOutputs } from "./discovery.mjs";
import { demoEvidenceTrailRoute } from "./demo-evidence-trail.mjs";
import { demoRunbookInboundLinkRoutes, demoRunbookRoute } from "./demo-runbook.mjs";
import { deployAuditRequiredRoutes } from "./deploy-audit.mjs";
import { evidenceDecisionRecordRoute } from "./evidence-decision-record.mjs";
import { evidenceGapRegisterRoute } from "./evidence-gap-register.mjs";
import { evidenceHandoffTemplateRoute } from "./evidence-handoff-template.mjs";
import { endpointReviewRoute } from "./endpoint-review.mjs";
import { evidencePacketExamplesRoute } from "./evidence-packet-examples.mjs";
import { changeReviewRoute } from "./change-review.mjs";
import { glossaryRoute } from "./glossary.mjs";
import { incidentCallRoute } from "./incident-call.mjs";
import {
  incidentEvidenceHandoffRequiredLinks,
  incidentEvidenceHandoffRoute
} from "./incident-evidence-handoff.mjs";
import { managerBriefRoute } from "./manager-brief.mjs";
import {
  managerDemoScriptInboundLinkRoutes,
  managerDemoScriptRoute
} from "./manager-demo-script.mjs";
import { managerFaqRoute } from "./manager-faq.mjs";
import {
  ownerFollowupMapRequiredLinks,
  ownerFollowupMapRoute
} from "./owner-followup-map.mjs";
import { proofPathFaqRoute } from "./proof-path-faq.mjs";
import { proofPathTourRoute } from "./proof-path-tour.mjs";
import { proofSourceCatalogRoute } from "./proof-source-catalog.mjs";
import { reducedCoveragePlaybookRoute } from "./reduced-coverage-playbook.mjs";
import { reviewerQuickstartRoute } from "./reviewer-quickstart.mjs";
import { reviewPacketAssemblyRoute } from "./review-packet-assembly.mjs";
import { reviewClaimChecklistInboundRoutes, reviewClaimChecklistRoute } from "./review-claim-checklist.mjs";
import { releaseReviewBoundaryRoute } from "./release-review-boundary.mjs";
import { reviewMeetingAgendaRoute } from "./review-meeting-agenda.mjs";
import { reviewRoomRoute } from "./review-room.mjs";
import { roadmapClaimLedgerRoute } from "./roadmap-claim-ledger.mjs";
import { staticTriageRoute } from "./static-triage.mjs";
import { staticVsRuntimeRoute } from "./static-vs-runtime.mjs";
import {
  stakeholderObjectionGuideRoute,
  validateStakeholderObjectionGuideDist
} from "./stakeholder-objection-guide.mjs";
import { stakeholderQuestionIndexRoute } from "./stakeholder-question-index.mjs";
import {
  teamEvidenceHandoffRequiredLinks,
  teamEvidenceHandoffRoute
} from "./team-evidence-handoff.mjs";
import { testPlanningHandoffRoute } from "./test-planning-handoff.mjs";
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

test("validateStakeholderObjectionGuideDist reports missing supporting routes", async () => {
  const root = await createDistFixture();
  const dist = join(root, "dist");
  const errors = [];

  await rm(join(dist, "static-vs-runtime"), { recursive: true, force: true });
  await validateStakeholderObjectionGuideDist({ dist, errors });

  assert.match(errors.join("\n"), /Stakeholder objection guide references missing supporting route: \/static-vs-runtime\//);
});

test("validateStakeholderObjectionGuideDist rejects hard private leaks inside bounded rows", async () => {
  const root = await createDistFixture();
  const pagePath = join(root, "dist", "questions", "objections", "index.html");
  const hardLeak = ["/", "Users", "/private"].join("");
  const html = await readFile(pagePath, "utf8");
  const errors = [];

  await writeFile(pagePath, html.replace("raw facts", hardLeak), "utf8");
  await validateStakeholderObjectionGuideDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /Stakeholder objection guide contains forbidden private or credential-like text/);
});

async function createDistFixture({
  docsHtml = page("<p>Docs</p>"),
  indexHtml = page('<a href="/docs/">Docs</a><link rel="canonical" href="https://tracemap.tools/">'),
  robots = "User-agent: *\nAllow: /\n\n# LLM discovery: https://tracemap.tools/llms.txt\nSitemap: https://tracemap.tools/sitemap.xml\n",
  sitemapUrls = [
    ...new Set([
      ...deployAuditRequiredRoutes,
      adoptionPlaybookRoute,
      blogProofPathSeriesRoute,
      demoEvidenceTrailRoute,
      demoRunbookRoute,
      evidenceDecisionRecordRoute,
      evidenceGapRegisterRoute,
      evidenceHandoffTemplateRoute,
      endpointReviewRoute,
      changeReviewRoute,
      changeRiskLanguageGuideRoute,
      "/evidence/",
      "/examples/scan-packet/",
      glossaryRoute,
      incidentCallRoute,
      incidentEvidenceHandoffRoute,
      ...incidentEvidenceHandoffRequiredLinks,
      teamEvidenceHandoffRoute,
      ...teamEvidenceHandoffRequiredLinks,
      testPlanningHandoffRoute,
      managerBriefRoute,
      managerDemoScriptRoute,
      managerFaqRoute,
      ownerFollowupMapRoute,
      ...ownerFollowupMapRequiredLinks,
      proofPathFaqRoute,
      proofPathTourRoute,
      proofSourceCatalogRoute,
      reducedCoveragePlaybookRoute,
      reviewerQuickstartRoute,
      evidencePacketExamplesRoute,
      reviewPacketAssemblyRoute,
      claimReviewDrillRoute,
      reviewClaimChecklistRoute,
      releaseReviewBoundaryRoute,
      reviewMeetingAgendaRoute,
      reviewRoomRoute,
      roadmapClaimLedgerRoute,
      staticTriageRoute,
      staticVsRuntimeRoute,
      stakeholderObjectionGuideRoute,
      stakeholderQuestionIndexRoute
    ])
  ].map((route) => `https://tracemap.tools${route}`)
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-site-validate-test-"));
  const dist = join(root, "dist");

  const fixtureRoutes = new Set([
    ...deployAuditRequiredRoutes,
    adoptionPlaybookRoute,
    "/blog/",
    blogProofPathSeriesRoute,
    "/capabilities/",
    "/demo/start-here/",
    "/demo/proof-upgrades/",
    "/demo/proof-assets/",
    demoEvidenceTrailRoute,
    demoRunbookRoute,
    evidenceDecisionRecordRoute,
    evidenceGapRegisterRoute,
    evidenceHandoffTemplateRoute,
    managerDemoScriptRoute,
    endpointReviewRoute,
    changeReviewRoute,
    changeRiskLanguageGuideRoute,
    "/evidence/",
    "/examples/",
    "/examples/scan-packet/",
    incidentCallRoute,
    incidentEvidenceHandoffRoute,
    teamEvidenceHandoffRoute,
    testPlanningHandoffRoute,
    "/legacy-modernization/evidence-map/",
    "/legacy-validation/",
    glossaryRoute,
    managerBriefRoute,
    managerFaqRoute,
    ownerFollowupMapRoute,
    ...ownerFollowupMapRequiredLinks,
    proofPathFaqRoute,
    proofPathTourRoute,
    proofSourceCatalogRoute,
    reducedCoveragePlaybookRoute,
    reviewerQuickstartRoute,
    "/manager-packet/",
    "/packets/",
    evidencePacketExamplesRoute,
    reviewPacketAssemblyRoute,
    claimReviewDrillRoute,
    reviewClaimChecklistRoute,
    releaseReviewBoundaryRoute,
    reviewMeetingAgendaRoute,
    reviewRoomRoute,
    roadmapClaimLedgerRoute,
    staticTriageRoute,
    staticVsRuntimeRoute,
    stakeholderObjectionGuideRoute,
    stakeholderQuestionIndexRoute,
    "/use-cases/",
    "/outputs/",
    "/use-cases/incident-review/",
    "/vault-export/",
    "/workflows/"
  ]);

  for (const route of fixtureRoutes) {
    if (route === "/") {
      continue;
    }

    const path = route.replace(/^\/|\/$/g, "");
    await mkdir(join(dist, path), { recursive: true });
    await writeFile(join(dist, path, "index.html"), await fixturePageHtml(route, path), "utf8");
  }

  await writeFile(join(dist, "index.html"), indexHtml, "utf8");
  await writeFile(join(dist, "docs", "index.html"), docsHtml, "utf8");
  await writeFile(join(dist, "favicon.svg"), "<svg></svg>", "utf8");
  await writeFile(join(dist, "styles.css"), "body { margin: 0; }\n", "utf8");
  await writeFile(join(dist, "robots.txt"), robots, "utf8");
  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapUrls), "utf8");
  await writeDiscoveryFiles(dist);
  await writeEvidenceDecisionRecordImplementationState(root);
  await writeEvidenceGapRegisterImplementationState(root);
  await writeReviewMeetingAgendaImplementationState(root);

  return root;
}

async function writeEvidenceDecisionRecordImplementationState(root) {
  const statePath = join(
    root,
    ".kiro",
    "specs",
    "site-tracemap-tools-evidence-decision-record",
    "implementation-state.md"
  );
  await mkdir(join(statePath, ".."), { recursive: true });
  await writeFile(
    statePath,
    `Selected placement: \`/decisions/evidence-record/\`

Rejected alternatives:
- \`/review-room/decision-record/\` because this route is a standalone decision-after-evidence record.
- section on \`/review-room/\` because the review-room agenda stays separate.
- section on \`/packets/assembly/\` because the packet assembly checklist stays separate.

This decision-after-evidence record is not a claim checklist, manager packet, objection guide, proof-path tour, release gate, runtime workflow, approval workflow, or autonomous decision system.
`,
    "utf8"
  );
}

async function writeEvidenceGapRegisterImplementationState(root) {
  const statePath = join(
    root,
    ".kiro",
    "specs",
    "site-tracemap-tools-evidence-gap-register",
    "implementation-state.md"
  );
  await mkdir(join(statePath, ".."), { recursive: true });
  await writeFile(
    statePath,
    `Selected placement: standalone route \`/evidence/gaps/\`

Rejected alternatives:

- \`/coverage/gaps/\`

Adjacent route inventory before site edits:

- \`/limitations/reduced-coverage/\`: present; linked directly.

Rejected-pattern marker: use \`data-evidence-gap-boundary="rejected-patterns"\`

No adjacent route substitutions, omissions, or deferrals are needed.

Discovery artifacts for validation: sitemap, routes-index, and llms.txt.
`,
    "utf8"
  );
}

async function writeReviewMeetingAgendaImplementationState(root) {
  const statePath = join(
    root,
    ".kiro",
    "specs",
    "site-tracemap-tools-review-meeting-agenda",
    "implementation-state.md"
  );
  await mkdir(join(statePath, ".."), { recursive: true });
  await writeFile(
    statePath,
    `Selected placement: \`/review-room/agenda/\`

Rejected alternative: \`/meetings/evidence-review/\`
Rejected alternative: section on \`/review-room/\`
Rejected alternative: section on \`/reviewer-quickstart/\`

Primary navigation remains unchanged.
Word-count bounds: 700 to 1500 rendered main-content words
Manual public-safety reviewer signoff: completed by implementation owner
`,
    "utf8"
  );
}

async function fixturePageHtml(route, path) {
  if (route === "/deploy-audit/") {
    return deployAuditPage();
  }

  if (route === adoptionPlaybookRoute) {
    return adoptionPage();
  }

  if (route === "/blog/") {
    return page(`<a href="${blogProofPathSeriesRoute}">What a Proof Path Is</a>`);
  }

  if (route === blogProofPathSeriesRoute) {
    return blogProofPathSeriesPage();
  }

  if (route === demoEvidenceTrailRoute) {
    return demoEvidenceTrailPage();
  }

  if (route === demoRunbookRoute) {
    return demoRunbookPage();
  }

  if (route === evidenceDecisionRecordRoute) {
    return readFile(new URL("../src/decisions/evidence-record/index.html", import.meta.url), "utf8");
  }

  if (route === evidenceGapRegisterRoute) {
    return readFile(new URL("../src/evidence/gaps/index.html", import.meta.url), "utf8");
  }

  if (route === evidenceHandoffTemplateRoute) {
    return readFile(new URL("../src/handoff/template/index.html", import.meta.url), "utf8");
  }

  if (route === managerDemoScriptRoute) {
    return managerDemoScriptPage();
  }

  if (route === endpointReviewRoute) {
    return endpointReviewPage();
  }

  if (route === changeReviewRoute) {
    return changeReviewPage();
  }

  if (route === changeRiskLanguageGuideRoute) {
    return readFile(new URL("../src/language/change-risk/index.html", import.meta.url), "utf8");
  }

  if (route === "/evidence/") {
    return readFile(new URL("../src/evidence/index.html", import.meta.url), "utf8");
  }

  if (route === glossaryRoute) {
    return glossaryPage();
  }

  if (route === incidentCallRoute) {
    return incidentCallPage();
  }

  if (route === incidentEvidenceHandoffRoute) {
    return incidentEvidenceHandoffPage();
  }

  if (route === teamEvidenceHandoffRoute) {
    return readFile(new URL("../src/team-evidence-handoff/index.html", import.meta.url), "utf8");
  }

  if (route === testPlanningHandoffRoute) {
    return readFile(new URL("../src/test-planning/index.html", import.meta.url), "utf8");
  }

  if (route === managerBriefRoute) {
    return managerBriefPage();
  }

  if (route === managerFaqRoute) {
    return managerFaqPage();
  }

  if (route === ownerFollowupMapRoute) {
    return readFile(new URL("../src/owners/follow-up/index.html", import.meta.url), "utf8");
  }

  if (route === proofPathTourRoute) {
    return readFile(new URL("../src/proof-paths/tour/index.html", import.meta.url), "utf8");
  }

  if (route === proofPathFaqRoute) {
    return readFile(new URL("../src/proof-paths/faq/index.html", import.meta.url), "utf8");
  }

  if (route === proofSourceCatalogRoute) {
    return proofSourceCatalogPage();
  }

  if (route === reducedCoveragePlaybookRoute) {
    return readFile(new URL("../src/limitations/reduced-coverage/index.html", import.meta.url), "utf8");
  }

  if (route === reviewerQuickstartRoute) {
    return reviewerQuickstartPage();
  }

  if (route === reviewPacketAssemblyRoute) {
    return readFile(new URL("../src/packets/assembly/index.html", import.meta.url), "utf8");
  }

  if (route === evidencePacketExamplesRoute) {
    return readFile(new URL("../src/packets/examples/index.html", import.meta.url), "utf8");
  }

  if (route === claimReviewDrillRoute) {
    return readFile(new URL("../src/review-claim-checklist/drill/index.html", import.meta.url), "utf8");
  }

  if (route === reviewClaimChecklistRoute) {
    return readFile(new URL("../src/review-claim-checklist/index.html", import.meta.url), "utf8");
  }

  if (route === releaseReviewBoundaryRoute) {
    return readFile(new URL("../src/release-review-boundary/index.html", import.meta.url), "utf8");
  }

  if (route === reviewRoomRoute) {
    return reviewRoomPage();
  }

  if (route === reviewMeetingAgendaRoute) {
    return readFile(new URL("../src/review-room/agenda/index.html", import.meta.url), "utf8");
  }

  if (route === roadmapClaimLedgerRoute) {
    return roadmapClaimLedgerPage();
  }

  if (route === staticTriageRoute) {
    return staticTriagePage();
  }

  if (route === staticVsRuntimeRoute) {
    return staticVsRuntimePage();
  }

  if (route === stakeholderObjectionGuideRoute) {
    return stakeholderObjectionGuidePage();
  }

  if (route === stakeholderQuestionIndexRoute) {
    return stakeholderQuestionIndexPage();
  }

  return page(
    `<p>${path}</p>${demoRunbookInboundLinkRoutes.includes(route) ? `<a href="${demoRunbookRoute}">Public demo runbook</a>` : ""}${managerDemoScriptInboundLinkRoutes.includes(route) ? `<a href="${managerDemoScriptRoute}">Manager demo script</a>` : ""}${reviewClaimChecklistInboundRoutes.includes(route) ? `<a href="${reviewClaimChecklistRoute}">Review claim checklist</a>` : ""}${route === "/limitations/" ? `<a href="${reducedCoveragePlaybookRoute}">Reduced coverage playbook</a>` : ""}${route === "/packets/" ? `<a href="${reviewPacketAssemblyRoute}">Review packet assembly</a><a href="${evidencePacketExamplesRoute}">Evidence packet examples</a>` : ""}${route === reviewPacketAssemblyRoute ? `<a href="${evidencePacketExamplesRoute}">Evidence packet examples</a>` : ""}${route === "/proof-paths/" ? `<a href="${proofPathTourRoute}">Guided proof-path tour</a><a href="${proofPathFaqRoute}">Proof path FAQ</a>` : ""}`
  );
}

function blogProofPathSeriesPage() {
  const links = blogProofPathRequiredLinks.map((link) => `<a href="${link}">${link}</a>`).join(" ");
  const filler = Array.from(
    { length: 65 },
    () =>
      "The reviewer follows static evidence, checks the rule family, reads the coverage label, records the limitation, and hands unresolved runtime questions to owners."
  ).join(" ");

  return page(
    `<article>
      <meta property="og:type" content="article">
      <link rel="canonical" href="https://tracemap.tools${blogProofPathSeriesRoute}">
      <header>
        <h1>What a proof path is</h1>
        <p>Public claim level: concept</p>
      </header>
      <section data-proof-blog-block="opening-problem">
        <h2>The problem is claim drift.</h2>
        <p>${filler}</p>
      </section>
      <section data-proof-blog-block="evidence-backed-claim-example">
        <h2>An evidence-backed claim names what would support it.</h2>
        <p>${links}</p>
      </section>
      <section data-proof-blog-block="proof-path-reading-steps">
        <h2>Read a proof path in order.</h2>
        <p>Start with the claim, check the public claim level, open the proof surface, read the tier, keep the limitation visible, and use the checklist.</p>
      </section>
      <section data-proof-blog-block="proof-surfaces">
        <h2>The supporting surfaces each have a job.</h2>
        <p>${links}</p>
      </section>
      <section data-proof-blog-block="limitations-and-non-claims" data-tm-boundary="claim-boundary">
        <h2>Limitations and non-claims are part of the proof path.</h2>
        <p>TraceMap does not prove runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, release approval, complete coverage, or product behavior.</p>
        <p>Do not publish raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, hidden validation details, raw command output, or credential-like values.</p>
      </section>
      <section data-proof-blog-block="safe-language-examples">
        <h2>Safe language examples</h2>
        <p>This proof path shows where the public claim is supported and what limits still apply.</p>
      </section>
      <section data-proof-blog-block="unsafe-language-examples" data-tm-boundary="wording-to-avoid">
        <h2>Unsafe language examples, framed as wording to avoid</h2>
        <p>TraceMap proves this endpoint is safe in production.</p>
      </section>
      <section data-proof-blog-block="closing-handoff-action">
        <h2>Close the loop with a handoff, not a bigger claim.</h2>
        <p>Repeat the sentence with the same limits or take the static evidence to the owner, telemetry, logs, traces, tests, or release review.</p>
      </section>
    </article>`
  );
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
        path: managerDemoScriptRoute,
        title: "Manager Demo Script",
        summary: "Fixture manager demo script route for validation.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "demo",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture manager demo script limitations remain bounded."],
        nonClaims: ["No runtime behavior or production usage proof."]
      },
      {
        path: "/capabilities/",
        title: "Capabilities",
        summary: "Fixture capabilities route for validation.",
        publicClaimLevel: "demo",
        sourceType: "site-page",
        hintCategory: "start",
        preferredProofPath: "/docs/",
        limitations: ["Fixture capabilities limitations remain bounded."],
        nonClaims: ["No runtime behavior or production usage proof."]
      },
      {
        path: "/evidence/",
        title: "Evidence Model",
        summary: "Fixture evidence model route for validation.",
        publicClaimLevel: "demo",
        sourceType: "site-page",
        hintCategory: "evidence",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture evidence model limitations remain bounded."],
        nonClaims: ["No runtime behavior or production usage proof."]
      },
      {
        path: evidenceDecisionRecordRoute,
        title: "Evidence Decision Record",
        summary: "Concept-level template for documenting a human owner decision after TraceMap evidence review while preserving proof path, limitation, follow-up, and residual risk.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: [
          "The route is a record template over existing public-safe evidence surfaces, not a new proof source, workflow engine, or authority system.",
          "Every record must keep the proof path, rule ID or family, evidence tier, coverage label, limitation, non-claim, follow-up owner, and residual risk attached."
        ],
        nonClaims: [
          "No autonomous decision, approval workflow, release approval, release safety, operational safety, runtime proof, production proof, endpoint performance proof, outage cause, absence-of-impact proof, complete coverage, AI analysis, LLM analysis, embeddings, vector databases, or prompt classification.",
          "No replacement of tests, code review, source review, runtime observability, telemetry, release process, service-owner review, governance, or human judgment.",
          "No raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values are public record material."
        ]
      },
      {
        path: evidenceGapRegisterRoute,
        title: "Evidence Gap Register",
        summary: "Concept-level register for recording missing, reduced, stale, private-only, unsupported, unknown, validation, and owner-question evidence gaps as bounded follow-up rows.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "evidence",
        preferredProofPath: "/review-claim-checklist/",
        limitations: [
          "The register records follow-up rows and stop conditions; it is not scanner output, reducer output, validation success, or a public proof source.",
          "Gap rows must keep what evidence exists, what cannot be concluded, next owner, proof or validation route, safe wording, and stop condition attached."
        ],
        nonClaims: [
          "No absence-of-impact proof, runtime behavior proof, production traffic proof, endpoint performance proof, outage-cause proof, release approval, release readiness, operational certainty, clean-repo status, complete coverage, AI analysis, LLM analysis, embeddings, vector databases, prompt classification, autonomous approval, or replacement of human review.",
          "No raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values are public gap-register material."
        ]
      },
      {
        path: evidenceHandoffTemplateRoute,
        title: "Evidence Handoff Template",
        summary: "Concept-level reusable template for carrying one TraceMap static-evidence question with proof path, rule context, limits, and next role.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: [
          "The route is an authored public-safe template, not generated handoff output or a new proof source.",
          "Missing private-only scan context, reduced coverage, weak evidence, or absent validation remains a visible limitation or stop condition."
        ],
        nonClaims: [
          "No runtime behavior, production traffic, endpoint performance, outage cause, release approval, release safety, operational safety, real organization ownership, complete coverage, AI impact analysis, LLM analysis, autonomous review, generated handoff feature, or replacement of human review.",
          "No raw artifacts, source excerpts, database text, configuration values, credentials, workstation paths, repository locations, scan folders, command output, hidden validation detail, private sample names, or personal owner names are public handoff-template material."
        ]
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
        path: changeReviewRoute,
        title: "Change Review Brief",
        summary: "Fixture change review brief route for validation.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture change review limitations remain bounded."],
        nonClaims: [
          "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, AI impact analysis, LLM analysis, or complete product coverage proof.",
          "No release approval proof, raw facts, raw SQLite, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, facts.ndjson, index.sqlite, report.md, scan-manifest.json, or logs/analyzer.log are published."
        ]
      },
      {
        path: changeRiskLanguageGuideRoute,
        title: "Change-Risk Language Guide",
        summary: "Concept-level wording guide for choosing bounded public language around deterministic static change evidence, reduced coverage, owner handoffs, and stop conditions.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "evidence",
        preferredProofPath: "/proof-paths/",
        limitations: [
          "The guide teaches public-safe wording and cannot upgrade static evidence into stronger product, runtime, release, or safety conclusions.",
          "Evidence-bearing scanner facts, reducer findings, rule catalog entries, coverage labels, and documented limitations remain the source of support."
        ],
        nonClaims: [
          "No impact proof, absence-of-impact proof, release approval, release safety, operational safety, runtime proof, production traffic proof, endpoint performance proof, complete coverage, AI impact analysis, LLM analysis, autonomous approval, or replacement of human judgment.",
          "No raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values are public language-guide material."
        ]
      },
      {
        path: glossaryRoute,
        title: "Evidence Glossary",
        summary: "Concept-level vocabulary for public-safe TraceMap evidence terms before readers repeat public claims.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "evidence",
        preferredProofPath: "/proof-paths/",
        limitations: [
          "Glossary definitions are vocabulary guidance, not scanner or reducer coverage evidence.",
          "Terms must stay attached to route-specific proof paths, coverage labels, and limitations."
        ],
        nonClaims: [
          "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, AI impact analysis, LLM analysis, or complete product coverage proof.",
          "No raw artifact publication, raw facts, raw SQLite indexes, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, or hidden validation details."
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
        path: testPlanningHandoffRoute,
        title: "Test Planning Handoff",
        summary: "Concept-level handoff for turning TraceMap deterministic static evidence into human-owned test-planning questions.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: ["The fixture route translates static evidence into test-planning questions without acting as a validation result or release gate."],
        nonClaims: [
          "No generated tests, test sufficiency, runtime behavior, production traffic, endpoint performance, release safety, release approval, complete coverage, AI impact analysis, LLM analysis, or replacement of QA proof."
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
        path: ownerFollowupMapRoute,
        title: "Owner Follow-Up Map",
        summary: "Concept-level map for routing static-evidence questions to human owner categories while preserving proof paths, limitations, handoff wording, and stop conditions.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: [
          "The map routes questions to owner categories, not real teams, people, approval chains, on-call rotations, service catalogs, database stewardship, or production ownership records.",
          "Every row must keep the static evidence trigger, what TraceMap can and cannot show, proof path, limitation, handoff wording, and stop condition attached."
        ],
        nonClaims: [
          "No real org ownership claim, production ownership proof, runtime behavior, production traffic, endpoint performance, release approval, release safety, operational safety, complete coverage, or replacement of human judgment.",
          "No AI impact analysis, LLM analysis, embeddings, vector databases, prompt classification, automated ownership detection, automated release approval, raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values are public owner follow-up material."
        ]
      },
      {
        path: proofPathTourRoute,
        title: "Guided Proof-Path Tour",
        summary: "Concept-level guided reading flow for inspecting one public claim through proof path, rule family, evidence tier, coverage label, source context, limitation, non-claim, and next owner.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "evidence",
        preferredProofPath: "/proof-paths/",
        limitations: [
          "The tour is concept-level reading guidance over existing public-safe evidence surfaces, not a proof engine, runtime trace, approval workflow, validation result, or new evidence source.",
          "Missing rule, tier, coverage, source context, extractor version, limitation, or public-safe support means the public claim stops or moves to owner follow-up."
        ],
        nonClaims: [
          "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, release approval, complete coverage, product behavior proof, autonomous approval, or replacement for tests, code review, source review, runtime observability, or human judgment.",
          "No AI impact analysis, LLM analysis, embeddings, vector databases, prompt classification, raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, hidden validation details, raw command output, or credential-like values are public proof material."
        ]
      },
      {
        path: proofPathFaqRoute,
        title: "Proof Path FAQ",
        summary: "Concept-level FAQ for reading proof paths, evidence tiers, coverage labels, limitations, review-packet context, missing-evidence gaps, and static-evidence boundaries.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "evidence",
        preferredProofPath: "/proof-paths/",
        limitations: [
          "The FAQ is concept-level explanation over existing public-safe evidence surfaces, not a generated proof source, scanner result, reducer result, approval workflow, or validation result.",
          "Claims repeated from the FAQ must keep proof path, rule family, tier, coverage label, limitation, non-claim, public claim level, source context, and next-owner handoff attached."
        ],
        nonClaims: [
          "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, release approval, complete coverage, autonomous approval, AI impact analysis, LLM analysis, embeddings, vector databases, prompt classification, or replacement for tests, code review, source review, runtime observability, service-owner judgment, or human judgment.",
          "No raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values are public FAQ material."
        ]
      },
      {
        path: "/packets/",
        title: "Evidence Packet Guide",
        summary: "Fixture packet guide route for validation.",
        publicClaimLevel: "demo",
        sourceType: "site-page",
        hintCategory: "evidence",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture packet guide limitations remain bounded."],
        nonClaims: ["No runtime behavior or production usage proof."]
      },
      {
        path: "/examples/scan-packet/",
        title: "Scan Packet Example",
        summary: "Fixture scan packet example route for validation.",
        publicClaimLevel: "demo",
        sourceType: "site-page",
        hintCategory: "evidence",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture scan packet example limitations remain bounded."],
        nonClaims: ["No runtime behavior or production usage proof."]
      },
      {
        path: evidencePacketExamplesRoute,
        title: "Evidence Packet Examples",
        summary: "Concept-level gallery of synthetic public-safe packet shapes showing claims, proof paths, tiers, coverage labels, limitations, non-claims, owners, and validation evidence.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/packets/",
        limitations: ["The fixture route teaches synthetic public-safe packet shapes, not real customer, private repository, production, or raw artifact evidence."],
        nonClaims: [
          "No runtime behavior, production traffic, endpoint performance, outage cause, release approval, release safety, operational safety, complete coverage, AI impact analysis, LLM analysis, autonomous approval, autonomous review, or replacement of human review."
        ]
      },
      {
        path: "/manager-packet/",
        title: "Manager Packet",
        summary: "Fixture manager packet route for validation.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: ["Fixture manager packet limitations remain bounded."],
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
        path: reducedCoveragePlaybookRoute,
        title: "Reduced Coverage Playbook",
        summary: "Concept-level playbook for labeling partial static evidence, preserving coverage labels, and routing owner follow-up.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "limitations",
        preferredProofPath: "/limitations/",
        limitations: [
          "The playbook is guidance for labeling reduced coverage and owner handoff, not scanner output or reducer output.",
          "Coverage labels, evidence tiers, limitations, proof links, and stop conditions must remain attached before wording is repeated."
        ],
        nonClaims: [
          "No absence-of-impact proof, clean-repo claim under reduced analysis, runtime behavior proof, production traffic proof, endpoint performance proof, outage cause proof, release approval, release safety, operational safety, or complete coverage proof.",
          "No AI impact analysis, LLM analysis, embeddings, vector databases, prompt-based classification, autonomous approval, replacement of human review, raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values."
        ]
      },
      {
        path: reviewerQuickstartRoute,
        title: "Reviewer Quickstart",
        summary: "Five-minute concept-level guide for inspecting a public-safe TraceMap evidence packet before repeating or routing a claim.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: ["The fixture route remains bounded to first-stop reviewer orientation over existing public-safe proof surfaces."],
        nonClaims: [
          "No runtime behavior, production traffic, endpoint performance, outage cause, release approval, release safety, operational safety, complete coverage, AI impact analysis, LLM analysis, embeddings, vector database analysis, prompt classification, autonomous approval, or replacement of tests proof."
        ]
      },
      {
        path: reviewPacketAssemblyRoute,
        title: "Review Packet Assembly",
        summary: "Concept-level checklist for assembling public-safe review handoff material from existing TraceMap evidence surfaces.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: ["The fixture route remains bounded to human checklist guidance."],
        nonClaims: [
          "No runtime behavior, production traffic, endpoint performance, outage cause, release approval or safety, operational safety, AI impact analysis, LLM analysis, autonomous review, generated packet-builder behavior, or complete coverage proof."
        ]
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
        path: claimReviewDrillRoute,
        title: "Claim Review Drill",
        summary: "Concept-level practice drill for checking whether a public claim has proof vocabulary before it is repeated.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/review-claim-checklist/",
        limitations: ["The fixture drill remains bounded to authored practice rows and does not create new proof."],
        nonClaims: [
          "No runtime behavior, production traffic, endpoint performance, outage cause, release approval, release safety, operational safety, absence-of-impact proof, complete coverage, AI impact analysis, LLM analysis, automated grading, or replacement of human review.",
          "No raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values are public drill material."
        ]
      },
      {
        path: releaseReviewBoundaryRoute,
        title: "Release Review Boundary",
        summary: "Concept-level handoff for deterministic static evidence during release review while release-control decisions remain owner-owned.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: [
          "This is a static-evidence release-review handoff, not a release gate, approval system, runtime workflow, deploy audit, validation proof, manager packet, or objection guide.",
          "Static repository evidence can orient questions and gaps but cannot replace release owners, release controls, tests, source review, service-owner judgment, runtime observability, or human judgment."
        ],
        nonClaims: [
          "No release approval, release safety, operational safety, production proof, runtime behavior proof, endpoint performance proof, deployment success proof, absence-of-impact proof, complete coverage, AI impact analysis, LLM analysis, embeddings, vector databases, prompt classification, or replacement of release controls.",
          "No raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values are public release-boundary material."
        ]
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
        path: reviewMeetingAgendaRoute,
        title: "Evidence Review Meeting Agenda",
        summary: "Concept-level meeting agenda for checking TraceMap proof paths, evidence tiers, coverage labels, limitations, gaps, owners, and decision-record handoff.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: [
          "The route is a human meeting agenda over existing public-safe static evidence surfaces, not meeting automation or a new proof source.",
          "The agenda preserves review questions, proof paths, rule context, evidence tiers, coverage labels, limitations, gaps, owners, validation evidence category, and non-claims without upgrading missing evidence."
        ],
        nonClaims: [
          "No meeting automation, release approval, release safety, operational safety, runtime proof, production traffic proof, endpoint performance proof, absence-of-impact proof, complete coverage, AI analysis, LLM analysis, embeddings, vector databases, prompt classification, automated impact analysis, or replacement of human judgment or governance.",
          "No raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, credential-like values, connection strings, tokens, or keys are public agenda material."
        ]
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
        path: stakeholderObjectionGuideRoute,
        title: "Stakeholder Objection Guide",
        summary: "Concept-level guide that turns skeptical stakeholder objections into public-safe evidence checks, stop conditions, limitations, and owner handoffs.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: [
          "The guide is an objection-to-evidence handoff over existing public routes, not a new proof source or release workflow.",
          "Rows must keep the safe answer, evidence check, stop condition, owner handoff, limitation, non-claim, supporting route, and public claim level attached."
        ],
        nonClaims: [
          "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, complete coverage, release approval, autonomous approval, or absence-of-impact proof.",
          "No AI impact analysis, LLM analysis, embeddings, vector databases, prompt classification, raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values are public objection-guide material."
        ]
      },
      {
        path: stakeholderQuestionIndexRoute,
        title: "Stakeholder Question Index",
        summary: "Concept-level orientation route from stakeholder questions to public-safe proof paths.",
        publicClaimLevel: "concept",
        sourceType: "site-page",
        hintCategory: "use-case",
        preferredProofPath: "/proof-paths/",
        limitations: [
          "Rows preserve route-specific proof paths, rule IDs or rule families, evidence tiers, coverage labels, limitations, and non-claims."
        ],
        nonClaims: [
          "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, private-repo behavior, or complete product coverage proof.",
          "No AI impact analysis, LLM analysis, prompt-based classification, raw facts, raw SQLite, analyzer logs, source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, or hidden validation details are published."
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

async function reviewerQuickstartPage() {
  return readFile(new URL("../src/reviewer-quickstart/index.html", import.meta.url), "utf8");
}

function reviewPacketAssemblyPage() {
  const filler = Array.from({ length: 80 }, (_, index) => `bounded evidence assembly ${index}`).join(" ");
  return page(`
    <title>Review Packet Assembly | TraceMap</title>
    <meta name="description" content="Concept checklist">
    <link rel="canonical" href="https://tracemap.tools/packets/assembly/">
    <meta property="og:type" content="article">
    <meta property="og:title" content="Review Packet Assembly">
    <meta property="og:description" content="Concept checklist">
    <meta property="og:url" content="https://tracemap.tools/packets/assembly/">
    <p>Public claim level: concept. No public conclusion without evidence. This is not a generated packet-builder feature.</p>
    <table>
      <tr data-packet-ingredient="claim being reviewed"><td>claim being reviewed</td></tr>
      <tr data-packet-ingredient="audience"><td>audience</td></tr>
      <tr data-packet-ingredient="proof path"><td>proof path</td><td>A public-safe trail or named private review location.</td></tr>
      <tr data-packet-ingredient="public claim level"><td>public claim level</td></tr>
      <tr data-packet-ingredient="rule ID or rule family"><td>rule ID or rule family</td></tr>
      <tr data-packet-ingredient="evidence tier"><td>evidence tier Tier1Semantic Tier2Structural Tier3SyntaxOrTextual Tier4Unknown</td></tr>
      <tr data-packet-ingredient="coverage label"><td>coverage label</td></tr>
      <tr data-packet-ingredient="commit SHA"><td>commit SHA</td></tr>
      <tr data-packet-ingredient="extractor version"><td>extractor version</td></tr>
      <tr data-packet-ingredient="public-safe file path and line span"><td>public-safe file path and line span</td></tr>
      <tr data-packet-ingredient="limitations"><td>limitations</td></tr>
      <tr data-packet-ingredient="non-claims"><td>non-claims</td></tr>
      <tr data-packet-ingredient="next owner"><td>next owner</td></tr>
      <tr data-packet-ingredient="validation evidence"><td>validation evidence</td></tr>
      <tr data-packet-ingredient="unresolved gaps"><td>unresolved gaps</td></tr>
    </table>
    <p>Missing fields stay visible as limitations.</p>
    <h2>Choose the question</h2>
    <h2>Collect public-safe evidence</h2>
    <h2>Attach limitations</h2>
    <h2>Name next owners</h2>
    <h2>Run claim checklist</h2>
    <h2>Stop conditions</h2>
    <h2>Handoff notes</h2>
    <section data-boundary-region>
      <p>missing proof path private-only support raw artifact leakage unknown or reduced coverage without label unsupported runtime, release, or safety wording no next owner no validation evidence</p>
    </section>
    <a href="/packets/">Packets</a>
    <a href="/manager-packet/">Manager packet</a>
    <a href="/team-evidence-handoff/">Team evidence handoff</a>
    <a href="/incident-evidence-handoff/">Incident evidence handoff</a>
    <a href="/review-room/">Review room</a>
    <a href="${reviewerQuickstartRoute}">Reviewer quickstart</a>
    <a href="/review-claim-checklist/">Review claim checklist</a>
    <a href="/proof-source-catalog/">Proof source catalog</a>
    <a href="/proof-paths/">Proof paths</a>
    <a href="/limitations/">Limitations</a>
    <a href="/validation/">Validation</a>
    <p>${filler}</p>
  `);
}

async function glossaryPage() {
  return readFile(new URL("../src/glossary/index.html", import.meta.url), "utf8");
}

async function stakeholderQuestionIndexPage() {
  return readFile(new URL("../src/questions/index.html", import.meta.url), "utf8");
}

async function stakeholderObjectionGuidePage() {
  return readFile(new URL("../src/questions/objections/index.html", import.meta.url), "utf8");
}

function incidentCallPage() {
  return page(`
    <meta property="og:type" content="article">
    <meta property="og:title" content="Manager Demo Script">
    <meta property="og:description" content="Fixture manager demo script description.">
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

function managerDemoScriptPage() {
  const filler = Array.from({ length: 170 }, (_, index) => `manager demo script evidence boundary ${index}`).join(" ");
  const links = [
    "/",
    "/capabilities/",
    "/proof-paths/",
    "/proof-source-catalog/",
    "/demo/result/",
    "/demo/runbook/",
    "/questions/",
    "/limitations/",
    "/validation/",
    "/static-vs-runtime/"
  ]
    .map((route) => `<a href="${route}">${route}</a>`)
    .join("\n");
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
    <meta property="og:type" content="article">
    <meta property="og:title" content="Manager Demo Script">
    <meta property="og:description" content="Fixture manager demo script description.">
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

function changeReviewPage() {
  const filler = Array.from({ length: 150 }, (_, index) => `change review boundary ${index}`).join(" ");

  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>A change review brief is a bounded static-evidence packet for a PR, release, or change-review conversation.</p>
    <a href="${releaseReviewBoundaryRoute}">Release review boundary</a>
    <p>Engineers Code reviewers Architects and managers Release reviewers and agents</p>
    <meta property="og:type" content="article">
    <section id="change-context">
      <h2>Change Context</h2>
      <p>review question changed area commit or branch context review trigger outside scope</p>
    </section>
    <section id="evidence-packet">
      <h2>Evidence Packet</h2>
      <p>proof path Rule ID or rule family Visible static dependency surfaces coverage label file path and line span commit SHA extractor version limitations non-claims</p>
      <p>Tier1Semantic Tier2Structural Tier3SyntaxOrTextual Tier4Unknown</p>
    </section>
    <section id="review-questions">
      <h2>Review Questions</h2>
      <p>Which code review, test review, runtime review, release review, architecture review, or agent handoff question remains open?</p>
    </section>
    <section id="change-review-stop-conditions">
      <h2>Stop Conditions</h2>
      <p>missing proof path private-only evidence unknown or reduced coverage unsupported runtime or release wording raw artifact exposure no named next owner raw facts facts.ndjson raw SQLite index.sqlite report.md scan-manifest.json logs/analyzer.log analyzer logs raw source snippets raw SQL config values secrets local paths raw remotes generated scan directories private sample names raw command output hidden validation details credentials connection strings</p>
    </section>
    <section id="next-owners">
      <h2>Next Owners</h2>
      <p>code owner reviewer test owner runtime/service owner release reviewer architect agent handoff owner</p>
    </section>
    <section id="change-review-limitations">
      <h2>Limitations</h2>
      <p>partial analysis syntax-only evidence unknown unavailable future-only coverage The brief does not replace tests, code review, source review, runtime observability, release review, owner confirmation, or human judgment.</p>
    </section>
    <section id="change-review-non-claims">
      <h2>Non-Claims</h2>
      <p>A change review brief is not release approval and does not approve a release.</p>
      <p>runtime behavior production traffic endpoint performance outage cause release safety operational safety AI impact analysis LLM analysis complete product coverage impacted safe unsafe approved blocked root cause validated for release production proven operational assurance production observability tool</p>
    </section>
    <section id="adjacent-routes">
      <p>team evidence handoff manager packet static triage manager brief deploy audit</p>
    </section>
    <a href="/proof-paths/">Proof paths</a>
    <a href="/packets/">Packet vocabulary</a>
    <a href="/review-room/">Review room</a>
    <a href="/validation/">Validation limits</a>
    <a href="/limitations/">Limitations</a>
    <a href="/use-cases/endpoint-review/">Endpoint review use case</a>
    <a href="/use-cases/incident-review/">Incident review use case</a>
    <a href="/static-vs-runtime/">Static versus runtime</a>
    <a href="/review-claim-checklist/">Claim checklist</a>
    <a href="/use-cases/">Use-case index</a>
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
    <a href="${evidenceDecisionRecordRoute}">Evidence decision record</a>
    <a href="/review-claim-checklist/">Review claim checklist</a>
    <a href="${changeRiskLanguageGuideRoute}">Change-risk language guide</a>
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
    <a href="${releaseReviewBoundaryRoute}">Release review boundary</a>
    <a href="${evidenceDecisionRecordRoute}">Evidence decision record</a>
    <a href="${reviewPacketAssemblyRoute}">Review packet assembly</a>
    <a href="${reviewerQuickstartRoute}">Reviewer quickstart</a>
    <a href="${reviewMeetingAgendaRoute}">Evidence review meeting agenda</a>
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
    <a href="${changeRiskLanguageGuideRoute}">Change-risk language guide</a>
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
