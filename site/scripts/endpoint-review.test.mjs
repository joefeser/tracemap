import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  endpointReviewRequiredLinks,
  endpointReviewRoute,
  validateEndpointReviewDist
} from "./endpoint-review.mjs";

test("validateEndpointReviewDist accepts the endpoint review route", async (t) => {
  const root = await createManagedEndpointReviewDistFixture(t);
  const errors = [];

  await validateEndpointReviewDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateEndpointReviewDist accepts wrapped safe wording placeholders", async (t) => {
  const root = await createManagedEndpointReviewDistFixture(t, {
    pageHtml: endpointReviewPage().replace(
      "rule ID &lt;rule-id&gt;, Tier2Structural, partial coverage",
      "rule ID &lt;rule-id&gt;,\n          Tier2Structural, partial coverage"
    )
  });
  const errors = [];

  await validateEndpointReviewDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateEndpointReviewDist accepts sanctioned boundary content on non-section elements", async (t) => {
  const root = await createManagedEndpointReviewDistFixture(t, {
    pageHtml: endpointReviewPage()
      .replace('<section id="artifact-boundary">', '<div id="artifact-boundary">')
      .replace("</section>\n    <section id=\"claim-safe-language\">", "</div>\n    <section id=\"claim-safe-language\">")
  });
  const errors = [];

  await validateEndpointReviewDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateEndpointReviewDist rejects unescaped rule-id placeholder tags", async (t) => {
  const root = await createManagedEndpointReviewDistFixture(t, {
    pageHtml: endpointReviewPage().replace(
      "rule ID &lt;rule-id&gt;, Tier2Structural, partial coverage",
      "rule ID <rule-id>, Tier2Structural, partial coverage"
    )
  });
  const errors = [];

  await validateEndpointReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: rule ID <rule-id>, Tier2Structural, partial coverage/);
});

test("validateEndpointReviewDist reports missing required page text", async (t) => {
  const root = await createManagedEndpointReviewDistFixture(t, {
    pageHtml: page("<p>Endpoint review placeholder.</p>")
  });
  const errors = [];

  await validateEndpointReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: concept/);
});

test("validateEndpointReviewDist reports missing route metadata", async (t) => {
  const root = await createManagedEndpointReviewDistFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateEndpointReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/use-cases\/endpoint-review\//);
});

test("validateEndpointReviewDist reports route metadata regressions", async (t) => {
  const root = await createManagedEndpointReviewDistFixture(t);
  await rewriteEndpointReviewRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateEndpointReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
});

test("validateEndpointReviewDist rejects missing discovery non-claim parity", async (t) => {
  const root = await createManagedEndpointReviewDistFixture(t);
  await rewriteEndpointReviewRoutesIndexEntry(join(root, "dist"), {
    nonClaims: ["No runtime behavior proof."]
  });
  const errors = [];

  await validateEndpointReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /nonClaims are missing boundary phrase: production traffic/);
  assert.match(errors.join("\n"), /nonClaims are missing boundary phrase: facts\.ndjson/);
});

test("validateEndpointReviewDist rejects artifact-family text outside sanctioned page sections", async (t) => {
  const root = await createManagedEndpointReviewDistFixture(t, {
    pageHtml: endpointReviewPage("<p>facts.ndjson appears in a regular paragraph.</p>")
  });
  const errors = [];

  await validateEndpointReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /artifact-family text outside sanctioned sections: facts\.ndjson/);
});

test("validateEndpointReviewDist rejects artifact-family text in discovery summary", async (t) => {
  const root = await createManagedEndpointReviewDistFixture(t);
  await rewriteEndpointReviewRoutesIndexEntry(join(root, "dist"), {
    summary: "Endpoint review summary that names facts.ndjson."
  });
  const errors = [];

  await validateEndpointReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /discovery summary contains artifact-family text outside nonClaims/);
});

test("validateEndpointReviewDist rejects unavailable discovery wording", async (t) => {
  const root = await createManagedEndpointReviewDistFixture(t);
  await rewriteEndpointReviewRoutesIndexEntry(join(root, "dist"), {
    limitations: ["The endpoint review playbook is available for runtime proof."]
  });
  const errors = [];

  await validateEndpointReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /discovery limitations\[0\] uses unavailable status wording/);
});

