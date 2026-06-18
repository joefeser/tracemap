import assert from "node:assert/strict";
import { mkdir, mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs } from "./discovery.mjs";
import { deployAuditRequiredRoutes, validateDeployAuditDist } from "./deploy-audit.mjs";

test("validateDeployAuditDist accepts the required static publish surface", async () => {
  const root = await createDeployDistFixture();
  const errors = [];

  const result = await validateDeployAuditDist({ dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
  assert.equal(result.requiredRouteCount, deployAuditRequiredRoutes.length);
});

test("validateDeployAuditDist accepts normalized sitemap urls", async () => {
  const root = await createDeployDistFixture({
    sitemap: renderSitemap(deployAuditRequiredRoutes, { indentLocs: true })
  });
  const errors = [];

  await validateDeployAuditDist({ baseUrl: "https://tracemap.tools/", dist: join(root, "dist"), errors });

  assert.deepEqual(errors, []);
});

test("validateDeployAuditDist reports missing deployment-critical files and routes", async () => {
  const root = await createDeployDistFixture();
  await rm(join(root, "dist", "llms.txt"));
  await rm(join(root, "dist", "proof-paths"), { recursive: true, force: true });
  const errors = [];

  await validateDeployAuditDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /missing required generated file: llms\.txt/);
  assert.match(errors.join("\n"), /missing required public route: \/proof-paths\//);
});

test("validateDeployAuditDist rejects deploy audit page private artifact text", async () => {
  const root = await createDeployDistFixture({
    deployAuditHtml: deployAuditPage(`<p>${localHomePath("example/private")}</p>`)
  });
  const errors = [];

  await validateDeployAuditDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /contains forbidden public text: \/Users\//);
});

test("validateDeployAuditDist rejects encoded deploy audit page private artifact text", async () => {
  const root = await createDeployDistFixture({
    deployAuditHtml: deployAuditPage("<p>&#47;Users&#47;example&#47;private</p>")
  });
  const errors = [];

  await validateDeployAuditDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /contains forbidden public text: \/Users\//);
});

test("validateDeployAuditDist rejects invalid routes index shape", async () => {
  const root = await createDeployDistFixture();
  await writeFile(join(root, "dist", "routes-index.json"), "null\n", "utf8");
  const errors = [];

  await validateDeployAuditDist({ dist: join(root, "dist"), errors });

  assert.match(errors.join("\n"), /routes-index\.json is invalid: expected an object/);
});

async function createDeployDistFixture({ deployAuditHtml = deployAuditPage(), sitemap } = {}) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-deploy-audit-test-"));
  const dist = join(root, "dist");

  for (const route of deployAuditRequiredRoutes) {
    const path = route === "/" ? dist : join(dist, route.replace(/^\/|\/$/g, ""));
    await mkdir(path, { recursive: true });
    await writeFile(join(path, "index.html"), route === "/deploy-audit/" ? deployAuditHtml : page(route), "utf8");
  }

  await writeFile(join(dist, "robots.txt"), "User-agent: *\nAllow: /\n# LLM discovery: https://tracemap.tools/llms.txt\nSitemap: https://tracemap.tools/sitemap.xml\n", "utf8");
  await writeFile(join(dist, "sitemap.xml"), sitemap ?? renderSitemap(deployAuditRequiredRoutes), "utf8");
  await writeDiscoveryFiles(dist);

  return root;
}

async function writeDiscoveryFiles(dist) {
  const entries = deployAuditRequiredRoutes.map((route) => ({
    path: route,
    title: `Route ${route}`,
    summary: "Deterministic static evidence route for deploy audit validation.",
    publicClaimLevel: "demo",
    sourceType: "site-page",
    hintCategory: route === "/limitations/" ? "limitations" : "evidence",
    limitations: ["Fixture deploy audit route remains static and public-safe."],
    nonClaims: ["No runtime behavior, production usage, deployment state, endpoint performance, or release approval proof."]
  }));
  const outputs = await createDiscoveryOutputs(entries, { dist, resolveInternalPaths: true });

  await writeFile(join(dist, "llms.txt"), outputs.llmsText, "utf8");
  await writeFile(join(dist, "docs-index.json"), outputs.docsIndexJson, "utf8");
  await writeFile(join(dist, "routes-index.json"), outputs.routesIndexJson, "utf8");
}

function renderSitemap(routes, { indentLocs = false } = {}) {
  const loc = (route) =>
    indentLocs
      ? `  <url>\n    <loc>\n      https://tracemap.tools${route}\n    </loc>\n  </url>`
      : `  <url><loc>https://tracemap.tools${route}</loc></url>`;

  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${routes.map((route) => loc(route)).join("\n")}
</urlset>`;
}

function page(route) {
  return `<!doctype html><html><body><main>${route}</main></body></html>`;
}

function deployAuditPage(extra = "") {
  return `<!doctype html><html><body><main>
    <p>Public claim level: demo</p>
    <p>No public conclusion without evidence</p>
    <p>This is not live AWS state, not runtime behavior proof, and not deployment success proof.</p>
    <p>Check sitemap.xml, robots.txt, llms.txt, docs-index.json, and routes-index.json.</p>
    ${extra}
  </main></body></html>`;
}

function localHomePath(path) {
  return `${String.fromCharCode(47)}Users/${path}`;
}
