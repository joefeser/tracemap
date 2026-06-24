import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import { claimReviewDrillRoute } from "./claim-review-drill.mjs";
import {
  proofPathFaqRequiredLinks,
  proofPathFaqRoute,
  validateProofPathFaqDist
} from "./proof-path-faq.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePagePath = resolve(scriptDir, "..", "src", "proof-paths", "faq", "index.html");

test("validateProofPathFaqDist accepts the proof-path FAQ route", async (t) => {
  const root = await createManagedProofPathFaqFixture(t);
  const errors = [];

  await validateProofPathFaqDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateProofPathFaqDist reports a missing required FAQ anchor", async (t) => {
  const source = await proofPathFaqPage();
  const root = await createManagedProofPathFaqFixture(t, {
    faqHtml: source.replace('id="what-is-a-proof-path"', 'id="proof-path-definition"')
  });
  const errors = [];

  await validateProofPathFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /does not include required anchor: #what-is-a-proof-path/);
});

test("validateProofPathFaqDist reports route metadata regressions", async (t) => {
  const root = await createManagedProofPathFaqFixture(t);
  await rewriteRouteEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "use-case",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/",
    nonClaims: ["No runtime behavior proof."]
  });
  const errors = [];

  await validateProofPathFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory evidence, got use-case/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
  assert.match(errors.join("\n"), /nonClaims do not include required term: production traffic/);
});

test("validateProofPathFaqDist cites route metadata for metadata-only forbidden claims", async (t) => {
  const root = await createManagedProofPathFaqFixture(t);
  await rewriteRouteEntry(join(root, "dist"), {
    summary: "TraceMap proves runtime behavior."
  });
  const errors = [];

  await validateProofPathFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim in metadata/);
  assert.match(errors.join("\n"), /Evidence: routes-index\.json\./);
});

test("validateProofPathFaqDist rejects forbidden claims outside bounded sections", async (t) => {
  const source = await proofPathFaqPage();
  const root = await createManagedProofPathFaqFixture(t, {
    faqHtml: source.replace("</main>", "<p>TraceMap proves runtime behavior for this endpoint.</p></main>")
  });
  const errors = [];

  await validateProofPathFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateProofPathFaqDist rejects unsupported conclusion verbs outside bounded sections", async (t) => {
  const source = await proofPathFaqPage();
  const root = await createManagedProofPathFaqFixture(t, {
    faqHtml: source.replace("</main>", "<p>The FAQ guarantees the answer.</p></main>")
  });
  const errors = [];

  await validateProofPathFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unsupported conclusion verb/);
});

test("validateProofPathFaqDist rejects raw material outside bounded sections", async (t) => {
  const source = await proofPathFaqPage();
  const root = await createManagedProofPathFaqFixture(t, {
    faqHtml: source.replace("</main>", "<p>Publish raw facts with this proof path.</p></main>")
  });
  const errors = [];

  await validateProofPathFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden raw\/private material/);
});

test("validateProofPathFaqDist permits bounded private-material and unsafe wording", async (t) => {
  const source = await proofPathFaqPage();
  const root = await createManagedProofPathFaqFixture(t, {
    faqHtml: source.replace(
      "</main>",
      '<section data-proof-faq-boundary="unsafe-patterns"><p>Do not say TraceMap proves runtime behavior, guarantees release safety, publishes raw facts, or uses AI impact analysis.</p></section></main>'
    )
  });
  const errors = [];

  await validateProofPathFaqDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateProofPathFaqDist rejects unknown boundary names", async (t) => {
  const source = await proofPathFaqPage();
  const root = await createManagedProofPathFaqFixture(t, {
    faqHtml: source.replace(
      "</main>",
      '<section data-proof-faq-boundary="custom"><p>TraceMap proves runtime behavior.</p></section></main>'
    )
  });
  const errors = [];

  await validateProofPathFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden public claim/);
});

test("validateProofPathFaqDist strips approved boundary sections with nested sections", async (t) => {
  const source = await proofPathFaqPage();
  const root = await createManagedProofPathFaqFixture(t, {
    faqHtml: source.replace(
      "</main>",
      '<section data-proof-faq-boundary="unsafe-patterns"><section><p>Do not say TraceMap proves runtime behavior or publishes raw facts.</p></section></section></main>'
    )
  });
  const errors = [];

  await validateProofPathFaqDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateProofPathFaqDist rejects hard private material in attributes", async (t) => {
  const source = await proofPathFaqPage();
  const root = await createManagedProofPathFaqFixture(t, {
    faqHtml: source.replace("</main>", '<img alt="file&#58;//private/report"></main>')
  });
  const errors = [];

  await validateProofPathFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /hard private material/);
});

