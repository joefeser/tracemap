import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  evidenceDecisionRecordRequiredLinks,
  evidenceDecisionRecordRoute,
  validateEvidenceDecisionRecordDist
} from "./evidence-decision-record.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const siteRoot = join(scriptDir, "..");

test("validateEvidenceDecisionRecordDist accepts the canonical evidence decision record route", async (t) => {
  const root = await createManagedEvidenceDecisionRecordFixture(t);
  const errors = [];

  await validateEvidenceDecisionRecordDist({ dist: join(root, "site", "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateEvidenceDecisionRecordDist reports missing required fields", async (t) => {
  const root = await createManagedEvidenceDecisionRecordFixture(t, {
    pageHtml: (await canonicalPage()).replaceAll('data-record-field="commit SHA"', 'data-record-field="commit context"')
  });
  const errors = [];

  await validateEvidenceDecisionRecordDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /template is missing required field: commit SHA/);
});

test("validateEvidenceDecisionRecordDist reports route metadata regressions", async (t) => {
  const root = await createManagedEvidenceDecisionRecordFixture(t);
  await rewriteRouteEntry(join(root, "site", "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/",
    nonClaims: ["No runtime proof."]
  });
  const errors = [];

  await validateEvidenceDecisionRecordDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
  assert.match(errors.join("\n"), /nonClaims are missing required term: autonomous decision/);
});

test("validateEvidenceDecisionRecordDist reports missing required adjacent link", async (t) => {
  const root = await createManagedEvidenceDecisionRecordFixture(t, {
    pageHtml: (await canonicalPage()).replaceAll('href="/review-room/"', 'href="/review-room-missing/"')
  });
  const errors = [];

  await validateEvidenceDecisionRecordDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/review-room\//);
});

test("validateEvidenceDecisionRecordDist rejects forbidden positive claims outside allowed contexts", async (t) => {
  const root = await createManagedEvidenceDecisionRecordFixture(t, {
    pageHtml: (await canonicalPage()).replace("</main>", "<p>TraceMap approves release decisions.</p></main>")
  });
  const errors = [];

  await validateEvidenceDecisionRecordDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /forbidden positive claim/);
});

test("validateEvidenceDecisionRecordDist rejects mislabeled unsafe-example wrappers outside allowed sections", async (t) => {
  const root = await createManagedEvidenceDecisionRecordFixture(t, {
    pageHtml: (await canonicalPage()).replace(
      '<section class="section boundary-section" id="unsafe-record-examples"',
      '<div data-tracemap-validation-context="unsafe-example">TraceMap approves release decisions.</div><section class="section boundary-section" id="unsafe-record-examples"'
    )
  });
  const errors = [];

  await validateEvidenceDecisionRecordDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /forbidden positive claim/);
});

test("validateEvidenceDecisionRecordDist rejects raw material outside allowed contexts", async (t) => {
  const root = await createManagedEvidenceDecisionRecordFixture(t, {
    pageHtml: (await canonicalPage()).replace("</main>", "<p>Share raw facts in the public record.</p></main>")
  });
  const errors = [];

  await validateEvidenceDecisionRecordDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /forbidden raw\/private material/);
});

test("validateEvidenceDecisionRecordDist rejects hard private text even in attributes", async (t) => {
  const root = await createManagedEvidenceDecisionRecordFixture(t, {
    pageHtml: (await canonicalPage()).replace("</main>", '<img alt="file&#58;//private/report"></main>')
  });
  const errors = [];

  await validateEvidenceDecisionRecordDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /hard private material/);
});

test("validateEvidenceDecisionRecordDist rejects real-looking commit SHA examples", async (t) => {
  const root = await createManagedEvidenceDecisionRecordFixture(t, {
    pageHtml: (await canonicalPage()).replace("example-public-sha", "0123456789abcdef0123456789abcdef01234567")
  });
  const errors = [];

  await validateEvidenceDecisionRecordDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /must not publish a real-looking 40-character commit SHA/);
});

test("validateEvidenceDecisionRecordDist reports missing placement decision state", async (t) => {
  const root = await createManagedEvidenceDecisionRecordFixture(t, {
    implementationState: "Selected placement pending."
  });
  const errors = [];

  await validateEvidenceDecisionRecordDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /implementation-state is missing placement record phrase/);
});

test("validateEvidenceDecisionRecordDist reports word count outside bounds", async (t) => {
  const root = await createManagedEvidenceDecisionRecordFixture(t, {
    pageHtml: (await canonicalPage()).replace(/<main>[\s\S]*?<\/main>/, "<main><p>Public claim level: concept. No public conclusion without evidence. TraceMap provides evidence, not the decision.</p></main>")
  });
  const errors = [];

  await validateEvidenceDecisionRecordDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /word count must be between 700 and 2500 words/);
});

