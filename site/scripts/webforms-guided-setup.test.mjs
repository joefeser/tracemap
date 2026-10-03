import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import { buildSite } from "./build.mjs";
import { validateWebformsGuidedSetupDist } from "./webforms-guided-setup.mjs";

const siteRoot = resolve(fileURLToPath(new URL("..", import.meta.url)));

test("Web Forms guided setup builds with workflow, metadata, links, and boundaries", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log: () => {} });
  const errors = [];
  await validateWebformsGuidedSetupDist({ baseUrl: "https://tracemap.tools", dist: join(root, "dist"), errors, root });
  assert.deepEqual(errors, []);
});

test("Web Forms guided setup rejects missing workflow concepts and inbound links", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src/webforms/index.html");
  const articlePath = join(root, "src/_blog/articles/modernizing-web-forms-without-running-it.html");
  await writeFile(pagePath, (await readFile(pagePath, "utf8")).replaceAll("forms.txt", "selection-file"));
  await writeFile(articlePath, (await readFile(articlePath, "utf8")).replace('href="/webforms/"', 'href="/limitations/"'));
  await buildSite({ root, log: () => {} });
  const errors = [];
  await validateWebformsGuidedSetupDist({ baseUrl: "https://tracemap.tools", dist: join(root, "dist"), errors, root });
  assert.ok(errors.some((error) => String(error).includes("forms.txt")));
  assert.ok(errors.some((error) => String(error).includes("must link")));
});

test("Web Forms guided setup rejects overclaims and private material", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src/webforms/index.html");
  const page = await readFile(pagePath, "utf8");
  await writeFile(pagePath, page.replace("</main>", "<p>Publication was verified.</p><p>/Us<span>ers</span>/example/private</p></main>"));
  await buildSite({ root, log: () => {} });
  const errors = [];
  await validateWebformsGuidedSetupDist({ baseUrl: "https://tracemap.tools", dist: join(root, "dist"), errors, root });
  assert.ok(errors.filter((error) => String(error).includes("forbidden")).length >= 2);
});

test("Web Forms guided setup rejects discovery claim drift", async (t) => {
  const root = await fixture(t);
  const discoveryPath = join(root, "src/_site/discovery.json");
  const discovery = JSON.parse(await readFile(discoveryPath, "utf8"));
  discovery.find((entry) => entry.path === "/webforms/").publicClaimLevel = "demo";
  await writeFile(discoveryPath, `${JSON.stringify(discovery, null, 2)}\n`);
  await buildSite({ root, log: () => {} });
  const errors = [];
  await validateWebformsGuidedSetupDist({ baseUrl: "https://tracemap.tools", dist: join(root, "dist"), errors, root });
  assert.ok(errors.some((error) => String(error).includes("claim level")));
});

async function fixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-webforms-setup-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
