import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  evidenceHandoffTemplateInboundRoutes,
  evidenceHandoffTemplateRequiredLinks,
  evidenceHandoffTemplateRoute,
  validateEvidenceHandoffTemplateDist
} from "./evidence-handoff-template.mjs";

test("validateEvidenceHandoffTemplateDist accepts the evidence handoff template route", async (t) => {
  const root = await createManagedFixture(t);
  const errors = [];

  await validateEvidenceHandoffTemplateDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateEvidenceHandoffTemplateDist reports missing template fields and examples", async (t) => {
  const root = await createManagedFixture(t, {
    templateHtml: page("<main><p>Public claim level: concept. No public conclusion without evidence.</p></main>")
  });
  const errors = [];

  await validateEvidenceHandoffTemplateDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required section: template/);
  assert.match(errors.join("\n"), /missing template field label: handoff question/);
  assert.match(errors.join("\n"), /synthetic example is missing field label: handoff question/);
});

test("validateEvidenceHandoffTemplateDist reports route metadata regressions", async (t) => {
  const root = await createManagedFixture(t);
  await rewriteRouteEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    preferredProofPath: "/validation/",
    nonClaims: ["No runtime behavior proof."]
  });
  const errors = [];

  await validateEvidenceHandoffTemplateDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
  assert.match(errors.join("\n"), /nonClaims are missing required term: production traffic/);
});

test("validateEvidenceHandoffTemplateDist reports missing neighbor and support links", async (t) => {
  const root = await createManagedFixture(t, {
    templateHtml: evidenceHandoffTemplatePage({ omittedLink: "/validation/" })
  });
  const errors = [];

  await validateEvidenceHandoffTemplateDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/validation\//);
});

test("validateEvidenceHandoffTemplateDist rejects positive overclaims outside boundary copy", async (t) => {
  const root = await createManagedFixture(t, {
    templateHtml: evidenceHandoffTemplatePage({ extraBody: "<p>TraceMap proves runtime behavior.</p>" })
  });
  const errors = [];

  await validateEvidenceHandoffTemplateDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateEvidenceHandoffTemplateDist rejects hard private material and realistic SHAs", async (t) => {
  const root = await createManagedFixture(t, {
    templateHtml: evidenceHandoffTemplatePage({ extraBody: "<p>abcd1234</p>" })
  });
  const errors = [];

  await validateEvidenceHandoffTemplateDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /hard private material/);
});

test("validateEvidenceHandoffTemplateDist rejects uppercase realistic SHAs", async (t) => {
  const root = await createManagedFixture(t, {
    templateHtml: evidenceHandoffTemplatePage({ extraBody: "<p>ABCD1234</p>" })
  });
  const errors = [];

  await validateEvidenceHandoffTemplateDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /hard private material/);
});

test("validateEvidenceHandoffTemplateDist allows hex-like words that are not SHA context", async (t) => {
  const root = await createManagedFixture(t, {
    templateHtml: evidenceHandoffTemplatePage({ extraBody: "<p>defaced effaced feedback</p>" })
  });
  const errors = [];

  await validateEvidenceHandoffTemplateDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateEvidenceHandoffTemplateDist reports non-array limitations without throwing", async (t) => {
  const root = await createManagedFixture(t);
  await rewriteRouteEntry(join(root, "dist"), {
    limitations: { text: "not an array" }
  });
  const errors = [];

  await validateEvidenceHandoffTemplateDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /must include limitations metadata/);
});

test("validateEvidenceHandoffTemplateDist rejects affirmative overclaims in route nonClaims", async (t) => {
  const root = await createManagedFixture(t);
  await rewriteRouteEntry(join(root, "dist"), {
    nonClaims: [
      "No runtime behavior, production traffic, endpoint performance, outage cause, release approval, release safety, operational safety, real organization ownership, complete coverage, AI impact analysis, LLM analysis, autonomous review, generated handoff feature, or replacement of human review.",
      "TraceMap proves runtime behavior."
    ]
  });
  const errors = [];

  await validateEvidenceHandoffTemplateDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateEvidenceHandoffTemplateDist rejects data-id section spoofing", async (t) => {
  const root = await createManagedFixture(t, {
    templateHtml: evidenceHandoffTemplatePage().replace('id="template"', 'data-id="template"')
  });
  const errors = [];

  await validateEvidenceHandoffTemplateDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required section: template/);
});

test("validateEvidenceHandoffTemplateDist rejects data-rel metadata spoofing", async (t) => {
  const root = await createManagedFixture(t, {
    templateHtml: evidenceHandoffTemplatePage().replace('rel="canonical"', 'data-rel="canonical"')
  });
  const errors = [];

  await validateEvidenceHandoffTemplateDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required metadata: canonical URL/);
});

test("validateEvidenceHandoffTemplateDist rejects not-only overclaim wording", async (t) => {
  const root = await createManagedFixture(t, {
    templateHtml: evidenceHandoffTemplatePage({ extraBody: "<p>Not only does TraceMap prove runtime behavior, it certifies the claim.</p>" })
  });
  const errors = [];

  await validateEvidenceHandoffTemplateDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateEvidenceHandoffTemplateDist reports missing inbound links from adjacent routes", async (t) => {
  const root = await createManagedFixture(t, { includeInboundLinks: false });
  const errors = [];

  await validateEvidenceHandoffTemplateDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing inbound links from live adjacent routes: \/team-evidence-handoff\/, \/packets\/assembly\//);
});

