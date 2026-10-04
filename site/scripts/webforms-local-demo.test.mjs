import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import { buildSite } from "./build.mjs";
import { validateWebFormsLocalDemoDist } from "./webforms-local-demo.mjs";

const siteRoot = resolve(fileURLToPath(new URL("..", import.meta.url)));
const repositoryRoot = resolve(siteRoot, "..");
const implementationStatePath = resolve(repositoryRoot, ".kiro/specs/site-webforms-local-demo/implementation-state.md");

async function validateFixture(root, errors, overrides = {}) {
  await validateWebFormsLocalDemoDist({
    dist: join(root, "dist"),
    errors,
    root,
    repositoryRoot,
    implementationStatePath,
    ...overrides
  });
}

test("Web Forms local demo builds with exact-tree receipt, artifact map, links, and boundaries", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateFixture(root, errors);
  assert.deepEqual(errors, []);
});

test("Web Forms local demo requires all layouts, query views, and fail-closed outcomes", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src", "webforms", "local-demo", "index.html");
  let page = await readFile(pagePath, "utf8");
  page = page.replaceAll("separate-dll-only", "provider-layout");
  page = page.replaceAll("DEEP_CORPUS_WINDOWS_REQUIRED", "WINDOWS_REQUIRED");
  page = page.replaceAll("TruncatedByLimit", "bounded-result");
  await writeFile(pagePath, page);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateFixture(root, errors);
  assert.match(errors.join("\n"), /separate-dll-only/);
  assert.match(errors.join("\n"), /DEEP_CORPUS_WINDOWS_REQUIRED/);
  assert.match(errors.join("\n"), /TruncatedByLimit/);
});

test("Web Forms local demo requires rule, tier, coverage, and provenance on reversed and repeat examples", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src", "webforms", "local-demo", "index.html");
  const page = await readFile(pagePath, "utf8");
  await writeFile(pagePath, page
    .replace('data-evidence-example="reversed" data-rule-id="validation.deep-projectless-corpus.v1"', 'data-evidence-example="reversed"')
    .replace('data-evidence-example="repeat"', 'data-evidence-example="repeat-without-contract"'));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateFixture(root, errors);
  const joined = errors.join("\n");
  assert.match(joined, /reversed evidence metadata is missing data-rule-id/);
  assert.match(joined, /missing active evidence metadata for repeat/);
});

test("Web Forms local demo ignores required evidence hidden in HTML comments", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src", "webforms", "local-demo", "index.html");
  const page = await readFile(pagePath, "utf8");
  await writeFile(pagePath, page.replaceAll("separate-dll-only", "provider-layout").replace(
    "<div><strong><code>all</code></strong>",
    "<!-- separate-dll-only --><div><strong><code>all</code></strong>"
  ));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateFixture(root, errors);
  assert.match(errors.join("\n"), /separate-dll-only/);
});

test("Web Forms local demo rejects durable-result and projection provenance drift", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src", "webforms", "local-demo", "index.html");
  const page = await readFile(pagePath, "utf8");
  await writeFile(pagePath, page
    .replace("37125942534", "00000000000")
    .replace("529f399c5492f89d12785beb091eda70b7ea6191be60c8fd53a81e818027ec39", "0".repeat(64))
    .replace("23000bf21810695f70f3b5e9f96f460037c1611534b8a5ab7d63a8d63effcd54", "f".repeat(64)));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateFixture(root, errors);
  const joined = errors.join("\n");
  assert.match(joined, /durable result|exact result provenance/);
  assert.match(joined, /projection digest/);
});

test("Web Forms local demo requires active inbound and outbound links", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src", "webforms", "local-demo", "index.html");
  const guidedPath = join(root, "src", "webforms", "index.html");
  await writeFile(pagePath, (await readFile(pagePath, "utf8")).replaceAll('href="/webforms/"', 'href="/limitations/"'));
  await writeFile(guidedPath, (await readFile(guidedPath, "utf8")).replace(
    '<a href="/webforms/local-demo/">Reproducible local demo</a>',
    '<!-- <a href="/webforms/local-demo/">Reproducible local demo</a> -->'
  ));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateFixture(root, errors);
  assert.match(errors.join("\n"), /active required link: \/webforms\//);
  assert.match(errors.join("\n"), /missing inbound link from: \/webforms\//);
});

test("Web Forms local demo reports a missing proof asset instead of throwing", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  await rm(join(root, "dist", "assets", "webforms-source-compiled-proof.json"));
  const errors = [];
  await validateFixture(root, errors);
  assert.match(errors.join("\n"), /missing required proof asset/);
});

test("Web Forms local demo rejects public private material and affirmative runtime claims", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src", "webforms", "local-demo", "index.html");
  const page = await readFile(pagePath, "utf8");
  await writeFile(pagePath, page.replace("</main>", "<p>/Us<span>ers</span>/private/work</p><p>/tmp/report</p><p>/var/folders/private-run</p><p>~/private-run</p><p>TraceMap ran the Web Forms database.</p></main>"));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateFixture(root, errors);
  assert.ok(errors.filter((error) => String(error).includes("forbidden public material or claim")).length >= 2);
});

test("Web Forms local demo treats unavailable shallow history as unverifiable, not disproven", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateFixture(root, errors, { repositoryRoot: root });
  assert.doesNotMatch(errors.join("\n"), /implementation base .*not an ancestor/);
  assert.deepEqual(errors, []);
});

test("Web Forms local demo rejects discovery and branch-boundary drift", async (t) => {
  const root = await fixture(t);
  const discoveryPath = join(root, "src", "_site", "discovery.json");
  const discovery = JSON.parse(await readFile(discoveryPath, "utf8"));
  discovery.find((entry) => entry.path === "/webforms/local-demo/").publicClaimLevel = "shipped";
  await writeFile(discoveryPath, `${JSON.stringify(discovery, null, 2)}\n`);
  const pagePath = join(root, "src", "webforms", "local-demo", "index.html");
  await writeFile(pagePath, (await readFile(pagePath, "utf8")).replace(
    'data-main-boundary="79ac39cf4459e913944786d755566568d02eccab"',
    'data-main-boundary="0000000000000000000000000000000000000000"'
  ));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateFixture(root, errors);
  const joined = errors.join("\n");
  assert.match(joined, /discovery entry/);
  assert.match(joined, /page boundary does not match|not a verified ancestor/);
});

async function fixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-webforms-local-demo-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
