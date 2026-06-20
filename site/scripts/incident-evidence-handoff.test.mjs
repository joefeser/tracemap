import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import {
  incidentEvidenceHandoffRequiredLinks,
  incidentEvidenceHandoffRoute,
  validateIncidentEvidenceHandoffDist
} from "./incident-evidence-handoff.mjs";

test("validateIncidentEvidenceHandoffDist accepts the incident evidence handoff route", async (t) => {
  const root = await createManagedIncidentEvidenceHandoffDistFixture(t);
  const errors = [];

  await validateIncidentEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateIncidentEvidenceHandoffDist accepts href spacing around assignment", async (t) => {
  const root = await createManagedIncidentEvidenceHandoffDistFixture(t, {
    handoffHtml: incidentEvidenceHandoffPage("", { spacedHref: true })
  });
  const errors = [];

  await validateIncidentEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateIncidentEvidenceHandoffDist reports missing locked distinction copy", async (t) => {
  const root = await createManagedIncidentEvidenceHandoffDistFixture(t, {
    handoffHtml: page("<p>Incident evidence handoff placeholder.</p>")
  });
  const errors = [];

  await validateIncidentEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required text: Public claim level: concept/);
  assert.match(errors.join("\n"), /missing required text: Incident evidence handoff is the packet/);
  assert.match(errors.join("\n"), /missing required text: Static triage frames the question/);
});

test("validateIncidentEvidenceHandoffDist reports missing route metadata", async (t) => {
  const root = await createManagedIncidentEvidenceHandoffDistFixture(t, {
    discoveryRoutes: [],
    sitemapRoutes: []
  });
  const errors = [];

  await validateIncidentEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /sitemap is missing required route/);
  assert.match(errors.join("\n"), /routes-index\.json is missing required route: \/incident-evidence-handoff\//);
});

test("validateIncidentEvidenceHandoffDist reports route metadata regressions", async (t) => {
  const root = await createManagedIncidentEvidenceHandoffDistFixture(t);
  await rewriteHandoffRoutesIndexEntry(join(root, "dist"), {
    publicClaimLevel: "demo",
    hintCategory: "evidence",
    sourceType: "repo-doc",
    preferredProofPath: "/validation/"
  });
  const errors = [];

  await validateIncidentEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /expected publicClaimLevel concept, got demo/);
  assert.match(errors.join("\n"), /expected hintCategory use-case, got evidence/);
  assert.match(errors.join("\n"), /expected sourceType site-page, got repo-doc/);
  assert.match(errors.join("\n"), /expected preferredProofPath \/proof-paths\/, got \/validation\//);
});

test("validateIncidentEvidenceHandoffDist reports missing required links", async (t) => {
  const root = await createManagedIncidentEvidenceHandoffDistFixture(t, {
    handoffHtml: incidentEvidenceHandoffPage("", { omittedLink: "/manager-faq/" })
  });
  const errors = [];

  await validateIncidentEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required link: \/manager-faq\//);
});

test("validateIncidentEvidenceHandoffDist reports unresolved internal links", async (t) => {
  const root = await createManagedIncidentEvidenceHandoffDistFixture(t, {
    handoffHtml: incidentEvidenceHandoffPage('<a href="/missing-route/">Missing</a>')
  });
  const errors = [];

  await validateIncidentEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /links to unresolved internal route: \/missing-route\//);
});

test("validateIncidentEvidenceHandoffDist reports word count outside bounds", async (t) => {
  const root = await createManagedIncidentEvidenceHandoffDistFixture(t, {
    handoffHtml: incidentEvidenceHandoffPage("", { fillerWordCount: 20 })
  });
  const errors = [];

  await validateIncidentEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /word count must be between 400 and 1800 words/);
});

test("validateIncidentEvidenceHandoffDist rejects forbidden positioning", async (t) => {
  const root = await createManagedIncidentEvidenceHandoffDistFixture(t, {
    handoffHtml: incidentEvidenceHandoffPage("<p>TraceMap provides complete product coverage.</p>")
  });
  const errors = [];

  await validateIncidentEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden runtime\/AI positioning: complete product coverage/);
});

test("validateIncidentEvidenceHandoffDist rejects encoded private text", async (t) => {
  const root = await createManagedIncidentEvidenceHandoffDistFixture(t, {
    handoffHtml: incidentEvidenceHandoffPage("<p>raw&#32;SQL stays private.</p>")
  });
  const errors = [];

  await validateIncidentEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /forbidden private\/raw artifact text: raw SQL/);
});

