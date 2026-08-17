import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { buildSite } from "./build.mjs";
import { validateButtonIdentityArticleDist } from "./button-identity-article.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const articleBodyPath = join("src", "_blog", "articles", "a-button-named-save-is-not-an-identity.html");

test("Button identity article builds with eight sections, identity layers, ladder, chains, discovery, links, and sitemap metadata", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("Button identity validator rejects planted runtime, reachability, deployment, parity, migration, and intent claims", async (t) => {
  for (const claim of [
    "TraceMap proves the Save button fires the event at runtime.",
    "The user clicked Save before the failure.",
    "The deployment succeeded with identical behavior.",
    "The migration succeeded.",
    "This article confirms the button is reachable in production.",
    "Parity confirmed across both pages."
  ]) {
    await t.test(claim, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, articleBodyPath);
      const html = await readFile(path, "utf8");
      await writeFile(path, `${html}<p>${claim}</p>`, "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /unsupported positive claim/);
    });
  }
});

test("Button identity validator rejects forbidden material in published article metadata", async (t) => {
  for (const [field, planted] of [
    ["description", "TraceMap proves the handler is the same button."],
    ["ogDescription", "SELECT secret FROM customer_table"],
    ["cardDescription", "The deployment succeeded for both pages."],
    ["hero", "TraceMap confirms the user clicked Save."]
  ]) {
    await t.test(field, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, "src", "_blog", "articles.json");
      const articles = JSON.parse(await readFile(path, "utf8"));
      articles.find((article) => article.slug === "a-button-named-save-is-not-an-identity")[field] = planted;
      await writeFile(path, `${JSON.stringify(articles, null, 2)}\n`, "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /unsupported positive claim|raw or executable material/);
    });
  }
});

test("Button identity validator rejects positive claims and executable material inside the non-claim boundary", async (t) => {
  for (const planted of ["<p>TraceMap proves the button executed its handler.</p>", "<p>SELECT value FROM private_table</p>"]) {
    await t.test(planted, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, articleBodyPath);
      const html = await readFile(path, "utf8");
      await writeFile(
        path,
        html.replace(
          '<section data-save-identity-block="non-claims" data-save-identity-boundary="non-claims" data-tm-boundary="claim-boundary">',
          `<section data-save-identity-block="non-claims" data-save-identity-boundary="non-claims" data-tm-boundary="claim-boundary">${planted}`
        ),
        "utf8"
      );
      await buildSite({ root, log() {} });
      const errors = [];
      await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /unsupported positive claim|raw or executable material/);
    });
  }
});

test("Button identity validator rejects planted private and executable material", async (t) => {
  const slash = String.fromCharCode(47);
  for (const leak of [`${slash}Users${slash}example${slash}private`, "Password=leak-sentinel", "SELECT value FROM private_table"]) {
    await t.test(leak, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, articleBodyPath);
      const html = await readFile(path, "utf8");
      await writeFile(path, `${html}<p>${leak}</p>`, "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /hard private material|raw or executable material/);
    });
  }
});

test("Button identity validator rejects tokens split across markup tags and entity-encoded tokens", async (t) => {
  for (const [name, planted, expected] of [
    ["split private path", "<p>/Use<span>rs/</span>example</p>", /hard private material/],
    ["split sql keyword", "<p>SEL<span>ECT value </span>FROM private_table</p>", /raw or executable material/],
    ["split lowercase sql keyword", "<p>sel<span>ect</span> value fr<span>om</span> private_table</p>", /raw or executable material/],
    ["entity-encoded private path", "<p>/Us&#101rs/example</p>", /hard private material/],
    ["entity-encoded claim word", "<p>TraceMap pr&#111ves the button is reachable.</p>", /unsupported positive claim/]
  ]) {
    await t.test(name, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, articleBodyPath);
      const html = await readFile(path, "utf8");
      await writeFile(path, `${html}${planted}`, "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), expected);
    });
  }
});