async function createManagedFixture(t, options = {}) {
  const root = await createFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createFixture({
  templateHtml = evidenceHandoffTemplatePage(),
  discoveryRoutes = [evidenceHandoffTemplateRoute, ...evidenceHandoffTemplateRequiredLinks],
  includeInboundLinks = true,
  sitemapRoutes = [evidenceHandoffTemplateRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-evidence-handoff-template-test-"));
  const dist = join(root, "dist");
  const routes = new Set([evidenceHandoffTemplateRoute, ...evidenceHandoffTemplateRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    const body =
      route === evidenceHandoffTemplateRoute
        ? templateHtml
        : page(
            includeInboundLinks && evidenceHandoffTemplateInboundRoutes.includes(route)
              ? `<a href="${evidenceHandoffTemplateRoute}">template</a>`
              : route
          );
    await writeFile(join(path, "index.html"), body, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === evidenceHandoffTemplateRoute ? "Evidence Handoff Template" : `Route ${route}`,
    summary:
      route === evidenceHandoffTemplateRoute
        ? "Concept-level reusable template for carrying one TraceMap static-evidence question with proof path, rule context, limits, and next role."
        : "Fixture route for evidence handoff template validation.",
    publicClaimLevel: route === evidenceHandoffTemplateRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === evidenceHandoffTemplateRoute ? "use-case" : "evidence",
    ...(route === evidenceHandoffTemplateRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: ["Fixture limitations remain bounded."],
    nonClaims:
      route === evidenceHandoffTemplateRoute
        ? [
            "No runtime behavior, production traffic, endpoint performance, outage cause, release approval, release safety, operational safety, real organization ownership, complete coverage, AI impact analysis, LLM analysis, autonomous review, generated handoff feature, or replacement of human review."
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
  parsed.entries = parsed.entries.map((entry) => (entry.path === evidenceHandoffTemplateRoute ? { ...entry, ...fields } : entry));
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${routes.map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`).join("\n")}
</urlset>`;
}

function page(body) {
  return `<!doctype html><html><head>
    <title>Evidence Handoff Template | TraceMap</title>
    <link rel="canonical" href="https://tracemap.tools/handoff/template/">
    <meta name="description" content="Concept template">
    <meta property="og:type" content="article">
    <meta property="og:title" content="Evidence Handoff Template">
    <meta property="og:description" content="Concept template">
    <meta property="og:url" content="https://tracemap.tools/handoff/template/">
  </head><body><main>${body}</main></body></html>`;
}

function evidenceHandoffTemplatePage({ omittedLink = "", extraBody = "", fillerWords = 120 } = {}) {
  const linkHtml = evidenceHandoffTemplateRequiredLinks
    .filter((route) => route !== omittedLink)
    .map((route) => `<a href="${route}">${route}</a>`)
    .join("\n");
  const filler = Array.from({ length: fillerWords }, (_, index) => `bounded static evidence handoff ${index}`).join(" ");
  const fieldRows = [
    "handoff question",
    "audience",
    "proof path",
    "public claim level",
    "rule ID/family",
    "evidence tier",
    "coverage label",
    "public-safe path/span",
    "commit SHA",
    "extractor version",
    "limitation",
    "non-claim",
    "validation evidence",
    "owner to ask",
    "stop condition"
  ];

  return page(`
    <section id="when-to-use-it" data-context="when-to-use"><p>Public claim level: concept. No public conclusion without evidence. It is the receiver, while owner to ask is the role for the next open question.</p></section>
    <section id="neighbor-distinctions" data-context="neighbor-distinction">
      <p>Receiver-specific wording incident-adjacent static evidence transfer broader human workflow Reviewer onboarding without claiming real organization ownership not a final decision</p>
      ${linkHtml}
    </section>
    <section id="template"><table>${fieldRows.map((field) => `<tr data-handoff-field="${field}"><td>${field}</td></tr>`).join("")}</table><p>Tier1Semantic Tier2Structural Tier3SyntaxOrTextual Tier4Unknown</p></section>
    <section id="filled-synthetic-example" data-context="synthetic-example" data-filled-synthetic-example>
      ${fieldRows.map((field) => `<div><code>${field}</code><span>${field} synthetic value</span></div>`).join("")}
      <p>synthetic-sha-0001</p>
    </section>
    <section id="unsafe-example" data-context="unsafe-example" data-boundary-region><p>unsafe example</p></section>
    <section id="handoff-checklist" data-context="handoff-checklist">
      <p>handoff question audience public claim level proof path rule ID or family evidence tier coverage label limitation non-claim validation evidence owner to ask stop condition public-safe path/span commit SHA extractor version</p>
    </section>
    <section id="stop-conditions" data-context="stop-condition" data-boundary-region>
      <p>missing proof path private-only support raw or private material unknown or reduced coverage without label unsupported runtime proof wording unsupported release or safety wording unsupported complete-coverage wording AI or LLM analysis wording no validation evidence no owner to ask blame language</p>
    </section>
    <section id="non-claims" data-context="non-claim" data-boundary-region>
      <p>generate this handoff runtime behavior production traffic endpoint performance outage cause release approval release safety operational safety complete coverage AI impact analysis LLM analysis autonomous review replacement of human review real organization ownership source review ownership decisions telemetry logs traces APM tests release controls incident response service-owner judgment database-owner judgment security review compliance review manager judgment human judgment</p>
    </section>
    ${extraBody}
    <p>${filler}</p>
  `);
}
