import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { validateLegacyDataSurfaceHtml } from "./legacy-data-surface.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const pageSource = resolve(siteRoot, "src", "legacy-data-surface", "index.html");

test("legacy data surface source satisfies focused content guard", async () => {
  const html = await readFile(pageSource, "utf8");

  assert.deepEqual(validateLegacyDataSurfaceHtml(html), []);
});

test("legacy data surface guard requires required matrix columns", async () => {
  const html = (await readFile(pageSource, "utf8")).replace(
    "<th scope=\"col\">Owner follow-up</th>",
    "<th scope=\"col\">Follow-up</th>"
  );

  assert.match(validateLegacyDataSurfaceHtml(html).join("\n"), /missing required column: Owner follow-up/);
});

test("legacy data surface guard tolerates reordered and spaced attributes", async () => {
  const html = (await readFile(pageSource, "utf8"))
    .replace(
      '<link rel="canonical" href="https://tracemap.tools/legacy-data-surface/">',
      '<link href = "https://tracemap.tools/legacy-data-surface/" rel = "canonical">'
    )
    .replace(
      '<meta property="og:type" content="website">',
      '<meta content = "website" property = "og:type">'
    )
    .replace(
      '<tr data-surface-row="analysis-gaps" data-evidence-status="gap">',
      '<tr data-evidence-status = "gap" data-surface-row = "analysis-gaps">'
    );

  assert.deepEqual(validateLegacyDataSurfaceHtml(html), []);
});

test("legacy data surface guard rejects unsupported evidence statuses", async () => {
  const html = (await readFile(pageSource, "utf8")).replace(
    '<tr data-surface-row="analysis-gaps" data-evidence-status="gap">',
    '<tr data-surface-row="analysis-gaps" data-evidence-status="supported">'
  );

  assert.match(validateLegacyDataSurfaceHtml(html).join("\n"), /unsupported evidence status: supported/);
});

test("legacy data surface guard requires non-empty limitation cells", async () => {
  const html = (await readFile(pageSource, "utf8")).replace(
    "Does not prove live schema, rows, permissions, data values, or schema compatibility.",
    ""
  );

  assert.match(validateLegacyDataSurfaceHtml(html).join("\n"), /data-model-metadata.*limitation cell/);
});

test("legacy data surface guard allows labeled forbidden examples but rejects bare affirmative examples", async () => {
  const html = (await readFile(pageSource, "utf8")).replace(
    "Forbidden example: TraceMap executes SQL or proves runtime SQL behavior.",
    "TraceMap executes SQL or proves runtime SQL behavior."
  );

  assert.match(validateLegacyDataSurfaceHtml(html).join("\n"), /forbidden wording must be explicitly labeled/);
});

test("legacy data surface guard rejects affirmative overclaims outside negated boundaries", async () => {
  const html = `${await readFile(pageSource, "utf8")}<main><p>TraceMap executes SQL.</p></main>`;

  assert.match(validateLegacyDataSurfaceHtml(html).join("\n"), /affirmative overclaim.*executes SQL/);
});

test("legacy data surface guard scans metadata attributes for affirmative overclaims", async () => {
  const html = (await readFile(pageSource, "utf8")).replace(
    "</head>",
    '<meta name="description" content="TraceMap executes SQL."></head>'
  );

  assert.match(validateLegacyDataSurfaceHtml(html).join("\n"), /metadata attributes.*executes SQL/);
});

test("legacy data surface guard rejects private values and redacts error evidence", async () => {
  const html = `${await readFile(pageSource, "utf8")}<main><p>Server=db;Database=orders;User ID=sa;Password=secret;</p></main>`;
  const message = validateLegacyDataSurfaceHtml(html).join("\n");

  assert.match(message, /connection-string-value/);
  assert.doesNotMatch(message, /Password=secret/);
});

test("legacy data surface guard scans attributes for private values", async () => {
  const html = `${await readFile(
    pageSource,
    "utf8"
  )}<main><a href="https://example.com" title="api_key=hidden-value">Metadata</a></main>`;

  assert.match(validateLegacyDataSurfaceHtml(html).join("\n"), /credential-assignment/);
});

test("legacy data surface guard checks required route links", async () => {
  const html = (await readFile(pageSource, "utf8")).replaceAll('href="/legacy-validation/"', 'href="/missing/"');

  assert.match(validateLegacyDataSurfaceHtml(html).join("\n"), /missing required public-safe link: \/legacy-validation\//);
});
