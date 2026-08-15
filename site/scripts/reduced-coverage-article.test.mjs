import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { buildSite } from "./build.mjs";
import { validateReducedCoverageArticleDist } from "./reduced-coverage-article.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const articleBodyPath = join("src", "_blog", "articles", "successful-build-can-still-have-reduced-coverage.html");

test("Reduced coverage article builds with bounded claims, four layers, discovery, links, and sitemap metadata", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateReducedCoverageArticleDist({ dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("Reduced coverage validator rejects planted runtime and migration claims", async (t) => {
  for (const claim of ["TraceMap ran the Web Forms application.", "TraceMap proves the build is complete.", "The migration succeeded.", "The page is safe to release."]) {
    await t.test(claim, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, articleBodyPath);
      const html = await readFile(path, "utf8");
      await writeFile(path, `${html}<p>${claim}</p>`, "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateReducedCoverageArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /unsupported positive claim/);
    });
  }
});

test("Reduced coverage validator rejects forbidden material in published article metadata", async (t) => {
  for (const [field, planted] of [["description", "TraceMap ran the Web Forms application."], ["ogDescription", "SELECT secret FROM customer_table"], ["cardDescription", "TraceMap proves the event fired."]]) {
    await t.test(field, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, "src", "_blog", "articles.json");
      const articles = JSON.parse(await readFile(path, "utf8"));
      articles.find((article) => article.slug === "successful-build-can-still-have-reduced-coverage")[field] = planted;
      await writeFile(path, `${JSON.stringify(articles, null, 2)}\n`, "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateReducedCoverageArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /unsupported positive claim|raw or executable material/);
    });
  }
});

test("Reduced coverage validator rejects positive claims and executable material inside the non-claim boundary", async (t) => {
  for (const planted of ["<p>TraceMap ran the Web Forms application.</p>", "<p>SELECT value FROM private_table</p>"]) {
    await t.test(planted, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, articleBodyPath);
      const html = await readFile(path, "utf8");
      await writeFile(path, html.replace('<section data-reduced-coverage-block="non-claims" data-reduced-coverage-boundary="non-claims" data-tm-boundary="claim-boundary">', `<section data-reduced-coverage-block="non-claims" data-reduced-coverage-boundary="non-claims" data-tm-boundary="claim-boundary">${planted}`), "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateReducedCoverageArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /unsupported positive claim|raw or executable material/);
    });
  }
});

test("Reduced coverage validator rejects planted private and executable material", async (t) => {
  const slash = String.fromCharCode(47);
  for (const leak of [`${slash}Users${slash}example${slash}private`, "Password=leak-sentinel", "SELECT value FROM private_table"]) {
    await t.test(leak, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, articleBodyPath);
      const html = await readFile(path, "utf8");
      await writeFile(path, `${html}<p>${leak}</p>`, "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateReducedCoverageArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /hard private material|raw or executable material/);
    });
  }
});

test("Reduced coverage validator rejects tokens split across markup tags and entity-encoded tokens", async (t) => {
  for (const [name, planted, expected] of [
    ["split private path", "<p>/Use<span>rs/</span>example</p>", /hard private material/],
    ["split sql keyword", "<p>SEL<span>ECT value </span>FROM private_table</p>", /raw or executable material/],
    ["split lowercase sql keyword", "<p>sel<span>ect</span> value fr<span>om</span> private_table</p>", /raw or executable material/],
    ["entity-encoded private path", "<p>/Us&#101rs/example</p>", /hard private material/],
    ["entity-encoded claim word", "<p>TraceMap pr&#111ves the handler is reachable.</p>", /unsupported positive claim/]
  ]) {
    await t.test(name, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, articleBodyPath);
      const html = await readFile(path, "utf8");
      await writeFile(path, `${html}${planted}`, "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateReducedCoverageArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), expected);
    });
  }
});

test("Reduced coverage validator requires the four-layer distinction", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, articleBodyPath);
  const html = await readFile(path, "utf8");
  await writeFile(path, html.replace("Analyzer capability.</strong>", "Analyzer features.</strong>"), "utf8");
  await buildSite({ root, log() {} });
  const errors = [];
  await validateReducedCoverageArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /missing required layer term: Analyzer capability/);
});

