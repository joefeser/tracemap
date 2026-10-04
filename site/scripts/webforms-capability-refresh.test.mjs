import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import { buildSite } from "./build.mjs";
import { validateWebFormsCapabilityRefreshDist } from "./webforms-capability-refresh.mjs";

const siteRoot = resolve(fileURLToPath(new URL("..", import.meta.url)));

test("Web Forms capability refresh preserves links, levels, future boundaries, and discovery metadata", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateWebFormsCapabilityRefreshDist({ dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("Web Forms capability refresh rejects missing proof links", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const path = join(root, "dist", "manager-faq", "index.html");
  await writeFile(path, (await readFile(path, "utf8")).replaceAll('href="/webforms/local-demo/"', 'href="/limitations/"'));
  const errors = [];
  await validateWebFormsCapabilityRefreshDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /manager-faq.*missing proof link: \/webforms\/local-demo\//);
});

test("Web Forms capability refresh rejects claim-level drift and future-work promotion", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const capabilities = join(root, "dist", "capabilities", "index.html");
  const roadmap = join(root, "dist", "roadmap", "index.html");
  await writeFile(capabilities, (await readFile(capabilities, "utf8"))
    .replace('data-webforms-capability="source-compiled" data-public-claim-level="concept"', 'data-webforms-capability="source-compiled" data-public-claim-level="shipped"')
    .replaceAll("Automatic solution-wide discovery", "Automatic repository onboarding"));
  await writeFile(roadmap, (await readFile(roadmap, "utf8")).replaceAll("Automatic solution-wide discovery", "Automatic repository onboarding"));
  const errors = [];
  await validateWebFormsCapabilityRefreshDist({ dist: join(root, "dist"), errors });
  const joined = errors.join("\n");
  assert.match(joined, /source-compiled claim level concept/);
  assert.match(joined, /future-work boundary: Automatic solution-wide discovery/);
});

test("Web Forms capability refresh audits future boundaries on each intended route", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const path = join(root, "dist", "capabilities", "index.html");
  await writeFile(path, (await readFile(path, "utf8")).replaceAll("Automatic solution-wide discovery", "Automatic repository onboarding"));
  const errors = [];
  await validateWebFormsCapabilityRefreshDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /route \/capabilities\/ is missing future-work boundary: Automatic solution-wide discovery/);
});

test("Web Forms capability refresh pins discovery claim and proof metadata", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const path = join(root, "dist", "routes-index.json");
  const index = JSON.parse(await readFile(path, "utf8"));
  const entry = index.entries.find((item) => item.path === "/manager-packet/");
  entry.publicClaimLevel = "shipped";
  entry.preferredProofPath = "/capabilities/";
  await writeFile(path, `${JSON.stringify(index, null, 2)}\n`);
  const errors = [];
  await validateWebFormsCapabilityRefreshDist({ dist: join(root, "dist"), errors });
  const joined = errors.join("\n");
  assert.match(joined, /discovery publicClaimLevel for \/manager-packet\/ must be demo/);
  assert.match(joined, /discovery preferredProofPath for \/manager-packet\/ must be \/demo\/proof-upgrades\//);
});

test("Web Forms capability refresh rejects private material and affirmative runtime claims", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const path = join(root, "dist", "manager-packet", "index.html");
  const privatePath = ["", "Users", "private", "work"].join("/");
  await writeFile(path, (await readFile(path, "utf8")).replace("</main>", `<p>${privatePath}</p><p>TraceMap ran the Web Forms page.</p></main>`));
  const errors = [];
  await validateWebFormsCapabilityRefreshDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /manager-packet.*forbidden public material or claim/);
});

test("Web Forms capability refresh decodes link targets and rejects ordinary runtime overclaims", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const path = join(root, "dist", "manager-packet", "index.html");
  const html = (await readFile(path, "utf8")).replace(
    "</main>",
    '<a href="/limitations/?Password&#61;synthetic">Boundary</a><p>TraceMap proves the Web Forms page executes at runtime.</p></main>'
  );
  await writeFile(path, html);
  const errors = [];
  await validateWebFormsCapabilityRefreshDist({ dist: join(root, "dist"), errors });
  const joined = errors.join("\n");
  assert.match(joined, /manager-packet.*forbidden public material or claim.*Password/);
  assert.match(joined, /manager-packet.*forbidden public material or claim.*TraceMap/);
});

async function fixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-webforms-capability-refresh-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
