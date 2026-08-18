import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { buildSite } from "./build.mjs";
import {
  beforeAddingAnotherLanguageDefineScanRoute,
  validateBeforeAddingAnotherLanguageDefineScanDist
} from "./before-adding-another-language-define-scan.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const articleBodyPath = join("src", "_blog", "articles", "before-adding-another-language-define-scan.html");
const machineLocalPath = ["/", "Users", "/"].join("") + "example/private-repo";

test("language-scan contract article builds with registry, discovery, matrix, artifacts, and boundaries", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateBeforeAddingAnotherLanguageDefineScanDist({ dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("validator accepts reordered boundary attributes but rejects missing boundary attributes", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, articleBodyPath);
  const original = await readFile(path, "utf8");
  await writeFile(path, original.replace(
    '<section data-language-scan-block="claim-boundary" data-language-scan-boundary="claim-boundary" data-tm-boundary="claim-boundary">',
    '<section data-tm-boundary="claim-boundary" data-language-scan-boundary="claim-boundary" data-language-scan-block="claim-boundary">'
  ), "utf8");
  await buildSite({ root, log() {} });
  const accepted = [];
  await validateBeforeAddingAnotherLanguageDefineScanDist({ dist: join(root, "dist"), errors: accepted });
  assert.deepEqual(accepted, []);

  await writeFile(path, original.replace(/ data-language-scan-boundary="claim-boundary" data-tm-boundary="claim-boundary"/, ""), "utf8");
  await buildSite({ root, log() {} });
  const rejected = [];
  await validateBeforeAddingAnotherLanguageDefineScanDist({ dist: join(root, "dist"), errors: rejected });
  assert.match(rejected.join("\n"), /must carry data-language-scan-boundary|must carry data-tm-boundary/);
});

test("validator fail-closes malformed discovery, missing artifacts, outcomes, and fake rules", async (t) => {
  const cases = [
    ["malformed discovery", async (root) => writeFile(join(root, "dist", "routes-index.json"), JSON.stringify({ entries: {} }), "utf8"), /must contain an entries array/],
    ["missing artifact", async (root) => mutateBody(root, (value) => value.replaceAll("index.sqlite", "index-database")), /missing required artifact: index\.sqlite/],
    ["missing outcome", async (root) => mutateBody(root, (value) => value.replaceAll("not-run", "not-executed")), /missing required outcome: not-run/],
    ["fake rule", async (root) => mutateBody(root, (value) => `${value}<p>fake.rule.v1</p>`), /outside the verified catalog list: fake\.rule\.v1/]
  ];
  for (const [name, mutate, expected] of cases) {
    await t.test(name, async (subtest) => {
      const root = await createSiteFixture(subtest);
      await buildSite({ root, log() {} });
      await mutate(root);
      const errors = [];
      await validateBeforeAddingAnotherLanguageDefineScanDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), expected);
    });
  }
});

test("validator catches tag-split claims and numeric browser entities", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  const pagePath = join(root, "dist", "blog", "before-adding-another-language-define-scan", "index.html");
  const original = await readFile(pagePath, "utf8");
  await writeFile(pagePath, `${original}<p>Trace<span></span>Map&#32;proves&#32;semantic&#32;parity.</p>`, "utf8");
  const errors = [];
  await validateBeforeAddingAnotherLanguageDefineScanDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /unsupported positive claim/);
});

test("validator scans article metadata, blog card, and discovery copy for claims and raw material", async (t) => {
  const cases = [
    ["article claim", ["src", "_blog", "articles", "before-adding-another-language-define-scan.html"], (value) => `${value}<p>TraceMap guarantees complete coverage.</p>`, /unsupported positive claim/],
    ["metadata claim", ["src", "_blog", "articles.json"], (value) => value.replace("Define the scan contract first:", "TraceMap guarantees complete coverage:"), /unsupported positive claim/],
    ["discovery claim", ["src", "_site", "discovery.json"], (value) => value.replace("A concept-level guide to the evidence", "TraceMap guarantees complete coverage in the evidence"), /unsupported positive claim/],
    ["article private path", ["src", "_blog", "articles", "before-adding-another-language-define-scan.html"], (value) => `${value}<p>${machineLocalPath}</p>`, /hard private material/],
    ["metadata raw SQL", ["src", "_blog", "articles.json"], (value) => value.replace("Define the scan contract first:", "SELECT value FROM private_table. Define the scan contract first:"), /raw or executable material/],
    ["discovery raw SQL", ["src", "_site", "discovery.json"], (value) => value.replace("A concept-level guide to the evidence", "SELECT value FROM private_table. A concept-level guide to the evidence"), /raw or executable material/]
  ];
  for (const [name, parts, mutate, expected] of cases) {
    await t.test(name, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, ...parts);
      const original = await readFile(path, "utf8");
      await writeFile(path, mutate(original), "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateBeforeAddingAnotherLanguageDefineScanDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), expected);
    });
  }
});

test("validator exposes structured conformance evidence for a missing required section", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, articleBodyPath);
  const original = await readFile(path, "utf8");
  await writeFile(path, original.replace('data-language-scan-block="authority"', 'data-language-scan-block="missing-authority"'), "utf8");
  await buildSite({ root, log() {} });
  const errors = [];
  await validateBeforeAddingAnotherLanguageDefineScanDist({ dist: join(root, "dist"), errors });
  const finding = errors.find((error) => error.message.includes("missing required section: authority"));
  assert.ok(finding);
  assert.equal(finding.rule_id, "adapter.scan-truth.conformance.v1");
  assert.equal(finding.evidence_tier, "Tier2Structural");
  assert.equal(finding.extractor_version, "before-adding-another-language-define-scan-validator.v1");
  assert.equal(finding.evidence[0].line_span.start_line, 1);
  assert.ok(finding.evidence[0].line_span.end_line > 1);
});

async function mutateBody(root, mutate) {
  const path = join(root, articleBodyPath);
  const original = await readFile(path, "utf8");
  await writeFile(path, mutate(original), "utf8");
  await buildSite({ root, log() {} });
}

async function createSiteFixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-language-scan-site-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
