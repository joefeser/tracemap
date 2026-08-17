import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { buildSite } from "./build.mjs";
import { validateGapLineNumberArticleDist } from "./gap-line-number-article.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const articleBodyPath = join("src", "_blog", "articles", "when-a-gap-has-no-honest-line-number.html");

test("Gap line-number article builds with five location classes, labeled examples, discovery, links, and sitemap metadata", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("Gap line-number validator rejects planted causality and proof claims", async (t) => {
  for (const claim of [
    "TraceMap proves the anchored line caused the failure.",
    "The anchor caused the workspace condition.",
    "The span is guaranteed to be exact.",
    "This article confirms the container executed the operation."
  ]) {
    await t.test(claim, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, articleBodyPath);
      const html = await readFile(path, "utf8");
      await writeFile(path, `${html}<p>${claim}</p>`, "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /unsupported positive claim/);
    });
  }
});

test("Gap line-number validator rejects forbidden material in published article metadata", async (t) => {
  for (const [field, planted] of [
    ["description", "TraceMap proves the span is exact."],
    ["ogDescription", "SELECT secret FROM customer_table"],
    ["cardDescription", "TraceMap confirms the line executed."]
  ]) {
    await t.test(field, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, "src", "_blog", "articles.json");
      const articles = JSON.parse(await readFile(path, "utf8"));
      articles.find((article) => article.slug === "when-a-gap-has-no-honest-line-number")[field] = planted;
      await writeFile(path, `${JSON.stringify(articles, null, 2)}\n`, "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /unsupported positive claim|raw or executable material/);
    });
  }
});

test("Gap line-number validator rejects positive claims and executable material inside the non-claim boundary", async (t) => {
  for (const planted of ["<p>TraceMap proves the anchored line executed.</p>", "<p>SELECT value FROM private_table</p>"]) {
    await t.test(planted, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, articleBodyPath);
      const html = await readFile(path, "utf8");
      await writeFile(
        path,
        html.replace(
          '<section data-gap-span-block="non-claims" data-gap-span-boundary="non-claims" data-tm-boundary="claim-boundary">',
          `<section data-gap-span-block="non-claims" data-gap-span-boundary="non-claims" data-tm-boundary="claim-boundary">${planted}`
        ),
        "utf8"
      );
      await buildSite({ root, log() {} });
      const errors = [];
      await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /unsupported positive claim|raw or executable material/);
    });
  }
});

test("Gap line-number validator rejects planted private and executable material", async (t) => {
  const slash = String.fromCharCode(47);
  for (const leak of [`${slash}Users${slash}example${slash}private`, "Password=leak-sentinel", "SELECT value FROM private_table"]) {
    await t.test(leak, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, articleBodyPath);
      const html = await readFile(path, "utf8");
      await writeFile(path, `${html}<p>${leak}</p>`, "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /hard private material|raw or executable material/);
    });
  }
});

test("Gap line-number validator rejects tokens split across markup tags and entity-encoded tokens", async (t) => {
  for (const [name, planted, expected] of [
    ["split private path", "<p>/Use<span>rs/</span>example</p>", /hard private material/],
    ["split sql keyword", "<p>SEL<span>ECT value </span>FROM private_table</p>", /raw or executable material/],
    ["split lowercase sql keyword", "<p>sel<span>ect</span> value fr<span>om</span> private_table</p>", /raw or executable material/],
    ["entity-encoded private path", "<p>/Us&#101rs/example</p>", /hard private material/],
    ["entity-encoded claim word", "<p>TraceMap pr&#111ves the anchor is exact.</p>", /unsupported positive claim/]
  ]) {
    await t.test(name, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, articleBodyPath);
      const html = await readFile(path, "utf8");
      await writeFile(path, `${html}${planted}`, "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), expected);
    });
  }
});

test("Gap line-number validator requires each structural section", async (t) => {
  await t.test("missing five-classes block", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace('data-gap-span-block="five-classes"', 'data-gap-span-block="renamed-classes"'), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /missing required block: five-classes/);
  });

  await t.test("missing persistence block", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace('data-gap-span-block="persistence"', 'data-gap-span-block="storage"'), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /missing required block: persistence/);
  });
});

test("Gap line-number validator requires the five location classes and their semantics", async (t) => {
  await t.test("renamed location class", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace("Owning-container anchor.", "Container evidence."), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /missing location class: Owning-container anchor/);
  });

  await t.test("dropped line-one semantics distinction", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace("every line-one span means the same thing", "spans are spans"), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /missing required text: not every line-one span/);
  });
});

test("Gap line-number validator requires labeled steps inside each synthetic example", async (t) => {
  await t.test("workspace example loses workspace anchor step", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace("<strong>workspace anchor:</strong>", "<strong>anchor:</strong>"), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /example example-workspace is missing labeled step: workspace anchor/);
  });

  await t.test("container example loses exact evidence step", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replaceAll("<strong>exact evidence:</strong>", "<strong>evidence:</strong>"), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /example example-container is missing labeled step: exact evidence/);
  });
});

