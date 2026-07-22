import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { buildSite } from "./build.mjs";
import { validateSqlStaticObservedValidationDist } from "./sql-static-observed-validation.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

test("SQL static/observed validation route builds with bounded claims and discovery", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlStaticObservedValidationDist({ dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("SQL static/observed validation rejects executable and overclaim text", async (t) => {
  const root = await fixture(t);
  const page = join(root, "src", "sql", "operator-handoff", "validation", "index.html");
  const html = await readFile(page, "utf8");
  await writeFile(page, html.replace("</main>", "<p>SELECT * FROM private_table</p><p>validation passed</p></main>"));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlStaticObservedValidationDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /forbidden private, executable, or overclaim text/);
});

test("SQL static/observed validation rejects tag-split private paths with quoted greater-than attributes", async (t) => {
  const root = await fixture(t);
  const page = join(root, "src", "sql", "operator-handoff", "validation", "index.html");
  const html = await readFile(page, "utf8");
  await writeFile(page, html.replace("</main>", '<p>/U<span data-marker=">">s</span>ers/example</p></main>'));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlStaticObservedValidationDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /forbidden private, executable, or overclaim text/);
});

test("SQL static/observed validation accepts equivalent inbound href formatting", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  for (const relative of ["sql/operator-handoff/index.html", "sql/operator-handoff/proof-packet/index.html"]) {
    const path = join(root, "dist", relative);
    const html = await readFile(path, "utf8");
    await writeFile(path, html.replace(`href="/sql/operator-handoff/validation/"`, "href = '/sql/operator-handoff/validation/'"));
  }
  const errors = [];
  await validateSqlStaticObservedValidationDist({ dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

async function fixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-sql-static-observed-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
