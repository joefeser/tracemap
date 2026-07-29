import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { buildSite } from "./build.mjs";
import { validateSqlProjectRefactorIntentStoryDist } from "./sql-project-refactor-intent-story.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

test("SQL project refactor-intent story builds with bounded claims, provenance, gaps, discovery, and links", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlProjectRefactorIntentStoryDist({ dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("SQL project refactor-intent validator rejects planted private, raw, command, key, SQL, and XML leaks", async (t) => {
  const slash = String.fromCharCode(47);
  const leakCases = [
    ["local path", `${slash}Users${slash}example${slash}private`],
    ["credential", "Password=credential-leak-sentinel"],
    ["connection string", "Server=private-host.invalid;User Id=fixture"],
    ["private infrastructure", "private-infrastructure-leak-sentinel"],
    ["operation key", "8e1d9dd1-93d4-45d9-aa3b-172dd15585e2"],
    ["copyable command", "SqlPackage /Action:Publish"],
    ["raw SQL", "ALTER TABLE InventoryItem DROP COLUMN DisplayName"],
    ["raw XML", "<Operation Name=\"Rename Refactor\">"]
  ];

  for (const [label, leak] of leakCases) {
    await t.test(label, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const assetPath = join(root, "src", "assets", "sql-project-refactor-intent-proof-packet.json");
      const packet = JSON.parse(await readFile(assetPath, "utf8"));
      packet.limitations.push(leak);
      await writeFile(assetPath, `${JSON.stringify(packet, null, 2)}\n`);
      await buildSite({ root, log() {} });
      const errors = [];
      await validateSqlProjectRefactorIntentStoryDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /forbidden private, raw, key, command, SQL, or XML material/);
    });
  }
});

test("SQL project refactor-intent validator rejects tag-split private material", async (t) => {
  const root = await createSiteFixture(t);
  const pagePath = join(root, "src", "sql", "project-refactor-intent", "index.html");
  const html = await readFile(pagePath, "utf8");
  await writeFile(pagePath, html.replace("</main>", "<p>Passw<span>ord</span>=tag-split-leak</p></main>"));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlProjectRefactorIntentStoryDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /forbidden private, raw, key, command, SQL, or XML material/);
});

test("SQL project refactor-intent validator rejects attribute and whitespace-obfuscated private material", async (t) => {
  const slash = String.fromCharCode(47);
  const cases = [
    ["attribute", `<a href="file:${slash}${slash}${slash}Users${slash}example${slash}private">fixture</a>`],
    ["whitespace-obfuscated", "<p>Pass w ord = leak</p>"]
  ];

  for (const [label, plantedHtml] of cases) {
    await t.test(label, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const pagePath = join(root, "src", "sql", "project-refactor-intent", "index.html");
      const html = await readFile(pagePath, "utf8");
      await writeFile(pagePath, html.replace("</main>", `${plantedHtml}</main>`));
      await buildSite({ root, log() {} });
      const errors = [];
      await validateSqlProjectRefactorIntentStoryDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /forbidden private, raw, key, command, SQL, or XML material/);
    });
  }
});

test("SQL project refactor-intent validator rejects positive deployment claims outside the non-claim boundary", async (t) => {
  const root = await createSiteFixture(t);
  const pagePath = join(root, "src", "sql", "project-refactor-intent", "index.html");
  const html = await readFile(pagePath, "utf8");
  await writeFile(pagePath, html.replace("</main>", "<p>The deployment succeeded.</p></main>"));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlProjectRefactorIntentStoryDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /forbidden positive deployment, approval, or safety claim/);
});

test("SQL project refactor-intent validator rejects positive deployment claims inside the non-claim boundary", async (t) => {
  const root = await createSiteFixture(t);
  const pagePath = join(root, "src", "sql", "project-refactor-intent", "index.html");
  const html = await readFile(pagePath, "utf8");
  const boundary = '<section class="section boundary-section" data-sql-project-refactor-boundary="non-claims">';
  await writeFile(pagePath, html.replace(boundary, `${boundary}<p>The deployment succeeded.</p>`));
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlProjectRefactorIntentStoryDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /forbidden positive deployment, approval, or safety claim/);
});

