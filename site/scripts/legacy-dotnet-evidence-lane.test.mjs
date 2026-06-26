import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { validateLegacyDotnetEvidenceLaneHtml } from "./legacy-dotnet-evidence-lane.mjs";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const pageSource = resolve(siteRoot, "src", "legacy-dotnet", "evidence", "index.html");

test("legacy .NET evidence lane source satisfies focused content guard", async () => {
  const html = await readFile(pageSource, "utf8");

  assert.deepEqual(validateLegacyDotnetEvidenceLaneHtml(html), []);
});

test("legacy .NET evidence lane guard requires future status for general rows", async () => {
  const html = (await readFile(pageSource, "utf8")).replace(
    'data-lane-row="evidence-tier-model" data-row-category="general-model" data-public-status="future"',
    'data-lane-row="evidence-tier-model" data-row-category="general-model" data-public-status="hidden"'
  );

  assert.match(validateLegacyDotnetEvidenceLaneHtml(html).join("\n"), /evidence-tier-model.*expected future/);
});

test("legacy .NET evidence lane guard requires hidden status for legacy surface rows", async () => {
  const html = (await readFile(pageSource, "utf8")).replace(
    'data-lane-row="wcf" data-row-category="legacy-surface" data-public-status="hidden"',
    'data-lane-row="wcf" data-row-category="legacy-surface" data-public-status="future"'
  );

  assert.match(validateLegacyDotnetEvidenceLaneHtml(html).join("\n"), /wcf.*expected hidden/);
});

test("legacy .NET evidence lane guard rejects missing required rows", async () => {
  const html = (await readFile(pageSource, "utf8")).replace(
    'data-lane-row="asmx-soap"',
    'data-lane-row="asmx-soap-removed"'
  );

  assert.match(validateLegacyDotnetEvidenceLaneHtml(html).join("\n"), /missing evidence-lane row: asmx-soap/);
});

test("legacy .NET evidence lane guard requires eight matrix fields", async () => {
  const html = (await readFile(pageSource, "utf8")).replace(
    /<tr data-lane-row="winforms"[\s\S]*?<\/tr>/,
    '<tr data-lane-row="winforms" data-row-category="legacy-surface" data-public-status="hidden"><td>WinForms</td></tr>'
  );

  assert.match(validateLegacyDotnetEvidenceLaneHtml(html).join("\n"), /winforms has 1 data cells; expected 8/);
});

test("legacy .NET evidence lane guard rejects private material and unsupported wording", async () => {
  const localPathLeak = `${String.fromCharCode(47)}Users/example/private-sample`;
  const cases = [
    [`<p>See ${localPathLeak}.</p>`, /local-absolute-path/],
    ["<p>&#47;Users/example/private-sample</p>", /local-absolute-path/],
    ["<p>&#x2f;home/example/private-sample</p>", /local-absolute-path/],
    ["<p>Server=db;Database=orders;User ID=sa;Password=pw;</p>", /connection-string/],
    ["<p>api_key = &apos;hidden-value&apos;</p>", /credential-assignment/],
    ["<p>The service is impacted.</p>", /unsupported support wording/],
    ["<p>TraceMap approves the modernization plan.</p>", /unsupported support wording/],
    ["<p>git@github.com:private/repo.git</p>", /raw-repository-remote/]
  ];

  for (const [body, expected] of cases) {
    const html = `${await readFile(pageSource, "utf8")}${body}`;
    assert.match(validateLegacyDotnetEvidenceLaneHtml(html).join("\n"), expected);
  }
});

test("legacy .NET evidence lane guard redacts sensitive evidence in errors", async () => {
  const html = `${await readFile(
    pageSource,
    "utf8"
  )}<p>Server=db;Database=orders;User ID=sa;Password=secret;</p>`;
  const message = validateLegacyDotnetEvidenceLaneHtml(html).join("\n");

  assert.match(message, /redacted connection-string/);
  assert.doesNotMatch(message, /Password=secret/);
});

test("legacy .NET evidence lane guard scans public metadata for unsupported claims", async () => {
  const html = (await readFile(pageSource, "utf8")).replace(
    "</head>",
    '<meta property="og:description" content="TraceMap validates runtime behavior"></head>'
  );

  assert.match(validateLegacyDotnetEvidenceLaneHtml(html).join("\n"), /unsupported support wording/);
});
