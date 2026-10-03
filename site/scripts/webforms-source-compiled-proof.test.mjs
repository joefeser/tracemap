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

test("Web Forms source + compiled concept builds with exact provenance, illustrative hops, links, and non-claims", async (t) => {
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

test("Web Forms proof generator rejects multiline raw SQL in an allowlisted string", async (t) => {
  const root = await fixture(t);
  const inputPath = join(root, "src", "_data", "webforms-source-compiled-proof-input.json");
  const input = JSON.parse(await readFile(inputPath, "utf8"));
  input.limitations.push("SELECT\nprivate_value FROM private_table");
  await writeFile(inputPath, `${JSON.stringify(input, null, 2)}\n`);
  await assert.rejects(
    generateWebFormsSourceCompiledProof({ inputPath, outputPath: join(root, "generated.json") }),
    /Forbidden value/
  );
});

test("Web Forms proof generator rejects multiline raw SQL beyond the former scan window", async (t) => {
  const root = await fixture(t);
  const inputPath = join(root, "src", "_data", "webforms-source-compiled-proof-input.json");
  const input = JSON.parse(await readFile(inputPath, "utf8"));
  input.limitations.push(`SELECT\n${"x".repeat(4096)} FROM private_table`);
  await writeFile(inputPath, `${JSON.stringify(input, null, 2)}\n`);
  await assert.rejects(
    generateWebFormsSourceCompiledProof({ inputPath, outputPath: join(root, "generated.json") }),
    /Forbidden value/
  );
});

test("Web Forms concept generator rejects reintroduced self-authored support aliases", async (t) => {
  const root = await fixture(t);
  const inputPath = join(root, "src", "_data", "webforms-source-compiled-proof-input.json");
  const input = JSON.parse(await readFile(inputPath, "utf8"));
  input.outcomes[0].orderedHops[0].supportingEvidenceIds = ["self-authored-alias"];
  await writeFile(inputPath, `${JSON.stringify(input, null, 2)}\n`);
  await assert.rejects(
    generateWebFormsSourceCompiledProof({ inputPath, outputPath: join(root, "generated.json") }),
    /Unallowlisted or protected key/
  );
});

test("Web Forms concept rejects self-derived evidence aliases, canonical drift, complete status, and upgraded bridges", async (t) => {
  const root = await fixture(t);
  const assetPath = join(root, "src", "assets", "webforms-source-compiled-proof.json");
  const packet = JSON.parse(await readFile(assetPath, "utf8"));
  packet.coverage.resultStatus = "complete";
  packet.limitations[0] = `${packet.limitations[0]} Altered.`;
  packet.limitations = packet.limitations.filter((value) => !value.includes("No independent extractor output"));
  const constructor = packet.outcomes[0].orderedHops.find((hop) => hop.id === "dynamic-constructor");
  constructor.evidenceTier = "Tier2Structural";
  constructor.supportingEvidenceIds = ["self-derived-alias"];
  packet.evidence = [{ id: "self-derived-alias" }];
  await writeFile(assetPath, `${JSON.stringify(packet, null, 2)}\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateWebFormsSourceCompiledProofDist({ dist: join(root, "dist"), errors, root });
  const joined = errors.join("\n");
  assert.match(joined, /result status as partial/);
  assert.match(joined, /independent extractor output is not checked in/);
  assert.match(joined, /unbound compiled bridge hop at Tier3SyntaxOrTextual/);
  assert.match(joined, /must not publish a self-derived evidence registry/);
  assert.match(joined, /must not publish unverified supporting evidence IDs/);
  assert.match(joined, /does not match a fresh canonical projection/);
});

test("Web Forms proof reports missing provenance inputs without throwing", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log() {} });
  await rm(join(root, "scripts", "generate-webforms-source-compiled-proof.mjs"));
  const errors = [];
  await validateWebFormsSourceCompiledProofDist({ dist: join(root, "dist"), errors, root });
  assert.match(errors.join("\n"), /provenance could not be verified/);
});

test("Web Forms proof rejects rendered page drift from the checked-in asset", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src", "webforms", "source-plus-compiled-proof", "index.html");
  const page = await readFile(pagePath, "utf8");
  await writeFile(pagePath, page.replace("a9aae27d806b21bb1d8c48f861d0b82533e0862f1c8b12e1683ad58027f4946c", "0".repeat(64)));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateWebFormsSourceCompiledProofDist({ dist: join(root, "dist"), errors, root });
  assert.match(errors.join("\n"), /page is stale relative to asset digest/);
});

async function fixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-webforms-proof-"));
  t.after(async () => rm(root, { recursive: true, force: true }));
  await cp(siteRoot, root, { recursive: true, filter: (source) => !source.includes(`${join(siteRoot, "dist")}`) && !source.includes(`${join(siteRoot, "node_modules")}`) });
  return root;
}
