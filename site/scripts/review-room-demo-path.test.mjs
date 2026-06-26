import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  reviewRoomDemoPathRequiredLinks,
  reviewRoomDemoPathRoute,
  validateReviewRoomDemoPathDist
} from "./review-room-demo-path.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const siteRoot = join(scriptDir, "..");

test("validateReviewRoomDemoPathDist accepts the canonical route", async (t) => {
  const root = await createManagedReviewRoomDemoPathFixture(t);
  const errors = [];

  await validateReviewRoomDemoPathDist({ dist: join(root, "site", "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateReviewRoomDemoPathDist accepts spaced attributes and reordered metadata", async (t) => {
  const page = (await canonicalPage())
    .replace(
      '<link rel="canonical" href="https://tracemap.tools/review-room/demo-path/">',
      '<link href="https://tracemap.tools/review-room/demo-path/" rel = "canonical">'
    )
    .replace('<meta property="og:type" content="article">', '<meta content = "article" property = "og:type">')
    .replace(
      '<meta property="og:title" content="TraceMap Review Room Demo Path">',
      '<meta content = "TraceMap Review Room Demo Path" property = "og:title">'
    )
    .replace(
      '<meta property="og:url" content="https://tracemap.tools/review-room/demo-path/">',
      '<meta content = "https://tracemap.tools/review-room/demo-path/" property = "og:url">'
    )
    .replaceAll('scope="col"', 'scope = "col"')
    .replaceAll('data-review-demo-step="', 'data-review-demo-step = "')
    .replaceAll('data-field="', 'data-field = "')
    .replaceAll('id="proof-packet-fields"', 'id = "proof-packet-fields"')
    .replaceAll('id="stop-conditions"', 'id = "stop-conditions"')
    .replaceAll('id="non-claims"', 'id = "non-claims"')
    .replaceAll('data-review-demo-path-boundary="', 'data-review-demo-path-boundary = "');
  const root = await createManagedReviewRoomDemoPathFixture(t, { pageHtml: page });
  const errors = [];

  await validateReviewRoomDemoPathDist({ dist: join(root, "site", "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateReviewRoomDemoPathDist reports missing required visible markers", async (t) => {
  const root = await createManagedReviewRoomDemoPathFixture(t, {
    pageHtml: (await canonicalPage()).replace("Public claim level: concept", "Claim level pending")
  });
  const errors = [];

  await validateReviewRoomDemoPathDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /missing required visible text: Public claim level: concept/);
});

test("validateReviewRoomDemoPathDist reports unordered guided path steps", async (t) => {
  const root = await createManagedReviewRoomDemoPathFixture(t, {
    pageHtml: (await canonicalPage()).replace(
      'data-review-demo-step="inspect proof paths"',
      'data-review-demo-step="inspect proof route"'
    )
  });
  const errors = [];

  await validateReviewRoomDemoPathDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /step 4 expected "inspect proof paths"/);
});

test("validateReviewRoomDemoPathDist reports empty required step fields", async (t) => {
  const root = await createManagedReviewRoomDemoPathFixture(t, {
    pageHtml: (await canonicalPage()).replace(
      "The question cannot ask whether a live system is acceptable or whether a release can proceed.",
      "-"
    )
  });
  const errors = [];

  await validateReviewRoomDemoPathDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /empty or placeholder field: limitation/);
});

test("validateReviewRoomDemoPathDist guards the evidence-packet step link", async (t) => {
  const root = await createManagedReviewRoomDemoPathFixture(t, {
    pageHtml: (await canonicalPage())
      .replaceAll('href="/packets/"', 'href="/packet-missing/"')
      .replaceAll('href="/packets/assembly/"', 'href="/packet-assembly-missing/"')
      .replaceAll('href="/packets/examples/"', 'href="/packet-examples-missing/"')
  });
  const errors = [];

  await validateReviewRoomDemoPathDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /evidence-packet step must link at least one packet route/);
});

test("validateReviewRoomDemoPathDist requires the final stop step", async (t) => {
  const root = await createManagedReviewRoomDemoPathFixture(t, {
    pageHtml: (await canonicalPage()).replace(
      'data-review-demo-step="stop when evidence is insufficient"',
      'data-review-demo-step="route to summary"'
    )
  });
  const errors = [];

  await validateReviewRoomDemoPathDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /final guided step must be stop when evidence is insufficient/);
});

test("validateReviewRoomDemoPathDist reports route metadata regressions", async (t) => {
  const root = await createManagedReviewRoomDemoPathFixture(t);
  await rewriteRouteEntry(join(root, "site", "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/",
    summary: "A short route."
  });
  const errors = [];

  await validateReviewRoomDemoPathDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
  assert.match(errors.join("\n"), /discovery metadata is missing required term: guided path/);
});

test("validateReviewRoomDemoPathDist reports unresolved rendered links", async (t) => {
  const root = await createManagedReviewRoomDemoPathFixture(t, {
    sitemapRoutes: reviewRoomDemoPathRequiredLinks.filter((route) => route !== "/owners/follow-up/")
  });
  const errors = [];

  await validateReviewRoomDemoPathDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /required link does not resolve in generated output: \/owners\/follow-up\//);
});

test("validateReviewRoomDemoPathDist reports required links missing from routes index", async (t) => {
  const root = await createManagedReviewRoomDemoPathFixture(t, {
    discoveryRoutes: [reviewRoomDemoPathRoute, ...reviewRoomDemoPathRequiredLinks.filter((route) => route !== "/owners/follow-up/")]
  });
  const errors = [];

  await validateReviewRoomDemoPathDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /required link does not resolve in generated output: \/owners\/follow-up\//);
});

test("validateReviewRoomDemoPathDist rejects forbidden positive claims", async (t) => {
  const root = await createManagedReviewRoomDemoPathFixture(t, {
    pageHtml: (await canonicalPage()).replace(
      "</main>",
      "<p>TraceMap proves runtime behavior for this claim.</p></main>"
    )
  });
  const errors = [];

  await validateReviewRoomDemoPathDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /forbidden positive claim outside bounded contexts/);
});

test("validateReviewRoomDemoPathDist rejects private and raw material outside boundaries", async (t) => {
  const root = await createManagedReviewRoomDemoPathFixture(t, {
    pageHtml: (await canonicalPage()).replace(
      "</main>",
      "<p>Publish analyzer logs from file&#58;//private/report.</p></main>"
    )
  });
  const errors = [];

  await validateReviewRoomDemoPathDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /hard private material/);
  assert.match(errors.join("\n"), /forbidden raw\/private material outside bounded contexts/);
});

test("validateReviewRoomDemoPathDist reports missing implementation-state decisions", async (t) => {
  const root = await createManagedReviewRoomDemoPathFixture(t, {
    implementationState: "Implementation state pending."
  });
  const errors = [];

  await validateReviewRoomDemoPathDist({ dist: join(root, "site", "dist"), errors });

  assert.match(errors.join("\n"), /implementation-state is missing phrase/);
});

async function createManagedReviewRoomDemoPathFixture(t, options = {}) {
  const root = await createReviewRoomDemoPathFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createReviewRoomDemoPathFixture({
  pageHtml,
  discoveryRoutes = [reviewRoomDemoPathRoute, ...reviewRoomDemoPathRequiredLinks],
  implementationState = canonicalImplementationState(),
  sitemapRoutes = [reviewRoomDemoPathRoute, ...reviewRoomDemoPathRequiredLinks]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-review-room-demo-path-"));
  const dist = join(root, "site", "dist");
  await mkdir(dist, { recursive: true });

  const routes = new Set([reviewRoomDemoPathRoute, ...reviewRoomDemoPathRequiredLinks]);
  for (const route of routes) {
    const routeDir = join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(routeDir, { recursive: true });
    await writeFile(
      join(routeDir, "index.html"),
      route === reviewRoomDemoPathRoute ? pageHtml ?? (await canonicalPage()) : adjacentPage(route),
      "utf8"
    );
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);
  await writeImplementationState(root, implementationState);

  return root;
}

async function canonicalPage() {
  return readFile(join(siteRoot, "src", "review-room", "demo-path", "index.html"), "utf8");
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: route === reviewRoomDemoPathRoute ? "Review Room Demo Path" : `Route ${route}`,
    summary:
      route === reviewRoomDemoPathRoute
        ? "Concept-level guided path for moving one public-safe static question through review-room, agenda, proof-path, packet, checklist, limitation, validation, owner-routing, and stop-condition surfaces."
        : "Supporting public-safe route for review room demo path validation.",
    publicClaimLevel: route === reviewRoomDemoPathRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === reviewRoomDemoPathRoute ? "use-case" : "evidence",
    preferredProofPath: route === reviewRoomDemoPathRoute ? "/proof-paths/" : "/validation/",
    limitations:
      route === reviewRoomDemoPathRoute
        ? [
            "The route is an authored public reading path over existing public-safe static evidence surfaces, not a live review room, generated packet builder, proof engine, approval flow, or new proof source.",
            "Every guided-path step keeps limitation, stop condition, owner or route, evidence tier, coverage label, validation evidence, and public-safe packet context visible instead of upgrading missing evidence."
          ]
        : ["Fixture route exists only to resolve review room demo path links."],
    nonClaims:
      route === reviewRoomDemoPathRoute
        ? [
            "No runtime proof, production traffic proof, endpoint performance proof, outage-cause proof, release approval, release safety, operational safety, production proof, live workflow completeness, complete coverage, AI impact analysis, LLM analysis, prompt classification, embeddings, vector databases, autonomous review, autonomous approval, or automated management decision.",
            "No raw facts, raw SQLite content, analyzer logs, raw source snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, private sample names, raw command output, hidden validation details, private labels, credential-like values, connection strings, tokens, or keys are public demo-path material."
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
    entry.path === reviewRoomDemoPathRoute
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

function adjacentPage(route) {
  return `<!doctype html><html><head><title>${route}</title><meta property="og:type" content="article"></head><body><main><a href="${reviewRoomDemoPathRoute}">Review room demo path</a></main></body></html>`;
}

async function writeImplementationState(root, content) {
  const path = join(root, ".kiro", "specs", "site-tracemap-tools-review-room-demo-path");
  await mkdir(path, { recursive: true });
  await writeFile(join(path, "implementation-state.md"), content, "utf8");
}

function canonicalImplementationState() {
  return `
Implementation branch: \`codex/impl-site-review-room-demo-path-20260626095826\`
Selected placement: \`/review-room/demo-path/\`
Rejected alternative: section on \`/review-room/\`
Rejected alternative: section on \`/review-room/agenda/\`
Rejected alternative: section on \`/demo/start-here/\`
Primary navigation remains unchanged.
All preferred adjacent routes exist at implementation time.
Evidence-packet routes present: \`/packets/\`, \`/packets/assembly/\`, \`/packets/examples/\`
Browser sanity: pending validation run
`;
}
