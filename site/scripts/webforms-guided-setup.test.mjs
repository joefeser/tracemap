import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import { buildSite } from "./build.mjs";
import { validateWebformsGuidedSetupDist } from "./webforms-guided-setup.mjs";

const siteRoot = resolve(fileURLToPath(new URL("..", import.meta.url)));
const repositoryRoot = resolve(siteRoot, "..");

async function validateFixture(root, errors, overrides = {}) {
  return validateWebformsGuidedSetupDist({
    baseUrl: "https://tracemap.tools",
    dist: join(root, "dist"),
    errors,
    root,
    repositoryRoot,
    ...overrides
  });
}

test("Web Forms guided setup builds with workflow, metadata, links, and boundaries", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log: () => {} });
  const errors = [];
  await validateFixture(root, errors);
  assert.deepEqual(errors, []);
});

test("Web Forms guided setup rejects missing workflow concepts and inbound links", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src/webforms/index.html");
  const articlePath = join(root, "src/_blog/articles/modernizing-web-forms-without-running-it.html");
  await writeFile(pagePath, (await readFile(pagePath, "utf8")).replaceAll("forms.txt", "selection-file"));
  await writeFile(articlePath, (await readFile(articlePath, "utf8")).replace('href="/webforms/"', 'href="/limitations/"'));
  await buildSite({ root, log: () => {} });
  const errors = [];
  await validateFixture(root, errors);
  assert.ok(errors.some((error) => String(error).includes("forms.txt")));
  assert.ok(errors.some((error) => String(error).includes("must link")));
});

test("Web Forms guided setup rejects overclaims and private material", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src/webforms/index.html");
  const page = await readFile(pagePath, "utf8");
  await writeFile(pagePath, page.replace("</main>", "<p>Publication was verified.</p><p>/Us<span>ers</span>/example/private</p></main>"));
  await buildSite({ root, log: () => {} });
  const errors = [];
  await validateFixture(root, errors);
  assert.ok(errors.filter((error) => String(error).includes("forbidden")).length >= 2);
});

test("Web Forms guided setup rejects discovery claim drift", async (t) => {
  const root = await fixture(t);
  const discoveryPath = join(root, "src/_site/discovery.json");
  const discovery = JSON.parse(await readFile(discoveryPath, "utf8"));
  discovery.find((entry) => entry.path === "/webforms/").publicClaimLevel = "demo";
  await writeFile(discoveryPath, `${JSON.stringify(discovery, null, 2)}\n`);
  await buildSite({ root, log: () => {} });
  const errors = [];
  await validateFixture(root, errors);
  assert.ok(errors.some((error) => String(error).includes("claim level")));
});

test("Web Forms guided setup requires active anchors rather than commented links", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src/webforms/index.html");
  const page = await readFile(pagePath, "utf8");
  await writeFile(pagePath, page.replace(
    '<a href="/webforms/source-plus-compiled-proof/">Source plus compiled concept</a>',
    '<!-- <a href="/webforms/source-plus-compiled-proof/">Source plus compiled concept</a> -->'
  ));
  await buildSite({ root, log: () => {} });
  const errors = [];
  await validateFixture(root, errors);
  assert.ok(errors.some((error) => String(error).includes("active required link: /webforms/source-plus-compiled-proof/")));
});

test("Web Forms guided setup scans decoded published attribute values", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src/webforms/index.html");
  const page = await readFile(pagePath, "utf8");
  await writeFile(pagePath, page.replace(
    'href="/webforms/source-plus-compiled-proof/"',
    'href="/webforms/source-plus-compiled-proof/?to&#107;en=leak-sentinel"'
  ));
  await buildSite({ root, log: () => {} });
  const errors = [];
  await validateFixture(root, errors);
  assert.ok(errors.some((error) => String(error).includes("forbidden public material")));
});

test("Web Forms guided setup binds the page boundary to the independent implementation record", async (t) => {
  const root = await fixture(t);
  const statePath = join(root, "implementation-state.md");
  await writeFile(statePath, [
    "Exact base: `5ffd4a54176c002e4c6d41ce0133eab5963ad79b`",
    "PR #803 ancestry at exact base: `not-shipped`",
    ""
  ].join("\n"));
  await buildSite({ root, log: () => {} });
  const errors = [];
  await validateFixture(root, errors, { implementationStatePath: statePath });
  assert.ok(errors.some((error) => String(error).includes("does not match the independently recorded")));
});

test("Web Forms guided setup rejects ancestry claim drift", async (t) => {
  const root = await fixture(t);
  const pagePath = join(root, "src/webforms/index.html");
  const page = await readFile(pagePath, "utf8");
  await writeFile(pagePath, page.replace('data-repairs-803="not-shipped"', 'data-repairs-803="shipped"'));
  await buildSite({ root, log: () => {} });
  const errors = [];
  await validateFixture(root, errors);
  assert.ok(errors.some((error) => String(error).includes("#803 ancestry claim must be not-shipped")));
});

test("Web Forms guided setup accepts its recorded base as a shallow synthetic-merge parent", async (t) => {
  const root = await fixture(t);
  await buildSite({ root, log: () => {} });
  const errors = [];
  await validateFixture(root, errors, {
    repositoryRoot: join(root, "missing-shallow-history"),
    implementationStatePath: resolve(repositoryRoot, ".kiro/specs/site-webforms-guided-setup/implementation-state.md"),
    checkoutHeadParents: ["684acb3457d1942fb7fde43db77c5cb27e5d1648", "10aeb8b688eb64bd0709b3f103b6970933cc980a"]
  });
  assert.deepEqual(errors, []);
});

async function fixture(t) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-webforms-setup-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true });
  return root;
}
