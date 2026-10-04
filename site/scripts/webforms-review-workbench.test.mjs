import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import { buildSite } from "./build.mjs";
import { validateWebFormsReviewWorkbenchDist } from "./webforms-review-workbench.mjs";

const siteRoot = resolve(fileURLToPath(new URL("..", import.meta.url)));
const repositoryRoot = resolve(siteRoot, "..");
const implementationStatePath = resolve(repositoryRoot, ".kiro/specs/site-webforms-review-workbench/implementation-state.md");

async function validateFixture(root, errors, overrides = {}) {
  await validateWebFormsReviewWorkbenchDist({
    dist: join(root, "dist"),
    errors,
    root,
    repositoryRoot,
    implementationStatePath,
    ...overrides
  });
}

test("Web Forms review workbench builds with seven steps, proof dependency, metadata, links, and boundaries", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateFixture(root, errors);
  assert.deepEqual(errors, []);
});

test("Web Forms review workbench enforces exact seven-step order", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src", "webforms", "review-workbench", "index.html");
  const page = await readFile(pagePath, "utf8");
  await writeFile(pagePath, page.replace('data-review-step="4"', 'data-review-step="6"'));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateFixture(root, errors);
  assert.match(errors.join("\n"), /step order must be exactly 1 through 7/);
});

test("Web Forms review workbench binds route and provenance to the #806 projection", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src", "webforms", "review-workbench", "index.html");
  const page = await readFile(pagePath, "utf8");
  await writeFile(pagePath, page
    .replace('data-route-hop="dynamic-provider"', 'data-route-hop="provider-guess"')
    .replace("a9aae27d806b21bb1d8c48f861d0b82533e0862f1c8b12e1683ad58027f4946c", "0".repeat(64)));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateFixture(root, errors);
  const joined = errors.join("\n");
  assert.match(joined, /ordered route does not match/);
  assert.match(joined, /missing an exact #806 projection digest/);
});

test("Web Forms review workbench requires evidence metadata and visible missing support", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src", "webforms", "review-workbench", "index.html");
  const page = await readFile(pagePath, "utf8");
  await writeFile(pagePath, page
    .replace('data-evidence-tier="Tier2Structural"', 'data-evidence-tier="Tier3SyntaxOrTextual"')
    .replaceAll("unavailable-in-concept-projection", "support-present"));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateFixture(root, errors);
  const joined = errors.join("\n");
  assert.match(joined, /evidence detail is missing data-evidence-tier/);
  assert.match(joined, /missing supporting-reference and namespace boundary/);
});

test("Web Forms review workbench requires review states and independent inventory bounds", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src", "webforms", "review-workbench", "index.html");
  const page = await readFile(pagePath, "utf8");
  await writeFile(pagePath, page
    .replace('data-review-state="query-omitted"', 'data-review-state="query-returned"')
    .replace('data-server-inventory-limit="10000"', 'data-server-inventory-limit="5000"')
    .replaceAll("WebFormsModernizationGapLimitReached", "SpecificGapAlwaysSurvives"));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateFixture(root, errors);
  const joined = errors.join("\n");
  assert.match(joined, /missing review state: query-omitted/);
  assert.match(joined, /independent 10,000-row client and server inventory bounds/);
  assert.match(joined, /WebFormsModernizationGapLimitReached/);
});

test("Web Forms review workbench reports a missing proof asset without throwing", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  await rm(join(root, "dist", "assets", "webforms-source-compiled-proof.json"));
  const errors = [];
  await validateFixture(root, errors);
  assert.match(errors.join("\n"), /missing required #806 proof asset/);
});

test("Web Forms review workbench requires active inbound and outbound links", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src", "webforms", "review-workbench", "index.html");
  await writeFile(pagePath, (await readFile(pagePath, "utf8")).replaceAll('href="/manager-packet/"', 'href="/limitations/"'));
  const docsPath = join(root, "src", "docs", "index.html");
  await writeFile(docsPath, (await readFile(docsPath, "utf8")).replace('href="/webforms/review-workbench/"', 'href="/webforms/source-plus-compiled-proof/"'));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateFixture(root, errors);
  const joined = errors.join("\n");
  assert.match(joined, /missing active required link: \/manager-packet\//);
  assert.match(joined, /missing inbound link from: \/docs\//);
});

test("Web Forms review workbench rejects private material and affirmative runtime claims", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src", "webforms", "review-workbench", "index.html");
  const page = await readFile(pagePath, "utf8");
  await writeFile(pagePath, page.replace("</main>", "<p>/tmp/private-report</p><p>TraceMap ran the Web Forms page.</p></main>"));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateFixture(root, errors);
  assert.ok(errors.filter((error) => String(error).includes("forbidden public material or claim")).length >= 2);
});

test("Web Forms review workbench rejects discovery and implementation-boundary drift", async (t) => {
  const root = await fixture(t);
  const discoveryPath = join(root, "src", "_site", "discovery.json");
  const discovery = JSON.parse(await readFile(discoveryPath, "utf8"));
  discovery.find((entry) => entry.path === "/webforms/review-workbench/").publicClaimLevel = "shipped";
  await writeFile(discoveryPath, `${JSON.stringify(discovery, null, 2)}\n`);
  const pagePath = join(root, "src", "webforms", "review-workbench", "index.html");
  await writeFile(pagePath, (await readFile(pagePath, "utf8")).replace(
    'data-implementation-base="5fd50ebec3bef40c7c0b3660a754ab08cab45982"',
    'data-implementation-base="0000000000000000000000000000000000000000"'
  ));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateFixture(root, errors);
  const joined = errors.join("\n");
  assert.match(joined, /discovery entry/);
  assert.match(joined, /page boundary does not match/);
});

async function fixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-webforms-review-workbench-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