test("SQL project refactor-intent validator applies positive-claim guardrails to the JSON packet", async (t) => {
  const root = await createSiteFixture(t);
  const assetPath = join(root, "src", "assets", "sql-project-refactor-intent-proof-packet.json");
  const packet = JSON.parse(await readFile(assetPath, "utf8"));
  packet.purpose = "The deployment succeeded.";
  await writeFile(assetPath, `${JSON.stringify(packet, null, 2)}\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlProjectRefactorIntentStoryDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /forbidden positive deployment, approval, or safety claim/);
});

test("SQL project refactor-intent validator rejects invalid provenance and invented fixture column evidence", async (t) => {
  const root = await createSiteFixture(t);
  const assetPath = join(root, "src", "assets", "sql-project-refactor-intent-proof-packet.json");
  const packet = JSON.parse(await readFile(assetPath, "utf8"));
  packet.evidence[0].commitSha = "bad";
  packet.evidence[1].span.startLine = 0;
  packet.evidence.push({
    ...packet.evidence[1],
    id: "invented-column-row",
    operationKind: "rename-column"
  });
  await writeFile(assetPath, `${JSON.stringify(packet, null, 2)}\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlProjectRefactorIntentStoryDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /incomplete rule, tier, coverage, fact, commit, or extractor provenance/);
  assert.match(errors.join("\n"), /invalid line span/);
  assert.match(errors.join("\n"), /must not present column rename as fixture evidence/);
});

test("SQL project refactor-intent validator pins fixture evidence to exact checked-in facts", async (t) => {
  const root = await createSiteFixture(t);
  const assetPath = join(root, "src", "assets", "sql-project-refactor-intent-proof-packet.json");
  const packet = JSON.parse(await readFile(assetPath, "utf8"));
  const rename = packet.evidence.find((row) => row.id === "table-rename-intent");
  rename.sourceFactId = "fact-fabricated";
  rename.safeSource = "dbo.Fabricated";
  rename.span.startLine = 99;
  rename.span.endLine = 99;
  await writeFile(assetPath, `${JSON.stringify(packet, null, 2)}\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlProjectRefactorIntentStoryDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /does not match pinned fixture fact: table-rename-intent/);
});

test("SQL project refactor-intent validator pins scan, gap, and downstream handoff identities", async (t) => {
  const root = await createSiteFixture(t);
  const assetPath = join(root, "src", "assets", "sql-project-refactor-intent-proof-packet.json");
  const packet = JSON.parse(await readFile(assetPath, "utf8"));
  packet.source.scanId = "scan-fabricated";
  packet.gaps[0].classification = "FabricatedGap";
  packet.downstreamReviewSurfaces[0].classification = "DefiniteImpact";
  packet.downstreamReviewSurfaces[2].state = "approved-for-deployment";
  await writeFile(assetPath, `${JSON.stringify(packet, null, 2)}\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlProjectRefactorIntentStoryDist({ dist: join(root, "dist"), errors });
  const joined = errors.join("\n");
  assert.match(joined, /invalid public source provenance/);
  assert.match(joined, /does not match pinned gap shape: unsupported-or-unsafe-shape/);
  assert.match(joined, /does not match pinned downstream review surface: database-design-review/);
  assert.match(joined, /does not match pinned downstream review surface: sql-runbook/);
});

test("SQL project refactor-intent validator reports missing bidirectional, inbound, and discovery links", async (t) => {
  const root = await createSiteFixture(t);
  const proofPath = join(root, "src", "sql", "project-refactor-intent", "index.html");
  const proof = await readFile(proofPath, "utf8");
  await writeFile(proofPath, proof.replaceAll('href="/blog/sql-project-refactor-intent-evidence/"', 'href="/blog/"'));
  const inboundPath = join(root, "src", "outputs", "index.html");
  const inbound = await readFile(inboundPath, "utf8");
  await writeFile(inboundPath, inbound.replace('href="/sql/project-refactor-intent/"', 'href="/outputs/"'));
  const discoveryPath = join(root, "src", "_site", "discovery.json");
  const discovery = JSON.parse(await readFile(discoveryPath, "utf8"));
  await writeFile(discoveryPath, `${JSON.stringify(discovery.filter((entry) => entry.path !== "/sql/project-refactor-intent/"), null, 2)}\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateSqlProjectRefactorIntentStoryDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /article and proof page must link bidirectionally/);
  assert.match(errors.join("\n"), /inbound route does not link/);
  assert.match(errors.join("\n"), /routes-index\.json is missing/);
});

async function createSiteFixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-sql-project-refactor-story-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