test("Button identity validator requires each structural section and the claim-boundary attributes", async (t) => {
  await t.test("missing identity-layers block", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace('data-save-identity-block="identity-layers"', 'data-save-identity-block="layers"'), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /missing required block: identity-layers/);
  });

  await t.test("missing fail-closed block", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace('data-save-identity-block="fail-closed"', 'data-save-identity-block="closed"'), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /missing required block: fail-closed/);
  });

  await t.test("dropped claim-boundary attribute", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace(' data-tm-boundary="claim-boundary"', ""), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /must carry data-tm-boundary="claim-boundary"/);
  });

  await t.test("dropped boundary attribute", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace(' data-save-identity-boundary="non-claims"', ""), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /must carry data-save-identity-boundary="non-claims"/);
  });
});

test("Button identity validator accepts reordered boundary attributes", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, articleBodyPath);
  const html = await readFile(path, "utf8");
  await writeFile(
    path,
    html.replace(
      '<section data-save-identity-block="non-claims" data-save-identity-boundary="non-claims" data-tm-boundary="claim-boundary">',
      '<section data-tm-boundary="claim-boundary" data-save-identity-boundary="non-claims" data-save-identity-block="non-claims">'
    ),
    "utf8"
  );
  await buildSite({ root, log() {} });
  const errors = [];
  await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("Button identity validator requires the four-tier resolution ladder", async (t) => {
  await t.test("renamed structural tier", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace("<strong>Tier2Structural.</strong>", "<strong>Second tier.</strong>"), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /missing resolution ladder tier: Tier2Structural/);
  });

  await t.test("renamed gap tier", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace("<strong>Tier4Unknown.</strong>", "<strong>Unknown tier.</strong>"), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /missing resolution ladder tier: Tier4Unknown/);
  });
});

test("Button identity validator requires the identity-layer vocabulary", async (t) => {
  await t.test("renamed code-behind scope layer", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace("<strong>Linked code-behind scope.</strong>", "<strong>Code file.</strong>"), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /missing identity layer: Linked code-behind scope/);
  });

  await t.test("renamed canonical handler layer", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace("<strong>Canonical handler method symbol.</strong>", "<strong>Handler name.</strong>"), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /missing identity layer: Canonical handler method symbol/);
  });
});

test("Button identity validator requires labeled steps inside both synthetic chains", async (t) => {
  await t.test("semantic chain loses semantic step", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace("<strong>semantic:</strong>", "<strong>resolved:</strong>"), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /chain semantic-page is missing labeled step: semantic/);
  });

  await t.test("textual chain loses candidate step", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace("<strong>candidate:</strong>", "<strong>maybe:</strong>"), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /chain textual-page is missing labeled step: candidate/);
  });

  await t.test("textual chain loses syntax/textual step", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace("<strong>syntax/textual:</strong>", "<strong>text:</strong>"), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /chain textual-page is missing labeled step: syntax\/textual/);
  });

  await t.test("removed chain attribute", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, articleBodyPath);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace('data-save-identity-chain="textual-page"', 'data-save-identity-chain="other"'), "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /missing chain: textual-page/);
  });
});

test("Button identity validator rejects rule IDs outside the verified catalog list", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, articleBodyPath);
  const html = await readFile(path, "utf8");
  await writeFile(path, `${html}<p>Under <code>fake.webforms.identity.v1</code> the join would be safe.</p>`, "utf8");
  await buildSite({ root, log() {} });
  const errors = [];
  await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /outside the verified catalog list: fake\.webforms\.identity\.v1/);
});

test("Button identity validator requires every cited rule to remain present", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, articleBodyPath);
  const html = await readFile(path, "utf8");
  await writeFile(path, html.replace("legacy.webforms.designer-control.v1", "legacy.webforms.designer.v1"), "utf8");
  await buildSite({ root, log() {} });
  const errors = [];
  await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /missing required rule ID: legacy\.webforms\.designer-control\.v1/);
  assert.match(errors.join("\n"), /outside the verified catalog list: legacy\.webforms\.designer\.v1/);
});

test("Button identity validator safety-scans discovery entry strings", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, "src", "_site", "discovery.json");
  const entries = JSON.parse(await readFile(path, "utf8"));
  entries.find((entry) => entry.path === "/blog/a-button-named-save-is-not-an-identity/").summary = "The migration succeeded.";
  await writeFile(path, `${JSON.stringify(entries, null, 2)}\n`, "utf8");
  await buildSite({ root, log() {} });
  const errors = [];
  await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /unsupported positive claim/);
  assert.match(errors.join("\n"), /routes-index\.json/);
});

