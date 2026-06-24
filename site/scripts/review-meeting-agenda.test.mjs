import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  reviewMeetingAgendaRequiredLinks,
  reviewMeetingAgendaRoute,
  validateReviewMeetingAgendaDist
} from "./review-meeting-agenda.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const siteRoot = join(scriptDir, "..");

test("validateReviewMeetingAgendaDist accepts the canonical review meeting agenda route", async (t) => {
  const root = await createManagedReviewMeetingAgendaFixture(t);
  const errors = [];

  await validateReviewMeetingAgendaDist({ dist: join(root, "site", "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateReviewMeetingAgendaDist reports missing required agenda rows", async (t) => {
  const root = await createManagedReviewMeetingAgendaFixture(t, {
    pageHtml: (await canonicalPage()).replace('data-review-agenda-row="closeout"', 'data-review-agenda-row="wrapup"')
  });
  const errors = [];

  await validateReviewMeetingAgendaDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /unexpected agenda row: wrapup/);
  assert.match(errors.join("\n"), /missing required agenda row: closeout/);
});

test("validateReviewMeetingAgendaDist reports missing agenda row fields", async (t) => {
  const root = await createManagedReviewMeetingAgendaFixture(t, {
    pageHtml: (await canonicalPage()).replaceAll('data-field="evidence input"', 'data-field="input"')
  });
  const errors = [];

  await validateReviewMeetingAgendaDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /missing required field: evidence input/);
});

test("validateReviewMeetingAgendaDist reports route metadata regressions", async (t) => {
  const root = await createManagedReviewMeetingAgendaFixture(t);
  await rewriteRouteEntry(join(root, "site", "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/",
    summary: "A meeting agenda."
  });
  const errors = [];

  await validateReviewMeetingAgendaDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
  assert.match(errors.join("\n"), /discovery metadata is missing required term: concept/);
});

test("validateReviewMeetingAgendaDist reports missing required adjacent links", async (t) => {
  const root = await createManagedReviewMeetingAgendaFixture(t, {
    pageHtml: (await canonicalPage()).replaceAll('href="/owners/follow-up/"', 'href="/owners/missing/"')
  });
  const errors = [];

  await validateReviewMeetingAgendaDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/owners\/follow-up\//);
});

test("validateReviewMeetingAgendaDist rejects forbidden positive claims outside boundaries", async (t) => {
  const root = await createManagedReviewMeetingAgendaFixture(t, {
    pageHtml: (await canonicalPage()).replace("</main>", "<p>TraceMap proves runtime behavior and approves the release.</p></main>")
  });
  const errors = [];

  await validateReviewMeetingAgendaDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /forbidden positive claim outside bounded contexts/);
});

test("validateReviewMeetingAgendaDist permits bounded non-claim wording", async (t) => {
  const root = await createManagedReviewMeetingAgendaFixture(t, {
    pageHtml: (await canonicalPage()).replace(
      "</main>",
      '<section data-review-agenda-boundary="non-claims"><p>The agenda does not prove runtime behavior or replace human judgment.</p></section></main>'
    )
  });
  const errors = [];

  await validateReviewMeetingAgendaDist({ dist: join(root, "site", "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateReviewMeetingAgendaDist rejects raw material outside boundaries", async (t) => {
  const root = await createManagedReviewMeetingAgendaFixture(t, {
    pageHtml: (await canonicalPage()).replace("</main>", "<p>Publish raw facts and analyzer logs in the meeting agenda.</p></main>")
  });
  const errors = [];

  await validateReviewMeetingAgendaDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /forbidden raw\/private material outside bounded contexts/);
});

test("validateReviewMeetingAgendaDist rejects unsupported certainty language outside boundaries", async (t) => {
  const root = await createManagedReviewMeetingAgendaFixture(t, {
    pageHtml: (await canonicalPage()).replace("</main>", "<p>The agenda says the feature is safe.</p></main>")
  });
  const errors = [];

  await validateReviewMeetingAgendaDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /unsupported certainty language/);
});

test("validateReviewMeetingAgendaDist reports missing implementation state", async (t) => {
  const root = await createManagedReviewMeetingAgendaFixture(t, {
    implementationState: "Selected placement pending."
  });
  const errors = [];

  await validateReviewMeetingAgendaDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /implementation-state is missing phrase/);
});

test("validateReviewMeetingAgendaDist reports word count outside bounds", async (t) => {
  const root = await createManagedReviewMeetingAgendaFixture(t, {
    pageHtml: (await canonicalPage()).replace(
      /<main>[\s\S]*?<\/main>/,
      "<main><p>Public claim level: concept. No public conclusion without evidence. review question proof path rule ID or rule family evidence tier coverage label limitation gap next owner decision record non-claims.</p></main>"
    )
  });
  const errors = [];

  await validateReviewMeetingAgendaDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /visible prose word count must be between 700 and 1500 words/);
});

async function createManagedReviewMeetingAgendaFixture(t, options = {}) {
  const root = await createReviewMeetingAgendaFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createReviewMeetingAgendaFixture({
  pageHtml,
  discoveryRoutes = [reviewMeetingAgendaRoute, ...reviewMeetingAgendaRequiredLinks],
  implementationState = canonicalImplementationState(),
  includeInboundLink = true
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-review-meeting-agenda-"));
  const dist = join(root, "site", "dist");
  await mkdir(dist, { recursive: true });

  const routes = new Set([reviewMeetingAgendaRoute, ...reviewMeetingAgendaRequiredLinks]);
  for (const route of routes) {
    const routeDir = join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(routeDir, { recursive: true });
    await writeFile(
      join(routeDir, "index.html"),
      route === reviewMeetingAgendaRoute
        ? pageHtml ?? (await canonicalPage())
        : adjacentPage(route, includeInboundLink && route === "/review-room/"),
      "utf8"
    );
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap([...routes]), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);
  await writeImplementationState(root, implementationState);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === reviewMeetingAgendaRoute ? "Evidence Review Meeting Agenda" : `Route ${route}`,
    summary:
      route === reviewMeetingAgendaRoute
        ? "Concept-level meeting agenda for checking TraceMap proof paths, evidence tiers, coverage labels, limitations, gaps, owners, and decision-record handoff."
        : "Supporting public-safe route for review meeting agenda validation.",
    publicClaimLevel: route === reviewMeetingAgendaRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === reviewMeetingAgendaRoute ? "use-case" : "evidence",
    preferredProofPath: route === reviewMeetingAgendaRoute ? "/proof-paths/" : "/validation/",
    limitations:
      route === reviewMeetingAgendaRoute
        ? [
            "The route is a human meeting agenda over existing public-safe static evidence surfaces, not meeting automation or a new proof source.",
            "The agenda preserves review questions, proof paths, rule context, evidence tiers, coverage labels, limitations, gaps, owners, validation evidence category, and non-claims without upgrading missing evidence."
          ]
        : ["Fixture route exists only to resolve review meeting agenda links."],
    nonClaims:
      route === reviewMeetingAgendaRoute
        ? [
            "No meeting automation, release approval, release safety, operational safety, runtime proof, production traffic proof, endpoint performance proof, absence-of-impact proof, complete coverage, AI analysis, LLM analysis, embeddings, vector databases, prompt classification, automated impact analysis, or replacement of human judgment or governance.",
            "No raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, credential-like values, connection strings, tokens, or keys are public agenda material."
          ]
        : ["Fixture route is not public proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteRouteEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === reviewMeetingAgendaRoute
      ? {
          ...entry,
          ...fields
        }
      : entry
  );
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

async function canonicalPage() {
  return readFile(join(siteRoot, "src", "review-room", "agenda", "index.html"), "utf8");
}

function adjacentPage(route, includeInboundLink) {
  const inbound = includeInboundLink ? `<a href="${reviewMeetingAgendaRoute}">Evidence review meeting agenda</a>` : "";
  return `<!doctype html><html><head><meta property="og:type" content="article"></head><body><main><h1>${route}</h1>${inbound}</main></body></html>`;
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/0.9">
${routes.map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`).join("\n")}
</urlset>`;
}

async function writeImplementationState(root, text) {
  const dir = join(root, ".kiro", "specs", "site-tracemap-tools-review-meeting-agenda");
  await mkdir(dir, { recursive: true });
  await writeFile(join(dir, "implementation-state.md"), text, "utf8");
}

function canonicalImplementationState() {
  return [
    "Selected placement: `/review-room/agenda/`",
    "Rejected alternative: `/meetings/evidence-review/`",
    "Rejected alternative: section on `/review-room/`",
    "Rejected alternative: section on `/reviewer-quickstart/`",
    "Primary navigation remains unchanged.",
    "Word-count bounds: 700 to 1500 rendered main-content words",
    "Manual public-safety reviewer signoff: completed by implementation owner"
  ].join("\n");
}