test("validateProofPathFaqDist reports duplicate ids", async (t) => {
  const source = await proofPathFaqPage();
  const root = await createManagedProofPathFaqFixture(t, {
    faqHtml: source.replace("</main>", '<section id="how-to-read"></section></main>')
  });
  const errors = [];

  await validateProofPathFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /duplicate id: how-to-read/);
});

test("validateProofPathFaqDist reports unsafe patterns without bounded marker", async (t) => {
  const source = await proofPathFaqPage();
  const root = await createManagedProofPathFaqFixture(t, {
    faqHtml: source.replace('id="unsafe-answer-patterns" data-proof-faq-boundary="unsafe-patterns"', 'id="unsafe-answer-patterns"')
  });
  const errors = [];

  await validateProofPathFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /unsafe patterns must be inside a bounded unsafe-patterns section/);
});

test("validateProofPathFaqDist reports missing inbound links from adjacent routes", async (t) => {
  const root = await createManagedProofPathFaqFixture(t, {
    includeInboundLinks: false
  });
  const errors = [];

  await validateProofPathFaqDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /does not include inbound links from live adjacent routes: \/proof-paths\//);
});

async function createManagedProofPathFaqFixture(t, options = {}) {
  const root = await createProofPathFaqFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createProofPathFaqFixture({
  discoveryRoutes = [proofPathFaqRoute, ...proofPathFaqRequiredLinks, claimReviewDrillRoute],
  faqHtml,
  includeInboundLinks = true,
  sitemapRoutes = [proofPathFaqRoute]
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-proof-path-faq-test-"));
  const dist = join(root, "dist");
  const routes = new Set(discoveryRoutes);
  const pageHtml = faqHtml ?? (await proofPathFaqPage());

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    const body =
      route === proofPathFaqRoute
        ? pageHtml
        : page(includeInboundLinks && route === "/proof-paths/" ? `<a href="${proofPathFaqRoute}">FAQ</a>` : route);
    await writeFile(join(path, "index.html"), body, "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function proofPathFaqPage() {
  return readFile(sourcePagePath, "utf8");
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) =>
    route === proofPathFaqRoute
      ? proofPathFaqEntry()
      : {
          path: route,
          title: `Route ${route}`,
          summary: "Fixture route for internal link validation.",
          publicClaimLevel: "concept",
          sourceType: "site-page",
          hintCategory: "evidence",
          limitations: ["Fixture page only."],
          nonClaims: ["No runtime behavior proof."]
        }
  );
  const outputs = await createDiscoveryOutputs(entries);

  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
}

function proofPathFaqEntry() {
  return {
    path: proofPathFaqRoute,
    title: "Proof Path FAQ",
    summary: "Concept-level FAQ for reading proof paths, evidence tiers, coverage labels, limitations, review-packet context, missing-evidence gaps, and static-evidence boundaries.",
    publicClaimLevel: "concept",
    sourceType: "site-page",
    hintCategory: "evidence",
    preferredProofPath: "/proof-paths/",
    limitations: [
      "The FAQ is concept-level explanation over existing public-safe evidence surfaces, not a generated proof source.",
      "Claims repeated from the FAQ must keep proof path, rule family, tier, coverage label, limitation, non-claim, public claim level, source context, and next-owner handoff attached."
    ],
    nonClaims: [
      "No runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, release approval, complete coverage, autonomous approval, AI impact analysis, LLM analysis, embeddings, vector databases, prompt classification, or replacement for tests.",
      "No replacement for code review, source review, runtime observability, service-owner judgment, or human judgment."
    ]
  };
}

async function rewriteRouteEntry(dist, patch) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  if (!parsed || typeof parsed !== "object" || !Array.isArray(parsed.entries)) {
    throw new Error("Fixture routes-index.json must include an entries array.");
  }

  const index = parsed.entries.findIndex((entry) => entry.path === proofPathFaqRoute);
  if (index === -1) {
    throw new Error("Fixture routes-index.json missing proofPathFaqRoute.");
  }

  parsed.entries[index] = { ...parsed.entries[index], ...patch };
  await writeFile(path, `${JSON.stringify(parsed, null, 2)}\n`, "utf8");
}

function page(content = "") {
  return `<!doctype html><html><body><main>${content}</main></body></html>`;
}

function renderSitemap(routes) {
  return `<?xml version="1.0" encoding="UTF-8"?><urlset>${routes
    .map((route) => `<url><loc>https://tracemap.tools${route}</loc></url>`)
    .join("")}</urlset>`;
}
