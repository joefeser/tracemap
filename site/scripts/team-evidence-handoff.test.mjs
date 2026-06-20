import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  teamEvidenceHandoffRequiredLinks,
  teamEvidenceHandoffRoute,
  validateTeamEvidenceHandoffDist
} from "./team-evidence-handoff.mjs";

test("validateTeamEvidenceHandoffDist accepts the team evidence handoff route", async (t) => {
  const root = await createManagedTeamEvidenceHandoffDistFixture(t);
  const errors = [];

  await validateTeamEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateTeamEvidenceHandoffDist reports missing required text", async (t) => {
  const root = await createManagedTeamEvidenceHandoffDistFixture(t, {
    handoffHtml: page("<main><p>Team evidence handoff placeholder.</p></main>")
  });
  const errors = [];

  await validateTeamEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: concept/);
  assert.match(errors.join("\n"), /missing required text: A handoff is complete only when/);
});

test("validateTeamEvidenceHandoffDist reports route metadata regressions", async (t) => {
  const root = await createManagedTeamEvidenceHandoffDistFixture(t);
  await rewriteHandoffRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/",
    nonClaims: ["No runtime behavior proof."]
  });
  const errors = [];

  await validateTeamEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
  assert.match(errors.join("\n"), /nonClaims are missing required term: production traffic/);
});

test("validateTeamEvidenceHandoffDist rejects forbidden route metadata positioning", async (t) => {
  const root = await createManagedTeamEvidenceHandoffDistFixture(t);
  await rewriteHandoffRoutesIndexEntry(join(root, "dist"), {
    summary: "AI-powered handoff summary."
  });
  const errors = [];

  await validateTeamEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden AI\/LLM positioning/);
});

test("validateTeamEvidenceHandoffDist rejects hard private values in route nonClaims", async (t) => {
  const root = await createManagedTeamEvidenceHandoffDistFixture(t);
  await rewriteHandoffRoutesIndexEntry(join(root, "dist"), {
    nonClaims: [
      "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, AI impact analysis, LLM analysis, or complete product coverage proof.",
      "Do not publish secret=value."
    ]
  });
  const errors = [];

  await validateTeamEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden private\/raw material/);
});

test("validateTeamEvidenceHandoffDist reports missing required links", async (t) => {
  const root = await createManagedTeamEvidenceHandoffDistFixture(t, {
    handoffHtml: teamEvidenceHandoffPage({ omittedLink: "/manager-faq/" })
  });
  const errors = [];

  await validateTeamEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/manager-faq\//);
});

test("validateTeamEvidenceHandoffDist reports unresolved internal links", async (t) => {
  const root = await createManagedTeamEvidenceHandoffDistFixture(t, {
    handoffHtml: teamEvidenceHandoffPage({ extraBody: '<a href="/missing-route/">Missing</a>' })
  });
  const errors = [];

  await validateTeamEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /links to unresolved internal route: \/missing-route\//);
});

test("validateTeamEvidenceHandoffDist reports word count outside bounds", async (t) => {
  const root = await createManagedTeamEvidenceHandoffDistFixture(t, {
    handoffHtml: teamEvidenceHandoffPage({ fillerWordCount: 20 })
  });
  const errors = [];

  await validateTeamEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /word count must be between 400 and 1500 words/);
});

test("validateTeamEvidenceHandoffDist rejects forbidden positioning outside boundary copy", async (t) => {
  const root = await createManagedTeamEvidenceHandoffDistFixture(t, {
    handoffHtml: teamEvidenceHandoffPage({ extraBody: "<p>TraceMap is AI-powered.</p>" })
  });
  const errors = [];

  await validateTeamEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden AI\/LLM positioning/);
});

test("validateTeamEvidenceHandoffDist permits sanctioned non-claims and public-safe wording", async (t) => {
  const root = await createManagedTeamEvidenceHandoffDistFixture(t, {
    handoffHtml: teamEvidenceHandoffPage({
      extraBody: "<p>The proof path points to public-safe proof surfaces.</p>"
    })
  });
  const errors = [];

  await validateTeamEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateTeamEvidenceHandoffDist accepts greater-than characters in metadata attributes", async (t) => {
  const root = await createManagedTeamEvidenceHandoffDistFixture(t, {
    handoffHtml: teamEvidenceHandoffPage({
      metadataDescription: "Receiver > handoff fixture."
    })
  });
  const errors = [];

  await validateTeamEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateTeamEvidenceHandoffDist rejects private text outside boundary copy", async (t) => {
  const root = await createManagedTeamEvidenceHandoffDistFixture(t, {
    handoffHtml: teamEvidenceHandoffPage({ extraBody: "<p>Use file:///tmp/private.html.</p>" })
  });
  const errors = [];

  await validateTeamEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden private\/raw material/);
});