test("Gap line-number validator rejects rule IDs outside the verified catalog list", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, articleBodyPath);
  const html = await readFile(path, "utf8");
  await writeFile(path, `${html}<p>Under <code>fake.location.anchor.v1</code> the span would be exact.</p>`, "utf8");
  await buildSite({ root, log() {} });
  const errors = [];
  await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /outside the verified catalog list: fake\.location\.anchor\.v1/);
});

test("Gap line-number validator requires every cited rule to remain present", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, articleBodyPath);
  const html = await readFile(path, "utf8");
  await writeFile(path, html.replace("csharp.semantic.workspace.v1", "csharp.semantic.gap.v1"), "utf8");
  await buildSite({ root, log() {} });
  const errors = [];
  await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /missing required rule ID: csharp\.semantic\.workspace\.v1/);
  assert.match(errors.join("\n"), /outside the verified catalog list: csharp\.semantic\.gap\.v1/);
});

test("Gap line-number validator safety-scans discovery entry strings", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, "src", "_site", "discovery.json");
  const entries = JSON.parse(await readFile(path, "utf8"));
  entries.find((entry) => entry.path === "/blog/when-a-gap-has-no-honest-line-number/").summary = "The migration succeeded.";
  await writeFile(path, `${JSON.stringify(entries, null, 2)}\n`, "utf8");
  await buildSite({ root, log() {} });
  const errors = [];
  await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /unsupported positive claim/);
  assert.match(errors.join("\n"), /routes-index\.json/);
});

test("Gap line-number validator reports missing discovery entry instead of crashing on null entries", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  await writeFile(join(root, "dist", "routes-index.json"), JSON.stringify({ entries: [null] }), "utf8");
  const errors = [];
  await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /discovery entry is missing/);
});

test("Gap line-number validator rejects discovery claim-level drift", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, "src", "_site", "discovery.json");
  const entries = JSON.parse(await readFile(path, "utf8"));
  entries.find((entry) => entry.path === "/blog/when-a-gap-has-no-honest-line-number/").publicClaimLevel = "demo";
  await writeFile(path, `${JSON.stringify(entries, null, 2)}\n`, "utf8");
  await buildSite({ root, log() {} });
  const errors = [];
  await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /claim level must be concept/);
});

test("Gap line-number validator aggregates malformed discovery output", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  await writeFile(join(root, "dist", "routes-index.json"), "{not-json", "utf8");
  const errors = [];
  await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /invalid JSON/);
});

test("Gap line-number validator accepts attribute spacing and the supplied canonical base URL", async (t) => {
  const root = await createSiteFixture(t);
  const sourcePath = join(root, articleBodyPath);
  await writeFile(sourcePath, (await readFile(sourcePath, "utf8")).replace('data-gap-span-block="five-classes"', 'data-gap-span-block = "five-classes"'), "utf8");
  await buildSite({ root, log() {} });
  const pagePath = join(root, "dist", "blog", "when-a-gap-has-no-honest-line-number", "index.html");
  await writeFile(pagePath, (await readFile(pagePath, "utf8")).replaceAll("https://tracemap.tools", "https://preview.example"), "utf8");
  const sitemapPath = join(root, "dist", "sitemap.xml");
  await writeFile(sitemapPath, (await readFile(sitemapPath, "utf8")).replaceAll("https://tracemap.tools", "https://preview.example"), "utf8");
  const errors = [];
  await validateGapLineNumberArticleDist({ baseUrl: "https://preview.example/", dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("Gap line-number validator tight-scans blog card and discovery strings", async (t) => {
  await t.test("whitespace-free sql in blog card description", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, "src", "_blog", "articles.json");
    const articles = JSON.parse(await readFile(path, "utf8"));
    articles.find((article) => article.slug === "when-a-gap-has-no-honest-line-number").cardDescription = "SELECTsecretFROMprivate_table";
    await writeFile(path, `${JSON.stringify(articles, null, 2)}\n`, "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /raw or executable material/);
    assert.match(errors.join("\n"), /blog\/index\.html/);
  });

  await t.test("whitespace-free sql in discovery summary", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, "src", "_site", "discovery.json");
    const entries = JSON.parse(await readFile(path, "utf8"));
    entries.find((entry) => entry.path === "/blog/when-a-gap-has-no-honest-line-number/").summary = "SELECTsecretFROMprivate_table";
    await writeFile(path, `${JSON.stringify(entries, null, 2)}\n`, "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateGapLineNumberArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /raw or executable material/);
    assert.match(errors.join("\n"), /routes-index\.json/);
  });
});

async function createSiteFixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-gap-line-number-site-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
