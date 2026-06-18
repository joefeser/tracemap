# Static HTML Evidence Explorer Design

## Overview

The static HTML evidence explorer is an additive generated artifact over
existing TraceMap outputs. It gives reviewers a local, browser-based way to
navigate sources, coverage, surfaces, paths, gaps, rules, limitations, and
evidence rows without changing scanner or reducer semantics.

The implementation should behave like a deterministic renderer:

1. Load selected generated TraceMap artifacts.
2. Normalize them into safe view models with provenance and rule-backed gaps.
3. Apply the selected safety profile.
4. Write a local static HTML bundle and manifest.
5. Validate generated output for deterministic ordering, safety, and no-network
   behavior.

No part of the explorer should run new repository analysis, create evidence,
call a model, use embeddings, write a vector database, contact a remote
service, or infer runtime behavior.

## Current State

TraceMap already emits artifacts that can seed a local explorer:

- `scan-manifest.json` for scan provenance, repo/commit metadata, coverage,
  extractor versions, and generated-output context.
- `facts.ndjson` for deterministic facts and evidence rows.
- `index.sqlite` for indexed facts and queryable relationships.
- `report.md` and JSON report artifacts for human summaries and reducer output.
- combined index/report artifacts for cross-source surfaces, paths, gaps,
  limitations, release-review output, dependency reports, and impact reducer
  classifications.
- rule catalog material that documents rule IDs and limitations.

The explorer must not make these artifacts less safe. It should preserve the
existing no-raw-snippet default, public/demo strictness, hidden/local labeling,
and evidence tiers.

## Non-Goals

- No hosted explorer or public site changes in v1.
- No live backend, API server, database, service worker sync, telemetry,
  analytics, remote font, remote asset, or CDN dependency.
- No new scanner, reducer, adapter, or runtime-analysis behavior.
- No LLM calls, embeddings, vector database writes, prompt classifiers,
  semantic search service, generated summaries, or AI impact analysis in core.
- No runtime proof, production traffic inference, reachability, ownership,
  vulnerability, incident root-cause, release-safety, or operational-safety
  claims.
- No raw private values in public/demo output.

## Proposed CLI Shape

Implementation may add a new command or an option on an existing report command.
The exact command should follow current CLI conventions, but the design assumes
one of these shapes:

```text
tracemap explorer generate --input <artifact-or-report-dir> --out <dir>
tracemap report html --input <artifact-or-report-dir> --out <dir>
```

The command should accept generated TraceMap artifact directories rather than
source repository paths. If a future workflow generates the explorer as part of
`tracemap scan`, it should still render only from generated artifacts produced
by that run and should preserve the existing required scan outputs.

Suggested output layout:

```text
evidence-explorer/
  index.html
  assets/
    explorer.css
    explorer.js
  data/
    explorer-manifest.json
    explorer-data.json
  README.md
```

`index.html` should work without JavaScript for core evidence tables. JavaScript
may enhance filtering, sorting, local graph interactions, copy buttons, and
section toggles over safe embedded data.

The generated artifact should not be written under public site source or
generated site output directories. It is a local TraceMap report artifact, not a
`tracemap.tools` page.

## Input Model

### Artifact Discovery

The generator should discover supported inputs from a selected artifact or
report directory:

- scan manifest;
- fact stream;
- SQLite index;
- Markdown and JSON reports;
- combined index/report outputs;
- reducer outputs;
- rule catalog data;
- generated validation summaries when available.

Each discovered artifact becomes an `ExplorerArtifact` record:

```json
{
  "artifactId": "artifact:scan-manifest",
  "artifactKind": "scan-manifest",
  "safeLabel": "Scan manifest",
  "contentHash": "sha256:...",
  "schemaVersion": "scan-manifest.v1",
  "claimLevel": "demo-safe",
  "coverageLabels": ["PartialAnalysis"],
  "sourceIds": ["source:public-demo-api"],
  "limitations": [],
  "gaps": []
}
```

`safeLabel` must never be derived from an absolute path, raw remote, hostname,
private repository name, or other unsafe value. Use closed labels, user-provided
safe labels that pass validation, or stable IDs.

When the generator reads `facts.ndjson`, it must treat each fact as raw
evidence subject to the selected safety profile. It must not embed fact values
verbatim in explorer output unless they pass the same redaction, omission, or
rejection path used by existing report exporters.

The explorer manifest should define these policy fields:

- `repoIdentityPolicy`: how repository identity is represented without unsafe
  values. Allowed values should be closed strings such as `commit-sha-only`,
  `safe-source-label`, `stable-source-id`, or `omitted-for-safety`.
- `generationTimestampPolicy`: how generation time is handled for determinism.
  Allowed values should be closed strings such as `source-manifest-derived`,
  `wallclock-recorded-in-manifest`, or `omitted-deterministic`.

Input artifact `contentHash` fields are hashes of source artifacts. The
manifest must not include a self-hash over its own generated bytes.

### Provenance Reconciliation

The generator should reconcile source metadata before rendering combined views.
At minimum, compare:

- commit SHA;
- source label;
- source kind;
- claim level;
- coverage labels;
- schema versions;
- extractor versions;
- generated artifact hashes.

