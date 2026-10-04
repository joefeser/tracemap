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

test("Web Forms capability refresh rejects visible capability-level drift", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const path = join(root, "dist", "capabilities", "index.html");
  await writeFile(path, (await readFile(path, "utf8")).replace("Concept · source + compiled projection", "Shipped · source + compiled projection"));
  const errors = [];
  await validateWebFormsCapabilityRefreshDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /expected source-compiled visible claim label: Concept · source \+ compiled projection/);
});

test("Web Forms capability refresh audits future boundaries on each intended route", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const capabilities = join(root, "dist", "capabilities", "index.html");
  const roadmap = join(root, "dist", "roadmap", "index.html");
  await writeFile(capabilities, (await readFile(capabilities, "utf8")).replace("Still future", "Now shipped"));
  await writeFile(roadmap, (await readFile(roadmap, "utf8"))
    .replace("are not established", "are shipped")
    .replace(
      /(<tr id="claim-webforms-future-automation"[^>]*data-evidence-status=)"future-only"([^>]*data-wording-status=)"future-facing"/,
      '$1"evidence-backed"$2"live"'
    ));
  const errors = [];
  await validateWebFormsCapabilityRefreshDist({ dist: join(root, "dist"), errors });
  const joined = errors.join("\n");
  assert.match(joined, /route \/capabilities\/ is missing future-only boundary wording: Still future/);
  assert.match(joined, /route \/roadmap\/ is missing future-only boundary wording: are not established/);
  assert.match(joined, /roadmap future automation row must retain future-only evidence and future-facing wording statuses/);
});

test("Web Forms capability refresh preserves claim levels on every supporting ladder", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const mutations = [
    ["manager-packet", "Concept projection", "Shipped projection"],
    ["manager-faq", "remains concept-level", "is shipped"],
    [join("proof-paths", "for-managers"), "concept projection", "shipped projection"],
    [join("legacy-modernization", "review-handoff"), "concept source-plus-compiled projection", "shipped source-plus-compiled projection"]
  ];
  for (const [route, before, after] of mutations) {
    const path = join(root, "dist", route, "index.html");
    await writeFile(path, (await readFile(path, "utf8")).replace(before, after));
  }
  const errors = [];
  await validateWebFormsCapabilityRefreshDist({ dist: join(root, "dist"), errors });
  const joined = errors.join("\n");
  assert.match(joined, /route \/manager-packet\/ is missing ladder claim: Concept projection/);
  assert.match(joined, /route \/manager-faq\/ is missing ladder claim: source-plus-compiled projection remains concept-level/);
  assert.match(joined, /route \/proof-paths\/for-managers\/ is missing ladder claim: concept projection/);
  assert.match(joined, /route \/legacy-modernization\/review-handoff\/ is missing ladder claim: concept source-plus-compiled projection/);
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

test("Web Forms capability refresh safety-scans every audited discovery text field", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const path = join(root, "dist", "routes-index.json");
  const index = JSON.parse(await readFile(path, "utf8"));
  const entry = index.entries.find((item) => item.path === "/manager-packet/");
  const privatePath = ["", "Users", "private", "work"].join("/");
  entry.summary = "TraceMap confirms Web Forms runs in production.";
  entry.limitations[0] = privatePath;
  entry.nonClaims[0] = "Password&#61;synthetic";
  await writeFile(path, `${JSON.stringify(index, null, 2)}\n`);
  const errors = [];
  await validateWebFormsCapabilityRefreshDist({ dist: join(root, "dist"), errors });
  const joined = errors.join("\n");
  assert.match(joined, /discovery summary for \/manager-packet\/ contains forbidden public material or claim.*TraceMap/);
  assert.match(joined, /discovery limitations for \/manager-packet\/ contains forbidden public material or claim.*Users/);
  assert.match(joined, /discovery nonClaims for \/manager-packet\/ contains forbidden public material or claim.*Password/);
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

test("Web Forms capability refresh scans browser-visible tokens collapsed across tags", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const path = join(root, "dist", "manager-packet", "index.html");
  const html = (await readFile(path, "utf8")).replace(
    "</main>",
    "<p>/Us<span>ers</span>/private/work</p><p>TraceMap pro<span>ves</span> the Web Forms page executes at runtime.</p></main>"
  );
  await writeFile(path, html);
  const errors = [];
  await validateWebFormsCapabilityRefreshDist({ dist: join(root, "dist"), errors });
  const joined = errors.join("\n");
  assert.match(joined, /manager-packet.*forbidden public material or claim.*Users/);
  assert.match(joined, /manager-packet.*forbidden public material or claim.*TraceMap/);
});

test("Web Forms capability refresh rejects passive runtime-proof wording", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const path = join(root, "dist", "manager-packet", "index.html");
  await writeFile(path, (await readFile(path, "utf8")).replace("</main>", "<p>Web Forms runtime execution is proven.</p></main>"));
  const errors = [];
  await validateWebFormsCapabilityRefreshDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /manager-packet.*forbidden public material or claim.*runtime/);
});

async function fixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-webforms-capability-refresh-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
