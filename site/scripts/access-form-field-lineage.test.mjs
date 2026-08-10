import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { buildSite } from "./build.mjs";
import { validateAccessFormFieldLineageDist } from "./access-form-field-lineage.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

test("Access form-to-field article builds with rules, links, discovery, and sitemap metadata", async (t) => {
  const root = await fixture(t); await buildSite({ root, log() {} }); const errors = [];
  await validateAccessFormFieldLineageDist({ dist: join(root, "dist"), errors }); assert.deepEqual(errors, []);
});

test("Access form-to-field validator rejects planted runtime, safety, and reconstruction claims", async (t) => {
  for (const claim of ["TraceMap proved runtime execution.", "The path guarantees the write.", "The reconstruction succeeded.", "The database is safe to run."]) {
    await t.test(claim, async (subtest) => { const root = await fixture(subtest); await append(root, claim); await buildSite({ root, log() {} }); const errors = []; await validateAccessFormFieldLineageDist({ dist: join(root, "dist"), errors }); assert.match(errors.join("\n"), /unsupported positive claim/); });
  }
});

test("Access form-to-field validator rejects planted private and executable material", async (t) => {
  const slash = String.fromCharCode(47);
  for (const leak of [`${slash}Users${slash}example${slash}private`, "Password=lineage-leak", "SELECT value FROM private_table"]) {
    await t.test(leak, async (subtest) => { const root = await fixture(subtest); await append(root, leak); await buildSite({ root, log() {} }); const errors = []; await validateAccessFormFieldLineageDist({ dist: join(root, "dist"), errors }); assert.match(errors.join("\n"), /hard private material|raw or executable material/); });
  }
});

test("Access form-to-field validator rejects missing companion cross-link and discovery drift", async (t) => {
  const root = await fixture(t);
  const companion = join(root, "src", "_blog", "articles", "reverse-engineering-access-without-running-it.html");
  await writeFile(companion, (await readFile(companion, "utf8")).replace('href="/blog/access-form-to-field-lineage/"', 'href="/blog/"'), "utf8");
  const discovery = join(root, "src", "_site", "discovery.json"); const entries = JSON.parse(await readFile(discovery, "utf8")); entries.find((entry) => entry.path === "/blog/access-form-to-field-lineage/").publicClaimLevel = "concept"; await writeFile(discovery, `${JSON.stringify(entries, null, 2)}\n`, "utf8");
  await buildSite({ root, log() {} }); const errors = []; await validateAccessFormFieldLineageDist({ dist: join(root, "dist"), errors }); assert.match(errors.join("\n"), /companion article link is missing/); assert.match(errors.join("\n"), /claim level must be demo/);
});

async function fixture(t) { const root = await mkdtemp(join(tmpdir(), "tracemap-access-lineage-site-")); t.after(() => rm(root, { recursive: true, force: true })); await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true }); return root; }
async function append(root, text) { const path = join(root, "src", "_blog", "articles", "access-form-to-field-lineage.html"); await writeFile(path, `${await readFile(path, "utf8")}<p>${text}</p>`, "utf8"); }
