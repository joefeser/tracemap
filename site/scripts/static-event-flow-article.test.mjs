import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { buildSite } from "./build.mjs";
import { validateStaticEventFlowArticleDist } from "./static-event-flow-article.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const articleBodyPath = join("src", "_blog", "articles", "static-event-flow-what-it-proves.html");
const articleRoute = "/blog/static-event-flow-what-it-proves/";

test("Static event-flow article builds with eight sections, classifications, tiers, links, discovery, and sitemap metadata", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateStaticEventFlowArticleDist({ dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("Static event-flow validator rejects unsupported runtime and release claims", async (t) => {
  for (const claim of [
    "TraceMap proves the event fires at runtime.",
    "The user reached the screen.",
    "The service is available in production.",
    "The migration succeeded.",
    "Release approval is established."
  ]) {
    await t.test(claim, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, articleBodyPath);
      const html = await readFile(path, "utf8");
      await writeFile(path, `${html}<p>${claim}</p>`, "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateStaticEventFlowArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /unsupported positive claim/);
    });
  }
});

test("Static event-flow validator safety-scans metadata, blog card, and discovery strings", async (t) => {
  for (const [surface, pathParts, field, planted, expectedArtifact] of [
    ["metadata", ["src", "_blog", "articles.json"], "description", "TraceMap proves the event fires.", /blog\/static-event-flow-what-it-proves\/index\.html/],
    ["blog card", ["src", "_blog", "articles.json"], "cardDescription", "SELECT value FROM private_table", /blog\/index\.html/],
    ["discovery", ["src", "_site", "discovery.json"], "summary", "The migration succeeded.", /routes-index\.json/]
  ]) {
    await t.test(surface, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, ...pathParts);
      const entries = JSON.parse(await readFile(path, "utf8"));
      const entry = surface === "discovery"
        ? entries.find((candidate) => candidate.path === articleRoute)
        : entries.find((candidate) => candidate.slug === "static-event-flow-what-it-proves");
      entry[field] = planted;
      await writeFile(path, `${JSON.stringify(entries, null, 2)}\n`, "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateStaticEventFlowArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /unsupported positive claim|raw or executable material/);
      assert.match(errors.join("\n"), expectedArtifact);
    });
  }
});

test("Static event-flow validator catches tag-split and browser-decoded unsafe text", async (t) => {
  for (const [name, planted, expected] of [
    ["tag-split claim", "<p>TraceMap pr<span>oves the event fires.</p>", /unsupported positive claim/],
    ["tag-split SQL", "<p>SEL<span>ECT value </span>FROM private_table</p>", /raw or executable material/],
    ["tag-split private path", "<p>/Use<span>rs/example/private</span></p>", /hard private material/],
    ["numeric-entity claim", "<p>TraceMap pr&#111;ves the event fires.</p>", /unsupported positive claim/],
    ["numeric-entity private path", "<p>/Us&#101rs/example/private</p>", /hard private material/]
  ]) {
    await t.test(name, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, articleBodyPath);
      const html = await readFile(path, "utf8");
      await writeFile(path, `${html}${planted}`, "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateStaticEventFlowArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), expected);
    });
  }
});

test("Static event-flow validator requires the claim-boundary attributes and tolerates their order", async (t) => {
  const sourceBoundary = '<section data-static-event-flow-block="non-claims" data-static-event-flow-boundary="non-claims" data-tm-boundary="claim-boundary">';

  await t.test("reordered attributes", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace(sourceBoundary, '<section data-tm-boundary="claim-boundary" data-static-event-flow-boundary="non-claims" data-static-event-flow-block="non-claims">'), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateStaticEventFlowArticleDist({ dist: join(root, "dist"), errors });
    assert.deepEqual(errors, []);
  });

  for (const [name, removed, expected] of [
    ["missing non-claims boundary", ' data-static-event-flow-boundary="non-claims"', /must carry data-static-event-flow-boundary/],
    ["missing claim boundary", ' data-tm-boundary="claim-boundary"', /must carry data-tm-boundary/]
  ]) {
    await t.test(name, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, articleBodyPath);
      const html = await readFile(path, "utf8");
      await writeFile(path, html.replace(removed, ""), "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateStaticEventFlowArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), expected);
    });
  }
});

test("Static event-flow validator requires every classification and tier label", async (t) => {
  for (const [name, oldText, newText, expected] of [
    ["classification", "<strong>StrongStaticEventFlow:</strong>", "<strong>Strong static flow:</strong>", /missing classification: StrongStaticEventFlow/],
    ["tier", "<strong>Tier4Unknown:</strong>", "<strong>Unknown tier:</strong>", /missing evidence tier: Tier4Unknown/]
  ]) {
    await t.test(name, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, articleBodyPath);
      const html = await readFile(path, "utf8");
      await writeFile(path, html.replace(oldText, newText), "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateStaticEventFlowArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), expected);
    });
  }
});

test("Static event-flow validator rejects fake rule IDs", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, articleBodyPath);
  const html = await readFile(path, "utf8");
  await writeFile(path, `${html}<p><code>fake.webforms.flow.v1</code> is not a catalog rule.</p>`, "utf8");
  await buildSite({ root, log() {} });
  const errors = [];
  await validateStaticEventFlowArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /outside the verified catalog list: fake\.webforms\.flow\.v1/);
});

test("Static event-flow validator reports malformed discovery arrays without throwing", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  await writeFile(
    join(root, "dist", "routes-index.json"),
    JSON.stringify({
      entries: [{
        path: articleRoute,
        title: "Static Event Flow: What It Proves—and What It Does Not",
        summary: "A concept-level guide.",
        publicClaimLevel: "concept",
        preferredProofPath: "/legacy-modernization/evidence-map/",
        limitations: { malformed: true },
        nonClaims: "malformed"
      }]
    }),
    "utf8"
  );
  const errors = [];
  await validateStaticEventFlowArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /at least two limitations/);
  assert.match(errors.join("\n"), /at least two non-claims/);
});

test("Static event-flow validator handles null discovery entries and canonical base URLs", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  await writeFile(join(root, "dist", "routes-index.json"), JSON.stringify({ entries: [null] }), "utf8");
  const missingErrors = [];
  await validateStaticEventFlowArticleDist({ dist: join(root, "dist"), errors: missingErrors });
  assert.match(missingErrors.join("\n"), /discovery entry is missing/);

  const sourceRoot = await createSiteFixture(t);
  await buildSite({ root: sourceRoot, log() {} });
  const pagePath = join(sourceRoot, "dist", "blog", staticEventFlowSlug(), "index.html");
  await writeFile(pagePath, (await readFile(pagePath, "utf8")).replaceAll("https://tracemap.tools", "https://preview.example"), "utf8");
  const sitemapPath = join(sourceRoot, "dist", "sitemap.xml");
  await writeFile(sitemapPath, (await readFile(sitemapPath, "utf8")).replaceAll("https://tracemap.tools", "https://preview.example"), "utf8");
  const errors = [];
  await validateStaticEventFlowArticleDist({ baseUrl: "https://preview.example/", dist: join(sourceRoot, "dist"), errors });
  assert.deepEqual(errors, []);
});

function staticEventFlowSlug() {
  return "static-event-flow-what-it-proves";
}

async function createSiteFixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-static-event-flow-site-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
