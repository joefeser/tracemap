import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { buildSite } from "./build.mjs";
import { generateWebFormsSourceCompiledProof } from "./generate-webforms-source-compiled-proof.mjs";
import { validateWebFormsSourceCompiledProofDist } from "./webforms-source-compiled-proof.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

test("Web Forms source + compiled proof builds with exact provenance, per-hop tiers, links, and non-claims", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateWebFormsSourceCompiledProofDist({ dist: join(root, "dist"), errors, root });
  assert.deepEqual(errors, []);
});

test("Web Forms proof generator reproduces the checked-in asset", async (t) => {
  const root = await fixture(t);
  const generated = join(root, "generated.json");
  await generateWebFormsSourceCompiledProof({ inputPath: join(root, "src", "_data", "webforms-source-compiled-proof-input.json"), outputPath: generated });
  const expected = JSON.parse(await readFile(join(root, "src", "assets", "webforms-source-compiled-proof.json"), "utf8"));
  const actual = JSON.parse(await readFile(generated, "utf8"));
  assert.deepEqual(actual, expected);
});

test("Web Forms proof rejects tier flattening, protected fields, and raw SQL", async (t) => {
  const root = await fixture(t);
  const assetPath = join(root, "src", "assets", "webforms-source-compiled-proof.json");
  const packet = JSON.parse(await readFile(assetPath, "utf8"));
  packet.bridgeTierExamples = packet.bridgeTierExamples.filter((row) => row.evidenceTier !== "Tier2Structural");
  packet.rawSql = "SELECT private_value FROM private_table";
  await writeFile(assetPath, `${JSON.stringify(packet, null, 2)}\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateWebFormsSourceCompiledProofDist({ dist: join(root, "dist"), errors, root });
  assert.match(errors.join("\n"), /preserve compiled bridge emission at Tier2Structural/);
  assert.match(errors.join("\n"), /forbidden public material/);
  assert.match(errors.join("\n"), /protected key/);
});

test("Web Forms proof rejects stale generator and bounded-input hashes", async (t) => {
  const root = await fixture(t);
  const assetPath = join(root, "src", "assets", "webforms-source-compiled-proof.json");
  const packet = JSON.parse(await readFile(assetPath, "utf8"));
  packet.provenance.generatorSha256 = "0".repeat(64);
  packet.provenance.boundedInputSha256 = "f".repeat(64);
  await writeFile(assetPath, `${JSON.stringify(packet, null, 2)}\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateWebFormsSourceCompiledProofDist({ dist: join(root, "dist"), errors, root });
  assert.match(errors.join("\n"), /generator SHA-256 does not match/);
  assert.match(errors.join("\n"), /bounded-input SHA-256 does not match/);
});

async function fixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-webforms-proof-"));
  t.after(async () => rm(root, { recursive: true, force: true }));
  await cp(siteRoot, root, { recursive: true, filter: (source) => !source.includes(`${join(siteRoot, "dist")}`) && !source.includes(`${join(siteRoot, "node_modules")}`) });
  return root;
}