test("Reduced coverage validator safety-scans discovery entry strings", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, "src", "_site", "discovery.json");
  const entries = JSON.parse(await readFile(path, "utf8"));
  entries.find((entry) => entry.path === "/blog/successful-build-can-still-have-reduced-coverage/").summary = "The migration succeeded.";
  await writeFile(path, `${JSON.stringify(entries, null, 2)}\n`, "utf8");
  await buildSite({ root, log() {} });
  const errors = [];
  await validateReducedCoverageArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /unsupported positive claim/);
  assert.match(errors.join("\n"), /routes-index\.json/);
});

test("Reduced coverage validator reports missing discovery entry instead of crashing on null entries", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  await writeFile(join(root, "dist", "routes-index.json"), JSON.stringify({ entries: [null] }), "utf8");
  const errors = [];
  await validateReducedCoverageArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /discovery entry is missing/);
});

test("Reduced coverage validator rejects discovery claim-level drift", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, "src", "_site", "discovery.json");
  const entries = JSON.parse(await readFile(path, "utf8"));
  entries.find((entry) => entry.path === "/blog/successful-build-can-still-have-reduced-coverage/").publicClaimLevel = "demo";
  await writeFile(path, `${JSON.stringify(entries, null, 2)}\n`, "utf8");
  await buildSite({ root, log() {} });
  const errors = [];
  await validateReducedCoverageArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /claim level must be concept/);
});

test("Reduced coverage validator aggregates malformed discovery output", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  await writeFile(join(root, "dist", "routes-index.json"), "{not-json", "utf8");
  const errors = [];
  await validateReducedCoverageArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /invalid JSON/);
});

test("Reduced coverage validator accepts attribute spacing and the supplied canonical base URL", async (t) => {
  const root = await createSiteFixture(t);
  const sourcePath = join(root, articleBodyPath);
  await writeFile(sourcePath, (await readFile(sourcePath, "utf8")).replace('data-reduced-coverage-block="capability-facts"', 'data-reduced-coverage-block = "capability-facts"'), "utf8");
  await buildSite({ root, log() {} });
  const pagePath = join(root, "dist", "blog", "successful-build-can-still-have-reduced-coverage", "index.html");
  await writeFile(pagePath, (await readFile(pagePath, "utf8")).replaceAll("https://tracemap.tools", "https://preview.example"), "utf8");
  const sitemapPath = join(root, "dist", "sitemap.xml");
  await writeFile(sitemapPath, (await readFile(sitemapPath, "utf8")).replaceAll("https://tracemap.tools", "https://preview.example"), "utf8");
  const errors = [];
  await validateReducedCoverageArticleDist({ baseUrl: "https://preview.example/", dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("Reduced coverage validator tight-scans blog card and discovery strings", async (t) => {
  await t.test("whitespace-free sql in blog card description", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, "src", "_blog", "articles.json");
    const articles = JSON.parse(await readFile(path, "utf8"));
    articles.find((article) => article.slug === "successful-build-can-still-have-reduced-coverage").cardDescription = "SELECTsecretFROMprivate_table";
    await writeFile(path, `${JSON.stringify(articles, null, 2)}\n`, "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateReducedCoverageArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /raw or executable material/);
    assert.match(errors.join("\n"), /blog\/index\.html/);
  });

  await t.test("whitespace-free sql in discovery summary", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, "src", "_site", "discovery.json");
    const entries = JSON.parse(await readFile(path, "utf8"));
    entries.find((entry) => entry.path === "/blog/successful-build-can-still-have-reduced-coverage/").summary = "SELECTsecretFROMprivate_table";
    await writeFile(path, `${JSON.stringify(entries, null, 2)}\n`, "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateReducedCoverageArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /raw or executable material/);
    assert.match(errors.join("\n"), /routes-index\.json/);
  });
});

async function createSiteFixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-reduced-coverage-site-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