If values disagree in a way that affects interpretation, the affected section
must be stopped or marked partial with a rule-backed gap. For example, a scan
manifest from one commit and reducer output from another commit should not be
presented as a coherent current-head view.

Suggested conflict policy:

| Conflict | Default behavior | Rule |
| --- | --- | --- |
| Claim level conflict where a stricter public/demo profile would be weakened | Stop generation | `explorer.input.provenance-conflict.v1` |
| Commit SHA conflict between artifacts used in the same section | Mark that section partial or stop if no safe partition exists | `explorer.input.provenance-conflict.v1` |
| Unsupported schema for a required artifact | Stop the affected section; continue unrelated sections when safe | `explorer.input.unsupported-schema.v1` |
| Unsupported schema for an optional artifact | Mark the category unavailable with a gap | `explorer.input.unsupported-schema.v1` |
| Missing commit metadata | Mark source and dependent sections partial | `explorer.input.missing-commit.v1` |
| Unknown closed-vocabulary value | Mark affected rows partial or unsupported; do not invent a category | `explorer.input.unsupported-schema.v1` |

The implementation may tighten this table, but it should not silently merge
conflicting provenance.

If a single provenance-conflict rule ID covers multiple conflict types, the
catalog entry should require a structured `conflictKind` or equivalent field so
claim-level weakening, commit mismatch, schema mismatch, and identity mismatch
remain distinguishable in evidence rows.

## View Model

The renderer should convert raw artifacts into typed, safe records before HTML
serialization. Suggested records:

- `ExplorerSummary`
- `ExplorerSource`
- `ExplorerArtifact`
- `ExplorerSurface`
- `ExplorerPath`
- `ExplorerPathHop`
- `ExplorerReducerResult`
- `ExplorerEvidenceRow`
- `ExplorerGap`
- `ExplorerLimitation`
- `ExplorerRule`
- `ExplorerRedaction`

Every record that can support a conclusion should carry:

- stable ID;
- rule ID;
- evidence tier;
- support IDs;
- source/artifact IDs;
- coverage labels;
- limitations;
- claim level;
- safe display title;
- sort key.

Presentation-only records, such as section headers and filter labels, must be
derived from closed vocabularies or safe stable IDs.

Closed vocabularies should be derived from existing TraceMap schemas, enums,
rule catalog values, and report contracts where available. Explorer-specific
vocabulary additions should be documented in the rule catalog or explorer docs
and should emit a vocabulary/schema gap when inputs contain unknown values.

## Page Structure

`index.html` should use deterministic sections:

1. `Evidence Overview`
2. `Sources`
3. `Coverage`
4. `Surfaces`
5. `Paths`
6. `Reducer Results`
7. `Gaps`
8. `Limitations`
9. `Rules`
10. `Evidence Rows`
11. `Artifacts`
12. `About This Local Explorer`

The overview should show safe counts and prominent claim/coverage status.
Section anchors should be stable lowercase slugs from closed section names.

### Evidence Overview

The overview is the starting point. It should include:

- selected claim level;
- overall coverage labels;
- source and artifact counts;
- counts for surfaces, paths, reducer rows, evidence rows, gaps, limitations,
  rules, redactions, and omitted rows;
- whether reducer output is present;
- whether semantic analysis was full, reduced, failed, unsupported, or unknown;
- links to gaps and limitations before any detailed evidence sections.

Avoid success-state phrasing like "complete", "fully mapped", or "safe" unless
the input evidence explicitly supports that wording under documented rules.

### Sources And Coverage

The source view should group rows by safe source label and source kind. It
should show build/semantic coverage and fallback status without printing local
build paths, logs, or diagnostic text.

Coverage labels should be closed vocabulary values. If input artifacts use
free-form coverage strings, normalize them through documented mapping and emit
a gap for unknown values.

### Surfaces And Paths

Surface views should group by closed surface kind, then safe display title,
then stable ID. Path views should render each hop with row-level rule IDs,
evidence tiers, and support IDs. The graph-like experience, if implemented,
should be an alternate view over the same path/surface records.

Use wording such as:

- static evidence;
- candidate;
- nearby evidence;
- path evidence;
- reducer-backed result;
- needs review;
- analysis gap.

Avoid scanner-only wording such as:

- impacted;
- affected;
- broken;
- reachable;
- used in production;
- runtime dependency;
- safe to deploy.

Only reducer-backed rows may use impact terminology, and even then the row must
show reducer rule ID, supporting evidence, limitations, and needs-review state.

### Gaps, Limitations, Rules, And Evidence Rows

These sections are the audit spine of the explorer. They should be filterable
by safe fields:

- rule ID;
- evidence tier;
- source label;
- artifact kind;
- coverage label;
- surface kind;
- gap kind;
- limitation kind;
- reducer classification;
- needs-review state.

Search, if added, should be local substring matching over safe rendered fields.
It must not scan repository files, raw facts, or hidden unsafe fields.

## Safety Model

The explorer should reuse the strongest existing TraceMap safety primitives
available at implementation time. It should validate both structured view
models and final generated bytes.

