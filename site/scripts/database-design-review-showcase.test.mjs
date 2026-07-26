import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { buildSite } from "./build.mjs";
import { validateDatabaseDesignReviewShowcaseDist } from "./database-design-review-showcase.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

test("database design review showcase builds with two modes, provenance, discovery, links, and bounded claims", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateDatabaseDesignReviewShowcaseDist({ dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("database design review validator rejects protected, executable, and machine-local material", async (t) => {
  const leakCases = [
    ["statement", "SELECT fixture_value FROM private_table"],
    ["credential", "Password=credential-leak"],
    ["connection", "Server=private-host;User Id=fixture"],
    ["local path", "/Users/example/private"],
    ["private identity", "private-server"]
  ];
  for (const [label, value] of leakCases) {
    await t.test(label, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const assetPath = join(root, "src", "assets", "database-design-review-proof-packet.json");
      const projection = JSON.parse(await readFile(assetPath, "utf8"));
      projection.limitations.push(value);
      await writeFile(assetPath, `${JSON.stringify(projection, null, 2)}\n`);
      await buildSite({ root, log() {} });
      const errors = [];
      await validateDatabaseDesignReviewShowcaseDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /forbidden private or executable material/);
    });
  }
});

test("database design review validator rejects arbitrary packet fields and unsafe metadata", async (t) => {
  const root = await createSiteFixture(t);
  const assetPath = join(root, "src", "assets", "database-design-review-proof-packet.json");
  const projection = JSON.parse(await readFile(assetPath, "utf8"));
  projection.modes[0].packet.tables[0].declarations[0].evidence.snippetHash = "not-public";
  projection.modes[0].packet.tables[0].declarations[0].metadata.push({ key: "arbitrarySourceProperty", value: "not-public" });
  await writeFile(assetPath, `${JSON.stringify(projection, null, 2)}\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateDatabaseDesignReviewShowcaseDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /non-contract field: snippetHash/);
  assert.match(errors.join("\n"), /forbidden arbitrary or protected field: snippetHash/);
  assert.match(errors.join("\n"), /non-allowlisted key: arbitrarySourceProperty/);
});

test("database design review validator enforces single-index and combined-index route contracts", async (t) => {
  const root = await createSiteFixture(t);
  const assetPath = join(root, "src", "assets", "database-design-review-proof-packet.json");
  const projection = JSON.parse(await readFile(assetPath, "utf8"));
  projection.modes[0].packet.gaps = projection.modes[0].packet.gaps.filter((gap) => gap.gapKind !== "SingleIndexRoutePathUnavailable");
  projection.modes[1].packet.summary.routeReferenceCount = 0;
  projection.modes[1].packet.tables[0].routeReferences = [];
  await writeFile(assetPath, `${JSON.stringify(projection, null, 2)}\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateDatabaseDesignReviewShowcaseDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /zero route references and SingleIndexRoutePathUnavailable/);
  assert.match(errors.join("\n"), /include bounded route-reference evidence/);
});

test("database design review validator enforces public provenance and demo claim level", async (t) => {
  const root = await createSiteFixture(t);
  const assetPath = join(root, "src", "assets", "database-design-review-proof-packet.json");
  const projection = JSON.parse(await readFile(assetPath, "utf8"));
  projection.publicClaimLevel = "shipped";
  projection.modes[0].packet.tables[0].declarations[0].evidence.commitSha = "unknown";
  projection.modes[1].packet.tables[0].operations[0].evidence.filePath = "/tmp/private.cs";
  await writeFile(assetPath, `${JSON.stringify(projection, null, 2)}\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateDatabaseDesignReviewShowcaseDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /publicClaimLevel must remain demo/);
  assert.match(errors.join("\n"), /missing compatible rule, tier, or commit provenance/);
  assert.match(errors.join("\n"), /public repo-relative synthetic fixture path/);
});

async function createSiteFixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-database-design-review-site-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