test("Button identity validator reports missing discovery entry instead of crashing on null entries", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  await writeFile(join(root, "dist", "routes-index.json"), JSON.stringify({ entries: [null] }), "utf8");
  const errors = [];
  await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /discovery entry is missing/);
});

test("Button identity validator reports malformed discovery arrays instead of throwing", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  await writeFile(
    join(root, "dist", "routes-index.json"),
    JSON.stringify({
      entries: [
        {
          path: "/blog/a-button-named-save-is-not-an-identity/",
          title: "A Button Named Save Is Not an Identity",
          summary: "A concept-level identity guide.",
          publicClaimLevel: "concept",
          preferredProofPath: "/legacy-modernization/evidence-map/",
          limitations: 5,
          nonClaims: true
        }
      ]
    }),
    "utf8"
  );
  const errors = [];
  await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /at least two limitations/);
  assert.match(errors.join("\n"), /at least two non-claims/);
});

test("Button identity validator rejects discovery claim-level and proof-path drift", async (t) => {
  await t.test("claim level drift", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, "src", "_site", "discovery.json");
    const entries = JSON.parse(await readFile(path, "utf8"));
    entries.find((entry) => entry.path === "/blog/a-button-named-save-is-not-an-identity/").publicClaimLevel = "demo";
    await writeFile(path, `${JSON.stringify(entries, null, 2)}\n`, "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /claim level must be concept/);
  });

  await t.test("proof-path drift", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, "src", "_site", "discovery.json");
    const entries = JSON.parse(await readFile(path, "utf8"));
    entries.find((entry) => entry.path === "/blog/a-button-named-save-is-not-an-identity/").preferredProofPath = "/evidence/";
    await writeFile(path, `${JSON.stringify(entries, null, 2)}\n`, "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /preferred proof path must remain/);
  });
});

test("Button identity validator aggregates malformed discovery output", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  await writeFile(join(root, "dist", "routes-index.json"), "{not-json", "utf8");
  const errors = [];
  await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /invalid JSON/);
});

test("Button identity validator accepts attribute spacing and the supplied canonical base URL", async (t) => {
  const root = await createSiteFixture(t);
  const sourcePath = join(root, articleBodyPath);
  await writeFile(sourcePath, (await readFile(sourcePath, "utf8")).replace('data-save-identity-block="identity-layers"', 'data-save-identity-block = "identity-layers"'), "utf8");
  await buildSite({ root, log() {} });
  const pagePath = join(root, "dist", "blog", "a-button-named-save-is-not-an-identity", "index.html");
  await writeFile(pagePath, (await readFile(pagePath, "utf8")).replaceAll("https://tracemap.tools", "https://preview.example"), "utf8");
  const sitemapPath = join(root, "dist", "sitemap.xml");
  await writeFile(sitemapPath, (await readFile(sitemapPath, "utf8")).replaceAll("https://tracemap.tools", "https://preview.example"), "utf8");
  const errors = [];
  await validateButtonIdentityArticleDist({ baseUrl: "https://preview.example/", dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("Button identity validator tight-scans blog card and discovery strings", async (t) => {
  await t.test("whitespace-free sql in blog card description", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, "src", "_blog", "articles.json");
    const articles = JSON.parse(await readFile(path, "utf8"));
    articles.find((article) => article.slug === "a-button-named-save-is-not-an-identity").cardDescription = "SELECTsecretFROMprivate_table";
    await writeFile(path, `${JSON.stringify(articles, null, 2)}\n`, "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /raw or executable material/);
    assert.match(errors.join("\n"), /blog\/index\.html/);
  });

  await t.test("whitespace-free sql in discovery summary", async (subtest) => {
    const root = await createSiteFixture(subtest);
    const path = join(root, "src", "_site", "discovery.json");
    const entries = JSON.parse(await readFile(path, "utf8"));
    entries.find((entry) => entry.path === "/blog/a-button-named-save-is-not-an-identity/").summary = "SELECTsecretFROMprivate_table";
    await writeFile(path, `${JSON.stringify(entries, null, 2)}\n`, "utf8");
    await buildSite({ root, log() {} });
    const errors = [];
    await validateButtonIdentityArticleDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /raw or executable material/);
    assert.match(errors.join("\n"), /routes-index\.json/);
  });
});

async function createSiteFixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-button-identity-site-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