The safety profile should be selected explicitly by the command or inherited
from the input artifact claim level when the current CLI already provides that
contract. Suggested closed profile names are `public-demo` and `hidden-local`.
The selected profile must be recorded in the explorer manifest and applied to
visible UI, embedded data, copied text, and downloadable files.

Implementation should inventory and reuse the existing public/demo and
docs-export safety policy as the source of truth rather than creating a
parallel redaction system. If an explorer-specific validation path is needed,
it should have parity tests proving it rejects or redacts the same unsafe input
classes as the existing strict profile.

Public/demo output must not contain:

- raw source snippets;
- raw SQL;
- config values;
- connection strings;
- secrets or secret-like values;
- local absolute paths;
- raw remotes;
- raw endpoint addresses;
- raw query strings;
- hostnames;
- exact private routes;
- private repository names;
- private sample names;
- analyzer logs;
- raw facts;
- raw SQLite content;
- source maps with local paths;
- generated comments or metadata that leak build paths.

Hidden/local output may use redacted, hashed, category-only, or omitted values
only when the output is visibly labeled and the manifest records affected
sections and counts.

The safety pass should inspect:

- HTML text;
- attributes;
- DOM IDs;
- URL fragments;
- embedded JSON;
- downloadable data;
- CSS comments and custom properties;
- JavaScript bundles and comments;
- source maps, if any;
- manifest metadata;
- generated README text.

If a generated asset fails safety validation, fail the command and report the
rule ID plus generated artifact path without printing the unsafe raw value.

## Determinism

The renderer should be byte-stable for identical inputs and options. Use:

- sorted artifact discovery;
- stable IDs;
- closed section order;
- closed vocabulary order before lexical order;
- stable tie-breakers;
- deterministic JSON serialization;
- deterministic CSS/JS asset names;
- normalized line endings;
- no random IDs;
- no wall-clock timestamps in byte-stable files unless explicitly excluded.

If a manifest needs generation time, place it in a documented field that tests
can normalize or allow an option such as `--deterministic` to omit wall-clock
time.

The no-JavaScript row threshold, if used, should be a build-time constant that
is documented in implementation docs and covered by byte-stability tests.

Byte-stability tests should compare `index.html`, CSS, JavaScript, source maps
if any, `explorer-manifest.json`, `explorer-data.json`, and any inline embedded
JSON. When the same evidence appears in both baseline HTML and explorer data,
tests should assert the two representations agree after applying the same
safety profile.

## Accessibility And Progressive Enhancement

The base HTML should work without JavaScript. Use semantic headings, tables,
lists, `details` elements, captions, row headers, labels, and visible focus
states. JavaScript enhancements should preserve keyboard navigation and should
not be required to read overview counts, section links, gaps, limitations, rule
IDs, or the documented baseline of evidence rows. Large datasets may use a
documented no-JavaScript row threshold when the page renders counts and
omitted-row labels for rows beyond the threshold.

Graph-like views should not be the only way to inspect paths. Always provide a
table or list view with the same evidence fields.

## Rule IDs

Implementation should add explorer-specific rules only where existing rules do
not already describe the behavior. Candidate rule IDs:

- `explorer.input.unsupported-schema.v1`
- `explorer.input.provenance-conflict.v1`
- `explorer.input.missing-commit.v1`
- `explorer.render.redacted-display-value.v1`
- `explorer.render.omitted-unsafe-value.v1`
- `explorer.render.catalog-unavailable.v1`
- `explorer.render.no-network-assets.v1`
- `explorer.render.partial-section.v1`

Each rule must document limitations, evidence tier expectations, and whether it
creates a gap, limitation, or validation failure.

## Compatibility

The explorer should be additive. Existing `tracemap scan` required outputs
remain unchanged:

```text
scan-manifest.json
facts.ndjson
index.sqlite
report.md
logs/analyzer.log
```

If the explorer is added to a scan command, it should be an optional additional
output or a separately named report output. Existing JSON schemas should remain
stable where possible. Any new manifest or data schema should include an
explicit schema version.

## Validation Plan

Implementation should include focused tests for:

- byte-stable reruns;
- manifest/data/inline JSON byte-stability;
- safe public/demo output;
- parity with existing strict safety policy;
- hidden/local redaction labels and counts;
- safety failure output that includes rule ID and generated artifact path but
  not the unsafe value;
- no raw snippet default;
- no network requests or remote asset references;
- no source maps or bundle metadata with local paths;
- unsupported schema gaps;
- distinct not-provided, unsupported, and no-evidence-under-credible-coverage
  states;
- provenance conflicts;
- missing commit metadata;
- unknown closed-vocabulary values;
- partial/reduced coverage labels;
- scanner-only non-impact wording;
- reducer-backed impact wording;
- rule catalog unavailable behavior;
- accessible section headings, labels, and table captions;
- JavaScript-disabled baseline content;
- downloadable data and copy-text redaction parity;
- line-ending normalization across supported platforms;
- embedded data safety for fields not visible in the UI.

Browser sanity should use a local file or local static server and verify that
the overview, section navigation, filters, large tables, and any graph-like view
render without external network access.
