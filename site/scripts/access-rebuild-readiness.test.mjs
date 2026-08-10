import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { buildSite } from "./build.mjs";
import { validateAccessRebuildReadinessDist } from "./access-rebuild-readiness.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

test("Access rebuild-readiness article builds with bounded evidence and discovery", async (t) => {
  const root = await fixture(t); await buildSite({ root, log() {} }); const errors = [];
  await validateAccessRebuildReadinessDist({ dist: join(root, "dist"), errors }); assert.deepEqual(errors, []);
});

test("Access rebuild-readiness validator rejects positive runtime and reconstruction claims", async (t) => {
  for (const claim of ["TraceMap proves the rebuild is complete.", "The application is safe.", "The reconstruction succeeded."]) {
    await t.test(claim, async (subtest) => { const root = await fixture(subtest); await append(root, claim); await buildSite({ root, log() {} }); const errors = []; await validateAccessRebuildReadinessDist({ dist: join(root, "dist"), errors }); assert.match(errors.join("\n"), /unsupported positive claim/); });
  }
});

test("Access rebuild-readiness validator rejects private and executable material including encoded forms", async (t) => {
  for (const leak of ["/Users/example/private", "/Us<span>ers</span>/example/private", "/Us&#101rs/example/private", "SE<span>LECT</span> value FROM private_table", "Password=secret-value"]) {
    await t.test(leak, async (subtest) => { const root = await fixture(subtest); await append(root, leak); await buildSite({ root, log() {} }); const errors = []; await validateAccessRebuildReadinessDist({ dist: join(root, "dist"), errors }); assert.match(errors.join("\n"), /hard private material|raw or executable material/); });
  }
});

test("Access rebuild-readiness validator reports discovery and companion-link drift", async (t) => {
  const root = await fixture(t);
  const discoveryPath = join(root, "src/_site/discovery.json");
  const entries = JSON.parse(await readFile(discoveryPath, "utf8"));
  entries.find((entry) => entry.path === "/blog/access-rebuild-readiness-gaps/").publicClaimLevel = "concept";
  await writeFile(discoveryPath, `${JSON.stringify(entries, null, 2)}\n`, "utf8");
  const companionPath = join(root, "src/_blog/articles/access-form-to-field-lineage.html");
  await writeFile(companionPath, (await readFile(companionPath, "utf8")).replace('href="/blog/access-rebuild-readiness-gaps/"', 'href="/blog/"'), "utf8");
  await buildSite({ root, log() {} }); const errors = []; await validateAccessRebuildReadinessDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /claim level must be demo/); assert.match(errors.join("\n"), /companion link is missing/);
});

test("Access rebuild-readiness validator reports malformed discovery without throwing", async (t) => {
  for (const malformed of ["{", JSON.stringify({ entries: {} })]) {
    await t.test(malformed, async (subtest) => { const root = await fixture(subtest); await buildSite({ root, log() {} }); await writeFile(join(root, "dist/routes-index.json"), malformed, "utf8"); const errors = []; await validateAccessRebuildReadinessDist({ dist: join(root, "dist"), errors }); assert.match(errors.join("\n"), /valid JSON|entries array/); });
  }
});

test("Access rebuild-readiness validator accepts attribute spacing and supplied base URL", async (t) => {
  const root = await fixture(t); await buildSite({ root, log() {} });
  const pagePath = join(root, "dist/blog/access-rebuild-readiness-gaps/index.html");
  await writeFile(pagePath, (await readFile(pagePath, "utf8")).replaceAll("data-access-readiness-block=", "data-access-readiness-block = ").replaceAll("https://tracemap.tools", "https://preview.example"), "utf8");
  const sitemapPath = join(root, "dist/sitemap.xml"); await writeFile(sitemapPath, (await readFile(sitemapPath, "utf8")).replaceAll("https://tracemap.tools", "https://preview.example"), "utf8");
  const errors = []; await validateAccessRebuildReadinessDist({ baseUrl: "https://preview.example/", dist: join(root, "dist"), errors }); assert.deepEqual(errors, []);
});

async function fixture(t) { const root = await mkdtemp(join(tmpdir(), "tracemap-access-readiness-site-")); t.after(() => rm(root, { recursive: true, force: true })); await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true }); return root; }
async function append(root, text) { const path = join(root, "src/_blog/articles/access-rebuild-readiness-gaps.html"); await writeFile(path, `${await readFile(path, "utf8")}<p>${text}</p>`, "utf8"); }