test("validateEndpointReviewDist rejects missing required links", async (t) => {
  const root = await createManagedEndpointReviewDistFixture(t, {
    pageHtml: endpointReviewPage().replace('href="/validation/"', 'href="/missing-validation/"')
  });
  const errors = [];

  await validateEndpointReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/validation\//);
});

test("validateEndpointReviewDist rejects private absolute paths outside sanctioned sections", async (t) => {
  const localPath = ["/", "Users", "/example/private"].join("");
  const root = await createManagedEndpointReviewDistFixture(t, {
    pageHtml: endpointReviewPage(`<p>${localPath}</p>`)
  });
  const errors = [];

  await validateEndpointReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public text outside sanctioned sections: \/Users\//);
});

test("validateEndpointReviewDist rejects encoded private URI text", async (t) => {
  const root = await createManagedEndpointReviewDistFixture(t, {
    pageHtml: endpointReviewPage("<p>file&#58;//private/report</p>")
  });
  const errors = [];

  await validateEndpointReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public text outside sanctioned sections: file:\/\//);
});

test("validateEndpointReviewDist rejects unsupported endpoint conclusions", async (t) => {
  const root = await createManagedEndpointReviewDistFixture(t, {
    pageHtml: endpointReviewPage("<p>The endpoint is impacted.</p><p>TraceMap proves endpoint performance.</p>")
  });
  const errors = [];

  await validateEndpointReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unsupported unsupported impact wording/);
  assert.match(errors.join("\n"), /unsupported runtime conclusion/);
});

test("validateEndpointReviewDist rejects scare framing and blame", async (t) => {
  const rejectedPhrase = ["this endpoint", " is ", "trash"].join("");
  const root = await createManagedEndpointReviewDistFixture(t, {
    pageHtml: endpointReviewPage(`<p>${rejectedPhrase}</p><p>The vendor caused the failure.</p>`)
  });
  const errors = [];

  await validateEndpointReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden rejected scare framing/);
  assert.match(errors.join("\n"), /forbidden vendor blame/);
});

test("validateEndpointReviewDist rejects raw content outside sanctioned sections", async (t) => {
  const root = await createManagedEndpointReviewDistFixture(t, {
    pageHtml: endpointReviewPage("<p>ConnectionString and raw SQL belong in a private packet.</p>")
  });
  const errors = [];

  await validateEndpointReviewDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public text outside sanctioned sections: connection string/);
  assert.match(errors.join("\n"), /forbidden public text outside sanctioned sections: raw SQL/);
});

async function createManagedEndpointReviewDistFixture(t, options = {}) {
  const root = await createEndpointReviewDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createEndpointReviewDistFixture({
  discoveryRoutes = [endpointReviewRoute],
  pageHtml = endpointReviewPage(),
  sitemapRoutes = [endpointReviewRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-endpoint-review-test-"));
  const dist = join(root, "dist");
  const routes = new Set([endpointReviewRoute, ...endpointReviewRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(join(path, "index.html"), route === endpointReviewRoute ? pageHtml : page(route), "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === endpointReviewRoute ? "Endpoint Review Playbook" : `Route ${route}`,
    summary:
      route === endpointReviewRoute
        ? "Concept-level endpoint review playbook for static evidence packets."
        : "Fixture route for endpoint review validation.",
    publicClaimLevel: route === endpointReviewRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === endpointReviewRoute ? "use-case" : "evidence",
    ...(route === endpointReviewRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations:
      route === endpointReviewRoute
        ? ["Concept route keeps endpoint review bounded to static evidence and gaps."]
        : ["Fixture limitations remain bounded."],
    nonClaims:
      route === endpointReviewRoute
        ? [
            "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, AI impact analysis, LLM analysis, or complete product coverage proof.",
            "No facts.ndjson, index.sqlite, logs/analyzer.log, raw source snippets, raw SQL, config values, secrets, local absolute paths, raw remotes, generated scan directories, connection strings, credentials, table dumps, or database contents are public."
          ]
        : ["No runtime behavior proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteEndpointReviewRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === endpointReviewRoute
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

function endpointReviewPage(extra = "", { fillerWordCount = 750 } = {}) {
  const filler = Array.from({ length: fillerWordCount }, (_, index) => `endpoint-review-boundary-${index}`).join(" ");

  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>Endpoint review starts with static evidence, not certainty.</p>
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
    ${endpointReviewRequiredLinks.map((route) => `<a href="${route}">${route}</a>`).join("\n")}
    <p>${filler}</p>
    ${extra}
  `);
}
