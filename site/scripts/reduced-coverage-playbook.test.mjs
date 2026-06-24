import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  reducedCoveragePlaybookRequiredLinks,
  reducedCoveragePlaybookRoute,
  validateReducedCoveragePlaybookDist
} from "./reduced-coverage-playbook.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePagePath = resolve(scriptDir, "..", "src", "limitations", "reduced-coverage", "index.html");

test("validateReducedCoveragePlaybookDist accepts the reduced coverage playbook route", async (t) => {
  const root = await createManagedFixture(t);
  const errors = [];

  await validateReducedCoveragePlaybookDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateReducedCoveragePlaybookDist reports missing required rows", async (t) => {
  const source = await reducedCoveragePlaybookPage();
  const root = await createManagedFixture(t, {
    pageHtml: source.replace('data-reduced-coverage-row="unknown evidence tier"', 'data-reduced-coverage-row="mystery tier"')
  });
  const errors = [];

  await validateReducedCoveragePlaybookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unexpected row: mystery tier/);
  assert.match(errors.join("\n"), /missing required row: unknown evidence tier/);
});

test("validateReducedCoveragePlaybookDist reports invalid tier and marker values", async (t) => {
  const source = await reducedCoveragePlaybookPage();
  const root = await createManagedFixture(t, {
    pageHtml: source
      .replace("<code>Tier4Unknown</code> <code>private-only</code>", "<code>Tier5Guess</code> <code>internal</code>")
  });
  const errors = [];

  await validateReducedCoveragePlaybookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /invalid evidence tier: Tier5Guess/);
  assert.match(errors.join("\n"), /invalid supplementary marker: internal/);
  assert.match(errors.join("\n"), /private-only support" must use Tier4Unknown/);
});

test("validateReducedCoveragePlaybookDist reports route metadata regressions", async (t) => {
  const root = await createManagedFixture(t);
  await rewriteRouteEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateReducedCoveragePlaybookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory limitations, got evidence/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/limitations\/, got \/validation\//);
});

test("validateReducedCoveragePlaybookDist rejects forbidden claims outside bounded contexts", async (t) => {
  const source = await reducedCoveragePlaybookPage();
  const root = await createManagedFixture(t, {
    pageHtml: source.replace("</main>", "<p>TraceMap proves runtime behavior and complete coverage.</p></main>")
  });
  const errors = [];

  await validateReducedCoveragePlaybookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden claim wording outside bounded contexts/);
});

test("validateReducedCoveragePlaybookDist permits bounded rejected and non-claim wording", async (t) => {
  const source = await reducedCoveragePlaybookPage();
  const root = await createManagedFixture(t, {
    pageHtml: source.replace(
      "</main>",
      '<section data-reduced-coverage-boundary="rejected-patterns"><p>Rejected pattern: TraceMap proves runtime behavior, publishes raw facts, and replaces human review.</p></section></main>'
    )
  });
  const errors = [];

  await validateReducedCoveragePlaybookDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateReducedCoveragePlaybookDist rejects raw material outside bounded contexts", async (t) => {
  const source = await reducedCoveragePlaybookPage();
  const root = await createManagedFixture(t, {
    pageHtml: source.replace("</main>", "<p>Publish raw facts and analyzer logs in this public page.</p></main>")
  });
  const errors = [];

  await validateReducedCoveragePlaybookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden raw\/private material outside bounded contexts/);
});

test("validateReducedCoveragePlaybookDist rejects hard private material in attributes", async (t) => {
  const source = await reducedCoveragePlaybookPage();
  const root = await createManagedFixture(t, {
    pageHtml: source.replace("</main>", '<img alt="file&#58;//private/report"></main>')
  });
  const errors = [];

  await validateReducedCoveragePlaybookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /hard private material/);
});

test("validateReducedCoveragePlaybookDist reports missing adjacent route distinctions", async (t) => {
  const source = await reducedCoveragePlaybookPage();
  const root = await createManagedFixture(t, {
    pageHtml: source.replace("Runtime question routing", "Runtime page")
  });
  const errors = [];

  await validateReducedCoveragePlaybookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /adjacent surface distinction is missing: \/static-vs-runtime\//);
});

test("validateReducedCoveragePlaybookDist reports missing inbound link from limitations", async (t) => {
  const root = await createManagedFixture(t, {
    includeInboundLink: false
  });
  const errors = [];

  await validateReducedCoveragePlaybookDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing inbound links from: \/limitations\//);
});

async function createManagedFixture(t, options = {}) {
  const root = await createFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createFixture({
  includeInboundLink = true,
  pageHtml,
  routes = [reducedCoveragePlaybookRoute, ...reducedCoveragePlaybookRequiredLinks]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-reduced-coverage-test-"));
  const dist = join(root, "dist");
  const routeSet = new Set(routes);
  routeSet.add("/limitations/");

  for (const route of routeSet) {
    const directory = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(directory, { recursive: true });
    const body =
      route === reducedCoveragePlaybookRoute
        ? pageHtml ?? (await reducedCoveragePlaybookPage())
        : page(route === "/limitations/" && includeInboundLink ? `<a href="${reducedCoveragePlaybookRoute}">Reduced coverage playbook</a>` : route);
    await writeFile(join(directory, "index.html"), body, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap([reducedCoveragePlaybookRoute]), "utf8");
  await writeDiscoveryFiles(dist, [...routeSet]);

  return root;
}

async function reducedCoveragePlaybookPage() {
  return readFile(sourcePagePath, "utf8");
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) =>
    route === reducedCoveragePlaybookRoute
      ? reducedCoveragePlaybookEntry()
      : {
          path: route,
          title: `Route ${route}`,
          summary: "Fixture route for internal link validation.",
          publicClaimLevel: "concept",
          sourceType: "site-page",
          hintCategory: "limitations",
          limitations: ["Fixture page only."],
          nonClaims: ["No runtime behavior proof."]
        }
  );
  const outputs = await createDiscoveryOutputs(entries);

  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
}

function reducedCoveragePlaybookEntry() {
  return {
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
  };
}

async function rewriteRouteEntry(dist, patch) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  const entry = parsed.entries.find((candidate) => candidate.path === reducedCoveragePlaybookRoute);
  Object.assign(entry, patch);
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

function page(body) {
  return `<!doctype html><html><head><title>Fixture</title></head><body>${body}</body></html>`;
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${routes.map((route) => `  <url><loc>https://tracemap.tools${route}</loc></url>`).join("\n")}
</urlset>
`;
}
