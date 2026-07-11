import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { buildSite } from "./build.mjs";
import { validateSqlOperatorHandoffDist } from "./sql-operator-handoff.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

test("SQL operator handoff route builds with safe metadata, claims, and inbound links", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlOperatorHandoffDist({ dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("SQL operator handoff validator rejects executable SQL and private paths", async (t) => {
  const root = await createSiteFixture(t);
  const pagePath = join(root, "src", "sql", "operator-handoff", "index.html");
  const html = await readFile(pagePath, "utf8");
  await writeFile(pagePath, html.replace("</main>", "<p>/Users/example/private</p><p>SELECT * FROM private_table</p></main>"));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlOperatorHandoffDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /forbidden private, executable, or overclaim text/);
});

async function createSiteFixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-sql-handoff-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
