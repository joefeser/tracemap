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

test("MSBuild binlog evidence honors an alternate validation base URL", async (t) => {
  const root = await createFixture(t);
  await buildSite({ root, log() {} });
  const dist = join(root, "dist");
  for (const path of [
    join(dist, "sitemap.xml"),
    join(dist, "blog", "what-an-msbuild-binlog-knows-that-a-source-diff-does-not", "index.html"),
    join(dist, "build", "msbuild-binlog", "proof-packet", "index.html")
  ]) {
    const content = await readFile(path, "utf8");
    await writeFile(path, content.replaceAll("https://tracemap.tools", "https://evidence.example"));
  }
  const errors = [];
  await validateMsbuildBinlogEvidenceDist({
    baseUrl: "https://evidence.example/",
    dist,
    errors
  });
  assert.deepEqual(errors, []);
});

test("MSBuild binlog evidence remains required when every published artifact is absent", async (t) => {
  const root = await createFixture(t);
  const errors = [];
  await validateMsbuildBinlogEvidenceDist({ dist: join(root, "dist"), errors });
  assert.equal(errors.filter((error) => error.includes("missing required artifact")).length, 4);
});

test("MSBuild binlog evidence requires its social image in source and distribution", async (t) => {
  await t.test("source", async (subtest) => {
    const root = await createFixture(subtest);
    await rm(join(root, "src", "assets", "msbuild-binlog-evidence-social-card.png"));
    await assert.rejects(
      buildSite({ root, log() {} }),
      /Open Graph image is missing/
    );
  });

  await t.test("distribution", async (subtest) => {
    const root = await createFixture(subtest);
    await buildSite({ root, log() {} });
    await rm(join(root, "dist", "assets", "msbuild-binlog-evidence-social-card.png"));
    const errors = [];
    await validateMsbuildBinlogEvidenceDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /missing required artifact.*social-card\.png/);
  });
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
  const plantedLocalPath = ["", "Users", "example", "private.binlog"].join("/");
  const plantedConnection = ["Host=example.invalid", "Password=fixture"].join(";");
  for (const value of [plantedLocalPath, plantedConnection]) {
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

test("MSBuild binlog proof packet rejects unsupported claims, provenance drift, and non-object JSON", async (t) => {
  for (const [name, mutate, expected] of [
    [
      "unsupported claim",
      (packet) => {
        packet.purpose = "TraceMap proves tests passed and the release is safe.";
        return packet;
      },
      /unsupported public claim/
    ],
    [
      "provenance drift",
      (packet) => {
        packet.source.commitSha = "0123456789abcdef0123456789abcdef01234567";
        return packet;
      },
      /does not match the evidenced smoke commit/
    ],
    ["non-object", () => null, /packet must be an object/]
  ]) {
    await t.test(name, async (subtest) => {
      const root = await createFixture(subtest);
      const assetPath = join(root, "src", "assets", "msbuild-binlog-proof-packet.json");
      const packet = mutate(JSON.parse(await readFile(assetPath, "utf8")));
      await writeFile(assetPath, `${JSON.stringify(packet, null, 2)}\n`);
      await buildSite({ root, log() {} });
      const errors = [];
      await validateMsbuildBinlogEvidenceDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), expected);
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

test("MSBuild binlog article rejects sentence-split unsupported claims", async (t) => {
  const root = await createFixture(t);
  const metadataPath = join(root, "src", "_blog", "articles", "what-an-msbuild-binlog-knows-that-a-source-diff-does-not.html");
  const article = await readFile(metadataPath, "utf8");
  await writeFile(metadataPath, `${article}<p>TraceMap proves. The release is safe.</p>\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateMsbuildBinlogEvidenceDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /unsupported public claim/);
});

test("MSBuild binlog private-material scan catches values split across markup", async (t) => {
  const root = await createFixture(t);
  const metadataPath = join(root, "src", "_blog", "articles", "what-an-msbuild-binlog-knows-that-a-source-diff-does-not.html");
  const article = await readFile(metadataPath, "utf8");
  await writeFile(metadataPath, `${article}<p>/Us<span>ers</span>/example/private.binlog</p>\n`);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateMsbuildBinlogEvidenceDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /forbidden private or executable material/);
});

test("MSBuild binlog private-material scan catches percent-encoded values", async (t) => {
  for (const value of [
    "%25252FUsers%25252Fexample%25252Fprivate.binlog",
    "Host%25253Dexample.invalid%25253BPassword%25253Dfixture",
    "&amp;#x2f;Users&amp;#x2f;example&amp;#x2f;private.binlog"
  ]) {
    await t.test(value.split("%")[0] || "path", async (subtest) => {
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

test("MSBuild binlog discovery reports malformed JSON and shape without throwing", async (t) => {
  await t.test("invalid JSON", async (subtest) => {
    const root = await createFixture(subtest);
    await buildSite({ root, log() {} });
    await writeFile(join(root, "dist", "routes-index.json"), "{");
    const errors = [];
    await validateMsbuildBinlogEvidenceDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /discovery metadata is unavailable or invalid/);
  });

  await t.test("invalid entries shape", async (subtest) => {
    const root = await createFixture(subtest);
    await buildSite({ root, log() {} });
    await writeFile(join(root, "dist", "routes-index.json"), "{\"entries\":{}}\n");
    const errors = [];
    await validateMsbuildBinlogEvidenceDist({ dist: join(root, "dist"), errors });
    assert.match(errors.join("\n"), /discovery metadata entries must be an array/);
  });
});

async function createFixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-msbuild-binlog-site-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