test("validateIncidentEvidenceHandoffDist rejects missing ownership rows", async (t) => {
  const root = await createManagedIncidentEvidenceHandoffDistFixture(t, {
    handoffHtml: incidentEvidenceHandoffPage("", { omittedOwnershipRow: "database ownership" })
  });
  const errors = [];

  await validateIncidentEvidenceHandoffDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required ownership row: database ownership/);
});

async function createManagedIncidentEvidenceHandoffDistFixture(t, options = {}) {
  const root = await createIncidentEvidenceHandoffDistFixture(options);
  t.after(() => rm(root, { recursive: true, force: true }));
  return root;
}

async function createIncidentEvidenceHandoffDistFixture({
  discoveryRoutes = [incidentEvidenceHandoffRoute, ...incidentEvidenceHandoffRequiredLinks],
  sitemapRoutes = [incidentEvidenceHandoffRoute, ...incidentEvidenceHandoffRequiredLinks],
  handoffHtml = incidentEvidenceHandoffPage()
} = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-incident-evidence-handoff-test-"));
  const dist = join(root, "dist");
  const routes = new Set([incidentEvidenceHandoffRoute, ...incidentEvidenceHandoffRequiredLinks]);

  for (const route of routes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(join(path, "index.html"), route === incidentEvidenceHandoffRoute ? handoffHtml : page(route), "utf8");
  }

  await writeFile(join(dist, "sitemap.xml"), renderSitemap(sitemapRoutes), "utf8");
  await writeDiscoveryFiles(dist, discoveryRoutes);

  return root;
}

async function writeDiscoveryFiles(dist, routes) {
  const entries = routes.map((route) => ({
    path: route,
    title: `Route ${route}`,
    summary: "Fixture route for incident evidence handoff validation.",
    publicClaimLevel: route === incidentEvidenceHandoffRoute ? "concept" : "demo",
    sourceType: "site-page",
    hintCategory: route === incidentEvidenceHandoffRoute ? "use-case" : "evidence",
    ...(route === incidentEvidenceHandoffRoute ? { preferredProofPath: "/proof-paths/" } : {}),
    limitations: ["Fixture limitations remain bounded."],
    nonClaims: ["No runtime behavior or production usage proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

async function rewriteHandoffRoutesIndexEntry(dist, fields) {
  const path = join(dist, "routes-index.json");
  const parsed = JSON.parse(await readFile(path, "utf8"));
  parsed.entries = parsed.entries.map((entry) =>
    entry.path === incidentEvidenceHandoffRoute
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
  return `<!doctype html><html><body><main>${body}</main></body></html>`;
}

function incidentEvidenceHandoffPage(
  extra = "",
  { fillerWordCount = 430, omittedLink = null, omittedOwnershipRow = null, spacedHref = false } = {}
) {
  const href = (route) => {
    if (route === omittedLink) {
      return "";
    }
    return spacedHref ? `<a href = "${route}">${route}</a>` : `<a href="${route}">${route}</a>`;
  };
  const ownershipRows = [
    "route existence",
    "DTO shape",
    "package reference",
    "dependency edge",
    "SQL-facing reference",
    "telemetry",
    "logs",
    "traces",
    "APM",
    "release controls",
    "tests",
    "database ownership",
    "service ownership",
    "incident command"
  ]
    .filter((row) => row !== omittedOwnershipRow)
    .map((row) => `<p>${row}</p>`)
    .join("\n");
  const filler = Array.from({ length: fillerWordCount }, (_, index) => `handoff-evidence-${index}`).join(" ");

  return `<!doctype html>
<html>
  <head>
    <title>Incident Evidence Handoff Packet | TraceMap</title>
    <meta name="description" content="Fixture incident evidence handoff description.">
    <link rel="canonical" href="https://tracemap.tools/incident-evidence-handoff/">
    <meta property="og:type" content="article">
    <meta property="og:title" content="Incident Evidence Handoff Packet">
    <meta property="og:description" content="Fixture handoff page.">
    <meta property="og:url" content="https://tracemap.tools/incident-evidence-handoff/">
  </head>
  <body>
    <main>
      <p>Public claim level: concept</p>
      <p>No public conclusion without evidence</p>
      <p>Incident evidence handoff is the packet of static evidence, proof paths, limits, and next owners; it is not runtime proof or incident command.</p>
      <p>Static triage frames the question; the incident evidence handoff packet carries the already-framed evidence, proof paths, limits, and next owners into the next conversation.</p>
      <p>static evidence</p>
      <p>proof path</p>
      <p>rule ID/evidence tier</p>
      <p>coverage label</p>
      <p>limitation</p>
      <p>next owner</p>
      ${ownershipRows}
      ${incidentEvidenceHandoffRequiredLinks.map((route) => href(route)).join("\n")}
      <p>${filler}</p>
      ${extra}
    </main>
  </body>
</html>`;
}
