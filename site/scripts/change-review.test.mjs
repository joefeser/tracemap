import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  changeReviewRequiredLinks,
  changeReviewRoute,
  validateChangeReviewDist
} from "./change-review.mjs";

test("validateChangeReviewDist accepts the change review route", async (t) => {
  const root = await createManagedChangeReviewDistFixture(t);
  const errors = [];

  await validateChangeReviewDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateChangeReviewDist reports missing required page text", async (t) => {
  const root = await createManagedChangeReviewDistFixture(t, {
    pageHtml: page("<p>Change review placeholder.</p>")
  });
  const errors = [];

  await validateChangeReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: concept/);
  assert.match(errors.join("\n"), /missing required section id: evidence-packet/);
});

test("validateChangeReviewDist reports missing route metadata", async (t) => {
  const root = await createManagedChangeReviewDistFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateChangeReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/use-cases\/change-review\//);
});

test("validateChangeReviewDist reports route metadata regressions", async (t) => {
  const root = await createManagedChangeReviewDistFixture(t);
  await rewriteChangeReviewRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateChangeReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
});

test("validateChangeReviewDist rejects missing discovery non-claim parity", async (t) => {
  const root = await createManagedChangeReviewDistFixture(t);
  await rewriteChangeReviewRoutesIndexEntry(join(root, "dist"), {
    nonClaims: ["No runtime behavior proof."]
  });
  const errors = [];

  await validateChangeReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /nonClaims are missing boundary phrase: production traffic/);
  assert.match(errors.join("\n"), /nonClaims are missing boundary phrase: release approval/);
  assert.match(errors.join("\n"), /nonClaims are missing boundary phrase: facts\.ndjson/);
});

test("validateChangeReviewDist rejects missing required links", async (t) => {
  const root = await createManagedChangeReviewDistFixture(t, {
    pageHtml: changeReviewPage().replace('href="/validation/"', 'href="/missing-validation/"')
  });
  const errors = [];

  await validateChangeReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/validation\//);
});

test("validateChangeReviewDist accepts sanctioned boundary content", async (t) => {
  const root = await createManagedChangeReviewDistFixture(t, {
    pageHtml: changeReviewPage("<p>facts.ndjson and release approval are contained in sanctioned regions by default.</p>")
  });
  const errors = [];

  await validateChangeReviewDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateChangeReviewDist rejects artifact-family text outside sanctioned sections", async (t) => {
  const root = await createManagedChangeReviewDistFixture(t, {
    pageHtml: changeReviewPage({ unsanctionedExtra: "<p>facts.ndjson appears in regular copy.</p>" })
  });
  const errors = [];

  await validateChangeReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /artifact-family text outside sanctioned sections: facts\.ndjson/);
});

test("validateChangeReviewDist rejects private and raw content outside sanctioned sections", async (t) => {
  const localPath = ["/", "Users", "/example/private"].join("");
  const root = await createManagedChangeReviewDistFixture(t, {
    pageHtml: changeReviewPage({
      unsanctionedExtra: `<p>${localPath}</p><p>raw SQL and hidden validation details appear here.</p>`
    })
  });
  const errors = [];

  await validateChangeReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden \/Users\//);
  assert.match(errors.join("\n"), /forbidden public text outside sanctioned sections: raw SQL/);
  assert.match(errors.join("\n"), /forbidden public text outside sanctioned sections: hidden validation details/);
});

test("validateChangeReviewDist rejects unsupported overclaims outside sanctioned sections", async (t) => {
  const root = await createManagedChangeReviewDistFixture(t, {
    pageHtml: changeReviewPage({
      unsanctionedExtra: "<p>The endpoint is impacted.</p><p>This replaces tests and approves the release.</p><p>TraceMap proves runtime behavior.</p>"
    })
  });
  const errors = [];

  await validateChangeReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unsupported impact wording/);
  assert.match(errors.join("\n"), /unsupported replacement wording/);
  assert.match(errors.join("\n"), /unsupported approval wording/);
  assert.match(errors.join("\n"), /unsupported runtime overclaim/);
});

test("validateChangeReviewDist rejects AI positioning outside sanctioned sections", async (t) => {
  const root = await createManagedChangeReviewDistFixture(t, {
    pageHtml: changeReviewPage({
      unsanctionedExtra: "<p>This is AI-powered intelligent impact analysis.</p>"
    })
  });
  const errors = [];

  await validateChangeReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unsupported AI or LLM positioning/);
});

