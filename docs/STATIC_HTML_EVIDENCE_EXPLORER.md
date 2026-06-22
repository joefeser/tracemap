# Static HTML Evidence Explorer

TraceMap can generate a local static HTML evidence explorer from existing
generated TraceMap artifacts:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- explorer generate \
  --input .tracemap \
  --out .tracemap-explorer
```

The explorer is a local generated artifact. It is not the public
`tracemap.tools` site, not a hosted service, and not a live repository
connection. It renders selected generated artifacts and does not rescan source
code, query SQLite at browser runtime, call services, run Roslyn, use LLMs,
create embeddings, write vector databases, or derive new impact conclusions.

## Command

```text
tracemap explorer generate --input <artifact-dir> --out <explorer-output>
  [--safety-profile <public-demo|hidden-local>] [--force]
```

`--input` must point at a directory containing generated TraceMap artifacts.
The first implementation slice supports:

- `scan-manifest.json` for safe commit, coverage, and extractor provenance;
- `facts.ndjson` for safe evidence rows;
- `index.sqlite` as a hashed provenance artifact only;
- `report.md` as a hashed provenance artifact only.

Other top-level JSON files are labeled unsupported with
`explorer.input.unsupported-schema.v1` gaps instead of being silently merged.
In this first slice, that includes report JSON artifacts such as
`dependency-report.json`, `release-review.json`, `demo-summary.json`, and
other combined/reducer report JSON files; compatible readers for those
artifacts are deferred to later slices.
Claim-level conflict detection across multiple compatible structured artifacts
is also deferred in this slice and is rendered as a visible
`explorer.input.provenance-conflict.v1` limitation.
Analyzer logs, raw SQLite content, raw facts, raw snippets, raw SQL, config
values, raw remotes, hostnames, endpoint addresses, query strings, private
sample names, and local absolute paths are not rendered.

## Output Layout

The command writes:

```text
index.html
assets/explorer.css
assets/explorer.js
data/explorer-manifest.json
data/explorer-data.json
README.md
```

`index.html` opens from disk and keeps the overview, sources, artifacts, gaps,
limitations, rule IDs, and a deterministic baseline of evidence rows readable
without JavaScript. JavaScript is local-only progressive enhancement over safe
rendered table fields.
The no-JavaScript evidence-row baseline renders the first 200 deterministic
rows; the full safe row set is available in `data/explorer-data.json`.

The follow-up rendering slice also includes:

- a `Coverage` table with rule-backed section status rows for overview,
  sources, artifacts, evidence rows, surfaces, paths, reducer results, rules,
  and redactions;
- a `Safety & Redactions` table showing safe categories, actions, locations,
  and counts for redacted, hashed, category-only, or omitted values;
- richer `Gaps`, `Limitations`, `Rules`, and `Evidence Rows` tables that show
  scopes, support IDs, descriptions, related sections, artifact IDs, source
  IDs, coverage labels, and limitation fields where available;
- observed evidence rule IDs from `facts.ndjson` in the rules table when a
  compatible full rule catalog artifact is not provided, with a visible
  `explorer.render.catalog-unavailable.v1` gap;
- matching `sectionStatuses` and `redactions` data in `data/explorer-data.json`
  so downloadable data is no less redacted than the visible UI.

Section status rows use the same semantic order in HTML and JSON: overview,
sources, artifacts, evidence rows, surfaces, paths, reducer results, rules,
then redactions.

First-slice rows such as `not-rendered-in-current-slice` and `not-provided`
are explorer compatibility labels only. They do not prove runtime behavior,
source reachability, production use, or absence of evidence outside compatible
inputs.

The generated files use stable ordering, deterministic asset names, normalized
line endings, and no wall-clock timestamp. The manifest records
`generationTimestampPolicy: "omitted-deterministic"` and does not include a
self-referential hash of generated output.

## Safety Profiles

The default safety profile is `public-demo`. It uses safe source labels,
commit-SHA-only repository identity, stable artifact IDs, content hashes, safe
repository-relative paths, and hashed placeholders for unsafe display values.
The generated manifest records `safetyProfile: "public-demo"` and
`claimLevel: "public-safe"` for this mode so downstream readers can distinguish
the selected safety profile from the public-safe claim vocabulary used by other
TraceMap reports.

`hidden-local` is visibly labeled in the page and manifest. This first slice
still uses the same conservative safe rendering path, but records redaction,
hash, category-only, and omission counts so future hidden/local expansion has a
stable contract.

If generated HTML, CSS, JavaScript, JSON data, manifests, or README text fail
post-generation safety validation, generation fails with a rule ID and
generated artifact path without printing the unsafe raw value.

## Manifest Schema

`data/explorer-manifest.json` uses schema version
`tracemap-static-html-evidence-explorer.v1` and includes:

- generator name, schema version, and TraceMap assembly version;
- safety profile and claim level;
- repo identity policy, currently `commit-sha-only` or `omitted-for-safety`;
- generation timestamp policy, currently `omitted-deterministic`;
- safe commit SHA when available;
- coverage status;
- counts for sources, artifacts, surfaces, paths, reducer rows, evidence rows,
  gaps, limitations, rules, redactions, and omitted/unavailable categories;
- input artifact IDs, kinds, safe labels, content hashes, schema versions,
  compatibility labels, coverage labels, source IDs, gaps, and limitations;
- redaction rows, gaps, and limitations.

Coverage status values in the first slice are closed labels: `partial` when
the explorer emitted gaps for unavailable or unsupported sections, `reduced`
when input coverage labels indicate reduced, failed, partial, or unknown
analysis, and `available` only when the supported first-slice inputs have no
coverage-reduction labels or explorer gaps.

`data/explorer-data.json` mirrors the safe view model used by the HTML page.
It is no less redacted than the visible UI.

## Partial Scope

This first implementation slice intentionally marks unsupported sections as
partial or unavailable:

- static surfaces and paths are counted but not rendered from SQLite;
- reducer-backed results are shown as not provided unless a future compatible
  reducer artifact reader is added;
- rule catalog rendering is limited to built-in explorer rule stubs and
  observed rule IDs in evidence rows. When a full compatible rule catalog is
  unavailable, observed rules are intentionally marked partial and do not
  strengthen the underlying evidence tier or limitation language.

Those gaps are explicit so absence is not confused with credible evidence that
no source behavior exists.
