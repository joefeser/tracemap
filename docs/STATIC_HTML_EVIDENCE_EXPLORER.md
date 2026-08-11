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
The explorer currently supports:

- `scan-manifest.json` for safe commit, coverage, and extractor provenance;
- `facts.ndjson` for safe evidence rows;
- `index.sqlite` as a hashed provenance artifact only;
- `report.md` as a hashed provenance artifact only.
- `release-review.json` v1.2 through a bounded compatibility-metadata reader.

Other top-level JSON files are labeled unsupported with
`explorer.input.unsupported-schema.v1` gaps instead of being silently merged.
That still includes report JSON artifacts such as `dependency-report.json`,
`demo-summary.json`, and other combined/reducer report JSON files; compatible
readers for those artifacts are deferred to later slices.

The `release-review.json` reader accepts only the exact generated
`reportType: release-review`, version `1.2`, supported single/combined mode,
matching before/after snapshot shape, closed `Full`/`Reduced` coverage values,
non-empty source collections, valid-or-null source commit identities, and
non-negative summary gap counts. The artifact is size bounded and identified
by a deterministic SHA-256 content hash. Duplicate required properties,
unsupported versions or modes, malformed metadata, invalid commit identities,
and oversized inputs remain unavailable under
`explorer.input.unsupported-schema.v1`.

This reader projects only safe compatibility metadata: schema, content hash,
closed coverage labels, and a rule-backed limitation. It does not read or
render release-review finding bodies, source labels, paths, messages,
metadata, checklist text, or reducer conclusions. A compatible report does not
prove runtime reachability, production behavior, release approval, deployment
safety, or complete analysis. Richer release-review rendering remains a
separate surface/path/reducer reader slice.
Current compatible inputs do not expose independent claim-level metadata. The
explorer records that metadata as `claim-level:unknown` with a visible
limitation and does not manufacture a profile or claim-level conflict. Commit
SHA disagreement between `scan-manifest.json` and `facts.ndjson` is detected as
the closed `commit-conflict` kind under
`explorer.input.provenance-conflict.v1`.
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

The v2 explorer data contract adds a `Compatibility Ledger` table and matching
`compatibilityLedger` JSON rows. Each row uses a stable safe subject ID, closed
subject kind and compatibility status, rule ID, evidence tier, coverage label,
scope, support IDs, limitation IDs, and an explorer-authored message. Artifact
rows cover supported, provenance-only, missing, and unsupported inputs;
section rows remain additive to the existing `sectionStatuses` contract.

Closed compatibility statuses are:

- `rendered-compatible` and `compatible-empty` for safely parsed inputs;
- `provenance-only` for inputs hashed without reading or rendering content;
- `not-provided`, `unsupported-schema`, and `unsupported-artifact` for inputs
  that cannot support a rendered conclusion;
- `partial` for rule-backed conflicts or compatibility gaps;
- `compatible` for safe section and selected-profile state;
- `profile-incompatible` and `safety-omitted` are reserved closed states for
  future compatible readers that expose independently verifiable profile data.

Missing and unsupported rows describe explorer compatibility only. They do not
claim evidence is absent from an artifact or repository.

Section status rows retain their existing semantic order in JSON: overview,
sources, artifacts, evidence rows, surfaces, paths, reducer results, rules,
then redactions. HTML navigation places Compatibility Ledger after Coverage
without changing the `sectionStatuses` collection.

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

`data/explorer-manifest.json` and `data/explorer-data.json` use schema version
`tracemap-static-html-evidence-explorer.v2`. The generator recognizes a prior
v1 TraceMap-generated manifest when `--force` replaces an existing generated
bundle, but all newly written bundles use v2. The manifest includes:

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
It is no less redacted than the visible UI and includes the same ordered
compatibility ledger rows rendered in the no-JavaScript HTML baseline.

## Compatibility And Conflict Behavior

The selected output profile remains the controlling policy. Public/demo
aliases normalize to `public-demo`; hidden/local aliases normalize to
`hidden-local`. These profile names are not compared directly with claim-level
tokens.

The current production conflict dimension is commit identity. When compatible
manifest and fact inputs disagree, the fact artifact and affected evidence-row
section remain partial and cite `explorer.input.provenance-conflict.v1` plus
the stable `commit-conflict` gap. Generated messages do not include unsafe
input paths or private identities.

Claim level, independently declared input safety profile, source identity, and
structured schema-version conflicts remain future hooks until a compatible
reader exposes safe structured fields for them. Unknown metadata alone is a
limitation, not a conflict.

## Partial Scope

This first implementation slice intentionally marks unsupported sections as
partial or unavailable:

- static surfaces and paths are counted but not rendered from SQLite;
- reducer-backed results are shown as not provided unless a future compatible
  reducer artifact reader is added;
- rule catalog rendering uses a compatible `rule-catalog.yml` or
  `rules/rule-catalog.yml` artifact when provided, and otherwise falls back to
  built-in explorer rule stubs plus observed rule IDs in evidence rows. When
  full catalog metadata is unavailable for an observed rule, that rule remains
  intentionally marked partial and does not strengthen the underlying evidence
  tier or limitation language.

Those gaps are explicit so absence is not confused with credible evidence that
no source behavior exists.
