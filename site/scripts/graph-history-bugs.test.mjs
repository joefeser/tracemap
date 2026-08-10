import assert from "node:assert/strict";
import { cp, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { buildSite } from "./build.mjs";
import { validateGraphHistoryBugsDist } from "./graph-history-bugs.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

test("Graph history article builds with bounded evidence and reciprocal discovery", async (t) => { const root = await fixture(t); await buildSite({ root, log() {} }); const errors = []; await validateGraphHistoryBugsDist({ dist: join(root, "dist"), errors }); assert.deepEqual(errors, []); });

test("Graph history validator rejects unsupported positive claims", async (t) => {
  for (const claim of ["TraceMap proves runtime reachability.", "The graph is complete.", "Production correctness established."]) await t.test(claim, async (subtest) => { const root = await fixture(subtest); await append(root, claim); await buildSite({ root, log() {} }); const errors = []; await validateGraphHistoryBugsDist({ dist: join(root, "dist"), errors }); assert.match(errors.join("\n"), /unsupported positive claim/); });
});

test("Graph history validator rejects private and source material including encoded forms", async (t) => {
  for (const leak of ["/Us" + "ers/example/workspace", "/Us<span>ers</span>/example/workspace", "/Us&#101rs/example/workspace", "namespace Private.Sample {", "Password=secret-value"]) await t.test(leak, async (subtest) => { const root = await fixture(subtest); await append(root, leak); await buildSite({ root, log() {} }); const errors = []; await validateGraphHistoryBugsDist({ dist: join(root, "dist"), errors }); assert.match(errors.join("\n"), /hard private material|source or executable material/); });
});

test("Graph history validator rejects discovery, required-link, and reciprocal-link drift", async (t) => {
  const root = await fixture(t); const discoveryPath = join(root, "src/_site/discovery.json"); const entries = JSON.parse(await readFile(discoveryPath, "utf8")); entries.find((entry) => entry.path === "/blog/bugs-hiding-in-graph-history/").publicClaimLevel = "concept"; await writeFile(discoveryPath, `${JSON.stringify(entries, null, 2)}\n`, "utf8"); const articlePath = join(root, "src/_blog/articles/bugs-hiding-in-graph-history.html"); await writeFile(articlePath, (await readFile(articlePath, "utf8")).replaceAll('href="/proof-paths/for-managers/"', 'href="/"'), "utf8"); const companionPath = join(root, "src/_blog/articles/csharp-extraction-without-plausible-wrong-graphs.html"); await writeFile(companionPath, (await readFile(companionPath, "utf8")).replaceAll('href="/blog/bugs-hiding-in-graph-history/"', 'href="/blog/"'), "utf8"); await buildSite({ root, log() {} }); const errors = []; await validateGraphHistoryBugsDist({ dist: join(root, "dist"), errors }); assert.match(errors.join("\n"), /claim level must be demo/); assert.match(errors.join("\n"), /missing required link/); assert.match(errors.join("\n"), /reciprocal companion link is missing/);
});

test("Graph history validator reports malformed discovery", async (t) => {
  for (const malformed of ["{", JSON.stringify({ entries: {} })]) await t.test(malformed, async (subtest) => { const root = await fixture(subtest); await buildSite({ root, log() {} }); await writeFile(join(root, "dist/routes-index.json"), malformed, "utf8"); const errors = []; await validateGraphHistoryBugsDist({ dist: join(root, "dist"), errors }); assert.match(errors.join("\n"), /valid JSON|entries array/); });
});

test("Graph history validator accepts attribute spacing and supplied base URL", async (t) => { const root = await fixture(t); await buildSite({ root, log() {} }); const pagePath = join(root, "dist/blog/bugs-hiding-in-graph-history/index.html"); await writeFile(pagePath, (await readFile(pagePath, "utf8")).replaceAll("data-graph-history-block=", "data-graph-history-block = ").replaceAll("https://tracemap.tools", "https://preview.example"), "utf8"); const sitemapPath = join(root, "dist/sitemap.xml"); await writeFile(sitemapPath, (await readFile(sitemapPath, "utf8")).replaceAll("https://tracemap.tools", "https://preview.example"), "utf8"); const errors = []; await validateGraphHistoryBugsDist({ baseUrl: "https://preview.example/", dist: join(root, "dist"), errors }); assert.deepEqual(errors, []); });

async function fixture(t) { const root = await mkdtemp(join(tmpdir(), "tracemap-graph-history-site-")); t.after(() => rm(root, { recursive: true, force: true })); await cp(join(siteRoot, "src"), join(root, "src"), { recursive: true }); return root; }
async function append(root, text) { const path = join(root, "src/_blog/articles/bugs-hiding-in-graph-history.html"); await writeFile(path, `${await readFile(path, "utf8")}<p>${text}</p>`, "utf8"); }
