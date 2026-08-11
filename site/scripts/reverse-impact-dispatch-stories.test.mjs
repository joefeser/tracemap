import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { buildSite } from "./build.mjs";
import { reverseImpactRoute, staticDispatchRoute, validateReverseImpactDispatchStoriesDist } from "./reverse-impact-dispatch-stories.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

test("reverse-impact and static-dispatch stories build with bounded discovery", async (t) => { const root = await fixture(t); await buildSite({ root, log() {} }); const errors = []; await validateReverseImpactDispatchStoriesDist({ dist: join(root, "dist"), errors }); assert.deepEqual(errors, []); });

test("story validator rejects unsupported claims in either article", async (t) => {
  for (const [slug, claim] of [["what-depends-on-this-symbol", "TraceMap proves runtime reachability."], ["interfaces-make-blast-radius-harder", "The implementation is selected at runtime."]]) await t.test(slug, async (subtest) => { const root = await fixture(subtest); const path = join(root, `src/_blog/articles/${slug}.html`); await writeFile(path, `${await readFile(path, "utf8")}<p>${claim}</p>`, "utf8"); await buildSite({ root, log() {} }); const errors = []; await validateReverseImpactDispatchStoriesDist({ dist: join(root, "dist"), errors }); assert.match(errors.join("\n"), /unsupported positive claim/); });
});

test("story validator rejects private and executable material", async (t) => {
  for (const leak of ["/Us" + "ers/example/workspace", "/Us&#101rs/example/workspace", "Password=secret-value", "SELECT value FROM private_table"]) await t.test(leak, async (subtest) => { const root = await fixture(subtest); const path = join(root, "src/_blog/articles/what-depends-on-this-symbol.html"); await writeFile(path, `${await readFile(path, "utf8")}<p>${leak}</p>`, "utf8"); await buildSite({ root, log() {} }); const errors = []; await validateReverseImpactDispatchStoriesDist({ dist: join(root, "dist"), errors }); assert.match(errors.join("\n"), /hard private material|source or executable material/); });
});

test("story validator scans cards and discovery text", async (t) => {
  const root = await fixture(t); const articlesPath = join(root, "src/_blog/articles.json"); const articles = JSON.parse(await readFile(articlesPath, "utf8")); articles.find((article) => article.slug === "what-depends-on-this-symbol").cardDescription = "TraceMap proves runtime reachability."; await writeFile(articlesPath, `${JSON.stringify(articles, null, 2)}\n`, "utf8"); await buildSite({ root, log() {} }); const discoveryPath = join(root, "dist/routes-index.json"); const discovery = JSON.parse(await readFile(discoveryPath, "utf8")); discovery.entries.find((entry) => entry.path === staticDispatchRoute).summary = "/Us" + "ers/example/workspace"; await writeFile(discoveryPath, `${JSON.stringify(discovery, null, 2)}\n`, "utf8"); const errors = []; await validateReverseImpactDispatchStoriesDist({ dist: join(root, "dist"), errors }); assert.match(errors.join("\n"), /blog card contains unsupported positive claim/); assert.match(errors.join("\n"), /discovery entry contains hard private material/);
});

test("story validator rejects route, cross-link, sitemap, and discovery drift", async (t) => {
  const root = await fixture(t); await buildSite({ root, log() {} }); const pagePath = join(root, "dist/blog/what-depends-on-this-symbol/index.html"); await writeFile(pagePath, (await readFile(pagePath, "utf8")).replaceAll(staticDispatchRoute, "/blog/"), "utf8"); await rm(join(root, "dist/sitemap.xml")); const discoveryPath = join(root, "dist/routes-index.json"); const discovery = JSON.parse(await readFile(discoveryPath, "utf8")); discovery.entries.find((entry) => entry.path === reverseImpactRoute).publicClaimLevel = "concept"; await writeFile(discoveryPath, `${JSON.stringify(discovery, null, 2)}\n`, "utf8"); const errors = []; await validateReverseImpactDispatchStoriesDist({ dist: join(root, "dist"), errors }); assert.match(errors.join("\n"), /sitemap is missing/); assert.match(errors.join("\n"), /missing required link/); assert.match(errors.join("\n"), /claim level must be demo/);
});

test("story validator does not accept data-href as a navigable cross-link", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const pagePath = join(root, "dist/blog/what-depends-on-this-symbol/index.html");
  const page = await readFile(pagePath, "utf8");
  await writeFile(pagePath, page.replaceAll(`href="${staticDispatchRoute}"`, `data-href="${staticDispatchRoute}"`), "utf8");
  const errors = [];
  await validateReverseImpactDispatchStoriesDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /missing required link/);
});

test("story validator reports malformed discovery without throwing", async (t) => {
  for (const malformed of ["{", JSON.stringify({ entries: {} })]) await t.test(malformed, async (subtest) => { const root = await fixture(subtest); await buildSite({ root, log() {} }); await writeFile(join(root, "dist/routes-index.json"), malformed, "utf8"); const errors = []; await validateReverseImpactDispatchStoriesDist({ dist: join(root, "dist"), errors }); assert.match(errors.join("\n"), /valid JSON|entries array/); });
});

async function fixture(t) { const root = await mkdtemp(join(tmpdir(), "tracemap-reverse-dispatch-site-")); t.after(() => rm(root, { recursive: true, force: true })); await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true }); return root; }
