import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  evidenceGapRegisterRequiredLinks,
  evidenceGapRegisterRoute,
  validateEvidenceGapRegisterDist
} from "./evidence-gap-register.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const siteRoot = join(scriptDir, "..");

test("validateEvidenceGapRegisterDist accepts the canonical evidence gap register route", async (t) => {
  const root = await createManagedEvidenceGapRegisterFixture(t);
  const errors = [];

  await validateEvidenceGapRegisterDist({ dist: join(root, "site", "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateEvidenceGapRegisterDist reports missing required rows", async (t) => {
  const root = await createManagedEvidenceGapRegisterFixture(t, {
    pageHtml: (await canonicalPage()).replace('data-evidence-gap-row="Tier4Unknown"', 'data-evidence-gap-row="unknown tier"')
  });
  const errors = [];

  await validateEvidenceGapRegisterDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /unexpected row: unknown tier/);
  assert.match(errors.join("\n"), /missing required row: Tier4Unknown/);
});

test("validateEvidenceGapRegisterDist reports missing required row fields", async (t) => {
  const root = await createManagedEvidenceGapRegisterFixture(t, {
    pageHtml: (await canonicalPage()).replaceAll('data-field="stop condition"', 'data-field="stop"')
  });
  const errors = [];

  await validateEvidenceGapRegisterDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /missing required field: stop condition/);
});

test("validateEvidenceGapRegisterDist reports route metadata regressions", async (t) => {
  const root = await createManagedEvidenceGapRegisterFixture(t);
  await rewriteRouteEntry(join(root, "site", "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "limitations",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/",
    summary: "A gap register."
  });
  const errors = [];

  await validateEvidenceGapRegisterDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory evidence, got limitations/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/review-claim-checklist\/, got \/validation\//);
  assert.match(errors.join("\n"), /discovery metadata is missing required term: missing/);
});

test("validateEvidenceGapRegisterDist reports missing adjacent links", async (t) => {
  const root = await createManagedEvidenceGapRegisterFixture(t, {
    pageHtml: (await canonicalPage()).replaceAll('href="/owners/follow-up/"', 'href="/owners/missing/"')
  });
  const errors = [];

  await validateEvidenceGapRegisterDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /adjacent surface link is missing: \/owners\/follow-up\//);
  assert.match(errors.join("\n"), /unsupported proof\/validation route: \/owners\/missing\//);
});

test("validateEvidenceGapRegisterDist rejects forbidden claims outside bounded contexts", async (t) => {
  const root = await createManagedEvidenceGapRegisterFixture(t, {
    pageHtml: (await canonicalPage()).replace("</main>", "<p>TraceMap proves runtime behavior and validates release readiness.</p></main>")
  });
  const errors = [];

  await validateEvidenceGapRegisterDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /forbidden claim wording outside bounded contexts/);
});

test("validateEvidenceGapRegisterDist permits bounded rejected wording", async (t) => {
  const root = await createManagedEvidenceGapRegisterFixture(t, {
    pageHtml: (await canonicalPage()).replace(
      "</main>",
      '<section data-evidence-gap-boundary="rejected-patterns"><p>Rejected pattern: TraceMap proves runtime behavior and replaces human review.</p></section></main>'
    )
  });
  const errors = [];

  await validateEvidenceGapRegisterDist({ dist: join(root, "site", "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateEvidenceGapRegisterDist rejects raw material outside bounded contexts", async (t) => {
  const root = await createManagedEvidenceGapRegisterFixture(t, {
    pageHtml: (await canonicalPage()).replace("</main>", "<p>Publish raw facts and analyzer logs in the public register.</p></main>")
  });
  const errors = [];

  await validateEvidenceGapRegisterDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /forbidden raw\/private material outside bounded contexts/);
});

test("validateEvidenceGapRegisterDist rejects hard private text in attributes", async (t) => {
  const root = await createManagedEvidenceGapRegisterFixture(t, {
    pageHtml: (await canonicalPage()).replace("</main>", '<img alt="file&#58;//private/report"></main>')
  });
  const errors = [];

  await validateEvidenceGapRegisterDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /hard private material/);
});

test("validateEvidenceGapRegisterDist reports missing placement state", async (t) => {
  const root = await createManagedEvidenceGapRegisterFixture(t, {
    implementationState: "Selected placement pending."
  });
  const errors = [];

  await validateEvidenceGapRegisterDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /implementation-state is missing phrase/);
});

test("validateEvidenceGapRegisterDist reports word count outside bounds", async (t) => {
  const root = await createManagedEvidenceGapRegisterFixture(t, {
    pageHtml: (await canonicalPage()).replace(
      /<main>[\s\S]*?<\/main>/,
      "<main><p>Public claim level: concept. No public conclusion without evidence. A bounded follow-up item.</p></main>"
    )
  });
  const errors = [];

  await validateEvidenceGapRegisterDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /visible prose word count must be between 900 and 1700 words/);
});

async function createManagedEvidenceGapRegisterFixture(t, options = {}) {
  const root = await createEvidenceGapRegisterFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createEvidenceGapRegisterFixture({
  pageHtml,
  discoveryRoutes = [evidenceGapRegisterRoute, ...evidenceGapRegisterRequiredLinks],
  includeInboundLinks = true,
  implementationState = canonicalImplementationState()
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-evidence-gap-register-"));
  const dist = join(root, "site", "dist");
  await mkdir(dist, { recursive: true });

  const routes = new Set([
    evidenceGapRegisterRoute,
    ...evidenceGapRegisterRequiredLinks,
    "/evidence/",
    "/proof-paths/"
  ]);

  for (const route of routes) {
    const routeDir = join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(routeDir, { recursive: true });
    await writeFile(
      join(routeDir, "index.html"),
      route === evidenceGapRegisterRoute
        ? pageHtml ?? (await canonicalPage())
        : adjacentPage(route, includeInboundLinks && ["/evidence/", "/limitations/reduced-coverage/"].includes(route)),
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
    title: route === evidenceGapRegisterRoute ? "Evidence Gap Register" : `Route ${route}`,
    summary:
      route === evidenceGapRegisterRoute
        ? "Concept-level register for recording missing, reduced, stale, private-only, unsupported, unknown, validation, and owner-question evidence gaps as bounded follow-up rows."
        : "Supporting public-safe route for evidence gap register validation.",
    publicClaimLevel: route === evidenceGapRegisterRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === evidenceGapRegisterRoute ? "evidence" : "use-case",
    preferredProofPath: route === evidenceGapRegisterRoute ? "/review-claim-checklist/" : "/proof-paths/",
    limitations:
      route === evidenceGapRegisterRoute
        ? [
            "The register records follow-up rows and stop conditions; it is not scanner output, reducer output, validation success, or a public proof source.",
            "Gap rows must keep what evidence exists, what cannot be concluded, next owner, proof or validation route, safe wording, and stop condition attached."
          ]
        : ["Supporting route fixture."],
    nonClaims:
      route === evidenceGapRegisterRoute
        ? [
            "No absence-of-impact proof, runtime behavior proof, production traffic proof, endpoint performance proof, outage-cause proof, release approval, release readiness, operational certainty, clean-repo status, complete coverage, AI analysis, LLM analysis, embeddings, vector databases, prompt classification, autonomous approval, or replacement of human review.",
            "No raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, or credential-like values are public gap-register material."
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
  const entry = parsed.entries.find((candidate) => candidate.path === evidenceGapRegisterRoute);
  Object.assign(entry, patch);
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

async function writeImplementationState(root, text) {
  const statePath = join(root, ".kiro", "specs", "site-tracemap-tools-evidence-gap-register", "implementation-state.md");
  await mkdir(dirname(statePath), { recursive: true });
  await writeFile(statePath, text, "utf8");
}

async function canonicalPage() {
  return readFile(join(siteRoot, "src", "evidence", "gaps", "index.html"), "utf8");
}

function adjacentPage(route, includeInboundLink) {
  return `<!doctype html>
<html><head><title>${route}</title></head><body><main>
<p>Adjacent route fixture for ${route}.</p>
${includeInboundLink ? `<a href="${evidenceGapRegisterRoute}">Evidence gap register</a>` : ""}
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
  return `# Site TraceMap Tools Evidence Gap Register Implementation State

Selected placement: standalone route \`/evidence/gaps/\`

Rejected alternatives:

- \`/coverage/gaps/\`

Adjacent route inventory before site edits:

- \`/limitations/reduced-coverage/\`: present; linked directly.

Rejected-pattern marker: use \`data-evidence-gap-boundary="rejected-patterns"\`

No adjacent route substitutions, omissions, or deferrals are needed.

Discovery artifacts for validation: sitemap, routes-index, and llms.txt.
`;
}
