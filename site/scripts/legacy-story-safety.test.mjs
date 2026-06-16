import assert from "node:assert/strict";
import { mkdir, mkdtemp, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  buildAndValidateLegacyStorySafety,
  validateLegacyStorySafety,
  validateRenderedLegacyStoryHtml
} from "./legacy-story-safety.mjs";

test("legacy story guard accepts clean concept copy and sanctioned disclaimers", () => {
  const errors = validateRenderedLegacyStoryHtml(
    html(`<main>
      <p>Public claim level: concept. No public conclusion without evidence.</p>
      <p>Static evidence does not claim runtime behavior, UI reachability, production traffic, deployment state, endpoint performance, exploitability, database existence, package compatibility, incident cause, release approval, or release safety.</p>
      <p>Public-safe summaries may name scan-manifest.json, facts.ndjson, index.sqlite, report.md, and logs/analyzer.log as artifact types.</p>
    </main>`)
  );

  assert.deepEqual(errors, []);
});

test("legacy story guard accepts boundary legacy terms by themselves", () => {
  const errors = validateRenderedLegacyStoryHtml(
    html(`<main><p>Boundary vocabulary: WCF, .svc, ASMX, MarshalByRefObject, DBML, EDMX.</p></main>`)
  );

  assert.deepEqual(errors, []);
});

test("legacy story guard rejects hard leaks and sensitive values", () => {
  const localPathLeak = `${String.fromCharCode(47)}Users/example/private-sample`;
  const cases = [
    ["local path", `See ${localPathLeak} for details.`, /local-absolute-path/],
    ["bare spec path", "Read .kiro/specs/legacy-secret/requirements.md.", /bare internal spec path/],
    ["connection string", "Server=db;Database=orders;User ID=sa;Password=pw;", /connection-string/],
    ["credential assignment", "apiKey = \"secret-value\"", /credential-assignment/],
    ["private URL", "Open http://localhost:5000/status for proof.", /private-local-url/],
    ["file URL", "Open file:///tmp/private/report.md.", /private-local-url/],
    ["raw remote", "Clone git@github.com:private/repo.git.", /raw-repository-remote/],
    ["source snippet", "source snippet: public void Save() {}", /raw-source-snippet/],
    ["generated output root", "Review site/dist/private/index.html.", /generated-output-root/],
    ["unicode credential", "Pa\u200BssWord: secret-value", /credential-assignment/]
  ];

  for (const [name, body, expected] of cases) {
    const errors = validateRenderedLegacyStoryHtml(html(`<main><p>${body}</p></main>`), { label: name });
    assert.match(errors.join("\n"), expected, name);
  }
});

test("legacy story guard strips tags with greater-than signs inside attributes", () => {
  const errors = validateRenderedLegacyStoryHtml(
    html('<main><input value="a > b"><p>Clean public concept copy.</p></main>')
  );

  assert.deepEqual(errors, []);
});

test("legacy story guard decodes common whitespace and numeric HTML entities before checking words", () => {
  const errors = validateRenderedLegacyStoryHtml(
    html("<main><p>api&nbsp;key &#61; secret-value and password&#58; hidden-value</p></main>")
  );

  assert.match(errors.join("\n"), /credential-assignment/);
});

test("legacy story guard redacts sensitive evidence from error messages", () => {
  const errors = validateRenderedLegacyStoryHtml(
    html("<main><p>Server=db;Database=orders;User ID=sa;Password=secret;</p></main>")
  );
  const message = errors.join("\n");

  assert.match(message, /redacted connection string/);
  assert.doesNotMatch(message, /Password=secret/);
});

test("legacy story guard rejects affirmative overclaims while allowing exact negated disclaimers", () => {
  assert.match(
    validateRenderedLegacyStoryHtml(html("<main><p>This page proves runtime proof and release safety.</p></main>")).join(
      "\n"
    ),
    /affirmative overclaim phrase/
  );

  assert.deepEqual(
    validateRenderedLegacyStoryHtml(
      html(
        "<main><p>No runtime proof, UI reachability, production traffic, deployment state, endpoint performance, exploitability, database existence, package compatibility, incident cause, release approval, or release safety is claimed by this concept page.</p></main>"
      )
    ),
    []
  );
});

test("legacy story guard rejects hidden theme enumeration without adjacent label", () => {
  const errors = validateRenderedLegacyStoryHtml(
    html("<main><p>WCF/service-reference mapping and WebForms event flow are in the evidence story.</p></main>")
  );

  assert.match(errors.join("\n"), /without adjacent hidden or omission label/);
});

test("legacy story guard accepts hidden theme enumeration with adjacent labels", () => {
  const errors = validateRenderedLegacyStoryHtml(
    html(`<main>
      <article><h3>WCF/service-reference mapping</h3><p>Label: hidden pending validation.</p></article>
      <article><h3>WCF metadata normalization</h3><p>Label: hidden pending validation.</p></article>
      <article><h3>.NET Remoting detection</h3><p>Label: hidden pending validation.</p></article>
      <article><h3>WebForms event flow</h3><p>Label: hidden pending validation.</p></article>
      <article><h3>Legacy data metadata</h3><p>Label: hidden pending validation.</p></article>
      <article><h3>Build diagnostics</h3><p>Label: hidden pending validation.</p></article>
      <article><h3>Flow composition</h3><p>Label: hidden pending validation.</p></article>
    </main>`)
  );

  assert.deepEqual(errors, []);
});

