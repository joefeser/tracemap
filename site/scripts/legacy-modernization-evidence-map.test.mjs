import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { validateLegacyModernizationEvidenceMapHtml } from "./legacy-modernization-evidence-map.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const pageSource = resolve(siteRoot, "src", "legacy-modernization", "evidence-map", "index.html");

test("legacy modernization evidence map source satisfies focused content guard", async () => {
  const html = await readFile(pageSource, "utf8");

  assert.deepEqual(validateLegacyModernizationEvidenceMapHtml(html), []);
});

test("legacy modernization evidence map guard requires concept status for general rows", async () => {
  const html = (await readFile(pageSource, "utf8")).replace(
    'data-map-row="syntax-fallback" data-row-category="general-model" data-public-status="concept"',
    'data-map-row="syntax-fallback" data-row-category="general-model" data-public-status="hidden"'
  );

  assert.match(validateLegacyModernizationEvidenceMapHtml(html).join("\n"), /syntax-fallback.*expected concept/);
});

test("legacy modernization evidence map guard requires hidden status for legacy surface rows", async () => {
  const html = (await readFile(pageSource, "utf8")).replace(
    'data-map-row="wcf-service-references" data-row-category="legacy-surface-detection" data-public-status="hidden"',
    'data-map-row="wcf-service-references" data-row-category="legacy-surface-detection" data-public-status="concept"'
  );

  assert.match(validateLegacyModernizationEvidenceMapHtml(html).join("\n"), /wcf-service-references.*expected hidden/);
});

test("legacy modernization evidence map guard rejects missing required rows", async () => {
  const html = (await readFile(pageSource, "utf8")).replace(
    'data-map-row="asmx-soap-services"',
    'data-map-row="asmx-soap-services-removed"'
  );

  assert.match(validateLegacyModernizationEvidenceMapHtml(html).join("\n"), /missing evidence-map row: asmx-soap-services/);
});

test("legacy modernization evidence map guard rejects private material and unsupported impact wording", async () => {
  const localPathLeak = `${String.fromCharCode(47)}Users/example/private-sample`;
  const cases = [
    [`<p>See ${localPathLeak}.</p>`, /local-absolute-path/],
    ["<p>See c:/work/private-sample.</p>", /local-absolute-path/],
    ["<p>See d:\\work\\private-sample.</p>", /local-absolute-path/],
    ["<p>Server=db;Database=orders;User ID=sa;Password=pw;</p>", /connection-string/],
    ["<p>Pa<span>ss</span>word: hidden-value</p>", /credential-assignment/],
    ["<p>api_key = &apos;hidden-value&apos;</p>", /credential-assignment/],
    ["<p>The service is impacted.</p>", /unsupported support wording/],
    ["<p>TraceMap proves migration safety.</p>", /unsupported support wording/],
    ["<p>git@github.com:private/repo.git</p>", /raw-repository-remote/]
  ];

  for (const [body, expected] of cases) {
    const html = `${await readFile(pageSource, "utf8")}${body}`;
    assert.match(validateLegacyModernizationEvidenceMapHtml(html).join("\n"), expected);
  }
});

test("legacy modernization evidence map guard slices rows with uppercase closing tags", async () => {
  const source = await readFile(pageSource, "utf8");
  const html = source.replace(
    /(<tr data-map-row="wcf-service-references"[\s\S]*?)<\/tr>/,
    "$1</TR >"
  );

  assert.deepEqual(validateLegacyModernizationEvidenceMapHtml(html), []);
});

test("legacy modernization evidence map guard handles unquoted apostrophes inside tag attributes", async () => {
  const source = await readFile(pageSource, "utf8");
  const html = source.replace(
    "<main>",
    "<main><img alt=don't-publish><p>Password: hidden-value</p>"
  );

  assert.match(validateLegacyModernizationEvidenceMapHtml(html).join("\n"), /credential-assignment/);
});
