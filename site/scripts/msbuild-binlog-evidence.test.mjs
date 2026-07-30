import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { buildSite } from "./build.mjs";
import { validateMsbuildBinlogEvidenceDist } from "./msbuild-binlog-evidence.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

test("MSBuild binlog article and proof packet build with bounded evidence", async (t) => {
  const root = await createFixture(t);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateMsbuildBinlogEvidenceDist({ dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("MSBuild binlog proof packet rejects arbitrary fields", async (t) => {
  const root = await createFixture(t);
  const assetPath = join(root, "src", "assets", "msbuild-binlog-proof-packet.json");
  const packet = JSON.parse(await readFile(assetPath, "utf8"));
  packet.observation.message = "not public";
  await writeFile(assetPath, `${JSON.stringify(packet, null, 2)}\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateMsbuildBinlogEvidenceDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /observation fields must match the reviewed contract/);
});

test("MSBuild binlog proof packet rejects local paths and connection material", async (t) => {
  for (const value of ["/Users/example/private.binlog", "Host=private-db.invalid;Password=fixture"]) {
    await t.test(value.split("=")[0], async (subtest) => {
      const root = await createFixture(subtest);
      const assetPath = join(root, "src", "assets", "msbuild-binlog-proof-packet.json");
      const packet = JSON.parse(await readFile(assetPath, "utf8"));
      packet.purpose = value;
      await writeFile(assetPath, `${JSON.stringify(packet, null, 2)}\n`);
      await buildSite({ root, log() {} });
      const errors = [];
      await validateMsbuildBinlogEvidenceDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /forbidden private or executable material/);
    });
  }
});

test("MSBuild binlog proof packet pins counts, rule, tier, coverage, and limitations", async (t) => {
  const root = await createFixture(t);
  const assetPath = join(root, "src", "assets", "msbuild-binlog-proof-packet.json");
  const packet = JSON.parse(await readFile(assetPath, "utf8"));
  packet.observation.projectObservationCount = 2;
  packet.observation.evidenceTier = "Tier1Semantic";
  packet.limitations.pop();
  await writeFile(assetPath, `${JSON.stringify(packet, null, 2)}\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateMsbuildBinlogEvidenceDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /observation field is unexpected: evidenceTier/);
  assert.match(errors.join("\n"), /observation field is unexpected: projectObservationCount/);
  assert.match(errors.join("\n"), /limitations must match the reviewed set/);
});

test("MSBuild binlog article rejects unsupported success claims", async (t) => {
  const root = await createFixture(t);
  const metadataPath = join(root, "src", "_blog", "articles", "what-an-msbuild-binlog-knows-that-a-source-diff-does-not.html");
  const article = await readFile(metadataPath, "utf8");
  await writeFile(metadataPath, `${article}<p>TraceMap proves tests passed and the release is safe.</p>\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateMsbuildBinlogEvidenceDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /unsupported public claim/);
});

async function createFixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-msbuild-binlog-site-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