test("legacy story guard boundary terms do not mask adjacent leaks or overclaims", () => {
  assert.match(
    validateRenderedLegacyStoryHtml(html("<main><p>WCF .svc ASMX Server=db;Database=x;Password=y;</p></main>")).join(
      "\n"
    ),
    /connection-string/
  );
  assert.match(
    validateRenderedLegacyStoryHtml(html("<main><p>DBML EDMX proves release safety.</p></main>")).join("\n"),
    /affirmative overclaim phrase/
  );
});

test("legacy story guard scans only rendered legacy story output", async () => {
  const root = await mkdtemp(join(tmpdir(), "tracemap-legacy-story-scope-"));
  await mkdir(join(root, "dist", "legacy-evidence"), { recursive: true });
  await mkdir(join(root, ".kiro", "specs", "fixture"), { recursive: true });
  await mkdir(join(root, "site", "scripts"), { recursive: true });
  await writeFile(join(root, "dist", "legacy-evidence", "index.html"), html("<main><p>Clean concept copy.</p></main>"), "utf8");
  await writeFile(join(root, ".kiro", "specs", "fixture", "requirements.md"), "Password=source-only", "utf8");
  await writeFile(join(root, "site", "scripts", "fixture.txt"), "http://localhost:3000", "utf8");

  await validateLegacyStorySafety({ root });
});

test("legacy story guard fails empty output and missing target pages", async () => {
  const emptyRoot = await mkdtemp(join(tmpdir(), "tracemap-legacy-story-empty-"));
  await mkdir(join(emptyRoot, "dist"), { recursive: true });
  await assert.rejects(
    validateLegacyStorySafety({ root: emptyRoot }),
    /found no rendered HTML files.*did not scan required target/s
  );

  const missingTargetRoot = await mkdtemp(join(tmpdir(), "tracemap-legacy-story-missing-"));
  await mkdir(join(missingTargetRoot, "dist"), { recursive: true });
  await writeFile(join(missingTargetRoot, "dist", "index.html"), html("<main><p>Home</p></main>"), "utf8");
  await assert.rejects(validateLegacyStorySafety({ root: missingTargetRoot }), /did not scan required target/);
});

test("legacy story guard builds before scanning instead of trusting stale dist", async () => {
  const root = await createBuildFixture({
    legacyBody: "<main><p>apiKey = \"fresh-source-leak\"</p></main>"
  });

  await mkdir(join(root, "dist", "legacy-evidence"), { recursive: true });
  await writeFile(
    join(root, "dist", "legacy-evidence", "index.html"),
    html("<main><p>Clean stale output.</p></main>"),
    "utf8"
  );

  await assert.rejects(buildAndValidateLegacyStorySafety({ root }), /credential-assignment/);
});

async function createBuildFixture({ legacyBody }) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-legacy-story-build-"));
  const src = join(root, "src");
  await mkdir(join(src, "_blog", "articles"), { recursive: true });
  await mkdir(join(src, "_site"), { recursive: true });
  await mkdir(join(src, "legacy-evidence"), { recursive: true });

  await writeFile(join(src, "index.html"), sourcePage("<main><p>Home.</p></main>"), "utf8");
  await writeFile(join(src, "legacy-evidence", "index.html"), sourcePage(legacyBody), "utf8");
  await writeFile(
    join(src, "_blog", "articles.json"),
    JSON.stringify(
      [
        {
          body: "articles/fixture.html",
          calloutHeading: "Next",
          calloutHtml: "Read <a href=\"/legacy-evidence/\">legacy evidence</a>.",
          cardDescription: "Fixture card.",
          category: "Test",
          description: "Fixture description.",
          h1: "Fixture",
          hero: "Fixture hero.",
          ogDescription: "Fixture OG description.",
          published: "2026-06-16",
          publishedDisplay: "June 16, 2026",
          slug: "fixture",
          title: "Fixture"
        }
      ],
      null,
      2
    ),
    "utf8"
  );
  await writeFile(join(src, "_blog", "articles", "fixture.html"), "<p>Fixture body.</p>", "utf8");
  await writeFile(
    join(src, "_site", "pages.json"),
    JSON.stringify(
      [
        { path: "/", changefreq: "weekly", priority: "1.0" },
        { path: "/legacy-evidence/", changefreq: "monthly", priority: "0.7" }
      ],
      null,
      2
    ),
    "utf8"
  );
  await writeFile(
    join(src, "_site", "discovery.json"),
    JSON.stringify(
      [
        {
          path: "/legacy-evidence/",
          title: "Legacy Evidence",
          summary: "Concept route for deterministic static evidence boundaries.",
          publicClaimLevel: "concept",
          sourceType: "site-page",
          hintCategory: "roadmap",
          preferredProofPath: "/legacy-evidence/",
          limitations: ["Concept route requires proof before stronger labels."],
          nonClaims: ["No runtime behavior or production usage proof."]
        }
      ],
      null,
      2
    ),
    "utf8"
  );

  return root;
}

function sourcePage(body) {
  return `<!doctype html><html><body><header class="site-header"><nav class="top-nav"><a href="/old/">Old</a></nav></header>${body}</body></html>`;
}

function html(body) {
  return `<!doctype html><html><body>${body}</body></html>`;
}