test("validateTeamEvidenceHandoffDist rejects secrets inside boundary copy", async (t) => {
  const root = await createManagedTeamEvidenceHandoffDistFixture(t, {
    handoffHtml: teamEvidenceHandoffPage({ extraBoundary: "<p>secret=value</p>" })
  });
  const errors = [];

  await validateTeamEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden private\/raw material/);
});

async function createManagedTeamEvidenceHandoffDistFixture(t, options = {}) {
  const root = await createTeamEvidenceHandoffDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createTeamEvidenceHandoffDistFixture({
  discoveryRoutes = [teamEvidenceHandoffRoute, ...teamEvidenceHandoffRequiredLinks],
  sitemapRoutes = [teamEvidenceHandoffRoute, ...teamEvidenceHandoffRequiredLinks],
  handoffHtml = teamEvidenceHandoffPage()
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-team-evidence-handoff-test-"));
  const dist = join(root, "dist");
  const routes = new Set([teamEvidenceHandoffRoute, ...teamEvidenceHandoffRequiredLinks, "/review-claim-checklist/", "/incident-evidence-handoff/"]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(join(path, "index.html"), route === teamEvidenceHandoffRoute ? handoffHtml : page(`<main><p>${route}</p></main>`), "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: `Route ${route}`,
    summary: "Fixture route for team evidence handoff validation.",
    publicClaimLevel: route === teamEvidenceHandoffRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === teamEvidenceHandoffRoute ? "use-case" : "evidence",
    ...(route === teamEvidenceHandoffRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: ["Fixture limitations remain bounded."],
    nonClaims:
      route === teamEvidenceHandoffRoute
        ? [
            "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, AI impact analysis, LLM analysis, or complete product coverage proof."
          ]
        : ["No runtime behavior or production usage proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteHandoffRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === teamEvidenceHandoffRoute
      ? {
          ...entry,
          ...fields
        }
      : entry
  );
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

function teamEvidenceHandoffPage({
  extraBody = "",
  extraBoundary = "",
  fillerWordCount = 320,
  metadataDescription = "Team evidence handoff fixture.",
  omittedLink = null
} = {}) {
  const links = teamEvidenceHandoffRequiredLinks
    .filter((link) => link !== omittedLink)
    .map((link) => `<a href="${link}">${link}</a>`)
    .join(" ");

  return page(`
    <main>
      <section>
        <p>Public claim level: concept. No public conclusion without evidence.</p>
        <p>A handoff is complete only when the summary, proof path, rule ID/rule family, evidence tier, coverage label, limitations, non-claims, local-only artifacts, and next owner/action travel together.</p>
        <p>The summary is a bounded statement of what static evidence supports, and the proof path points to public-safe proof surfaces or private review locations, not private scanner output on the public site.</p>
      </section>
      <section>
        <p>summary proof path rule ID/rule family evidence tier coverage label limitations non-claims local-only artifacts next owner/action.</p>
        <p>Teammate Reviewer Manager Agent teammate reviewer manager agent.</p>
        <p>Use /packets/ for packet artifact families. Use /manager-packet/ for manager-ready summaries. Use /review-room/ for a shared agenda. Use /manager-faq/ for stakeholder questions. Use /proof-source-catalog/ for proof-source families.</p>
        ${links}
        ${extraBody}
        <p>${"bounded evidence ".repeat(fillerWordCount)}</p>
      </section>
      <section data-boundary-region>
        <p>It does not claim runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, AI impact analysis, LLM analysis, autonomous approval, or complete product coverage.</p>
        <p>It does not replace human ownership, tests, telemetry, release review, code review, source review, logs, traces, incident response, or manager judgment.</p>
        <p>Do not publish raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, credential-like values, or private URLs. Private repository evidence needs private review before any public-safe summary is written.</p>
        ${extraBoundary}
      </section>
    </main>
  `, { metadataDescription });
}

function page(body, { metadataDescription = "Team evidence handoff fixture." } = {}) {
  return `<!doctype html>
<html lang="en">
  <head>
    <title>Team Evidence Handoff | TraceMap</title>
    <meta name="description" content="${metadataDescription}">
    <link rel="canonical" href="https://tracemap.tools/team-evidence-handoff/">
    <meta property="og:type" content="article">
    <meta property="og:title" content="TraceMap Team Evidence Handoff">
    <meta property="og:description" content="Team evidence handoff fixture.">
    <meta property="og:url" content="https://tracemap.tools/team-evidence-handoff/">
  </head>
  <body>${body}</body>
</html>`;
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${routes.map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`).join("\n")}
</urlset>
`;
}
