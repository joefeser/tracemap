import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  stakeholderQuestionIndexRequiredLinks,
  stakeholderQuestionIndexRoute,
  validateStakeholderQuestionIndexDist
} from "./stakeholder-question-index.mjs";

test("validateStakeholderQuestionIndexDist accepts the stakeholder question route", async (t) => {
  const root = await createManagedQuestionIndexDistFixture(t);
  const errors = [];

  await validateStakeholderQuestionIndexDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateStakeholderQuestionIndexDist reports missing route metadata", async (t) => {
  const root = await createManagedQuestionIndexDistFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateStakeholderQuestionIndexDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/questions\//);
});

test("validateStakeholderQuestionIndexDist reports route metadata regressions", async (t) => {
  const root = await createManagedQuestionIndexDistFixture(t);
  await rewriteQuestionIndexRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateStakeholderQuestionIndexDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
});

test("validateStakeholderQuestionIndexDist reports missing required row fields", async (t) => {
  const root = await createManagedQuestionIndexDistFixture(t, {
    questionHtml: questionIndexPage({
      rowOverrides: {
        "manager-planning": { omitField: "safeAnswerShape" }
      }
    })
  });
  const errors = [];

  await validateStakeholderQuestionIndexDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /manager-planning is missing required field: safeAnswerShape/);
});

test("validateStakeholderQuestionIndexDist requires rule ID or family cells", async (t) => {
  const root = await createManagedQuestionIndexDistFixture(t, {
    questionHtml: questionIndexPage({
      rowOverrides: {
        "agent-bot-discovery": { omitField: "ruleIdOrFamily" }
      }
    })
  });
  const errors = [];

  await validateStakeholderQuestionIndexDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /agent-bot-discovery is missing required field: ruleIdOrFamily/);
});

test("validateStakeholderQuestionIndexDist allows bounded non-claims but rejects unbounded AI wording", async (t) => {
  const root = await createManagedQuestionIndexDistFixture(t, {
    questionHtml: questionIndexPage({ extra: "<p>TraceMap performs AI impact analysis.</p>" })
  });
  const errors = [];

  await validateStakeholderQuestionIndexDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden unbounded claim wording: AI impact analysis/);
});

test("validateStakeholderQuestionIndexDist rejects direct raw-artifact proof links", async (t) => {
  const root = await createManagedQuestionIndexDistFixture(t, {
    questionHtml: questionIndexPage({ extra: '<a href="/facts.ndjson">bad proof</a>' })
  });
  const errors = [];

  await validateStakeholderQuestionIndexDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /links directly to forbidden proof target: \/facts\.ndjson/);
});

async function createManagedQuestionIndexDistFixture(t, options = {}) {
  const root = await createQuestionIndexDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createQuestionIndexDistFixture({
  discoveryRoutes = [stakeholderQuestionIndexRoute],
  questionHtml = questionIndexPage(),
  sitemapRoutes = [stakeholderQuestionIndexRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-question-index-test-"));
  const dist = join(root, "dist");
  const routes = new Set([stakeholderQuestionIndexRoute, ...stakeholderQuestionIndexRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(join(path, "index.html"), route === stakeholderQuestionIndexRoute ? questionHtml : page(route), "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: `Route ${route}`,
    summary: "Concept-level orientation route from stakeholder questions to public-safe proof paths.",
    publicClaimLevel: route === stakeholderQuestionIndexRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === stakeholderQuestionIndexRoute ? "use-case" : "evidence",
    ...(route === stakeholderQuestionIndexRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: [
      "Rows preserve route-specific proof paths, rule IDs or rule families, evidence tiers, coverage labels, limitations, and non-claims."
    ],
    nonClaims:
      route === stakeholderQuestionIndexRoute
        ? [
            "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, private-repo behavior, or complete product coverage proof.",
            "No AI impact analysis, LLM analysis, prompt-based classification, raw facts, raw SQLite, analyzer logs, source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, or hidden validation details are published."
          ]
        : ["No runtime behavior proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteQuestionIndexRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === stakeholderQuestionIndexRoute
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

function questionIndexPage({ extra = "", rowOverrides = {} } = {}) {
  const filler = Array.from({ length: 60 }, (_, index) => `evidence question boundary ${index}`).join(" ");
  return page(`
    <p>Public claim level: concept</p>
    <p>No public conclusion without evidence</p>
    <p>This page is an orientation index.</p>
    <p>Start with the question and route the answer to a public-safe proof path.</p>
    <p>Target-route claim levels remain separate from row claim levels.</p>
    <p>Keep the rule ID or rule family, evidence tier, coverage label, limitation, and non-claim attached.</p>
    ${stakeholderQuestionIndexRequiredLinks.map((route) => `<a href="${route}">${route}</a>`).join("\n")}
    <table data-stakeholder-question-index>
      <thead><tr><th scope="col">Audience</th><th scope="col">Question</th></tr></thead>
      <tbody>
        ${questionRows(rowOverrides)}
      </tbody>
    </table>
    <section id="non-claims">
      <p>TraceMap does not perform AI impact analysis, LLM analysis, prompt-based classification, embedding search, or vector database analysis.</p>
      <p>Agents and reviewers must not repeat a row after dropping its proof path.</p>
    </section>
    <p>${filler}</p>
    ${extra}
  `);
}

function questionRows(overrides) {
  const families = [
    "manager-planning",
    "engineer-endpoint-change-review",
    "incident-adjacent-handoff",
    "modernization-planning",
    "reviewer-claim-checking",
    "demo-evaluation",
    "proof-source-inspection",
    "agent-bot-discovery"
  ];

  return families.map((family) => questionRow(family, overrides[family] ?? {})).join("\n");
}

function questionRow(family, { omitField } = {}) {
  const fields = {
    audience: "Managers and reviewers",
    question: "Which bounded proof path should answer this stakeholder question?",
    safeAnswerShape: "Inspect, compare, follow, review, check, route to, record, and escalate.",
    targetRoute: '<a href="/proof-paths/">/proof-paths/</a>',
    evidenceSurface: "Proof paths, validation, limitations, and public-safe route metadata.",
    publicClaimLevel: "<code>concept</code>",
    proofPath: '<a href="/proof-paths/">proof paths</a>',
    ruleIdOrFamily: "Rule family plus limitation.",
    limitation: "Does not prove runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, or complete product coverage.",
    nonClaim: "Does not replace managers, service owners, architects, tests, telemetry, logs, traces, code review, incident command, release review, or human judgment."
  };

  const cells = Object.entries(fields)
    .filter(([field]) => field !== omitField)
    .map(([field, value]) => `<td data-field="${field}">${value}</td>`)
    .join("");

  return `<tr id="question-${family}" data-question-row data-question-family="${family}">${cells}</tr>`;
}

function page(body) {
  return `<!doctype html><html><body><main>${body}</main></body></html>`;
}
