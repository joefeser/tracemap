import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { buildSite } from "./build.mjs";
import { validateAccessSafeEvidenceAcquisitionDist } from "./access-safe-evidence-acquisition.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

test("Access acquisition article builds with bounded claims, discovery, links, and sitemap metadata", async (t) => {
  const root = await createSiteFixture(t);
  await buildSite({ root, log() {} });
  const errors = [];
  await validateAccessSafeEvidenceAcquisitionDist({ dist: join(root, "dist"), errors });
  assert.deepEqual(errors, []);
});

test("Access acquisition validator rejects planted runtime and reconstruction claims", async (t) => {
  for (const claim of ["TraceMap ran the Access application.", "The reconstruction succeeded.", "The file is safe to run."]) {
    await t.test(claim, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, "src", "_blog", "articles", "reverse-engineering-access-without-running-it.html");
      const html = await readFile(path, "utf8");
      await writeFile(path, `${html}<p>${claim}</p>`, "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateAccessSafeEvidenceAcquisitionDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /unsupported positive claim/);
    });
  }
});

test("Access acquisition validator rejects planted private and executable material", async (t) => {
  const slash = String.fromCharCode(47);
  for (const leak of [`${slash}Users${slash}example${slash}private`, "Password=leak-sentinel", "SELECT value FROM private_table"]) {
    await t.test(leak, async (subtest) => {
      const root = await createSiteFixture(subtest);
      const path = join(root, "src", "_blog", "articles", "reverse-engineering-access-without-running-it.html");
      const html = await readFile(path, "utf8");
      await writeFile(path, `${html}<p>${leak}</p>`, "utf8");
      await buildSite({ root, log() {} });
      const errors = [];
      await validateAccessSafeEvidenceAcquisitionDist({ dist: join(root, "dist"), errors });
      assert.match(errors.join("\n"), /hard private material|raw or executable material/);
    });
  }
});

test("Access acquisition validator rejects discovery claim-level drift", async (t) => {
  const root = await createSiteFixture(t);
  const path = join(root, "src", "_site", "discovery.json");
  const entries = JSON.parse(await readFile(path, "utf8"));
  entries.find((entry) => entry.path === "/blog/reverse-engineering-access-without-running-it/").publicClaimLevel = "concept";
  await writeFile(path, `${JSON.stringify(entries, null, 2)}\n`, "utf8");
  await buildSite({ root, log() {} });
  const errors = [];
  await validateAccessSafeEvidenceAcquisitionDist({ dist: join(root, "dist"), errors });
  assert.match(errors.join("\n"), /claim level must be demo/);
});

async function createSiteFixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-access-acquisition-site-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
