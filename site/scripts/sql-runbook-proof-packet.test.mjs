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

test("SQL runbook proof packet validator rejects every required planted leak category", async (t) => {
  const slash = String.fromCharCode(47);
  const leakCases = [
    ["executable statement", "SELECT fixture_value FROM private_table"],
    ["credential", "Password=credential-leak-sentinel"],
    ["connection string", "Server=private-host.invalid;User Id=fixture;Password=credential-leak-sentinel"],
    ["infrastructure name", "private-infrastructure-leak-sentinel"],
    ["machine-local path", `${slash}Users${slash}example${slash}private`],
    ["validation output", "validation-output-leak-sentinel"],
    ["ticket identifier", "ticket-12345"],
    ["scheduled command body", "raw-scheduled-command-leak-sentinel"]
  ];

  for (const [label, leak] of leakCases) {
    await t.test(label, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const assetPath = join(root, "src", "assets", "sql-operator-runbook-proof-packet.json");
      const packet = JSON.parse(await readFile(assetPath, "utf8"));
      packet.limitations.push(leak);
      await writeFile(assetPath, `${JSON.stringify(packet, null, 2)}\n`);
      await buildSite({ root, log() {} });
      const errors = [];
      await validateSqlRunbookProofPacketDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /forbidden private, executable, protected, or overclaim text/);
    });
  }
});

test("SQL runbook proof packet validator rejects tag-split protected text", async (t) => {
  const root = await createSiteFixture(t);
  const pagePath = join(root, "src", "sql", "operator-handoff", "proof-packet", "index.html");
  const html = await readFile(pagePath, "utf8");
  await writeFile(pagePath, html.replace("</main>", "<p>Passw<span>ord</span>=tag-split-leak</p></main>"));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlRunbookProofPacketDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /forbidden private, executable, protected, or overclaim text/);
});

test("SQL runbook proof packet validator rejects tag-split protected text with quoted angle brackets", async (t) => {
  const root = await createSiteFixture(t);
  const pagePath = join(root, "src", "sql", "operator-handoff", "proof-packet", "index.html");
  const html = await readFile(pagePath, "utf8");
  await writeFile(pagePath, html.replace("</main>", '<p>Passw<em title=">">ord</em>=quoted-tag-leak</p></main>'));
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

test("SQL runbook proof packet validator reports a malformed protectedSteps collection", async (t) => {
  const root = await createSiteFixture(t);
  const assetPath = join(root, "src", "assets", "sql-operator-runbook-proof-packet.json");
  const packet = JSON.parse(await readFile(assetPath, "utf8"));
  packet.protectedSteps = {};
  await writeFile(assetPath, `${JSON.stringify(packet, null, 2)}\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlRunbookProofPacketDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /protectedSteps must be an array/);
});

async function createSiteFixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-sql-proof-packet-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
