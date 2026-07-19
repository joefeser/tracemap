import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { buildSite } from "./build.mjs";
import { validateSqlRunbookProofPacketDist } from "./sql-runbook-proof-packet.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

test("SQL runbook proof packet builds with public provenance, bounded claims, discovery, and inbound links", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlRunbookProofPacketDist({ dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("SQL runbook proof packet validator rejects private, executable, and protected payloads", async (t) => {
  const root = await createSiteFixture(t);
  const assetPath = join(root, "src", "assets", "sql-operator-runbook-proof-packet.json");
  const packet = JSON.parse(await readFile(assetPath, "utf8"));
  packet.ownerQuestions.push("SELECT fixture_value FROM private_table");
  packet.limitations.push("private-password-leak-sentinel");
  await writeFile(assetPath, `${JSON.stringify(packet, null, 2)}\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlRunbookProofPacketDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /forbidden private, executable, protected, or overclaim text/);
});

test("SQL runbook proof packet validator rejects incomplete evidence provenance and invalid permission status", async (t) => {
  const root = await createSiteFixture(t);
  const assetPath = join(root, "src", "assets", "sql-operator-runbook-proof-packet.json");
  const packet = JSON.parse(await readFile(assetPath, "utf8"));
  packet.prerequisites[0].status = "effective";
  packet.evidence[0].extractorVersion = "";
  packet.evidence[1].lineSpan.startLine = 0;
  await writeFile(assetPath, `${JSON.stringify(packet, null, 2)}\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlRunbookProofPacketDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /invalid permission status: effective/);
  assert.match(errors.join("\n"), /missing rule, tier, or extractor provenance/);
  assert.match(errors.join("\n"), /invalid line span/);
});

test("SQL runbook proof packet validator rejects a broken public evidence reference", async (t) => {
  const root = await createSiteFixture(t);
  const assetPath = join(root, "src", "assets", "sql-operator-runbook-proof-packet.json");
  const packet = JSON.parse(await readFile(assetPath, "utf8"));
  packet.gaps[0].evidenceRef = "missing-evidence-row";
  await writeFile(assetPath, `${JSON.stringify(packet, null, 2)}\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlRunbookProofPacketDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /gaps row references missing evidence: missing-evidence-row/);
});

async function createSiteFixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-sql-proof-packet-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