async function createManagedEvidenceDecisionRecordFixture(t, options = {}) {
  const root = await createEvidenceDecisionRecordFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createEvidenceDecisionRecordFixture({
  pageHtml,
  discoveryRoutes = [evidenceDecisionRecordRoute, ...evidenceDecisionRecordRequiredLinks],
  includeInboundLinks = true,
  implementationState = canonicalImplementationState()
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-evidence-decision-record-"));
  const dist = join(root, "site", "dist");
  await mkdir(dist, { recursive: true });

  const routes = new Set([evidenceDecisionRecordRoute, ...evidenceDecisionRecordRequiredLinks]);
  for (const route of routes) {
    const routeDir = join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(routeDir, { recursive: true });
    await writeFile(
      join(routeDir, "index.html"),
      route === evidenceDecisionRecordRoute
        ? pageHtml ?? (await canonicalPage())
        : adjacentPage(route, includeInboundLinks && ["/review-room/", "/packets/assembly/"].includes(route)),
      "utf8"
    );
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap([evidenceDecisionRecordRoute, ...evidenceDecisionRecordRequiredLinks]), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);
  await writeImplementationState(root, implementationState);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === evidenceDecisionRecordRoute ? "Evidence Decision Record" : `Route ${route}`,
    summary:
      route === evidenceDecisionRecordRoute
        ? "Concept-level template for documenting a human owner decision after TraceMap evidence review while preserving proof path, limitation, follow-up, and residual risk."
        : "Supporting public-safe route for evidence decision record validation.",
    publicClaimLevel: route === evidenceDecisionRecordRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === evidenceDecisionRecordRoute ? "use-case" : "evidence",
    preferredProofPath: "/proof-paths/",
    limitations:
      route === evidenceDecisionRecordRoute
        ? [
            "The route is a record template over existing public-safe evidence surfaces, not a new proof source, workflow engine, or authority system.",
            "Every record must keep the proof path, rule ID or family, evidence tier, coverage label, limitation, non-claim, follow-up owner, and residual risk attached."
          ]
        : ["Supporting route fixture."],
    nonClaims:
      route === evidenceDecisionRecordRoute
        ? [
            "No autonomous decision, approval workflow, release approval, release safety, operational safety, runtime proof, production proof, endpoint performance proof, outage cause, absence-of-impact proof, complete coverage, AI analysis, LLM analysis, embeddings, vector databases, or prompt classification.",
            "No replacement of tests, code review, source review, runtime observability, telemetry, release process, service-owner review, governance, or human judgment.",
            "No raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values are public record material."
          ]
        : ["No runtime proof."]
  }));

  const outputs = await createDiscoveryOutputs(entries, {
    dist,
    resolveInternalPaths: true
  });
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
}

async function rewriteRouteEntry(dist, patch) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  const entry = parsed.entries.find((candidate) => candidate.path === evidenceDecisionRecordRoute);
  Object.assign(entry, patch);
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

async function writeImplementationState(root, text) {
  const statePath = join(root, ".kiro", "specs", "site-tracemap-tools-evidence-decision-record", "implementation-state.md");
  await mkdir(dirname(statePath), { recursive: true });
  await writeFile(statePath, text, "utf8");
}

async function canonicalPage() {
  return readFile(join(siteRoot, "src", "decisions", "evidence-record", "index.html"), "utf8");
}

function adjacentPage(route, includeInboundLink) {
  return `<!doctype html>
<html><head><title>${route}</title></head><body><main>
<p>Adjacent route fixture for ${route}.</p>
${includeInboundLink ? `<a href="${evidenceDecisionRecordRoute}">Evidence decision record</a>` : ""}
</main></body></html>`;
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${routes.map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`).join("\n")}
</urlset>
`;
}

function canonicalImplementationState() {
  return `# Site TraceMap Tools Evidence Decision Record Implementation State

Selected placement: \`/decisions/evidence-record/\`

Rejected alternatives:
- \`/review-room/decision-record/\` because the record needs a durable standalone public address without making the review-room namespace carry a post-review artifact.
- section on \`/review-room/\` because the review room remains a review-room agenda.
- section on \`/packets/assembly/\` because packet assembly remains a packet assembly checklist.

The selected route is a decision-after-evidence record. It is not a claim checklist, manager packet, objection guide, proof-path tour, release gate, runtime workflow, approval workflow, or autonomous decision system.
`;
}