async function createManagedChangeReviewDistFixture(t, options = {}) {
  const root = await createChangeReviewDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createChangeReviewDistFixture({
  discoveryRoutes = [changeReviewRoute],
  pageHtml = changeReviewPage(),
  sitemapRoutes = [changeReviewRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-change-review-test-"));
  const dist = join(root, "dist");
  const routes = new Set([changeReviewRoute, ...changeReviewRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(join(path, "index.html"), route === changeReviewRoute ? pageHtml : page(route), "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === changeReviewRoute ? "Change Review Brief" : `Route ${route}`,
    summary:
      route === changeReviewRoute
        ? "Concept-level change review brief for deterministic static evidence, visible coverage limits, and named next owners."
        : "Fixture route for change review validation.",
    publicClaimLevel: route === changeReviewRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === changeReviewRoute ? "use-case" : "evidence",
    ...(route === changeReviewRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations:
      route === changeReviewRoute
        ? ["Concept route keeps review questions bounded to static evidence and named follow-up."]
        : ["Fixture limitations remain bounded."],
    nonClaims:
      route === changeReviewRoute
        ? [
            "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, AI impact analysis, LLM analysis, or complete product coverage proof.",
            "No release approval proof, raw facts, raw SQLite, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, facts.ndjson, index.sqlite, report.md, scan-manifest.json, or logs/analyzer.log are published."
          ]
        : ["No runtime behavior proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteChangeReviewRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === changeReviewRoute
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

function changeReviewPage(options = {}) {
  const {
    sanctionedExtra = "",
    unsanctionedExtra = "",
    fillerWordCount = 760
  } = typeof options === "string" ? { sanctionedExtra: options, fillerWordCount: 760 } : options;
  const filler = Array.from({ length: fillerWordCount }, (_, index) => `change-review-boundary-${index}`).join(" ");

  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>A change review brief is a bounded static-evidence packet for a PR, release, or change-review conversation.</p>
    <p>Engineers Code reviewers Architects and managers Release reviewers and agents</p>
    <section id="change-context"><h2>Change Context</h2><p>review question changed area commit or branch context review trigger outside scope</p></section>
    <section id="evidence-packet"><h2>Evidence Packet</h2><p>proof path Rule ID or rule family Visible static dependency surfaces coverage label file path and line span commit SHA extractor version limitations non-claims</p><p>Tier1Semantic Tier2Structural Tier3SyntaxOrTextual Tier4Unknown</p></section>
    <section id="review-questions"><h2>Review Questions</h2><p>Which reviewer question remains open?</p></section>
    <section id="change-review-stop-conditions"><h2>Stop Conditions</h2><p>missing proof path private-only evidence unknown or reduced coverage unsupported runtime or release wording raw artifact exposure no named next owner raw facts facts.ndjson raw SQLite index.sqlite report.md scan-manifest.json logs/analyzer.log analyzer logs raw source snippets raw SQL config values secrets local paths raw remotes generated scan directories private sample names raw command output hidden validation details credentials</p>${sanctionedExtra}</section>
    <section id="next-owners"><h2>Next Owners</h2><p>code owner reviewer test owner runtime/service owner release reviewer architect agent handoff owner</p></section>
    <section id="change-review-limitations"><h2>Limitations</h2><p>partial analysis syntax-only evidence unknown unavailable future-only coverage The brief does not replace tests, code review, source review, runtime observability, release review, owner confirmation, or human judgment.</p></section>
    <section id="change-review-non-claims"><h2>Non-Claims</h2><p>A change review brief is not release approval and does not approve a release.</p><p>runtime behavior production traffic endpoint performance outage cause release safety operational safety AI impact analysis LLM analysis complete product coverage impacted safe unsafe approved blocked root cause validated for release production proven operational assurance production observability tool</p></section>
    <section id="adjacent-routes"><p>team evidence handoff manager packet static triage manager brief deploy audit</p></section>
    ${changeReviewRequiredLinks.map((route) => `<a href="${route}">${route}</a>`).join("\n")}
    <p>${filler}</p>
    ${unsanctionedExtra}
  `);
}
