import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { buildSite } from "./build.mjs";
import { validatePrivatePocPublicCapabilityArticleDist } from "./private-poc-public-capability-article.mjs";
import { EvidenceTiers } from "./validate-utils.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const articleBodyPath = join("src", "_blog", "articles", "private-poc-pain-to-public-safe-capability.html");
const articleRoute = "/blog/private-poc-pain-to-public-safe-capability/";

test("Private POC article builds with the eight sections, promotion chain, registry, discovery, proof path, and sitemap", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  const errors = [];
  await validatePrivatePocPublicCapabilityArticleDist({ dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("Private POC validator rejects a missing promotion chain and claim boundary", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, articleBodyPath);
  const html = await readFile(path, "utf8");
  await writeFile(
    path,
    html
      .replace(/<ol\s+data-private-poc-chain="promotion">[\s\S]*?<\/ol>/, "<ol data-private-poc-chain=\"wrong\"></ol>")
      .replace(/\s+data-private-poc-boundary="claim-boundary"/, "")
      .replace(/\s+data-tm-boundary="claim-boundary"/, ""),
    "utf8"
  );
  await buildSite({ root, log() {} });
  const errors = [];
  await validatePrivatePocPublicCapabilityArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /missing promotion chain|must carry data-private-poc-boundary|must carry data-tm-boundary/);
});

test("Private POC validator scans article metadata, blog registry card, and discovery copy", async (t) => {
  for (const [surface, parts, mutate] of [
    ["article", ["src", "_blog", "articles", "private-poc-pain-to-public-safe-capability.html"], (value) => `${value}<p>TraceMap proves the private application is safe to deploy.</p>`],
    ["metadata", ["src", "_blog", "articles.json"], (value) => value.replace("How TraceMap turns a private research signal", "TraceMap proves a private research signal")],
    ["discovery", ["src", "_site", "discovery.json"], (value) => value.replace("A concept-level guide to separating private observation", "TraceMap proves a private observation")]
  ]) {
    await t.test(surface, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, ...parts);
      const original = await readFile(path, "utf8");
      await writeFile(path, mutate(original), "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validatePrivatePocPublicCapabilityArticleDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /unsupported positive claim/);
    });
  }
});

test("Private POC validator rejects raw executable material", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, articleBodyPath);
  const html = await readFile(path, "utf8");
  await writeFile(path, `${html}<p>SELECT value FROM private_table</p>`, "utf8");
  await buildSite({ root, log() {} });
  const errors = [];
  await validatePrivatePocPublicCapabilityArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /raw or executable material/);
});

test("Private POC validator catches tag-split claims and lowercase SQL", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, articleBodyPath);
  const html = await readFile(path, "utf8");
  await writeFile(
    path,
    `${html}<p>Trace<span></span>Map<div></div>proves<div></div>private material is safe to deploy.</p><p>se<span></span>lect<div></div>value<div></div>fr<span></span>om private_table</p>`,
    "utf8"
  );
  await buildSite({ root, log() {} });
  const errors = [];
  await validatePrivatePocPublicCapabilityArticleDist({ dist: join(root, "dist"), errors });
  assert.ok(errors.some((error) => error.message.includes("unsupported positive claim") && error.message.includes("TraceMap")));
  assert.match(errors.join("\n"), /raw or executable material/);
});

test("Private POC validator emits catalogued tiers and integer artifact spans", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, articleBodyPath);
  const html = await readFile(path, "utf8");
  await writeFile(path, html.replace('data-private-poc-block="private-signal"', 'data-private-poc-block="missing-signal"'), "utf8");
  await buildSite({ root, log() {} });
  const errors = [];
  await validatePrivatePocPublicCapabilityArticleDist({ dist: join(root, "dist"), errors });
  const finding = errors.find((error) => error.message.includes("missing required section: private-signal"));
  assert.ok(finding);
  assert.deepEqual(finding.line_span, { start_line: 1, end_line: 1 });
  assert.equal(finding.evidence_tier, EvidenceTiers.Tier4Unknown);
  assert.deepEqual(finding.evidence[0].line_span, { start_line: 1, end_line: 1 });
  assert.ok(Object.values(EvidenceTiers).includes(finding.evidence[0].evidence_tier));
});

test("Private POC validator requires the exact public proof path", async (t) => {
  const root = await createSiteFixture(t);
  const discoveryPath = join(root, "src", "_site", "discovery.json");
  const entries = JSON.parse(await readFile(discoveryPath, "utf8"));
  entries.find((entry) => entry.path === articleRoute).preferredProofPath = "/evidence/";
  await writeFile(discoveryPath, `${JSON.stringify(entries, null, 2)}\n`, "utf8");
  await buildSite({ root, log() {} });
  const errors = [];
  await validatePrivatePocPublicCapabilityArticleDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /preferred proof path must remain \/proof-paths\//);
});

async function createSiteFixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-private-poc-site-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
