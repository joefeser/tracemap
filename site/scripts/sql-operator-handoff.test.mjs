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
  const slash = String.fromCharCode(47);
  const privatePath = `${slash}Users${slash}example${slash}private`;
  await writeFile(pagePath, html.replace("</main>", `<p>${privatePath}</p><p>SELECT * FROM private_table</p></main>`));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlOperatorHandoffDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /forbidden private, executable, or overclaim text/);
});

test("SQL operator handoff validator accepts equivalent attribute formatting", async (t) => {
  const root = await createSiteFixture(t);
  const source = join(root, "src");
  const pagePath = join(source, "sql", "operator-handoff", "index.html");
  const html = await readFile(pagePath, "utf8");
  await writeFile(
    pagePath,
    html
      .replace('<meta property="og:type" content="article">', "<meta content='article' property = 'og:type'>")
      .replace('rel="canonical" href="https://tracemap.tools/sql/operator-handoff/"', "href='https://tracemap.tools/sql/operator-handoff/' rel = 'canonical'")
  );
  for (const route of ["manager-packet", "outputs", "limitations", "proof-paths/for-managers", "packets"]) {
    const inboundPath = join(source, route, "index.html");
    const inbound = await readFile(inboundPath, "utf8");
    await writeFile(inboundPath, inbound.replace('href="/sql/operator-handoff/"', "href = '/sql/operator-handoff/'"));
  }
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlOperatorHandoffDist({ dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("SQL operator handoff validator rejects tag-split unsafe content", async (t) => {
  const root = await createSiteFixture(t);
  const pagePath = join(root, "src", "sql", "operator-handoff", "index.html");
  const html = await readFile(pagePath, "utf8");
  await writeFile(pagePath, html.replace("</main>", "<p>SEL<span>ECT</span> * FROM private_table</p><p>/Us<span>ers/</span>example/private</p></main>"));
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
